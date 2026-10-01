#if UNITY_EDITOR
using System.IO;
using SnackDash.LevelSystem;
using SnackDash.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SnackDash.Editor
{
    /// <summary>
    /// Builds a level board in an isolated preview scene and renders it to Temp/BoardPreview_*.png,
    /// so boards can be compared side by side with gameplay footage of the original.
    /// Preview objects live on layer 31 and the preview camera only renders that layer, so other open
    /// scenes never leak into the capture. The preview scene is closed again after rendering; open
    /// Scenes/Development/BoardPreview.unity to inspect the last generated board.
    /// </summary>
    public static class BoardPreviewTool
    {
        private const string ScenePath = "Assets/_MyGame/Scenes/Development/BoardPreview.unity";
        private const string DatabasePath = "Assets/_MyGame/Data/Levels/TileDefinitionDatabase.asset";
        private const string LevelFolder = "Assets/_MyGame/Data/Levels/SourceJson/";
        private const int PreviewLayer = 31;

        // Original Game.unity camera: orthographic, tilted -20 degrees around X, background colour.
        private static readonly Color Background = new Color(0.58310264f, 1f, 0.5424528f);

        [MenuItem("SnackDash/Debug/Preview Level 1 (5126_v4_nt_VO)")]
        private static void PreviewLevel1() => Preview("5126_v4_nt_VO");

        [MenuItem("SnackDash/Debug/Preview Level 2 (11002_v3_nt_VO)")]
        private static void PreviewLevel2() => Preview("11002_v3_nt_VO");

        public static string Preview(string levelName, int width = 1080, int height = 1920)
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(LevelFolder + levelName + ".json");
            var database = AssetDatabase.LoadAssetAtPath<TileDefinitionDatabase>(DatabasePath);
            if (json == null || database == null)
            {
                Debug.LogError($"[BoardPreview] Missing level json or tile database for {levelName}.");
                return null;
            }

            var scene = OpenPreviewScene();
            foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);

            var boardGo = new GameObject("Board");
            SceneManager.MoveGameObjectToScene(boardGo, scene);
            var board = boardGo.AddComponent<BoardView>();
            var so = new SerializedObject(board);
            so.FindProperty("database").objectReferenceValue = database;
            so.FindProperty("backgroundPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_MyGame/Prefabs/Original/tilescovers/visualoverhaul/FullScreenBackground.prefab");
            so.ApplyModifiedPropertiesWithoutUndo();
            board.Build(TiledMap.Parse(json.text));

            var cameraGo = new GameObject("Board Camera");
            SceneManager.MoveGameObjectToScene(cameraGo, scene);
            cameraGo.transform.rotation = Quaternion.Euler(-20f, 0f, 0f);
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.nearClipPlane = 0.0005f;
            camera.farClipPlane = 1000f;
            camera.cullingMask = 1 << PreviewLayer;
            camera.aspect = (float)width / height;
            cameraGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            cameraGo.AddComponent<BoardCameraFitter>().Fit(board.BoardBounds);

            foreach (var t in boardGo.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = PreviewLayer;
            EditorSceneManager.SaveScene(scene);

            var output = $"Temp/BoardPreview_{levelName}.png";
            Render(camera, width, height, output);
            Debug.Log($"[BoardPreview] {levelName} -> {output}");

            // Keep the Hierarchy clean: the scene is saved, so it can be reopened to inspect the board.
            if (SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
            return output;
        }

        private static Scene OpenPreviewScene()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.IsValid() && scene.isLoaded) return scene;
            if (File.Exists(ScenePath)) return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SaveScene(scene, ScenePath);
            return scene;
        }

        private static void Render(Camera camera, int width, int height, string path)
        {
            var rt = new RenderTexture(width, height, 24);
            camera.targetTexture = rt;
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(texture);
        }
    }
}
#endif
