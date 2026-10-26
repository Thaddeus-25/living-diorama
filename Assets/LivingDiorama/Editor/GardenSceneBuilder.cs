using System.Collections.Generic;
using LivingDiorama.Core;
using LivingDiorama.Data;
using LivingDiorama.SceneEditing;
using LivingDiorama.UI;
using LivingDiorama.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace LivingDiorama.EditorTools
{
    /// <summary>
    /// Builds Garden.unity: camera, sun, diorama base, painted backdrop and a few starting props.
    /// World layout: X = left/right, Y = up, Z = depth (away from the viewer). 1 unit = 1 m = 16 px.
    /// </summary>
    internal static class GardenSceneBuilder
    {
        public const string ScenePath = ProjectBootstrap.ScenesFolder + "/Garden.unity";

        /// <summary>Editable area (x = width, y = depth) in metres.</summary>
        public static readonly Vector2 GardenSize = new(20f, 12f);

        /// <summary>Thickness of the dirt block under the grass, for the diorama look.</summary>
        public const float BaseDepth = 1.5f;

        private static readonly (string id, Vector3 position)[] StartingProps =
        {
            ("tree", new Vector3(-7f, 0f, 3.5f)),
            ("tree", new Vector3(-3.2f, 0f, 4.8f)),
            ("tree", new Vector3(6.5f, 0f, 4.2f)),
            ("tree", new Vector3(8.2f, 0f, -2.5f)),
            ("rock", new Vector3(3f, 0f, -1.5f)),
            ("rock", new Vector3(-5.5f, 0f, -2.8f)),
            ("bench", new Vector3(0.5f, 0f, 1.5f)),
            ("human", new Vector3(-1.5f, 0f, -0.8f)),
            ("cat", new Vector3(2.2f, 0f, 0.6f)),
        };

        public static void Build(Dictionary<string, string> art)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var materials = GeneratedMaterials.LoadFromDisk();
            var database = AssetDatabase.LoadAssetAtPath<ObjectDatabase>(ProjectBootstrap.DatabasePath);

            var cameraComponent = CreateCamera(materials);
            CreateSun();
            ConfigureAmbientLight();
            var environment = CreateGround(materials);
            BakeNavMesh(environment);
            CreateBackdrop(art, materials, cameraComponent.GetComponent<DioramaCamera>());
            var world = CreateWorld(database);
            CreateEditor(world, cameraComponent, art, materials);

            Billboard.AlignAll(cameraComponent);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static Camera CreateCamera(GeneratedMaterials materials)
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = cameraObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = PixelCanvas.Hex("#b5d6ef");
            cameraObject.AddComponent<AudioListener>();

            var cameraData = cam.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.None;

            var dioramaCamera = cameraObject.AddComponent<DioramaCamera>();
            dioramaCamera.ApplyPose();
            cam.orthographicSize = dioramaCamera.DefaultSize;

            // The sky is a gradient quad parented to the camera, so it always fills the view.
            var sky = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sky.name = "Sky";
            Object.DestroyImmediate(sky.GetComponent<Collider>());
            sky.transform.SetParent(cameraObject.transform, false);
            sky.transform.localPosition = new Vector3(0f, 0f, 150f);
            sky.transform.localScale = new Vector3(48f, 28f, 1f);
            var skyRenderer = sky.GetComponent<MeshRenderer>();
            skyRenderer.sharedMaterial = materials.Sky;
            skyRenderer.shadowCastingMode = ShadowCastingMode.Off;
            skyRenderer.receiveShadows = false;

            return cam;
        }

        private static void CreateSun()
        {
            var sunObject = new GameObject("Sun");
            sunObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = PixelCanvas.Hex("#fff1d6");
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.65f;
        }

        private static void ConfigureAmbientLight()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = PixelCanvas.Hex("#a9bdd6");
            RenderSettings.ambientEquatorColor = PixelCanvas.Hex("#97a58f");
            RenderSettings.ambientGroundColor = PixelCanvas.Hex("#6b5a48");
        }

        /// <summary>
        /// Bakes walkable ground from the Environment only (the grass), saved next to the scene.
        /// Props are left out on purpose: they carve holes at runtime through NavMeshObstacle, so
        /// moving them never needs a rebake.
        /// </summary>
        private static void BakeNavMesh(GameObject environment)
        {
            var surface = environment.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surface.BuildNavMesh();

            string path = ProjectBootstrap.ScenesFolder + "/Garden_NavMesh.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(surface.navMeshData, path);
        }

        private static GameObject CreateGround(GeneratedMaterials materials)
        {
            var environment = new GameObject("Environment");

            // Grass top: a quad lying flat, its collider is what drag-and-drop raycasts hit.
            var grass = GameObject.CreatePrimitive(PrimitiveType.Quad);
            grass.name = "Ground";
            grass.transform.SetParent(environment.transform, false);
            grass.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
            grass.transform.localScale = new Vector3(GardenSize.x, GardenSize.y, 1f);
            var grassRenderer = grass.GetComponent<MeshRenderer>();
            grassRenderer.sharedMaterial = materials.Grass;
            grassRenderer.shadowCastingMode = ShadowCastingMode.Off;

            // Dirt block below: top sits just under the grass to avoid z-fighting.
            var dirt = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dirt.name = "DioramaBase";
            Object.DestroyImmediate(dirt.GetComponent<Collider>());
            dirt.transform.SetParent(environment.transform, false);
            dirt.transform.position = new Vector3(0f, -BaseDepth * 0.5f - 0.01f, 0f);
            dirt.transform.localScale = new Vector3(GardenSize.x, BaseDepth, GardenSize.y);
            dirt.GetComponent<MeshRenderer>().sharedMaterial = materials.Dirt;
            return environment;
        }

        private static void CreateBackdrop(Dictionary<string, string> art, GeneratedMaterials materials, DioramaCamera dioramaCamera)
        {
            var backdrop = new GameObject("Backdrop");

            // Each layer is placed in camera space: vBase is how far up the screen its bottom edge
            // sits (in metres from the focus point), depth is how far behind the focus point it is.
            CreateBackdropLayer(backdrop.transform, "HillsFar", ProjectBootstrap.LoadSprite(art["HillsFar"]),
                materials.Backdrop, dioramaCamera, vBase: -8.1f, depth: 22f);
            CreateBackdropLayer(backdrop.transform, "HillsNear", ProjectBootstrap.LoadSprite(art["HillsNear"]),
                materials.Backdrop, dioramaCamera, vBase: -7.6f, depth: 16f);
        }

        private static void CreateBackdropLayer(Transform parent, string name, Sprite sprite, Material material,
            DioramaCamera dioramaCamera, float vBase, float depth)
        {
            var layer = new GameObject(name);
            layer.transform.SetParent(parent, false);
            var cameraRotation = Quaternion.Euler(dioramaCamera.Pitch, 0f, 0f);
            layer.transform.position = dioramaCamera.FocusPoint
                                       + cameraRotation * Vector3.up * vBase
                                       + cameraRotation * Vector3.forward * depth;

            var visual = PrefabBuilder.CreateSpriteVisual(layer.transform, sprite, material, castShadows: false);
            visual.Renderer.receiveShadows = false;
            Object.DestroyImmediate(visual.GetComponent<BoxCollider>());
        }

        private static DioramaWorld CreateWorld(ObjectDatabase database)
        {
            var dioramaRoot = new GameObject("Diorama");
            var world = dioramaRoot.AddComponent<DioramaWorld>();
            Wire(world, "database", database);
            SetVector2(world, "gardenSize", GardenSize);
            CreateStartingProps(dioramaRoot, database);
            return world;
        }

        /// <summary>Mode controller, scene editor, selection ring, UI document and event system.</summary>
        private static void CreateEditor(DioramaWorld world, Camera cam, Dictionary<string, string> art, GeneratedMaterials materials)
        {
            var editorObject = new GameObject("SceneEditor");
            var modes = editorObject.AddComponent<AppModeController>();
            var editor = editorObject.AddComponent<SceneEditorController>();

            var indicatorObject = new GameObject("SelectionIndicator");
            indicatorObject.transform.SetParent(editorObject.transform, false);
            var indicator = indicatorObject.AddComponent<SelectionIndicator>();
            var ringVisual = PrefabBuilder.CreateSpriteVisual(indicatorObject.transform,
                ProjectBootstrap.LoadSprite(art["SelectionRing"]), materials.Backdrop, castShadows: false);
            ringVisual.name = "Ring";
            ringVisual.Renderer.receiveShadows = false;
            Object.DestroyImmediate(ringVisual.GetComponent<Billboard>());
            Object.DestroyImmediate(ringVisual.GetComponent<BoxCollider>());
            ringVisual.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // lie flat on the ground
            ringVisual.gameObject.SetActive(false);
            Wire(indicator, "ring", ringVisual.transform);

            Wire(editor, "world", world);
            Wire(editor, "worldCamera", cam);
            Wire(editor, "modes", modes);
            Wire(editor, "selectionIndicator", indicator);

            var uiObject = new GameObject("EditorUI");
            var document = uiObject.AddComponent<UIDocument>();
            document.panelSettings = UiAssetBuilder.EnsureAssets();
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiAssetBuilder.HudLayoutPath)
                                       ?? throw new System.IO.FileNotFoundException(UiAssetBuilder.HudLayoutPath);
            var hud = uiObject.AddComponent<EditorHud>();
            Wire(hud, "editor", editor);
            Wire(hud, "modes", modes);
            Wire(hud, "dioramaCamera", cam.GetComponent<DioramaCamera>());

            // UI Toolkit receives Input System events through the event system.
            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            var inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }

        /// <summary>Sets a private [SerializeField] reference the same way the Inspector would.</summary>
        private static void Wire(Object target, string fieldName, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(fieldName)
                           ?? throw new System.ArgumentException($"{target.GetType().Name} has no field '{fieldName}'");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVector2(Object target, string fieldName, Vector2 value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(fieldName).vector2Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateStartingProps(GameObject dioramaRoot, ObjectDatabase database)
        {
            foreach (var (id, position) in StartingProps)
            {
                var definition = database.Find(id);
                if (definition == null || definition.Prefab == null)
                {
                    Debug.LogWarning($"[GardenSceneBuilder] No prefab for '{id}', skipping.");
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(definition.Prefab);
                instance.transform.SetParent(dioramaRoot.transform, false);
                instance.transform.position = position;
                instance.GetComponent<PlacedObject>().AssignNewId();
            }
        }
    }
}
