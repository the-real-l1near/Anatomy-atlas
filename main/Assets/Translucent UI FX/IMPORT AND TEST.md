# Install Translucent UI FX 3.2.1

## Requirements

Use Unity 6.3 LTS (6000.3) with Universal Render Pipeline 17.3, RenderGraph enabled, and Unity UI (uGUI) 2.0. The demos also need Input System; the release project uses version 1.20.0. The glass component itself does not depend on the Input System package. Built-in, HDRP, UI Toolkit and WebGL are not supported.

## First installation

1. Install Universal RP and Unity UI with Unity Package Manager if they are not present. For the demos, also install Input System and set Active Input Handling to Input System Package (New) or Both. Restart Unity if prompted. For demo text, choose **Window > TextMeshPro > Import TMP Essential Resources** before opening or building the demo scenes. This standard Unity resource import is required even though the demos include their own font.
2. Import all files from the production package. It contains runtime, inspector, shaders, two demo scenes, materials, font and offline documentation. It excludes development tests, the bridge, reference assets and ProjectSettings.
3. Open Tools > Translucent UI FX > Setup & Diagnostics and install the renderer feature on your active URP renderer. Check both Graphics and Quality pipeline overrides. On a panel using a different renderer, its Set up background button targets that renderer.
4. Create GameObject > UI > Liquid Glass Panel. Camera selection is automatic. Choose a shape and adjust Tint, Blur or Refraction.
5. Open Demo/Scenes/Demo - Liquid Glass and press Play. Try shape buttons, sliders, fusion dragging, cutout, pointer lighting and Reset. Demo - Fused Liquid Glass contains another composition example.

The demos include a sample URP asset in Demo/URP Related. You may assign it in a disposable test project. Importing the asset does not replace your project's pipeline settings. Judge optical effects in Game View.

## Updating an existing installation

Back up your project and import every selected file into the original asset folder. Preserve its GUIDs and avoid keeping a second copy. The full production package updates the included demos as well as the runtime. Your own game scenes are not in the archive.

Older development exports may have left an obsolete Tests folder. Unity imports do not delete old files. If those test scripts refer to removed APIs, remove only Assets/Translucent UI FX/Tests from the receiving project. Release packages exclude this development folder.

Saved component values are preserved. Reset appearance restores the current clear-glass defaults while keeping shapes, sprites and background settings. Fusion remains intentional: select siblings and use Fuse shapes / Create fusion group.

## Player builds

Resources assets retain the glass, blur and sprite-field shaders. No development script is required in a player. The shader path requires shader-model-4.5-class hardware, structured buffers, texture arrays and RFloat support. Validate your shipping graphics API and hardware before advertising support. See README.md for integration, performance guidance and troubleshooting; see Third-Party Notices.txt for the bundled font license.
