# Living Diorama

A customizable, animated HD-2D / 2.5D diorama built with Unity 6 (URP, 3D renderer).
Pixel-art sprites stand in a lit 3D world, viewed through a fixed, angled orthographic camera.

## Requirements
- Unity **6000.3.10f1**
- Git LFS (`git lfs install`), since PNG art is stored in LFS

## Getting started
1. Open the folder in Unity Hub (**Add → Add project from disk**).
2. Open `Assets/LivingDiorama/Scenes/Garden.unity` and press **Play**.
3. Scroll the mouse wheel to zoom.

## Editor controls (Edit mode)
| Action | How |
|---|---|
| Place an object | Click it in the **Objects** panel, then click the ground (hold **Shift** to place several; right-click or **Esc** to stop) |
| Select / move | Click an object; drag it |
| Rotate | **R** (Shift+R reverses) or the Rotation slider |
| Duplicate / delete | **Ctrl+D** / **Delete**, or the panel buttons |
| Grid snap (0.5 m) | **G** or the toggle |
| Undo / redo | **Ctrl+Z** / **Ctrl+Y** (or Ctrl+Shift+Z) |
| Preview mode | **Tab** or the **Preview** button; **Tab** again returns to editing |
| Zoom | Mouse wheel |

"Depth" in the properties panel is the object's distance from the front edge (world Z). Since
the world is 3D, depth alone decides what's drawn in front.

## Generated content
Placeholder art, materials, definitions, prefabs and the Garden scene are produced by code:
**Living Diorama → Rebuild Generated Assets** (or the command below).

- PNGs in `Art/Placeholder` are only created when missing. Replace any file with your own art
  (same name) and it stays.
- Definitions (`Definitions/*.asset`) keep your edits; only their prefab link is refreshed.
- Materials, prefabs and `Garden.unity` are rebuilt every time.

## Command-line checks
Close the Unity editor first, since Unity locks an open project.

```bash
UNITY="/c/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Unity.exe"
# Regenerate assets
"$UNITY" -batchmode -quit -projectPath . -executeMethod LivingDiorama.EditorTools.ProjectBootstrap.Run -logFile -
# EditMode tests
"$UNITY" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults-EditMode.xml
# PlayMode tests (load the real Garden scene)
"$UNITY" -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults TestResults-PlayMode.xml
# Render the scene to Verification/garden.png
"$UNITY" -batchmode -quit -projectPath . -executeMethod LivingDiorama.EditorTools.ScreenshotCapture.CaptureGarden
# Render the scene plus the editor UI to Verification/hud.png
"$UNITY" -batchmode -projectPath . -runTests -testPlatform PlayMode -testFilter HudScreenshotCapture
```

## World conventions
- X = left/right, Y = up, Z = depth (away from the viewer). The ground is y = 0.
- 1 unit = 1 metre = 16 pixels. Sprites use bottom-centre pivots.
- Sprites are drawn as meshes (`SpriteVisual`) with a lit, alpha-clipped material, so they get
  real lighting, shadows and depth sorting.

## Layout
```
Assets/LivingDiorama/
  Scripts/   runtime code (Core, Data, World, ...)
  Editor/    bootstrap, art generator, screenshot tool
  Tests/     EditMode tests
  Art/ Materials/ Definitions/ Prefabs/ Scenes/ Settings/
```
