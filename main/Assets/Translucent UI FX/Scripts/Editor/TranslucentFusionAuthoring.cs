using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TranslucentUIFX.Editor
{
    /// <summary>
    /// Editor authoring workflow for converting standalone glass Images into one fused
    /// surface without asking designers to rebuild their hierarchy by hand.
    /// </summary>
    public static class TranslucentFusionAuthoring
    {
        private const float GroupMargin = 48f;
        private const string UndoFuse = "Fuse Selected Glass";
        private const string UndoDetach = "Detach Fused Glass Member";

        [MenuItem("GameObject/UI/Fuse Selected Glass", false, 3)]
        private static void FuseSelectedMenu()
        {
            FuseWithFeedback(GetSelectedStandaloneGlass());
        }

        [MenuItem("GameObject/UI/Fuse Selected Glass", true)]
        private static bool ValidateFuseSelectedMenu()
        {
            return CanFuse(GetSelectedStandaloneGlass(), out _);
        }

        public static TranslucentImageFX[] GetSelectedStandaloneGlass()
        {
            TranslucentImageFX active = Selection.activeGameObject == null
                ? null
                : Selection.activeGameObject.GetComponent<TranslucentImageFX>();

            return Selection.gameObjects
                .Select(gameObject => gameObject.GetComponent<TranslucentImageFX>())
                .Where(IsStandalone)
                .Distinct()
                .OrderByDescending(effect => effect == active)
                .ThenBy(effect => effect.transform.GetSiblingIndex())
                .ToArray();
        }

        public static TranslucentImageFX[] FindOverlappingStandaloneGlass(TranslucentImageFX source)
        {
            if (!IsStandalone(source) || source.transform.parent == null)
                return Array.Empty<TranslucentImageFX>();

            Rect sourceBounds = GetWorldBounds(source.rectTransform);
            List<TranslucentImageFX> matches = new List<TranslucentImageFX> { source };
            Transform parent = source.transform.parent;

            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child == source.transform)
                    continue;

                TranslucentImageFX candidate = child.GetComponent<TranslucentImageFX>();
                if (IsStandalone(candidate) && sourceBounds.Overlaps(GetWorldBounds(candidate.rectTransform), true))
                    matches.Add(candidate);
            }

            return matches
                .OrderByDescending(effect => effect == source)
                .ThenBy(effect => effect.transform.GetSiblingIndex())
                .ToArray();
        }

        public static bool CanFuse(IReadOnlyList<TranslucentImageFX> sources, out string error)
        {
            error = string.Empty;
            if (sources == null || sources.Count == 0)
                return Fail("Select at least one standalone Translucent Image.", out error);

            Transform commonParent = null;
            HashSet<Transform> selectedTransforms = new HashSet<Transform>();

            for (int index = 0; index < sources.Count; index++)
            {
                TranslucentImageFX source = sources[index];
                if (!IsStandalone(source))
                    return Fail("Every selected object must be a standalone Translucent Image.", out error);

                if (source.UsesSpriteSilhouette && source.type != Image.Type.Simple)
                    return Fail("Filled, sliced and tiled Images remain standalone. Use a Simple Image to fuse its sprite outline.", out error);

                if (EditorUtility.IsPersistent(source) || PrefabUtility.IsPartOfPrefabAsset(source))
                    return Fail("Open the prefab in Prefab Mode before converting prefab assets.", out error);

                if (PrefabUtility.IsPartOfPrefabInstance(source))
                    return Fail("Scene prefab instances are protected. Open the source prefab in Prefab Mode, or unpack the instance before fusing.", out error);

                if (source.canvas == null)
                    return Fail($"{source.name} is not below a Canvas.", out error);

                if (source.transform.parent is not RectTransform parent)
                    return Fail("Selected glass objects must have a RectTransform parent.", out error);

                if (commonParent == null)
                    commonParent = parent;
                else if (commonParent != parent)
                    return Fail("Selected glass objects must be siblings under the same parent.", out error);

                selectedTransforms.Add(source.transform);
            }

            foreach (Transform selected in selectedTransforms)
            {
                Transform ancestor = selected.parent;
                while (ancestor != null)
                {
                    if (selectedTransforms.Contains(ancestor))
                        return Fail("A selected glass object cannot be inside another selected glass object.", out error);
                    ancestor = ancestor.parent;
                }
            }

            if (commonParent != null && commonParent.GetComponent<LayoutGroup>() != null)
                return Fail("The shared parent is controlled by a Layout Group. Create the fused group outside that layout, then place members inside it.", out error);

            return true;
        }

        public static bool FuseWithFeedback(IReadOnlyList<TranslucentImageFX> sources)
        {
            if (!CanFuse(sources, out string error))
            {
                EditorUtility.DisplayDialog("Cannot Fuse Glass", error, "OK");
                return false;
            }

            if (!TryFuse(sources, out TranslucentGlassGroup group, out error))
            {
                EditorUtility.DisplayDialog("Fusion Failed", error, "OK");
                return false;
            }

            Selection.activeGameObject = group.gameObject;
            return true;
        }

        public static bool TryFuse(
            IReadOnlyList<TranslucentImageFX> sources,
            out TranslucentGlassGroup group,
            out string error)
        {
            group = null;
            if (!CanFuse(sources, out error))
                return false;

            int undoGroup = BeginUndoGroup(UndoFuse);
            try
            {
                RectTransform commonParent = (RectTransform)sources[0].transform.parent;
                int siblingIndex = sources.Min(source => source.transform.GetSiblingIndex());
                CalculateLocalBounds(commonParent, sources, out Vector2 minimum, out Vector2 maximum);

                string groupName = GameObjectUtility.GetUniqueNameForSibling(commonParent, "Fused Liquid Glass Group");
                GameObject groupObject = new GameObject(groupName, typeof(RectTransform), typeof(CanvasRenderer));
                Undo.RegisterCreatedObjectUndo(groupObject, UndoFuse);
                RectTransform groupRect = (RectTransform)groupObject.transform;
                groupRect.SetParent(commonParent, false);
                groupRect.anchorMin = groupRect.anchorMax = new Vector2(0.5f, 0.5f);
                groupRect.pivot = new Vector2(0.5f, 0.5f);
                groupRect.localRotation = Quaternion.identity;
                groupRect.localScale = Vector3.one;
                groupRect.localPosition = new Vector3(
                    (minimum.x + maximum.x) * 0.5f,
                    (minimum.y + maximum.y) * 0.5f,
                    sources[0].rectTransform.localPosition.z);
                groupRect.sizeDelta = maximum - minimum + Vector2.one * (GroupMargin * 2f);
                groupRect.SetSiblingIndex(siblingIndex);

                group = Undo.AddComponent<TranslucentGlassGroup>(groupObject);
                CopyGlassStyle(sources[0], group);
                // The group supplies a neutral fade/color multiplier. Keep each source's
                // opacity and Graphic color on its member, including a fully hidden source.
                group.GlassIntensity = 1f;
                group.color = Color.white;

                for (int index = 0; index < sources.Count; index++)
                    ConvertToMember(sources[index], groupRect);

                group.RefreshMembers();
                EditorUtility.SetDirty(group);
                MarkPrefabModifications(groupObject);
                Undo.CollapseUndoOperations(undoGroup);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                Undo.RevertAllInCurrentGroup();
                group = null;
                error = "The conversion was rolled back: " + exception.Message;
                return false;
            }
        }

        public static TranslucentGlassMember AddMember(TranslucentGlassGroup group)
        {
            if (group == null || PrefabUtility.IsPartOfPrefabInstance(group))
                return null;

            int undoGroup = BeginUndoGroup("Add Fused Glass Member");
            GameObject memberObject = new GameObject(
                GameObjectUtility.GetUniqueNameForSibling(group.transform, "Glass Member"),
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            Undo.RegisterCreatedObjectUndo(memberObject, "Add Fused Glass Member");

            RectTransform rect = (RectTransform)memberObject.transform;
            rect.SetParent(group.transform, false);
            rect.sizeDelta = new Vector2(220f, 112f);
            rect.anchoredPosition = new Vector2(group.MemberCount * 22f, -group.MemberCount * 14f);

            Image image = memberObject.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;

            TranslucentGlassMember member = Undo.AddComponent<TranslucentGlassMember>(memberObject);
            group.RefreshMembers();
            EditorUtility.SetDirty(group);
            MarkPrefabModifications(memberObject);
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = memberObject;
            return member;
        }

        public static bool TryDetach(
            IReadOnlyList<TranslucentGlassMember> members,
            out TranslucentImageFX[] detached,
            out string error)
        {
            detached = Array.Empty<TranslucentImageFX>();
            error = string.Empty;
            if (members == null || members.Count == 0)
                return Fail("Select at least one fused glass member.", out error);

            if (members.Any(member => member == null || member.GetComponentInParent<TranslucentGlassGroup>() == null))
                return Fail("Every selected object must belong to a fused glass group.", out error);

            if (members.Any(member => PrefabUtility.IsPartOfPrefabInstance(member)))
                return Fail("Scene prefab instances are protected. Detach the member in Prefab Mode, or unpack the instance first.", out error);

            int undoGroup = BeginUndoGroup(UndoDetach);
            List<TranslucentImageFX> results = new List<TranslucentImageFX>(members.Count);
            HashSet<TranslucentGlassGroup> affectedGroups = new HashSet<TranslucentGlassGroup>();

            try
            {
                foreach (TranslucentGlassMember member in members)
                {
                    TranslucentGlassGroup owner = member.GetComponentInParent<TranslucentGlassGroup>();
                    RectTransform destination = owner.transform.parent as RectTransform;
                    if (destination == null)
                        throw new InvalidOperationException($"{owner.name} has no RectTransform parent.");

                    GameObject memberObject = member.gameObject;
                    Image interactionImage = memberObject.GetComponent<Image>();
                    string interactionJson = interactionImage == null ? string.Empty : EditorJsonUtility.ToJson(interactionImage);
                    bool interactionRaycast = interactionImage == null || interactionImage.raycastTarget;
                    Selectable[] selectables = FindSelectablesReferencing(interactionImage);
                    Toggle[] toggles = FindTogglesReferencing(interactionImage);
                    string standaloneJson = member.EditorStandaloneJson;
                    GlassCornerSettings cornerSettings = member.CornerSettings;
                    GlassMemberAppearance appearance = member.Appearance;
                    float threshold = member.AlphaThreshold;
                    Sprite shapeSprite = member.ShapeSprite;
                    bool preserveAspect = member.PreserveAspect;
                    EdgeShape shape = member.Shape;
                    float radius = member.CornerRadius, continuity = member.CornerContinuity;
                    bool individualCorners = member.IndividualCorners;
                    Vector4 corners = member.CornerRadii;

                    Undo.SetTransformParent(member.transform, destination, UndoDetach);
                    Undo.DestroyObjectImmediate(member);
                    if (interactionImage != null)
                        Undo.DestroyObjectImmediate(interactionImage);

                    TranslucentImageFX effect = Undo.AddComponent<TranslucentImageFX>(memberObject);
                    if (!string.IsNullOrEmpty(standaloneJson))
                        EditorJsonUtility.FromJsonOverwrite(standaloneJson, effect);
                    else
                    {
                        CopyGlassStyle(owner, effect);
                        if (!string.IsNullOrEmpty(interactionJson))
                        {
                            Image temporary = effect;
                            EditorJsonUtility.FromJsonOverwrite(interactionJson, temporary);
                        }
                        effect.color = Color.white;
                    }

                    if (appearance != null && appearance.Override) appearance.ApplyTo(effect);
                    effect.overrideSprite = null;
                    effect.sprite = shapeSprite;
                    effect.SpriteAlphaThreshold = threshold;
                    effect.preserveAspect = preserveAspect;
                    effect.CornerSettings = cornerSettings;
                    effect.EdgeShape = shape;
                    effect.EdgeRounding = radius;
                    effect.CornerContinuity = continuity;
                    effect.IndividualCorners = individualCorners;
                    effect.CornerRadii = corners;
                    effect.raycastTarget = interactionRaycast;
                    RewireGraphics(selectables, toggles, effect);
                    MarkPrefabModifications(memberObject);
                    results.Add(effect);
                    affectedGroups.Add(owner);
                }

                foreach (TranslucentGlassGroup owner in affectedGroups)
                {
                    owner.RefreshMembers();
                    EditorUtility.SetDirty(owner);
                }

                detached = results.ToArray();
                Selection.objects = results.Select(effect => (UnityEngine.Object)effect.gameObject).ToArray();
                Undo.CollapseUndoOperations(undoGroup);
                return true;
            }
            catch (Exception exception)
            {
                Undo.RevertAllInCurrentGroup();
                detached = Array.Empty<TranslucentImageFX>();
                error = "Detach was rolled back: " + exception.Message;
                return false;
            }
        }

        private static void ConvertToMember(TranslucentImageFX source, RectTransform groupRect)
        {
            GameObject sourceObject = source.gameObject;
            string standaloneJson = EditorJsonUtility.ToJson(source);
            Selectable[] selectables = FindSelectablesReferencing(source);
            Toggle[] toggles = FindTogglesReferencing(source);
            GlassCornerSettings cornerSettings = JsonUtility.FromJson<GlassCornerSettings>(JsonUtility.ToJson(source.CornerSettings));
            GlassMemberAppearance appearance = GlassMemberAppearance.From(source);
            var groupStyle = groupRect.GetComponent<TranslucentGlassGroup>();
            appearance.Override = JsonUtility.ToJson(appearance) != JsonUtility.ToJson(GlassMemberAppearance.From(groupStyle));
            appearance.OpacityReference = Mathf.Max(0.0001f, groupStyle.GlassIntensity);
            float threshold = source.SpriteAlphaThreshold;
            Sprite shapeSprite = source.SilhouetteSprite;
            bool preserveAspect = source.preserveAspect;
            EdgeShape shape = source.EdgeShape;
            float radius = source.EdgeRounding;
            float continuity = source.CornerContinuity;
            bool individualCorners = source.IndividualCorners;
            Vector4 corners = source.CornerRadii;
            bool raycastTarget = source.raycastTarget;
            bool maskable = source.maskable;

            Undo.SetTransformParent(source.rectTransform, groupRect, UndoFuse);
            Undo.DestroyObjectImmediate(source);

            Image interactionImage = Undo.AddComponent<Image>(sourceObject);
            EditorJsonUtility.FromJsonOverwrite(standaloneJson, interactionImage);
            interactionImage.material = null;
            interactionImage.color = Color.clear;
            interactionImage.raycastTarget = raycastTarget;
            interactionImage.maskable = maskable;

            TranslucentGlassMember member = Undo.AddComponent<TranslucentGlassMember>(sourceObject);
            member.EditorStandaloneJson = standaloneJson;
            member.Appearance = appearance;
            member.CornerSettings = cornerSettings;
            member.ShapeSprite = shapeSprite;
            member.AlphaThreshold = threshold;
            member.PreserveAspect = preserveAspect;
            member.Shape = shape;
            member.IndividualCorners = individualCorners;
            member.CornerRadii = corners;
            member.CornerRadius = radius;
            member.CornerContinuity = continuity;
            member.SurfaceExpansion = 0f;

            RewireGraphics(selectables, toggles, interactionImage);
            MarkPrefabModifications(sourceObject);
        }

        private static void CopyGlassStyle(TranslucentImageFX source, TranslucentImageFX destination)
        {
            string sourceJson = EditorJsonUtility.ToJson(source);
            EditorJsonUtility.FromJsonOverwrite(sourceJson, destination);
            destination.sprite = null;
            destination.overrideSprite = null;
            destination.type = Image.Type.Simple;
            destination.color = source.color;
            destination.raycastTarget = false;
            destination.ProceduralShape = true;
            destination.SetAllDirty();
        }

        private static void CalculateLocalBounds(
            RectTransform parent,
            IReadOnlyList<TranslucentImageFX> sources,
            out Vector2 minimum,
            out Vector2 maximum)
        {
            minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            Vector3[] corners = new Vector3[4];

            foreach (TranslucentImageFX source in sources)
            {
                source.rectTransform.GetWorldCorners(corners);
                for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
                {
                    Vector3 localCorner = parent.InverseTransformPoint(corners[cornerIndex]);
                    minimum = Vector2.Min(minimum, localCorner);
                    maximum = Vector2.Max(maximum, localCorner);
                }
            }
        }

        private static Rect GetWorldBounds(RectTransform rectTransform)
        {
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            Vector2 minimum = corners[0];
            Vector2 maximum = corners[0];
            for (int index = 1; index < corners.Length; index++)
            {
                minimum = Vector2.Min(minimum, corners[index]);
                maximum = Vector2.Max(maximum, corners[index]);
            }
            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        private static bool IsStandalone(TranslucentImageFX effect)
        {
            return effect != null &&
                   effect is not TranslucentGlassGroup &&
                   effect.GetComponent<TranslucentGlassMember>() == null;
        }

        private static Selectable[] FindSelectablesReferencing(Graphic graphic)
        {
            if (graphic == null || !graphic.gameObject.scene.IsValid())
                return Array.Empty<Selectable>();

            Scene targetScene = graphic.gameObject.scene;
            return Resources.FindObjectsOfTypeAll<Selectable>()
                .Where(selectable => selectable != null &&
                                     !EditorUtility.IsPersistent(selectable) &&
                                     selectable.gameObject.scene == targetScene &&
                                     selectable.targetGraphic == graphic)
                .ToArray();
        }

        private static Toggle[] FindTogglesReferencing(Graphic graphic)
        {
            if (graphic == null || !graphic.gameObject.scene.IsValid())
                return Array.Empty<Toggle>();

            Scene targetScene = graphic.gameObject.scene;
            return Resources.FindObjectsOfTypeAll<Toggle>()
                .Where(toggle => toggle != null &&
                                 !EditorUtility.IsPersistent(toggle) &&
                                 toggle.gameObject.scene == targetScene &&
                                 toggle.graphic == graphic)
                .ToArray();
        }

        private static void RewireGraphics(Selectable[] selectables, Toggle[] toggles, Graphic replacement)
        {
            foreach (Selectable selectable in selectables)
            {
                Undo.RecordObject(selectable, "Preserve Glass Interaction");
                selectable.targetGraphic = replacement;
                EditorUtility.SetDirty(selectable);
                MarkPrefabModifications(selectable.gameObject);
            }

            foreach (Toggle toggle in toggles)
            {
                Undo.RecordObject(toggle, "Preserve Glass Toggle Graphic");
                toggle.graphic = replacement;
                EditorUtility.SetDirty(toggle);
                MarkPrefabModifications(toggle.gameObject);
            }
        }

        private static void MarkPrefabModifications(GameObject gameObject)
        {
            if (gameObject == null || !PrefabUtility.IsPartOfPrefabInstance(gameObject))
                return;

            PrefabUtility.RecordPrefabInstancePropertyModifications(gameObject.transform);
            foreach (Component component in gameObject.GetComponents<Component>())
                if (component != null)
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }

        private static int BeginUndoGroup(string name)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(name);
            return group;
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
