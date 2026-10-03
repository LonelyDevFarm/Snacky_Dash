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
            Set(crates, "holePrefab", Prefab("GameObject/Hole_VO"));
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
            Set(bootstrap, "levels", AssetDatabase.LoadAssetAtPath<LevelList>(Restore.LevelDataImporter.LevelListPath));
            Set(bootstrap, "database", database);
            Set(bootstrap, "board", board);
            Set(bootstrap, "cameraFitter", fitter);
            Set(bootstrap, "snake", snake);
            Set(bootstrap, "pills", pills);
            Set(bootstrap, "crates", crates);
            Set(bootstrap, "input", input);
            Set(bootstrap, "gameAudio", audio);

            CreateUiPrefabs(); // missing ones only; hand-edited prefabs are kept
            var ui = new GameObject("UI");
            var loseFlow = ui.AddComponent<SnackDash.UI.LoseFlowView>();
            FillSkin(loseFlow);
            Set(loseFlow, "prefabs.noWayOut", UiPrefabAsset("NoWayOut"));
            Set(loseFlow, "prefabs.secondChance", UiPrefabAsset("SecondChance"));
            Set(loseFlow, "prefabs.gameOver", UiPrefabAsset("GameOver"));
            Set(bootstrap, "loseFlow", loseFlow);
            var victory = ui.AddComponent<SnackDash.UI.VictoryView>();
            FillSkin(victory);
            Set(victory, "prefab", UiPrefabAsset("Victory"));
            Set(bootstrap, "victory", victory);
            var hud = ui.AddComponent<SnackDash.UI.HudView>();
            FillSkin(hud);
            Set(hud, "prefab", UiPrefabAsset("HUD"));
            Set(bootstrap, "hud", hud);
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[Setup] Built {ScenePath}");
        }

        private const string UiPrefabFolder = "Assets/_MyGame/Prefabs/UI/";

        /// <summary>
        /// Saves each popup / HUD layout as a prefab in Prefabs/UI so it can be edited by hand. Only
        /// prefabs that do not exist yet are made: hand edits are never overwritten (delete a prefab
        /// to get the code layout back).
        /// </summary>
        [MenuItem("SnackDash/Setup/Create UI Prefabs (missing only)")]
        public static void CreateUiPrefabs()
        {
            UiPrefab<SnackDash.UI.LoseFlowView>("NoWayOut", 10, (view, root) => view.BuildNoWayOut(root));
            UiPrefab<SnackDash.UI.LoseFlowView>("SecondChance", 10, (view, root) => view.BuildSecondChance(root));
            UiPrefab<SnackDash.UI.LoseFlowView>("GameOver", 10, (view, root) => view.BuildGameOver(root));
            UiPrefab<SnackDash.UI.VictoryView>("Victory", 20, (view, root) => view.BuildVictory(root));
            UiPrefab<SnackDash.UI.HudView>("HUD", 5, (view, root) => view.BuildHud(root));
            AssetDatabase.SaveAssets();
        }

        private static GameObject UiPrefab<T>(string name, int sortingOrder, System.Action<T, RectTransform> build) where T : MonoBehaviour
        {
            var path = UiPrefabFolder + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            System.IO.Directory.CreateDirectory(UiPrefabFolder);
            var host = new GameObject("UI prefab builder");
            var view = host.AddComponent<T>();
            FillSkin(view);
            var root = SnackDash.UI.UiBuilder.Canvas(null, name, sortingOrder);
            build(view, root);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            Object.DestroyImmediate(root.gameObject);
            Object.DestroyImmediate(host);
            Debug.Log($"[Setup] Created UI prefab {path}");
            return prefab;
        }

        private static GameObject UiPrefabAsset(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabFolder + name + ".prefab");

        /// <summary>Original popup sprites, font and TMP materials, found by their original names.</summary>
        private static void FillSkin(Object target)
        {
            var so = new SerializedObject(target);
            var skin = so.FindProperty("skin");
            void Ref(string field, Object value)
            {
                skin.FindPropertyRelative(field).objectReferenceValue = value;
                if (value == null) Debug.LogWarning($"[Setup] UI skin: nothing found for {field}");
            }
            void Sprites(string field, params string[] names)
            {
                var array = skin.FindPropertyRelative(field);
                array.arraySize = names.Length;
                for (int i = 0; i < names.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = Find<Sprite>(names[i]);
            }

            // Font + presets rebuilt for TMP 3.0.7 (SnackDash/Restore/Rebuild TMP Font): the exported
            // Unity 6 font asset crashes the editor when drawn.
            Material Preset(string original) => Find<Material>(Restore.OriginalAssetImporter.RebuiltFontName + " (" + original + ")");
            Ref("font", Find<TMPro.TMP_FontAsset>(Restore.OriginalAssetImporter.RebuiltFontName));
            Ref("titleBackMaterial", Preset("LuckiestGuy-Regular Combo - BG"));
            Ref("titleFrontMaterial", Preset("LuckiestGuy-Regular Combo - Base"));
            Ref("greenOutlineMaterial", Preset("LuckiestGuy-Regular SDF Green Outline"));
            Ref("darkRedMaterial", Preset("LuckiestGuy-Regular SDF Dark Red"));
            Ref("blueUnderlayMaterial", Preset("LuckiestGuy-Regular SDF Blue Underlay"));
            Ref("greenCartoonMaterial", Preset("LuckiestGuy-Regular SDF Green Cartoon"));
            Ref("plainMaterial", Preset("LuckiestGuy-Regular Atlas Material"));
            Ref("redCartoonMaterial", Preset("LuckiestGuy-Regular SDF Red Cartoon"));
            Ref("redCartoonInnerMaterial", Preset("LuckiestGuy-Regular SDF Red Cartoon Play btn"));
            Ref("brownLevelMaterial", Preset("LuckiestGuy-Regular Brown Level Number"));
            Ref("hudBar", Find<Sprite>("S_CoreTopUI_Main_0"));
            Ref("hudLevelHolder", Find<Sprite>("S_CoreTopUI_LevelHolder_0"));
            Ref("hudProgressBackground", Find<Sprite>("S_CoreTopUI_ProgressBarBG_0"));
            Ref("hudProgressFill", Find<Sprite>("S_CoreTopUI_ProgressBarStraight_0"));
            Ref("hudSnakeHead", Find<Sprite>("S_SnakeHead_0"));
            Ref("hudFruits", Find<Sprite>("Group 1321316041_0"));
            Ref("milestonePendingBar", Find<Sprite>("S_CoreTopUI_ProgressBarBGMilestone_0"));
            Ref("milestonePendingDot", Find<Sprite>("S_CoreTopUI_ProgressBar_Milestone_0"));
            Ref("milestoneDoneBar", Find<Sprite>("S_CoreTopUI_ProgressBarMilestone_0"));
            Ref("checkMark", Find<Sprite>("S_Icon_CheckMark"));
            Sprites("bigButton", "S_Button_Sheet_3", "S_Button_Sheet_2", "S_Button_Sheet_1", "S_Button_Sheet_0");
            Sprites("smallButton", "S_Button_Small_Sheet_3", "S_Button_Small_Sheet_2", "S_Button_Small_Sheet_1", "S_Button_Small_Sheet_0");
            Ref("buttonUnderlay", Find<Sprite>("S_Button_Underlay_Sheet_1"));
            Ref("bottomGradient", Find<Sprite>("gradient01"));
            Ref("topGradient", Find<Sprite>("VerticalGradient"));
            Ref("bannerFrame", Find<Sprite>("BannerFrame00_04~06_Bg"));
            Ref("titleBanner", Find<Sprite>("S_BG_Title (1)_0"));
            Ref("brokenHeart", Find<Sprite>("S_Icon_BrokenHeart_Large_0"));
            Ref("greenButton", Find<Sprite>("S_Button_Green"));
            Ref("cancelButton", Find<Sprite>("SP_Cancel_Btn"));
            Ref("snakeIllustration", Find<Sprite>("SP_Snake_0"));
            Ref("fruitsIllustration", Find<Sprite>("SP_Fruits (1)_34"));
            Ref("coinIcon", Find<Sprite>("S_Icon_Coin_Single"));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T Find<T>(string assetName) where T : Object
        {
            foreach (var guid in AssetDatabase.FindAssets(assetName + " t:" + typeof(T).Name, new[] { "Assets/_MyGame" }))
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                    if (asset is T typed && asset.name == assetName) return typed;
            return null;
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
