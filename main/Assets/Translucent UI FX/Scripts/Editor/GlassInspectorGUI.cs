using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TranslucentUIFX.Editor
{
    /// <summary>Compact, theme-aware controls shared by glass surfaces and members.</summary>
    internal static class GlassInspectorGUI
    {
        private static readonly string[] Names = { "Rectangle", "Circle", "Rounded", "Diamond", "Capsule", "Continuous", "Triangle", "Hexagon" };
        private static readonly int[] Order = { 5, 2, 4, 1, 0, 3, 6, 7 };
        private static readonly Dictionary<int, Texture2D> Icons = new Dictionary<int, Texture2D>();
        internal static Color Accent => EditorGUIUtility.isProSkin ? new Color(0.42f, 0.78f, 1f) : new Color(0.08f, 0.39f, 0.65f);
        internal static Color Muted => EditorGUIUtility.isProSkin ? new Color(0.62f, 0.68f, 0.75f) : new Color(0.36f, 0.42f, 0.49f);
        internal static Color CutoutColor => EditorGUIUtility.isProSkin ? new Color(1f, 0.66f, 0.53f) : new Color(0.64f, 0.24f, 0.12f);
        private static readonly Dictionary<bool, Texture2D> Panels = new Dictionary<bool, Texture2D>();
        static GlassInspectorGUI() { AssemblyReloadEvents.beforeAssemblyReload += ClearIcons; }
        private static void ClearIcons() { foreach (Texture2D icon in Icons.Values) Object.DestroyImmediate(icon); Icons.Clear(); foreach (var panel in Panels.Values) Object.DestroyImmediate(panel); Panels.Clear(); }

        internal sealed class Card : System.IDisposable
        {
            private readonly EditorGUILayout.VerticalScope scope;
            internal Card()
            {
                var style = new GUIStyle { padding = new RectOffset(14, 14, 14, 14), margin = new RectOffset(0, 0, 4, 10), border = new RectOffset(12, 12, 12, 12) };
                style.normal.background = PanelTexture();
                scope = new EditorGUILayout.VerticalScope(style);
            }
            public void Dispose() => scope.Dispose();
        }

        internal sealed class Layout : System.IDisposable
        {
            private readonly float width = EditorGUIUtility.labelWidth;
            internal Layout() { EditorGUIUtility.labelWidth = Mathf.Clamp(EditorGUIUtility.currentViewWidth * 0.35f, 110f, 180f); }
            public void Dispose() { EditorGUIUtility.labelWidth = width; }
        }

        internal static void Rounded(Rect rect, Color color, float radius = 9f)
        {
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, radius);
        }

        private static Texture2D PanelTexture()
        {
            bool dark = EditorGUIUtility.isProSkin;
            if (Panels.TryGetValue(dark, out var texture)) return texture;
            const int n = 32;
            texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, name = "Glass inspector card", filterMode = FilterMode.Bilinear };
            var pixels = new Color[n * n];
            Color fill = dark ? new Color(0.205f, 0.22f, 0.24f) : new Color(0.91f, 0.925f, 0.94f);
            Color border = dark ? new Color(0.28f, 0.305f, 0.335f) : new Color(0.77f, 0.805f, 0.84f);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                Vector2 q = new Vector2(Mathf.Abs(x + 0.5f - n / 2f), Mathf.Abs(y + 0.5f - n / 2f)) - Vector2.one * 6f;
                float d = Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - 9f;
                Color c = Color.Lerp(fill, border, Mathf.Clamp01(d + 1.2f)); c.a = Mathf.Clamp01(0.5f - d);
                pixels[y * n + x] = c;
            }
            texture.SetPixels(pixels); texture.Apply(false, true); Panels.Add(dark, texture); return texture;
        }

        internal static bool Foldout(bool expanded, string label, string summary)
        {
            Rect r = EditorGUILayout.GetControlRect(false, 39f);
            Rounded(r, EditorGUIUtility.isProSkin ? new Color(0.19f, 0.205f, 0.225f) : new Color(0.86f, 0.885f, 0.91f));
            var style = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            expanded = EditorGUI.Foldout(new Rect(r.x + 18f, r.y, r.width - 26f, r.height), expanded, label, true, style);
            float labelWidth = style.CalcSize(new GUIContent(label)).x + 36f;
            float summaryWidth = Mathf.Min(180f, r.width - labelWidth - 16f);
            if (summaryWidth >= 125f)
                GUI.Label(new Rect(r.xMax - summaryWidth - 12f, r.y + 10f, summaryWidth, 18f), summary,
                    new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, clipping = TextClipping.Clip, normal = { textColor = Muted } });
            return expanded;
        }

        internal static void Divider(string label)
        {
            Rect r = EditorGUILayout.GetControlRect(false, 28f);
            var style = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = Muted } };
            GUI.Label(r, label.ToUpperInvariant(), style);
        }

        internal static bool Action(string label, bool primary = false)
        {
            Color old = GUI.backgroundColor;
            if (primary) GUI.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.34f, 0.62f, 0.82f) : new Color(0.64f, 0.81f, 0.94f);
            bool clicked = GUILayout.Button(label, new GUIStyle(EditorStyles.miniButton) { fixedHeight = 32f, fontStyle = primary ? FontStyle.Bold : FontStyle.Normal });
            GUI.backgroundColor = old; return clicked;
        }

        internal static void FeaturedSlider(SerializedProperty property, string label, float min, float max, string left, string right)
        {
            Rect r = EditorGUILayout.GetControlRect(false, 55f);
            var content = new GUIContent(label);
            EditorGUI.BeginProperty(r, content, property);
            GUI.Label(new Rect(r.x, r.y, r.width - 78f, 20f), content, EditorStyles.boldLabel);
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            float value = EditorGUI.FloatField(new Rect(r.xMax - 66f, r.y, 66f, 20f), property.floatValue);
            value = GUI.HorizontalSlider(new Rect(r.x, r.y + 25f, r.width, 14f), value, min, max);
            if (EditorGUI.EndChangeCheck()) property.floatValue = Mathf.Clamp(value, min, max);
            EditorGUI.showMixedValue = false;
            var caption = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Muted } };
            GUI.Label(new Rect(r.x, r.y + 39f, r.width * 0.5f, 14f), left, caption);
            caption.alignment = TextAnchor.UpperRight;
            GUI.Label(new Rect(r.center.x, r.y + 39f, r.width * 0.5f, 14f), right, caption);
            EditorGUI.EndProperty();
        }

        internal static void TogglePair(SerializedProperty first, string firstLabel, SerializedProperty second, string secondLabel)
        {
            Rect r = EditorGUILayout.GetControlRect(false, 29f);
            ToggleChip(new Rect(r.x, r.y, (r.width - 6f) * 0.5f, r.height), first, firstLabel);
            using (new EditorGUI.DisabledScope(!first.boolValue && !first.hasMultipleDifferentValues))
                ToggleChip(new Rect(r.center.x + 3f, r.y, (r.width - 6f) * 0.5f, r.height), second, secondLabel);
        }
        private static void ToggleChip(Rect r, SerializedProperty property, string label)
        {
            EditorGUI.BeginProperty(r, new GUIContent(label), property);
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            Color old = GUI.backgroundColor;
            if (property.boolValue && !property.hasMultipleDifferentValues) GUI.backgroundColor = new Color(0.52f, 0.73f, 0.85f);
            EditorGUI.BeginChangeCheck();
            bool value = GUI.Toggle(r, property.boolValue, new GUIContent(property.hasMultipleDifferentValues ? label + " —" : label, "Toggle " + label.ToLowerInvariant()), EditorStyles.miniButton);
            if (EditorGUI.EndChangeCheck()) property.boolValue = value;
            GUI.backgroundColor = old; EditorGUI.showMixedValue = false; EditorGUI.EndProperty();
        }

        internal static void SpriteSource(SerializedProperty property)
        {
            Rect r = EditorGUILayout.GetControlRect(false, 70f);
            EditorGUI.BeginProperty(r, new GUIContent("Shape sprite"), property);
            var sprite = property.objectReferenceValue as Sprite;
            Rect preview = new Rect(r.x, r.y + 2f, 60f, 60f);
            Rounded(preview, EditorGUIUtility.isProSkin ? new Color(0.13f, 0.16f, 0.20f) : new Color(0.8f, 0.84f, 0.88f), 8f);
            if (sprite != null && !property.hasMultipleDifferentValues)
            {
                Texture icon = AssetPreview.GetAssetPreview(sprite) ?? AssetPreview.GetMiniThumbnail(sprite);
                if (icon != null) GUI.DrawTexture(new Rect(preview.x + 5f, preview.y + 5f, 50f, 50f), icon, ScaleMode.ScaleToFit);
            }
            float left = r.x + 72f, width = r.width - 72f;
            GUI.Label(new Rect(left, r.y, width, 18f), "Shape sprite", EditorStyles.boldLabel);
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.PropertyField(new Rect(left, r.y + 24f, width, 20f), property, GUIContent.none);
            EditorGUI.showMixedValue = false;
            GUI.Label(new Rect(left, r.y + 48f, width, 18f), "Alpha defines the optical edge", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Muted } });
            EditorGUI.EndProperty();
        }

        internal static void MemberRow(TranslucentGlassMember member, int number)
        {
            Rect r = EditorGUILayout.GetControlRect(false, 43f);
            Rounded(r, EditorGUIUtility.isProSkin ? new Color(0.16f, 0.18f, 0.205f) : new Color(0.84f, 0.87f, 0.90f), 7f);
            Rect toggle = new Rect(r.x + 9f, r.y + 12f, 18f, 18f);
            EditorGUI.BeginChangeCheck();
            bool included = GUI.Toggle(toggle, member.Include, new GUIContent("", "Include " + member.name));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(member, "Toggle fusion shape"); member.Include = included;
                PrefabUtility.RecordPrefabInstancePropertyModifications(member); EditorUtility.SetDirty(member);
            }
            Color ink = member.Operation == GlassOperation.Cutout ? CutoutColor : Accent;
            GUI.Label(new Rect(r.x + 34f, r.y + 11f, 27f, 21f), number.ToString("00"), new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = ink } });
            float nameWidth = Mathf.Max(30f, r.width - 113f);
            GUI.Label(new Rect(r.x + 66f, r.y + 4f, nameWidth, 19f), member.name, new GUIStyle(EditorStyles.boldLabel) { clipping = TextClipping.Clip });
            string shape = member.ShapeSprite != null ? member.ShapeSprite.name : ObjectNames.NicifyVariableName(member.Shape.ToString());
            string state = !member.isActiveAndEnabled || !member.Include ? "Excluded" : member.Operation == GlassOperation.Cutout ? "Cutout" : "Add";
            GUI.Label(new Rect(r.x + 66f, r.y + 22f, nameWidth, 16f), state + " · " + shape, new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Muted }, clipping = TextClipping.Clip });
            if (GUI.Button(new Rect(r.xMax - 41f, r.y + 9f, 33f, 24f), new GUIContent("Edit", "Edit " + member.name), EditorStyles.miniButton)) Selection.activeGameObject = member.gameObject;
        }

        internal static void Popup(SerializedProperty property, string label, string[] options)
        {
            Rect r = EditorGUILayout.GetControlRect(false, 24f);
            var content = new GUIContent(label);
            EditorGUI.BeginProperty(r, content, property);
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int value = EditorGUI.Popup(r, label, property.hasMultipleDifferentValues ? -1 : property.intValue, options);
            if (EditorGUI.EndChangeCheck()) property.intValue = value;
            EditorGUI.showMixedValue = false;
            EditorGUI.EndProperty();
        }

        internal static void Quality(SerializedProperty property)
        {
            Rect r = EditorGUILayout.GetControlRect(false, 27f);
            EditorGUI.BeginProperty(r, new GUIContent("Capture quality"), property);
            Rect field = EditorGUI.PrefixLabel(r, new GUIContent("Capture quality"));
            EditorGUI.BeginChangeCheck();
            int value = GUI.Toolbar(field, property.hasMultipleDifferentValues ? -1 : property.intValue, new[] { "Low", "Medium", "High" });
            if (EditorGUI.EndChangeCheck()) property.intValue = value;
            EditorGUI.EndProperty();
            string[] descriptions = { "Lightest capture. Best for small surfaces.", "Balanced capture detail and rendering cost.", "Sharpest capture. Best for large glass surfaces." };
            GUILayout.Label(property.hasMultipleDifferentValues ? "Different capture qualities selected." : descriptions[Mathf.Clamp(property.intValue, 0, 2)], EditorStyles.wordWrappedMiniLabel);
        }

        internal static void LightDirection(SerializedProperty property)
        {
            Rect row = EditorGUILayout.GetControlRect(false, 90f);
            EditorGUI.BeginProperty(row, new GUIContent("Light direction"), property);
            Rect pad = new Rect(row.x, row.y + 3f, 80f, 80f);
            Rounded(pad, EditorGUIUtility.isProSkin ? new Color(0.11f, 0.15f, 0.2f) : new Color(0.77f, 0.83f, 0.89f), 40f);
            EditorGUIUtility.AddCursorRect(pad, MouseCursor.Pan);
            Color line = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.13f) : new Color(0f, 0f, 0f, 0.13f);
            EditorGUI.DrawRect(new Rect(pad.center.x, pad.y + 8f, 1f, 64f), line);
            EditorGUI.DrawRect(new Rect(pad.x + 8f, pad.center.y, 64f, 1f), line);
            int id = GUIUtility.GetControlID(FocusType.Passive, pad);
            Event e = Event.current;
            if (GUI.enabled && e.type == EventType.MouseDown && e.button == 0 && pad.Contains(e.mousePosition)) { GUIUtility.hotControl = id; e.Use(); }
            if (GUIUtility.hotControl == id && (e.type == EventType.MouseDrag || e.type == EventType.Used))
            {
                Vector2 value = (e.mousePosition - pad.center) / 30f;
                value.y = -value.y;
                property.vector2Value = Vector2.ClampMagnitude(value, 1f);
                GUI.changed = true;
                if (e.type != EventType.Used) e.Use();
            }
            if (e.type == EventType.MouseUp && GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }
            Vector2 direction = Vector2.ClampMagnitude(property.vector2Value, 1f);
            Rect dot = new Rect(pad.center.x + direction.x * 30f - 4f, pad.center.y - direction.y * 30f - 4f, 8f, 8f);
            if (!property.hasMultipleDifferentValues)
            {
                Rounded(new Rect(dot.x - 4f, dot.y - 4f, 16f, 16f), new Color(Accent.r, Accent.g, Accent.b, 0.18f), 8f);
                Rounded(dot, Accent, 4f);
            }
            float left = pad.xMax + 14f;
            GUI.Label(new Rect(left, row.y + 2f, row.xMax - left, 20f), "Light direction", EditorStyles.boldLabel);
            GUI.Label(new Rect(left, row.y + 23f, row.xMax - left, 18f), "Drag the light across the pad.", EditorStyles.miniLabel);
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            Vector2 edited = EditorGUI.Vector2Field(new Rect(left, row.y + 48f, row.xMax - left, 36f), GUIContent.none, property.vector2Value);
            if (EditorGUI.EndChangeCheck()) property.vector2Value = edited;
            EditorGUI.showMixedValue = false;
            EditorGUI.EndProperty();
        }

        internal static void Header(string title, string subtitle, string status)
        {
            Rect r = GUILayoutUtility.GetRect(0f, 103f, GUILayout.ExpandWidth(true));
            bool dark = EditorGUIUtility.isProSkin;
            Rounded(r, dark ? new Color(0.075f, 0.115f, 0.17f) : new Color(0.82f, 0.895f, 0.96f), 12f);
            // Two translucent panes form a small, original glass mark.
            Rect mark = new Rect(r.x + 17f, r.y + 18f, 27f, 27f);
            Rounded(mark, new Color(0.45f, 0.75f, 0.98f, 0.35f), 8f);
            Rounded(new Rect(mark.x + 12f, mark.y + 8f, 27f, 27f), new Color(0.72f, 0.89f, 1f, 0.55f), 8f);
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = r.width < 320f ? 15 : 18, normal = { textColor = dark ? new Color(0.93f, 0.96f, 1f) : new Color(0.12f, 0.24f, 0.36f) } };
            GUI.Label(new Rect(r.x + 68f, r.y + 17f, r.width - 78f, 26f), title, titleStyle);
            GUI.Label(new Rect(r.x + 69f, r.y + 43f, r.width - 79f, 17f), "TRANSLUCENT UI FX", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Accent } });
            EditorGUI.DrawRect(new Rect(r.x + 17f, r.y + 70f, r.width - 34f, 1f), new Color(0.5f, 0.72f, 0.9f, 0.18f));
            GUI.Label(new Rect(r.x + 17f, r.y + 78f, r.width - 92f, 17f), subtitle, new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = dark ? new Color(0.68f, 0.79f, 0.87f) : Muted } });
            Color badge = status == "READY" ? (dark ? new Color(0.5f, 0.9f, 0.76f) : new Color(0.1f, 0.43f, 0.33f)) : Accent;
            GUI.Label(new Rect(r.xMax - 73f, r.y + 78f, 56f, 17f), status, new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleRight, normal = { textColor = badge } });
            EditorGUILayout.Space(9f);
        }

        internal static void Title(string title, string detail = null)
        {
            Rect r = EditorGUILayout.GetControlRect(false, 25f);
            GUI.Label(r, title, new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 });
            if (!string.IsNullOrEmpty(detail)) GUILayout.Label(detail, new GUIStyle(EditorStyles.wordWrappedMiniLabel) { normal = { textColor = Muted } });
            EditorGUILayout.Space(6f);
        }

        internal static void Slider(SerializedProperty property, string label, float min, float max, string tooltip = null)
        {
            // Explicit property controls avoid drawing runtime Header attributes again.
            Rect r = EditorGUILayout.GetControlRect(false, 24f);
            if (property.propertyType == SerializedPropertyType.Integer) EditorGUI.IntSlider(r, property, (int)min, (int)max, new GUIContent(label, tooltip));
            else EditorGUI.Slider(r, property, min, max, new GUIContent(label, tooltip));
        }

        internal static bool Toggle(SerializedProperty property, string label, string tooltip = null)
        {
            Rect r = EditorGUILayout.GetControlRect(false, 24f);
            EditorGUI.BeginProperty(r, new GUIContent(label, tooltip), property);
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            bool value = EditorGUI.Toggle(r, new GUIContent(label, tooltip), property.boolValue);
            if (EditorGUI.EndChangeCheck()) property.boolValue = value;
            EditorGUI.showMixedValue = false;
            EditorGUI.EndProperty();
            return value;
        }

        internal static void Shape(SerializedProperty shape, SerializedProperty radius, SerializedProperty smoothing,
            SerializedProperty individual, SerializedProperty corners, SerializedProperty custom = null)
        {
            int columns = EditorGUIUtility.currentViewWidth < 340f ? 2 : 4;
            Rect grid = GUILayoutUtility.GetRect(0f, (Order.Length / columns) * 56f, GUILayout.ExpandWidth(true));
            float width = (grid.width - (columns - 1) * 4f) / columns;
            EditorGUI.BeginProperty(grid, GUIContent.none, shape);
            for (int i = 0; i < Order.Length; i++)
            {
                int id = Order[i];
                Rect cell = new Rect(grid.x + i % columns * (width + 4f), grid.y + i / columns * 56f, width, 52f);
                bool selected = !shape.hasMultipleDifferentValues && shape.intValue == id;
                Rounded(cell, selected ? (EditorGUIUtility.isProSkin ? new Color(0.19f, 0.34f, 0.45f) : new Color(0.68f, 0.81f, 0.92f)) : (EditorGUIUtility.isProSkin ? new Color(0.16f, 0.18f, 0.21f) : new Color(0.82f, 0.855f, 0.89f)), 7f);
                if (GUI.Button(cell, new GUIContent("", Names[id]), GUIStyle.none)) shape.intValue = id;
                if (selected) Rounded(new Rect(cell.center.x - 9f, cell.yMax - 3f, 18f, 2f), Accent, 1f);
                GUI.DrawTexture(new Rect(cell.center.x - 24f, cell.y + 4f, 48f, 26f), Icon(id), ScaleMode.ScaleToFit);
                var label = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 10 };
                GUI.Label(new Rect(cell.x, cell.y + 31f, cell.width, 16f), Names[id], label);
            }
            EditorGUI.EndProperty();
            EdgeShape type = (EdgeShape)shape.intValue;
            if (type != EdgeShape.RoundedRect && type != EdgeShape.ContinuousRoundedRect && !shape.hasMultipleDifferentValues) return;
            EditorGUILayout.Space(4f);
            if (custom != null)
            {
                Toggle(custom.FindPropertyRelative("Enabled"), "Custom corner profiles", "Independent sizes and curves, including chamfers and concave corners.");
                if (custom.FindPropertyRelative("Enabled").boolValue)
                {
                    Popup(custom.FindPropertyRelative("Units"), "Corner units", new[] { "Canvas units", "% of shorter side", "% of longer side" });
                    Toggle(custom.FindPropertyRelative("Normalize"), "Fit corners", "Scale overlapping corners together to keep them inside the shape.");
                    bool continuous = Toggle(custom.FindPropertyRelative("Continuous"), "Continuous curves");
                    var radii = custom.FindPropertyRelative("Radii");
                    var curves = custom.FindPropertyRelative("Curves");
                    Rect heading = EditorGUILayout.GetControlRect(false, 19f);
                    float column = Mathf.Max(30f, (heading.width - 93f) * 0.5f);
                    GUI.Label(new Rect(heading.x, heading.y, 85f, 19f), "Corner", EditorStyles.miniLabel);
                    GUI.Label(new Rect(heading.x + 89f, heading.y, column, 19f), "Radius", EditorStyles.miniLabel);
                    GUI.Label(new Rect(heading.x + 93f + column, heading.y, column, 19f), continuous ? "Smoothing" : "Concavity", EditorStyles.miniLabel);
                    string[] cornerNames = { "Top left", "Top right", "Bottom right", "Bottom left" };
                    string[] axes = { "x", "y", "z", "w" };
                    for (int c = 0; c < 4; c++)
                    {
                        Rect r = EditorGUILayout.GetControlRect(false, 24f);
                        GUI.Label(new Rect(r.x, r.y, 85f, r.height), cornerNames[c]);
                        float fieldWidth = Mathf.Max(30f, (r.width - 93f) * 0.5f);
                        var radiusProperty = radii.FindPropertyRelative(axes[c]);
                        var curveProperty = curves.FindPropertyRelative(axes[c]);
                        EditorGUI.PropertyField(new Rect(r.x + 89f, r.y, fieldWidth, r.height), radiusProperty, GUIContent.none);
                        EditorGUI.PropertyField(new Rect(r.x + 93f + fieldWidth, r.y, fieldWidth, r.height), curveProperty, GUIContent.none);
                        if (!radiusProperty.hasMultipleDifferentValues) radiusProperty.floatValue = Mathf.Max(0f, radiusProperty.floatValue);
                        if (!curveProperty.hasMultipleDifferentValues) curveProperty.floatValue = Mathf.Clamp(curveProperty.floatValue, 0f, continuous ? 1f : 2f);
                    }
                    GUILayout.Label(continuous ? "0 = round  /  1 = square transition" : "0 = round  /  1 = chamfer  /  2 = concave", EditorStyles.wordWrappedMiniLabel);
                    return;
                }
            }
            Toggle(individual, "Individual corners", "Control the four corners separately.");
            if (individual.boolValue && !individual.hasMultipleDifferentValues)
            {
                Rect row = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 2f + 3f);
                EditorGUI.BeginProperty(row, new GUIContent("Corners"), corners);
                EditorGUI.showMixedValue = corners.hasMultipleDifferentValues;
                EditorGUI.BeginChangeCheck();
                Vector4 value = corners.vector4Value;
                float oldWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 70f;
                string[] labels = { "Top left", "Top right", "Bottom right", "Bottom left" };
                int[] positions = { 0, 1, 3, 2 };
                for (int i = 0; i < 4; i++)
                {
                    Rect field = new Rect(row.x + i % 2 * row.width * 0.5f, row.y + i / 2 * (EditorGUIUtility.singleLineHeight + 3f), row.width * 0.5f - 5f, EditorGUIUtility.singleLineHeight);
                    int index = positions[i];
                    value[index] = EditorGUI.FloatField(field, labels[index], value[index]);
                }
                EditorGUIUtility.labelWidth = oldWidth;
                if (EditorGUI.EndChangeCheck()) corners.vector4Value = GlassShapeUtility.ClampCorners(value);
                EditorGUI.showMixedValue = false;
                EditorGUI.EndProperty();
                EditorGUILayout.LabelField("0 = square  /  0.5 = half the shorter side", EditorStyles.miniLabel);
            }
            else Slider(radius, "Corner radius", 0f, 0.5f);
            if (type == EdgeShape.ContinuousRoundedRect || shape.hasMultipleDifferentValues)
                Slider(smoothing, "Corner smoothing", 0f, 1f, "Blend from circular corners to flatter, continuous corners.");
        }

        internal static Texture2D Icon(int shape)
        {
            int key = shape + (EditorGUIUtility.isProSkin ? 100 : 0);
            if (Icons.TryGetValue(key, out Texture2D existing)) return existing;
            const int w = 96, h = 52;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            Color color = EditorGUIUtility.isProSkin ? new Color(0.75f, 0.86f, 0.97f) : new Color(0.18f, 0.35f, 0.52f);
            Color[] pixels = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float d = GlassShapeUtility.Distance(new Vector2(x + 0.5f - w / 2f, y + 0.5f - h / 2f), new Vector2(64f, 40f), (EdgeShape)shape, 0.28f, 0.82f);
                color.a = Mathf.Clamp01(0.5f - d) * (d < -2f ? 0.28f : 1f);
                pixels[y * w + x] = color;
            }
            texture.SetPixels(pixels); texture.Apply(false, true); Icons.Add(key, texture); return texture;
        }
    }
}
