# Changelog

## Demo presentation refresh — 2026-09-24

- Added original blue-and-copper satin artwork, aspect-correct cropping and optional gentle motion.
- Redesigned the playground typography, shape buttons, switches and control spacing.
- Fixed demo switches losing their thumb when off; both states now keep a visible thumb with left/right positioning and contrasting tracks.
- Enlarged the fusion study and moved its drag guidance above the interactive shapes.
- Retained the existing demo controls and the production glass rendering implementation.

## 3.2.1 — Production package — 2026-09-24

- Prepared the complete release for Unity 6.3 LTS / URP 17.3.
- Clarified the demo prerequisite to import TMP Essential Resources in new projects.
- Removed Metal shader compilation warnings with equivalent bounded polygon loops and initialized distance-return paths.
- Explicitly release shared glass GPU resources when the player or Editor quits.
- Updated offline installation, background-routing and troubleshooting documentation.
- Added package-level third-party notices for the bundled Inter demo font.
- Fixed standalone player compilation: fusion authoring Reset now uses the same Editor-only guard as its Unity UI base method.
- Preserved optical behavior, serialized settings and the intentional fusion workflow used in production.

## Automatic background update

- Automatic game camera selection, shared across panels, including untagged cameras and camera switching.
- Background controls use plain language; camera/layer overrides live under Advanced.
- Missing backgrounds use translucent tint instead of opaque placeholders.
- Skip unused-camera passes and redundant material bindings; eliminate the last blur copy without lowering quality.
- Setup diagnoses the selected camera renderer and can re-enable its background feature.

## Unity 6.5 API compatibility fix — 2026-09-15

- Removed all four obsolete `GetInstanceID()` calls from the sprite field cache and fusion inspector. Texture identity uses object equality; inspector change signatures use object hashes.
- Fixes the reported CS0619 compilation errors that prevented the updated runtime and inspector assemblies from loading on Unity 6.5.
- Regression validation runs on Unity 6000.2.6f2; complete Unity 6.5 rendering/platform validation remains pending.

## Inspector polish — 2026-09-14

- Added a cached composition map with clickable member numbers, shared union/cutout geometry and no scene mutation during preview.
- Redesigned inspector cards, glass header, primary sliders, sprite source preview, shape tiles and light direction pad for Unity dark and light themes.
- Added numbered member rows with Undo-aware inclusion toggles, clear Add/Cutout status, and session-persistent advanced foldouts.
- Corrected narrow shape-grid spacing, aligned custom corner column labels and named native Image fill origins.
- Reviewed the supplied reference's composition behavior: additions combine before cutouts, appearance blends through joins, and softness bridges nearby surfaces. Retained explicit group ownership and the independent renderer implementation.
- Added four composition-map regression tests; the expanded suite passes 50 tests.

## 3.2.0 — Shape composition and optical control — 2026-09-14

- Added real sprite fusion and sprite cutouts, dynamic member capacity, and smooth per-member appearance blending. Conversion preserves independent Graphic colors and fades, including a hidden first source.
- Added custom per-corner units/profiles, chamfers and concave corners; alpha thresholds and selectable 64–1024 sprite field detail.
- Added optical lip units/smoothing, lens depth, magnification, transmission, directional/opposing/point lighting, independent lip bands and soft shadows.
- Added camera/layer capture routing, inherited Capture Source components, camera quality/blur overrides, mip chains, format fallback and optional filtering controls.
- Fixed retained Manual capture normalization, native Filled Image hit regions, standalone Metal buffer binding, and stray derivative outlines outside fusion bounds.
- Polished inspector cards, visual direction control, responsive shape tiles, conditional controls and separate collapsed Optics/Lighting/Capture sections. Fixed overlapping capture rows. Hid retired material recipes from the generated material inspector.
- Expanded rendered, interaction, authoring and camera-routing tests, plus a bounded Editor rendering benchmark. Updated guide and hardware requirements.

## 3.1.1 — Sprite optical edges

- Assigned sprites now define refraction and highlights from their alpha boundary, even on components saved with Procedural Shape enabled.
- Added shared sprite distance fields with GPU alpha extraction, non-readable texture support, atlas coordinate mapping, and import invalidation.
- Simplified shape source selection; procedural demo buttons clear the sprite and irrelevant corner controls are disabled.
- Darkened the playground backdrop and adjusted text and button contrast.
- Sprite-to-fusion conversion correctly detects sprite silhouettes before offering an approximation.


## 3.1.0 - 2026-09-14

- Fixed discarded inspector slider edits when Image & Interaction was expanded.
- Replaced the demo with a shape and fusion playground; added circular slider handles, 44-unit hit areas, pointer-raycast regression coverage, and clear Play Mode guidance.
- Added visual shape selection, triangle and hexagon silhouettes, individual corner radii, and corner smoothing.
- Reorganized the inspector into Appearance, Shape, and Fusion cards with a directly editable member list.
- Added cutouts, rotated fusion members, automatic render bounds, nested-group ownership, and silhouette-aware raycasts.
- Detach retains geometry edits; empty groups no longer draw a fallback rectangle.

## 3.0.0 - 2026-09-13

- Focused the asset on a single Liquid Glass appearance; removed alternative material recipes and the preset selector.
- New components use Liquid Glass defaults in editor and runtime. Added `ApplyLiquidGlass()`; legacy preset IDs remain compatible.
- Replaced four Inspector tabs with everyday controls and collapsed advanced settings. Reset preserves silhouette, sprite, and capture settings and records prefab overrides.
- Reworked original optics around a narrow boundary-following lip, aspect-correct displacement, clear-center transmission, and restrained edge highlights. Removed unconditional body darkening.
- Replaced the preset showcase with live blur, refraction, pointer, reset, and backdrop-motion controls over an original procedural background.
- Exposed procedural and sprite silhouettes in the main Inspector, with a live five-shape demo picker.
- Fixed the bundled font material rendering before the glass surface.
- Kept FlexibleUI outside the package dependency and export boundary.


## 2.1.1 - 2026-08-18

### Fixed

- Fixed shimmer and doubled background edges when refraction or color dispersion was viewed through sprite alpha gradients, feathered masks or CanvasGroup fades.
- Refraction and dispersion now collapse continuously toward the undistorted scene as local alpha approaches zero.
- Removed hidden demo dependencies on Unity template Volume Profiles and the project-level TextMesh Pro starter assets.
- The fused demo now uses the bundled Inter font asset, so both showcase scenes remain complete when this package folder is imported by itself.
- Reassigned copied URP demo assets to package-owned GUIDs to prevent collisions with Unity project-template assets.

### Improved

- Added the default-enabled **Stable Soft Alpha** control with a legacy opt-out for deliberately full-strength transparent-edge optics.
- Clarified that Smart, Interval and Manual captures cannot automatically follow arbitrary scene animation behind a stationary camera.
- Added a one-click **Use Motion-Safe Real-Time Capture** action when a non-real-time mode is selected.
- Fusion conversion now preserves Selectable and Toggle graphic references on hidden or inactive scene objects as well as ordinary visible controls.
- Extended the bundled PDF guide with soft-alpha stability and moving-background capture guidance.

## 2.1.0 - 2026-08-13

### Added

- An obvious **Enable Flare** master switch that removes the specular highlight without changing the rest of the glass treatment.
- One-click **Start Fusion**, **Fuse Selected** and context-aware **Fuse Overlapping** Inspector actions.
- Reversible **Detach to Standalone Glass** conversion for individual members.
- Group Inspector **Add Member** action for a duplication-friendly authoring loop.
- Smooth-union liquid-glass groups with a single continuous edge, refraction field and shared blur surface.
- `TranslucentGlassGroup` for bounded, allocation-free fusion of up to eight child shapes.
- `TranslucentGlassMember` for independently positioned and raycastable child controls.
- Dedicated group/member Inspectors with fusion distance, member count, limit warnings and contextual setup guidance.
- **GameObject > UI > Fused Liquid Glass Group** creation menu with a ready-to-edit two-member example.
- A focused **Demo - Fused Liquid Glass** scene that showcases circles, capsules and continuous-corner members becoming one surface.

### Improved

- Scene View now uses a deterministic authoring preview instead of sampling a stale or differently scaled Game-camera capture; Game View and player rendering remain unchanged.
- The fused-surface showcase now ships with flare disabled so the continuous merged boundary is easier to read.
- Fusion conversion now preserves transforms, child content, raycasts, Selectable/Toggle graphic references and the source optical style in a single Undo operation.
- Added explicit prefab-instance and Layout Group safeguards so hierarchy conversion never creates surprising structural overrides.
- Added a protected material-configuration extension point so specialized glass renderers reuse the existing optical stack and URP capture.
- Fused surfaces derive their edge normal from the smooth-union distance field, keeping refraction, rim depth and directional glints continuous through merged boundaries.
- Member transform changes update the union without allocations, hierarchy polling or a second rendering system.

### Performance

- Fusion adds no camera captures, blur passes or render textures.
- Each group is one UGUI graphic with a fixed maximum of eight analytic SDF evaluations per covered fragment.
- Child controls retain normal UGUI interaction and content rendering; only their shared backing surface is fused.

## 2.0.0 - 2026-08-13

### Added

- Sprite-free procedural rectangles, continuous rounded rectangles, capsules, circles and diamonds.
- Apple-style continuous corner control.
- Refractive-index, rim-depth and specular-sharpness optical controls.
- Optional pointer-reactive lighting for mouse and touch interfaces.
- A dedicated **Liquid Glass Panel** creation menu.
- One-click URP Renderer Feature installation and live Inspector diagnostics.
- A preserved source capture so each UI element has independent blur strength.
- New project README with workflow, performance and compatibility guidance.
- An interactive Liquid Glass showcase with live preset, blur and pointer-light controls.

### Improved

- Rebuilt the liquid glass preset around a clear continuous-corner panel with directional glint.
- Replaced the diagonal five-tap blur with a smoother nine-tap tent kernel.
- Added native UGUI clip keywords, alpha clipping and sprite-atlas sample correction.
- Preserved stencil state through the zero-material workflow for UGUI masking.
- Made explicit refresh requests durable until every active camera has processed them.
- Made preset application work across multi-object selections and record Undo correctly.
- Added duration safety to runtime fade helpers.
- Reorganized the component Inspector around presets plus Glass, Optics, Shape and Performance tabs, with clearer setup status and contextual guidance.
- Preserved authored numeric precision when inspecting components instead of silently rounding serialized values.

### Fixed

- Fixed Manual, Interval and Smart Update modes being forced to refresh continuously by the initial request state.
- Fixed the per-element Blur Strength slider not changing the sampled result.
- Fixed Smart Update overriding Interval elements when both modes share a camera.
- Prevented duplicate active-instance registrations.
- Fixed edge shapes depending on atlas UVs and stretched sprites.

### Upgrade notes

- Existing serialized elements preserve sprite-driven silhouettes until **Use Procedural Shape** is enabled or a 2.0 preset is reapplied.
- Reapply **Liquid Glass** to opt into the new continuous-corner shape and interactive highlight defaults.
