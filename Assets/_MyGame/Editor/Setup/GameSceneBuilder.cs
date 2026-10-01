#if UNITY_EDITOR
using SnackDash.Bootstrap;
using SnackDash.Input;
using SnackDash.LevelSystem;
using SnackDash.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SnackDash.Editor.Setup
{
    /// <summary>
    /// (Re)creates Scenes/Game.unity with every reference wired, using the camera and render
    /// settings of the original Game.unity. Safe to run again after changing the layout here.
    /// </summary>
    public static class GameSceneBuilder
    {
        private const string ScenePath = "Assets/_MyGame/Scenes/Game.unity";
        private const string Prefabs = "Assets/_MyGame/Prefabs/Original/";
        private const string DefaultLevel = "Assets/_MyGame/Data/Levels/Definitions/11002_v3_nt_VO.asset";

        [MenuItem("SnackDash/Setup/Build Game Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientSkyColor = new Color(0.212f, 0.227f, 0.259f);
            RenderSettings.skybox = null;

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraGo.transform.rotation = Quaternion.Euler(-20f, 0f, 0f);
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.nearClipPlane = 0.0005f;
            camera.farClipPlane = 1000f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.58310264f, 1f, 0.5424528f);
            cameraGo.AddComponent<UniversalAdditionalCameraData>();
            var fitter = cameraGo.AddComponent<BoardCameraFitter>();

            var level = new GameObject("Level").transform;
            var database = AssetDatabase.LoadAssetAtPath<TileDefinitionDatabase>("Assets/_MyGame/Data/Levels/TileDefinitionDatabase.asset");

            var board = Child<BoardView>(level, "Board");
            Set(board, "database", database);
            Set(board, "backgroundPrefab", Prefab("tilescovers/visualoverhaul/FullScreenBackground"));

            var pills = Child<PillsView>(level, "Pills");
            Set(pills, "pillPrefab", Prefab("GameObject/PillMaster_VO"));

            var crates = Child<CratesView>(level, "Crates");
            Set(crates, "crate9Prefab", Prefab("GameObject/Box_Crate_9_VO"));
            Set(crates, "crate18Prefab", Prefab("GameObject/Box_Crate_18"));
            Set(crates, "crate27Prefab", Prefab("GameObject/Box_Crate_27"));
            Set(crates, "pillPrefab", Prefab("GameObject/PillMaster_VO"));
            Set(crates, "fruits", AssetDatabase.LoadAssetAtPath<FruitGraphicSet>("Assets/_MyGame/Data/FruitGraphicSet.asset"));
            Set(crates, "board", board);

            var snake = Child<SnakeView>(level, "Snake");
            Set(snake, "headPrefab", Prefab("GameObject/SnakeHead_VO"));
            Set(snake, "segmentPrefab", Prefab("GameObject/TailSegment_VO"));
            Set(crates, "snake", snake);

            var game = new GameObject("Game");
            var input = game.AddComponent<SwipeInput>();
            var audio = game.AddComponent<SnackDash.Audio.GameAudio>();
            Set(audio, "library", AssetDatabase.LoadAssetAtPath<SnackDash.Audio.SoundLibrary>("Assets/_MyGame/Data/SoundLibrary.asset"));
            cameraGo.AddComponent<AudioListener>();
            var bootstrap = game.AddComponent<GameBootstrap>();
            Set(bootstrap, "level", AssetDatabase.LoadAssetAtPath<LevelDefinition>(DefaultLevel));
            Set(bootstrap, "database", database);
            Set(bootstrap, "board", board);
            Set(bootstrap, "cameraFitter", fitter);
            Set(bootstrap, "snake", snake);
            Set(bootstrap, "pills", pills);
            Set(bootstrap, "crates", crates);
            Set(bootstrap, "input", input);
            Set(bootstrap, "gameAudio", audio);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[Setup] Built {ScenePath}");
        }

        private static T Child<T>(Transform parent, string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<T>();
        }

        private static GameObject Prefab(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + path + ".prefab");

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
