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
            var vortex = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "boosters/Vortex2 FX.prefab");
            Set(snake, "vortexPrefab", vortex);
            Set(crates, "vortexPrefab", vortex);
            var secret = Prefab("GameObject/View_Secret_Collectible");
            Set(snake, "secretPrefab", secret);
            Set(pills, "secretPrefab", secret);
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
            var boosters = ui.AddComponent<SnackDash.UI.BoosterBarView>();
            FillSkin(boosters);
            Set(bootstrap, "boosters", boosters);
            var tutorial = ui.AddComponent<SnackDash.UI.TutorialView>();
            FillSkin(tutorial);
            Set(bootstrap, "tutorial", tutorial);
            var combo = ui.AddComponent<SnackDash.UI.ComboView>();
            FillSkin(combo);
            Set(bootstrap, "combo", combo);
            Set(boosters, "shovelCirclePrefab", Prefab("boosters/Booster_Tunnel_CircleToClick"));
            Set(boosters, "shovelHolePrefab", Prefab("boosters/Booster_Tunnel_Hole"));
            Set(boosters, "magnetFxPrefab", Prefab("boosters/FX_Snake_Magnetic"));
            Set(boosters, "undoFxPrefab", Prefab("boosters/Vortex Big2"));
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            SetBuildScenes();
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
            UiPrefab<SnackDash.UI.HomeView>("Home", 10, (view, root) => view.BuildLayout(root));
            PanelPrefab("WeeklyMissionsPopup", SnackDash.UI.WeeklyMissionsPopup.BuildLayout);
            PanelPrefab("WeeklyMissionsRules", SnackDash.UI.WeeklyMissionsRules.BuildLayout);
            PanelPrefab("WeeklyBadgeFtue", SnackDash.UI.WeeklyBadgeFtue.BuildLayout);
            PanelPrefab("SnackyPassView", SnackDash.UI.SnackyPassView.BuildLayout);
            PanelPrefab("SnackyPassRules", SnackDash.UI.SnackyPassRules.BuildLayout);
            PanelPrefab("SnackyPassOffer", SnackDash.UI.SnackyPassOffer.BuildLayout);
            PanelPrefab("SnackyPassBadgeFtue", SnackDash.UI.WeeklyBadgeFtue.BuildSnackyPassLayout);
            PanelPrefab("JungleHeistJoin", SnackDash.UI.JungleHeistJoin.BuildLayout);
            PanelPrefab("JungleHeistRules", SnackDash.UI.JungleHeistRules.BuildLayout);
            PanelPrefab("JungleHeistMain", SnackDash.UI.JungleHeistMain.BuildLayout);
            PanelPrefab("JungleHeistEnded", SnackDash.UI.JungleHeistEnded.BuildLayout);
            PanelPrefab("JungleHeistRewards", SnackDash.UI.JungleHeistRewards.BuildLayout);
            PanelPrefab("JungleHeistMatch", SnackDash.UI.JungleHeistMatch.BuildLayout);
            PanelPrefab("JungleHeistBadgeFtue", SnackDash.UI.WeeklyBadgeFtue.BuildJungleHeistLayout);
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

        /// <summary>
        /// A prefab of a panel opened inside another canvas (no canvas of its own, stretched over its parent), built with
        /// the Home skin; made only when missing so hand edits stay [user].
        /// </summary>
        private static GameObject PanelPrefab(string name, System.Action<RectTransform, SnackDash.UI.UiSkin> build)
        {
            var path = UiPrefabFolder + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            System.IO.Directory.CreateDirectory(UiPrefabFolder);
            var host = new GameObject("UI prefab builder").AddComponent<SnackDash.UI.HomeView>();
            FillSkin(host);
            var skin = (SnackDash.UI.UiSkin)typeof(SnackDash.UI.HomeView).GetField("skin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(host);
            var root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.sizeDelta = Vector2.zero;
            build(root, skin);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            Object.DestroyImmediate(root.gameObject);
            Object.DestroyImmediate(host.gameObject);
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
                for (int i = 0; i < names.Length; i++)
                {
                    var sprite = Find<Sprite>(names[i]);
                    if (sprite == null) Debug.LogWarning($"[Setup] UI skin: no sprite {names[i]} for {field}");
                    array.GetArrayElementAtIndex(i).objectReferenceValue = sprite;
                }
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
            Ref("purpleCartoonPlayMaterial", Preset("LuckiestGuy-Regular SDF Purple Cartoon Play btn"));
            Ref("purpleInnerShadowMaterial", Preset("LuckiestGuy-Regular SDF Purple Cartoon Inner Shadow"));
            Ref("redInnerShadowMaterial", Preset("LuckiestGuy-Regular SDF Red Cartoon Inner Shadow"));
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
            Ref("boosterSlot", Find<Sprite>("S_Boosters_BG (2)_0"));
            Ref("boomerangIcon", Find<Sprite>("S_Icon_Booster_FruitCanon_0"));
            Ref("shovelIcon", Find<Sprite>("S_Icon_Booster_Teleporter_0"));
            Ref("undoIcon", Find<Sprite>("S_Icon_Booster_Undo_0"));
            Ref("undoDisabledIcon", Find<Sprite>("S_Icon_Booster_Undo_Disabled_0"));
            Ref("magnetIcon", Find<Sprite>("S_Icon_Booster_Magnet_0"));
            Ref("magnetField", Find<Texture2D>("TX_FX_Magnet_Base"));
            Ref("tutorialFrame", Find<Sprite>("S_Tutorial_BG"));
            Ref("tutorialHand", Find<Sprite>("HandHD_cartoon"));
            Ref("tutorialHandTrail", Find<Sprite>("Hand_Trail"));
            Ref("cardFrameWhite", Find<Sprite>("S_OnboardingScreen_Frame_White"));
            Ref("cardFrameGold", Find<Sprite>("S_OnboardingScreen_Frame_Gold"));
            Ref("cardPin", Find<Sprite>("S_OnboardingScreen_Pin"));
            Ref("cardRibbon", Find<Sprite>("S_OnboardingScreen_Flag"));
            Ref("speechBubble", Find<Sprite>("S_Background_SpeechBubble_02"));
            Ref("speechTriangle", Find<Sprite>("S_Background_Triangle_0"));
            Sprites("mechanicIcons", "S_Icon_Bridge_2_0", "S_Icon_Gate_0", "S_Icon_Tunnel2_0", "S_Icon_Rocks_0", "S_Icon_SecretColectable_0", "S_Icon_SwitchingArrow_0", "S_Icon_Curtain_0", "S_Icon_RisingBlock_0");
            Ref("ribbonGreen", Find<Sprite>("S_Ribbon_Green"));
            Ref("glow", Find<Sprite>("Glow"));
            Ref("fxRay", Find<Texture2D>("lightray3"));
            Ref("fxGlow", Find<Texture2D>("TX_FX_glow1"));
            Ref("fxSparkle", Find<Texture2D>("TX_FX_Sparkle"));
            Ref("boosterCountBadge", Find<Sprite>("S_BoostersNumber_Bg (1)_0"));
            Ref("boosterPlus", Find<Sprite>("S_BoostersBuy_Bg (2)_0"));
            // Two assets carry the sprite name S_Icon_Lock_0; the scene uses the file S_Icon_Lock_0_0.
            Ref("lockIcon", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_MyGame/Art/Sprite/S_Icon_Lock_0_0.asset"));
            Sprites("lockedIcons", "S_Icon_Booster_Teleporter_Locked_0", "S_Icon_Booster_FruitCanon_Locked_0", "S_Icon_Booster_Undo_Locked_0", "S_Icon_Booster_Magnet_Locked_0");
            Ref("whitePlate", Find<Sprite>("JU-Texture-Common-ReosurcesBG"));
            Ref("bluePlate", Find<Sprite>("background"));
            Ref("settingsBackground", Find<Sprite>("S_Settings_Bg_0"));
            Ref("settingsIcon", Find<Sprite>("Icon_Settings"));
            Ref("overlayVignette", Find<Sprite>("S_BG_Overlay_0"));
            Ref("closeSquare", Find<Sprite>("S_Bg_Close_btn"));
            Ref("tutorialArrow", Find<Sprite>("PictoIcon_Arrow_Up"));
            Ref("homeScreenLevelsMaterial", Preset("LuckiestGuy-Regular HomeScreen Levels"));
            Ref("progressBarMaterial", Preset("LuckiestGuy-Regular Progress Bar"));
            Ref("splash", Find<Texture2D>("unity_static_splash"));
            Sprites("named", NamedSprites);
            Ref("shopFont", Find<TMPro.TMP_FontAsset>(Restore.OriginalAssetImporter.ShopFontName));
            Ref("shinyShader", AssetDatabase.LoadAssetAtPath<Shader>("Assets/_MyGame/Shader/UI_Shiny.shader"));
            Ref("uiAdditive", AssetDatabase.LoadAssetAtPath<Material>("Assets/_MyGame/Art/Material/MT_FX_UI_Particle_Additive.mat"));
            var eventMaterials = skin.FindPropertyRelative("eventMaterials");
            eventMaterials.arraySize = Restore.OriginalAssetImporter.EventFontPresets.Length;
            for (int i = 0; i < eventMaterials.arraySize; i++)
                eventMaterials.GetArrayElementAtIndex(i).objectReferenceValue = Preset(Restore.OriginalAssetImporter.EventFontPresets[i]);
            var shopMaterials = skin.FindPropertyRelative("shopMaterials");
            shopMaterials.arraySize = Restore.OriginalAssetImporter.ShopFontPresets.Length;
            for (int i = 0; i < shopMaterials.arraySize; i++)
                shopMaterials.GetArrayElementAtIndex(i).objectReferenceValue = Find<Material>(Restore.OriginalAssetImporter.ShopFontName + " (" + Restore.OriginalAssetImporter.ShopFontPresets[i] + ")");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Sprites the Home, settings and loading screens look up by name (UiSkin.Get).</summary>
        private static readonly string[] NamedSprites =
        {
            "Button_Cancel", "Icon_Arrow", "Icon_Arrow_2", "LineShadow", "LoadingBg", "Popup_Background_Blue", "ZigZag_Tile", "gdpr_rounded_square",
            "S_Active_Tab_3_0", "S_BG_Pop-up_0", "S_BG_Pop-up_Frame_0", "S_BG_Title (1)_0", "S_Bow_Widget (2)_0", "S_Close_Btn_0", "S_Green_btn_0",
            "S_Button_Sheet_0", "S_Button_Sheet_1", "S_Button_Sheet_2", "S_Button_Sheet_3", "S_Coin_CoreStreak_0", "S_Flame_CoreStreak_0",
            "S_StreakBar_Thin_0", "S_Streakbar_Shadow_0", "S_Bg_CoreStreak_Progress_Unfilled_Corner_0", "S_Bg_CoreStreak_Progress_Unfilled_0",
            "S_Counter_Bg_0", "S_Coin_Icon", "S_CoinBackground_Gold_0", "Coin_Icon_03_0", "Coin_Icon_02_0", "ProgressBar_BG", "ProgressBar_ProgressPart", "ProgressBar_Top", "Icon_RV", "S_Heart_Icon_0", "S_Plus_Icon_0", "S_Settings_Icon_0", "S_Notification_Icon_Widget_0",
            "S_Illustration_HomeScreen_Upper", "S_Illustration_HomeScreen_Down", "S_Icon_LoadingScreen_Eyes", "S_Icon_LoadingScreen_Slot",
            "S_Chest_Icon_Widget_0", "S_Bg_Widget_0", "S_Bow_Widget_Corner_0", "S_Lava Quest_Widget_0", "S_Icon_GoldenTicket_0", "S_Icon_NoAds", "S_Lock_Icon_0", "S_Clock_Icon_Widget_0",
            "S_Shop_Icon_0", "S_Home_Icon_0", "Shadow_Round", "Shadow_Round_sharp", "Button_Green_256", "Button_Blue_256", "S_Btn_Green_UI_0", "S_Icon_Support", "Pictogram_Home",
            "S_Button_Round_Green_Underlay", "S_Button_Round_Gray_Underlay",
            "S_Icon_Heart_Large_0", "S_Icon_Infinite_0", "S_Heart_Infinity_Icon_0", "S_Icon_Heart_bg_0", "S_Icon_Heart_0", "S_Button_Orange", "S_Button_Green", "S_Play_ads_Icon_0", "S_Icon_Coin_Single_0",
            "JU-Texture-Common-Coin", "S_Icon_Coin_Single", "S_Crate_Icon", "SpeechBubble_MainOutline", "SpeechBubble_MainInner", "SpeechBubble_PointerOutline", "SpeechBubble_PointerInner", "S_Shop_Header_Bg", "S_Close_Btn", "S_CircleShadow", "S_Shop_Frame", "S_Header_Purple_Bg", "S_Header_Orange_Bg", "S_Shop_Bundle_0", "S_Rewards_Banner_0", "GlowRays",
            "S_Offer_Stars_Small_Pack", "S_Offer_Stars_Medium_Pack", "S_Offer_Stars_Large_Pack", "S_Offer_Stars_XL_Pack", "S_Offer_Stars_XXL_Pack", "S_Offer_Blueberries_Overlay_Bg",
            "S_Btn_BG", "S_Offer_Dark_Beige_Inside_Bg", "S_rewards_Banner_OutlineBlur_0", "CoinContainer", "S_Icon_Heart_Infinit_Lives",
            "S_Bg_CoreStreak_Progress_Yellow_Corner_0", "S_Bg_CoreStreak_Progress_Yellow_0", "S_Bg_CoreStreak_Progress_Yellow_Corner-1_0", "S_Bg_CoreStreak_Progress_Orange_0", "S_Gradient_Vertical", "S_Gradient_Radial",
            "S_1000_Coins", "S_5 000_Coins", "S_10 000_Coins", "S_25 000_Coins", "S_50 000_Coins", "S_100 000_Coins",
            // WEEKLY ADVENTURE (WeeklyMissionsPopup, its chest and rules screens)
            "Coin", "Glow_Circle01", "PictoIcon_Check_2", "S_Background_Timer_0", "S_Chest_Illustration_0", "S_Icon_Booster_FruitCanon", "S_Icon_Booster_Magnet", "S_Icon_Booster_Teleporter", "S_Icon_Booster_Undo",
            "S_Icon_Cancel", "S_Icon_CompleteHardLevels_0", "S_Icon_CompleteLevelStreak_0", "S_Icon_CompleteLevels_0", "S_Icon_CompleteSuperHardLevels_0", "S_Icon_EarnCoins_0", "S_Icon_Info", "S_Icon_Lock_0",
            "S_Icon_Login_0", "S_Icon_MakeAPurchase_0", "S_Icon_Notification_0", "S_Icon_SpendCoins_0", "S_Icon_Timer_01_0", "S_Icon_UseAnyBooster_0", "S_Icon_WatchAds_0", "S_Path_0", "S_Progress_Illustration_0",
            "S_ProgressionBar_Fill_Yellow", "S_Snake_Head_Illustration_0", "S_Weekly_Background_Claimable_Gold_V2_0", "S_Weekly_Background_Claimable_Green_0", "S_Weekly_Background_Day_Dark_0", "S_Weekly_Background_Day_Light",
            "S_Weekly_Background_InProfgress_0", "S_Weekly_Background_InProfgress_Gold_V2_0", "S_Weekly_SafariHat_0", "S_Weekly_Target_0", "Tex_WeeklyMissions_Background 1_0", "Tex_WeeklyMissions_Background_Progression",
            "Tex_WeeklyMissions_Headaer_0", "generic_round_128", "image 1585_0", "S_Weekly_Chests_M_0", "S_Weekly_Chests_M_1", "S_Weekly_Chests_M_2", "S_Weekly_Chests_M_3", "S_Icon_Timer_02_0", "S_Icon_Timer_03_0", "S_Background_SpeechBubble_01", "S_Hand_Tutorial",
            "S_Icon_Music_Enabled", "S_Icon_Music_Disabled", "S_Icon_Haptics_Enabled", "S_Icon_Haptics_Disabled", "S_Icon_Sound_Enabled", "S_Icon_Sound_Disabled",
            // Level button on fire (StreakActive)
            "TX_FX_UI_PlayButton_Streak", "TX_FX_UI_Streakbar_Streak", "TX_FX_UI_PlayButton_StreakEdge", "TX_FX_Sparkle", "S_FX_SmallGlow_01_0", "TX_FX_Square_Glow2",
            // JUNGLE HEIST (join, rules, river, end popups)
            "S_Bg_Offer_Banner_Lava_Quest_0", "S_Title_Accessory_LavaQuest (2)_0", "S_Title_Leaf_LavaQuest (2)_0", "S_Pop-up_Lava_Quest_Frame_Brown _0", "S_Button_Close_Brown_0", "S_Pop-up_Frame_Inlay_Brown_0", "TXT_Avatars_Icons_0", "TXT_Avatars_Icons_1", "TXT_Avatars_Icons_2", "TXT_Avatars_Icons_3", "TXT_Avatars_Icons_4", "TXT_Avatars_Icons_5", "TXT_Avatars_Icons_6", "TXT_Avatars_Icons_7", "TXT_Avatars_Icons_8", "TXT_Avatars_Icons_9", "S_Profile_Frame_0", "S_Profile_Frame_Player_0", "Tex_Jungle_Heist_Start_Illustration_V2_0", "TX_FX_Glow", "Tex_FX_Rays", "S_Jungle_Heist_Start_Boat_0", "S_Gradient_Overlay_LavaQuest", "S_GrandPrize_LavaQuest_0", "Tex_FX_Environment_WaterEdge_0", "Tex_FX_Environment_WaterEdge_1", "Tex_FX_Environment_WaterEdge_2", "Tex_FX_Environment_Shadows_0", "Tex_FX_Environment_Shadows_1", "Tex_FX_Environment_Shadows_2", "Tex_LavaQuest_Illustration_1_0", "S_Waterfall_1", "S_Waterfall_2", "S_Waterfall_3", "S_Sign_Bg_0", "S_Lilypad_1_0", "S_Lilypad_2_0", "S_Lilypad_3_0", "S_Lilypad_4_0", "S_Lilypad_5_0", "S_Lilypad_6_0", "S_Lilypad_7_0", "Tex_FX_Lilypad_WaterEdge_0", "Tex_FX_Lilypad_WaterEdge_1", "Tex_FX_Lilypad_WaterEdge_2", "Tex_FX_Lilypad_WaterEdge_3", "Tex_FX_Lilypad_WaterEdge_4", "Tex_FX_Lilypad_WaterEdge_5", "Tex_LavaQuest_Foreground", "S_Lava_Quest_Bg_0", "S_Bg_Counter_Lava_Quest_0", "Tex_Jungle Heist_Lose_screen", "S_Coins_Medium_Pack_1_0", "S_Background_SpeechBubble_03_0",
            // SNACKY PASS (SnackyPassView, its rules, offer and chests)
            "S_SeasonPass_BackgroundImg_0", "S_SeasonPass_SnakeCoins_0", "SP_Shadow", "S_GoldenLine", "S_TimerBackground", "S_Img_TicketShdw_0", "S_Img_Ticket_0", "S_Button_Shadow_0", "S_Button_WMGreen_0",
            "S_Button_WMPurple_0", "S_Icon_LevelNumber_Purple_0", "ProgressBar_Back_0", "S_Icon_SpeechBubble_Purple_0", "S_Glow", "S_PurpleButtonPass_0", "S_GoldenButtonPass_0", "S_Icon_Locked", "S_Icon_PremiumReward",
            "S_Icon_Chests2_0", "S_Icon_Chests2_1", "S_Icon_Chests2_2", "S_Icon_Chests2_3", "S_FX_Sparkle_02_0", "S_FruitsIcon_0", "S_IconProgressBar_0", "S_TitleRewardBackground_0", "S_Pop-up_Frame_GoldenBorder_0",
            "BackgroundImage_0", "S_LinearGradiant_0", "S_Pop-up_Frame_InlayWhite_0", "S_Bg_Clock_0", "S_Button_CloseYellow_0", "S_Bg_Offer_Top_0", "S_Bg_Offer_Container", "S_Bg_Offer_BottomV2", "S_Button_Green_0", "S_Bg_Offer_Frame_0",
        };

        private const string HomeScenePath = "Assets/_MyGame/Scenes/Home.unity";

        /// <summary>(Re)creates Scenes/Home.unity: a UI camera, the EventSystem and HomeView (splash, loading, Home).</summary>
        [MenuItem("SnackDash/Setup/Build Home Scene")]
        public static void BuildHome()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.682f, 0.424f, 0.988f);
            camera.cullingMask = 0; // nothing in the world: Home is all canvas
            cameraGo.AddComponent<UniversalAdditionalCameraData>();
            var home = new GameObject("Home").AddComponent<SnackDash.UI.HomeView>();
            FillSkin(home);
            Set(home, "levels", AssetDatabase.LoadAssetAtPath<LevelList>("Assets/_MyGame/Data/Levels/LevelList.asset"));
            Set(home, "sounds", AssetDatabase.LoadAssetAtPath<SnackDash.Audio.SoundLibrary>("Assets/_MyGame/Data/SoundLibrary.asset"));
            Set(home, "prefab", UiPrefab<SnackDash.UI.HomeView>("Home", 10, (view, root) => view.BuildLayout(root))); // made once, then edited by hand [user]
            Set(home, "weeklyPrefab", PanelPrefab("WeeklyMissionsPopup", SnackDash.UI.WeeklyMissionsPopup.BuildLayout));
            Set(home, "weeklyRulesPrefab", PanelPrefab("WeeklyMissionsRules", SnackDash.UI.WeeklyMissionsRules.BuildLayout));
            Set(home, "weeklyBadgeFtuePrefab", PanelPrefab("WeeklyBadgeFtue", SnackDash.UI.WeeklyBadgeFtue.BuildLayout));
            Set(home, "snackyPassPrefab", PanelPrefab("SnackyPassView", SnackDash.UI.SnackyPassView.BuildLayout));
            Set(home, "snackyPassRulesPrefab", PanelPrefab("SnackyPassRules", SnackDash.UI.SnackyPassRules.BuildLayout));
            Set(home, "snackyPassOfferPrefab", PanelPrefab("SnackyPassOffer", SnackDash.UI.SnackyPassOffer.BuildLayout));
            Set(home, "snackyPassBadgeFtuePrefab", PanelPrefab("SnackyPassBadgeFtue", SnackDash.UI.WeeklyBadgeFtue.BuildSnackyPassLayout));
            Set(home, "jungleJoinPrefab", PanelPrefab("JungleHeistJoin", SnackDash.UI.JungleHeistJoin.BuildLayout));
            Set(home, "jungleRulesPrefab", PanelPrefab("JungleHeistRules", SnackDash.UI.JungleHeistRules.BuildLayout));
            Set(home, "jungleMainPrefab", PanelPrefab("JungleHeistMain", SnackDash.UI.JungleHeistMain.BuildLayout));
            Set(home, "jungleEndedPrefab", PanelPrefab("JungleHeistEnded", SnackDash.UI.JungleHeistEnded.BuildLayout));
            Set(home, "jungleRewardsPrefab", PanelPrefab("JungleHeistRewards", SnackDash.UI.JungleHeistRewards.BuildLayout));
            Set(home, "jungleMatchPrefab", PanelPrefab("JungleHeistMatch", SnackDash.UI.JungleHeistMatch.BuildLayout));
            Set(home, "jungleBadgeFtuePrefab", PanelPrefab("JungleHeistBadgeFtue", SnackDash.UI.WeeklyBadgeFtue.BuildJungleHeistLayout));
            System.Func<string, Material> mat = n => AssetDatabase.LoadAssetAtPath<Material>("Assets/_MyGame/Art/Material/" + n + ".mat");
            Set(home, "streakFront", mat("Mat_UI_PlayButton_StreakActive"));
            Set(home, "streakFrontSuperHard", mat("Mat_UI_PlayButton_StreakActive_SuperHard"));
            Set(home, "streakUnderlay", mat("Mat_UI_PlayButton_Underlay_StreakActive"));
            Set(home, "streakUnderlaySuperHard", mat("Mat_UI_PlayButton_Underlay_StreakActive_SuperHard"));
            Set(home, "streakGlow", mat("Mat_UI_Streak_Glow"));
            Set(home, "streakGlowEdge", mat("Mat_UI_Streak_Glow_2"));
            Set(home, "streakParticles", mat("MT_FX_UI_Particle_Additive"));
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            EditorSceneManager.SaveScene(scene, HomeScenePath);
            SetBuildScenes();
            Debug.Log($"[Setup] Built {HomeScenePath}");
        }

        /// <summary>Home first (the app starts there), then Game.</summary>
        private static void SetBuildScenes() =>
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(HomeScenePath, true), new EditorBuildSettingsScene(ScenePath, true) };

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
