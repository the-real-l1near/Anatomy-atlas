using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TranslucentUIFX.Tests.Editor
{
    public sealed class GlassPlaygroundInputTests
    {
        [UnityTearDown]
        public IEnumerator RestoreEditMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator PlaygroundRespondsToRaycastClicksDragsShapesAndCutouts()
        {
            EditorSceneManager.OpenScene("Assets/Translucent UI FX/Demo/Scenes/Demo - Liquid Glass.unity");
            yield return new EnterPlayMode();
            yield return null;
            yield return null;
            var root = GameObject.Find("Glass Playground").transform;
            // Hidden Test Runner Game Views can report a viewport only a few pixels tall.
            // Use the shipped Canvas and controls with a deterministic camera viewport;
            // no editor window is resized or focused, and ExitPlayMode restores the scene.
            Canvas canvas = root.GetComponent<Canvas>();
            Camera camera = Camera.main;
            RenderTexture viewport = new RenderTexture(1440, 900, 24);
            viewport.Create(); camera.targetTexture = viewport;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1f;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            var glass = root.Find("Shape Preview").GetComponent<TranslucentImageFX>();
            var group = root.Find("Fusion Surface").GetComponent<TranslucentGlassGroup>();
            Assert.NotNull(EventSystem.current);
            Assert.NotNull(EventSystem.current.currentInputModule);
            Click((RectTransform)root.Find("Continuous"));
            Assert.IsNull(glass.sprite, "Procedural selection must clear an assigned sprite.");
            foreach (string name in new[] { "Blur", "Refraction", "Radius", "Blend" })
            {
                Slider slider = root.Find(name).GetComponent<Slider>();
                Assert.NotNull(slider.handleRect, name + " needs a visible handle.");
                Assert.GreaterOrEqual(((RectTransform)slider.transform).rect.height, 44f);
                RectTransform area = (RectTransform)slider.handleRect.parent;
                // Click well above the thin visual track, inside the intended 44px target.
                Vector2 position = Screen(area, new Vector2(Mathf.Lerp(area.rect.xMin, area.rect.xMax, 0.75f), 16f));
                PointerEventData pointer = Raycast(position);
                Assert.AreEqual(slider, pointer.pointerCurrentRaycast.gameObject.GetComponentInParent<Slider>(), name + " is blocked by another Graphic.");
                ExecuteEvents.ExecuteHierarchy(pointer.pointerCurrentRaycast.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                Assert.AreEqual(0.75f, slider.normalizedValue, 0.025f, name + " did not respond to a click.");
                pointer.position = Screen(area, new Vector2(Mathf.Lerp(area.rect.xMin, area.rect.xMax, 0.3f), 0f));
                ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.dragHandler);
                ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                Assert.AreEqual(0.3f, slider.normalizedValue, 0.025f, name + " did not respond to drag.");
            }
            Assert.AreEqual(0.3f, glass.BlurStrength, 0.025f);
            Assert.AreEqual(0.036f, glass.RefractionAmount, 0.003f);
            Assert.AreEqual(0.15f, glass.EdgeRounding, 0.015f);
            Assert.AreEqual(28.8f, group.FusionSoftness, 1f);
            string[] names = { "Continuous", "Rounded", "Capsule", "Circle", "Rectangle", "Diamond", "Triangle", "Hexagon" };
            for (int i = 0; i < names.Length; i++)
            {
                Click((RectTransform)root.Find(names[i]));
                Assert.AreEqual(Demo.LiquidGlassDemoController.Shapes[i], glass.EdgeShape);
            }
            var member = root.Find("Fusion Surface/Satellite").GetComponent<TranslucentGlassMember>();
            Vector3 before = member.transform.localPosition;
            PointerEventData drag = Raycast(Screen(member.RectTransform, Vector2.zero));
            Assert.AreSame(member.gameObject, drag.pointerCurrentRaycast.gameObject);
            ExecuteEvents.Execute(member.gameObject, drag, ExecuteEvents.beginDragHandler);
            drag.position += Vector2.right * 36f;
            ExecuteEvents.Execute(member.gameObject, drag, ExecuteEvents.dragHandler);
            Assert.Greater(member.transform.localPosition.x, before.x + 10f);
            Click((RectTransform)root.Find("Cutout Toggle/Box"));
            var hole = root.Find("Fusion Surface/Cutout").GetComponent<TranslucentGlassMember>();
            Assert.IsTrue(hole.Include);
            Assert.IsFalse(group.ContainsScreenPoint(Screen(hole.RectTransform, Vector2.zero), camera));
            Click((RectTransform)root.Find("Reset"));
            Assert.AreEqual(0.08f, glass.BlurStrength);
            Assert.AreEqual(0.035f, glass.RefractionAmount);
            Assert.IsFalse(hole.Include);
            Assert.AreEqual(before, member.transform.localPosition);
            camera.targetTexture = null;
            viewport.Release(); Object.Destroy(viewport);
            yield return new ExitPlayMode();
        }

        private static Vector2 Screen(RectTransform rect, Vector2 local)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            return RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, rect.TransformPoint(local));
        }
        private static PointerEventData Raycast(Vector2 position)
        {
            var pointer = new PointerEventData(EventSystem.current) { position = position, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            string details = "";
            if (hits.Count == 0)
            {
                foreach (var raycaster in RaycasterManager.GetRaycasters()) details += $" Raycaster={raycaster.name}, active={raycaster.isActiveAndEnabled}, camera={raycaster.eventCamera}";
                foreach (var graphic in Object.FindObjectsByType<Graphic>(FindObjectsSortMode.None))
                    if (graphic.name == "Background") details += $" Background={graphic.transform.parent.name}, depth={graphic.depth}, cull={graphic.canvasRenderer.cull}, target={graphic.raycastTarget}, contains={RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform, position, null)}, raycast={graphic.Raycast(position, null)}";
            }
            Assert.IsNotEmpty(hits, "No Graphic received the pointer at " + position + " Screen=" + ScreenSize() + details);
            pointer.pointerCurrentRaycast = hits[0]; pointer.pointerPressRaycast = hits[0];
            return pointer;
        }
        private static string ScreenSize() => UnityEngine.Screen.width + "x" + UnityEngine.Screen.height;
        private static void Click(RectTransform rect)
        {
            PointerEventData pointer = Raycast(Screen(rect, rect.rect.center));
            ExecuteEvents.ExecuteHierarchy(pointer.pointerCurrentRaycast.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(pointer.pointerCurrentRaycast.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(pointer.pointerCurrentRaycast.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
    }
}
