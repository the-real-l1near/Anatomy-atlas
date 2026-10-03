# Translucent UI FX

Release 3.2.1 | Unity 6.3 LTS / URP 17.3 | uGUI

Liquid glass for Unity uGUI on URP. Start with one component; reveal deeper controls when you need them.

## Start here

1. Open **Tools > Translucent UI FX > Setup & Diagnostics** and install the renderer feature once.
2. Choose **GameObject > UI > Liquid Glass Panel**, or add **UI > Liquid Glass** to an empty UI RectTransform. Liquid Glass is an Image itself.
3. Resize it, choose a shape, then adjust Tint, Blur or Refraction. No material asset is required.

For the demos, install Input System and choose **Window > TextMeshPro > Import TMP Essential Resources** once for the project. Then open **Demo/Scenes/Demo - Liquid Glass** and press **Play** to try the sliders, eight shapes, pointer lighting, draggable fusion and cutout. Inspector controls also work in Edit mode. The radius slider is intentionally disabled for sprites and shapes without adjustable corners. Reset restores appearance and fusion positions while keeping your chosen silhouette.

The playground uses original blue-and-copper satin artwork with optional slow motion, so refraction remains visible against detailed curves. Its desktop layout scales to both widescreen and 16:10 views.

## A small inspector with room to grow

The visible cards are **Appearance**, **Shape** and **Fusion**. Optional **Optics**, **Lighting**, **Background** and **Image & Interaction** sections start collapsed. Controls appear only when relevant. The material inspector points back to the component instead of exposing obsolete recipes.

| Area | Controls |
| --- | --- |
| Appearance | Tint, blur, refraction, highlights, follow pointer |
| Shape | Visual shape picker or sprite alpha outline; optional corner profiles |
| Fusion | Create a group, add shapes/cutouts, blend distance, member list |
| Optics | Fade, lip width/units, smoothing, lens depth/index, dispersion, magnification, transmission |
| Lighting | Direction pad, opposing or point light, lip bands/colors, soft shadow |
| Background | Automatic background, quality and refresh; optional advanced overrides |
| Image & Interaction | Native Image settings, color, masking, shape-aware raycasts |

Fusion groups include a **Composition map** of their actual outlines. Blue shows the joined surface; warm outlines identify cutouts. Click a numbered marker or its matching member row to edit the shape. The map is a geometry guide, not an optical preview; use Game View to judge the final glass. Inclusion toggles let you temporarily remove a member without deleting it.

The inspector uses theme-aware cards, larger Blur/Refraction controls, sprite thumbnails and a circular light-direction pad. Advanced foldouts remember their state for the current Editor session.

**Reset appearance** preserves shapes, sprite sources and capture settings. **Refraction = 0** switches bending off; raise it to see the optical edge.

## Shapes

Choose Continuous, Rounded, Capsule, Circle, Rectangle, Diamond, Triangle or Hexagon. Circle remains circular in a wide RectTransform. Capsule follows the longer axis.

Rounded/Continuous provide a simple radius and optional independent corners. Enable **Custom corner profiles** for absolute or relative radii, normalized opposing corners, and independent curvature. Continuous profiles range from round to squircle; geometric profiles range from round through chamfer to concave. Hit areas follow the same outline.

Assign **Shape sprite** to use its alpha for the silhouette and refractive boundary. Sprite RGB does not color the glass. Read/Write is not required; sprite mesh/UV extraction supports atlas regions, packed orientation and pivots. Clear the source to return to procedural shapes. **Alpha threshold** selects the optical contour. The original sprite alpha retains soft transparency on standalone Images.

Sprite fields are shared by sprite, threshold and resolution while in use. **Shape detail** offers 64, 128, 256, 512 and 1024; 256 is the default. Generation uses GPU alpha extraction followed by a CPU distance transform and a texture upload. A cache miss includes a synchronous readback; avoid creating many new high-resolution sprite shapes in a latency-sensitive frame. Moving or rotating a shape reuses its field. Texture update counts and Editor imports invalidate changed sources. Call `GlassSpriteField.InvalidateAll()` after runtime geometry changes such as `Sprite.OverrideGeometry`.

## Fusion

1. Select sibling Liquid Glass objects and choose **Fuse shapes**, or create a group from one object.
2. Move, rotate, resize or duplicate its members. Nearby shapes merge into one surface.
3. Adjust **Blend distance**: zero gives a sharp union; larger values form a soft bridge.
4. Add a **Cutout** to remove glass and its pointer hit area.

Sprite and procedural members can mix, including sprite cutouts. Capacity grows dynamically; there is no eight-member authoring cap. Cost still grows with covered pixels and member count. Split very large or widely separated collections into smaller groups.

Members inherit group appearance by default. Their collapsed **Appearance** section can override tint, graphic color, blur, opacity, refraction, dispersion, lens depth/index, magnification, transmission, lip width/smoothing and light bands. Values blend through joins. The group supplies shared light direction/colors and one shadow for the combined silhouette.

Conversion records one Undo operation, preserves transforms, child content, source appearance and Button/Toggle graphic references. Differently colored or faded sources retain independent appearance. **Detach as standalone glass** retains shape edits and custom appearance. Filled, sliced and tiled sprite Images remain standalone; use Simple for sprite fusion. Prefab instances and Layout Group-controlled siblings receive guidance before hierarchy conversion.

Keep group Raycast Target off; put controls and drag behavior on members. Nested groups own their members independently. Rendering bounds follow moving members. Under RectMask2D, keep the group layout rectangle covering its members so Unity's parent-mask culling includes them.

## Optics and light

A narrow optical lip bends the backdrop at the boundary while the center stays clear. Lip width accepts Canvas units or a percentage of either side. Smoothing softens optical normals without changing the silhouette. Interior stabilization suppresses joins inside broad surfaces. Magnification zooms the captured background around the shape center; Transmission scales its brightness.

Directional light uses the visual direction pad. Opposing adds a second light; Point uses normalized viewport position and reach. Inner/outer lip bands accept fractions of the optical lip or Canvas units. Light and complementary shade colors have independent alpha. A shadow uses screen-pixel size and offset; its color alpha of zero disables it. Shadows may also be colored glows.

## Background and layered UI

The release targets **Unity 6.3 LTS (6000.3), URP 17.3 with RenderGraph, uGUI and shader-model-4.5-class hardware** with structured buffers, texture arrays and RFloat textures. The earlier 3.2 implementation was validated on Unity 6000.2.6f2 / Metal. Release 3.2.1 is validated on Unity 6000.3.24f1 / Metal. Other graphics APIs and target-device performance need platform validation; WebGL is not supported by this shader path.

Each camera/layer pair shares a retained source and blurred texture. The strongest visible matching component quality/blur request determines the shared work. Cameras with no matching visible consumers skip the glass pass. Panels share automatic camera lookups, unchanged texture bindings are reused, and the final blur writes directly to the retained image without an extra copy. High retains a full-resolution source; Medium and Low reduce bandwidth. The renderer feature exposes optional mip count, blur passes, kernel spread, dithering and color format. Unsupported format overrides fall back to the camera format. Capture covers the whole camera image, so a separate capture-padding control is unnecessary.

Use **Always** for moving backgrounds. **Smart Update** notices camera/projection changes and explicit requests, not arbitrary animation behind a stationary camera. **Interval** refreshes periodically. **Manual** retains the image until `TranslucentRendererFeature.RequestUpdate()`; changing blur while frozen does not pretend the stored image was regenerated.

Ordinary glass needs no camera assignment. Camera-space/world-space UI uses its Canvas camera. Overlay UI prefers a suitable Main Camera, then an active game camera on the same display, favoring full scene views over minimaps and UI-only cameras. Automatic selection excludes render-to-texture cameras and URP Overlay cameras and updates as cameras switch. A background is a temporary image in GPU memory for blur and refraction; nothing is recorded or saved.

**Background → Advanced background override** retains optional camera/layer routing for special setups. Alternatively add **Liquid Glass Capture Source** to a Canvas or ancestor to share an override. A **Liquid Glass Camera Override** on a camera can override quality and blur work. Without a usable background, the surface temporarily uses its translucent tint; glass optics resume when a background becomes available. The inspector checks the chosen camera's renderer and offers setup if its feature is missing or disabled.

Glass can sample only content drawn before its capture. For camera/world-space UI on the same source camera, capture **Before transparents**. For Overlay glass over scene transparency, capture **After transparents**. Overlay sibling graphics are drawn too late to enter the capture.

For glass over glass on one camera, add two renderer features with distinct layer numbers: layer 0 before transparents, then draw the back camera-space Canvas; layer 1 after transparents includes that Canvas. Front Overlay glass selects layer 1. Additional camera-space UI levels require explicit Render Objects/camera ordering. Two simultaneous cameras retain independent captures; rendering one does not replace the other's source.

## Interaction and integration

Screen Space Overlay, Screen Space Camera and World Space canvases use normal uGUI rendering. Mask, RectMask2D, CanvasGroup and atlas workflows remain available. Sprite raycasts follow the alpha field; Filled and border-only sliced/tiled Images also reject regions omitted from their mesh. Silhouette raycast padding adjusts the shape test within Unity's normal Graphic hit rectangle. Scene View is an authoring preview; judge optics in Game View.

```csharp
using TranslucentUIFX;

glass.ApplyLiquidGlass();
glass.BlurStrength = 0.12f;
glass.sprite = myShapeSprite;
glass.SetAllDirty();
TranslucentRendererFeature.RequestUpdate();
```

Clear both `sprite` and `overrideSprite` before selecting procedural geometry in code. Old `GlassPreset` numeric IDs remain compatible: named legacy styles now resolve to Liquid Glass; Custom leaves values intact. Existing serialized values remain until reset.

## Validation and package boundary

Tests include rendered refraction, sprite extraction, mixed fusion/cutouts, fades, shadows, corners, real EventSystem slider input, authoring/detach and camera/layer capture routing. The Editor benchmark is a regression check, not a player frame-rate guarantee or a comparison with another asset.

The production package includes **Assets/Translucent UI FX**, excluding the development **Tests** folder. When exporting your own additions, include any custom assets referenced by your scenes. The prepared playground uses package-owned assets and procedural shapes; no external sprite is required. Tests additionally require Unity Test Framework. The package includes its own demo backdrop and bundled Inter font.

The release is self-contained within its product folder. Development tools, tests, reference packages and project settings are excluded. The demo uses Inter under the SIL Open Font License 1.1; see **Third-Party Notices.txt** and the full bundled font license.

For help, open **Setup & help** or visit https://discord.gg/bXABNPthTb. Include Unity/URP versions, graphics API, Canvas mode and a minimal reproduction.

## Troubleshooting

- **Glass shows only a tint:** expand Background. Use Set up background if the selected camera renderer lacks the enabled feature. Confirm URP is active for the current Graphics and Quality settings, RenderGraph is enabled, and an active game camera renders to the intended display. Normal panels need no camera assignment.
- **Background is frozen:** set Background > Refresh to Real-time for animated scenes. Other refresh modes deliberately reuse earlier images.
- **UI behind the glass is missing:** Screen Space Overlay siblings are drawn after the scene background image. Use the documented camera-space/layered setup to include UI behind another glass panel.
- **Sprite has no bending:** raise Refraction above zero, use a sprite with a visible alpha boundary, and test in Game View.
- **Old inspector after an upgrade:** resolve all compilation errors, import every updated runtime/editor/shader file, and check for duplicate copies of the package. Old test exports may leave obsolete development Tests behind; remove that folder only in the receiving project.
- **Missing demo text or TMP_Settings errors:** import TMP Essential Resources from Window > TextMeshPro before building.
- **No demo input:** install Input System, enable New or Both in Active Input Handling, and press Play.

Built-in Render Pipeline, HDRP, UI Toolkit and WebGL are not supported. Mobile, XR and other graphics APIs are not advertised as validated by this release. Profile on the target device; no universal FPS claim is made.
