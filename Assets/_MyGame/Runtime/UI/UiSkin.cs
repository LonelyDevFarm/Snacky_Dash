using System;
using TMPro;
using UnityEngine;

namespace SnackDash.UI
{
    /// <summary>Original sprites, font and font materials used by the popups (assigned by the scene builder).</summary>
    [Serializable]
    public class UiSkin
    {
        public TMP_FontAsset font;                 // LuckiestGuy-Regular SDF
        public Material titleBackMaterial;         // LuckiestGuy-Regular Combo - BG
        public Material titleFrontMaterial;        // LuckiestGuy-Regular Combo - Base
        public Material greenOutlineMaterial;      // LuckiestGuy-Regular SDF Green Outline
        public Material darkRedMaterial;           // LuckiestGuy-Regular SDF Dark Red
        public Material blueUnderlayMaterial;      // LuckiestGuy-Regular SDF Blue Underlay
        public Material greenCartoonMaterial;      // LuckiestGuy-Regular SDF Green Cartoon
        public Material plainMaterial;             // LuckiestGuy-Regular Atlas Material
        public Material redCartoonMaterial;        // LuckiestGuy-Regular SDF Red Cartoon
        public Material redCartoonInnerMaterial;   // LuckiestGuy-Regular SDF Red Cartoon Play btn
        public Material brownLevelMaterial;        // LuckiestGuy-Regular Brown Level Number
        public Material purpleCartoonPlayMaterial; // LuckiestGuy-Regular SDF Purple Cartoon Play btn (Home Hard button)
        public Material purpleInnerShadowMaterial; // LuckiestGuy-Regular SDF Purple Cartoon Inner Shadow ("Hard")
        public Material redInnerShadowMaterial;    // LuckiestGuy-Regular SDF Red Cartoon Inner Shadow ("Super Hard")

        // In-game HUD (Panel_InGame_HUD in Game.unity)
        public Sprite hudBar;                        // S_CoreTopUI_Main_0
        public Sprite hudLevelHolder;                // S_CoreTopUI_LevelHolder_0
        public Sprite hudProgressBackground;         // S_CoreTopUI_ProgressBarBG_0
        public Sprite hudProgressFill;               // S_CoreTopUI_ProgressBarStraight_0
        public Sprite hudSnakeHead;                  // S_SnakeHead_0
        public Sprite hudFruits;                     // Group 1321316041_0
        public Sprite milestonePendingBar;           // S_CoreTopUI_ProgressBarBGMilestone_0 (WavePending_HUD)
        public Sprite milestonePendingDot;           // S_CoreTopUI_ProgressBar_Milestone_0
        public Sprite milestoneDoneBar;              // S_CoreTopUI_ProgressBarMilestone_0 (WaveComplete_HUD)
        public Sprite checkMark;                     // S_Icon_CheckMark

        public Sprite[] bigButton = new Sprite[4];   // S_Button_Sheet_3 (base), _2 (front), _1 (highlight), _0 (outline)
        public Sprite[] smallButton = new Sprite[4]; // S_Button_Small_Sheet_3, _2, _1, _0
        public Sprite buttonUnderlay;                // S_Button_Underlay_Sheet_1
        public Sprite bottomGradient;                // gradient01
        public Sprite topGradient;                   // VerticalGradient
        public Sprite bannerFrame;                   // BannerFrame00_04~06_Bg
        public Sprite titleBanner;                   // S_BG_Title (1)_0
        public Sprite brokenHeart;                   // S_Icon_BrokenHeart_Large_0
        public Sprite greenButton;                   // S_Button_Green
        public Sprite cancelButton;                  // SP_Cancel_Btn
        public Sprite snakeIllustration;             // SP_Snake_0 (second-chance popup)
        public Sprite fruitsIllustration;            // SP_Fruits (1)_34
        public Sprite coinIcon;                      // S_Icon_Coin_Single

        [Header("Boosters")]
        public Sprite boosterSlot;                   // S_Boosters_BG (2)_0 (beige hexagon)
        public Sprite boomerangIcon;                 // S_Icon_Booster_FruitCanon_0 (Boomerang's internal name)
        public Sprite shovelIcon;                    // S_Icon_Booster_Teleporter_0
        public Sprite undoIcon;                      // S_Icon_Booster_Undo_0
        public Sprite undoDisabledIcon;              // S_Icon_Booster_Undo_Disabled_0
        public Sprite magnetIcon;                    // S_Icon_Booster_Magnet_0
        public Texture2D magnetField;                // TX_FX_Magnet_Base: the field disc of MT_FX_Magnet_Projection (a URP decal in the original)
        public Sprite boosterCountBadge;             // S_BoostersNumber_Bg (1)_0
        public Sprite boosterPlus;                   // S_BoostersBuy_Bg (2)_0
        public Sprite lockIcon;                      // S_Icon_Lock_0_0
        public Sprite[] lockedIcons;                 // S_Icon_Booster_{Teleporter,FruitCanon,Undo,Magnet}_Locked_0 (shown under the lock)
        public Sprite whitePlate;                    // JU-Texture-Common-ReosurcesBG
        public Sprite bluePlate;                     // background (tinted 2C64CF)
        public Sprite settingsBackground;            // S_Settings_Bg_0
        public Sprite settingsIcon;                  // Icon_Settings
        public Sprite overlayVignette;               // S_BG_Overlay_0
        public Sprite closeSquare;                   // S_Bg_Close_btn
        public Sprite tutorialArrow;                 // PictoIcon_Arrow_Up (BoosterTutorialView)
        public Material homeScreenLevelsMaterial;    // LuckiestGuy-Regular HomeScreen Levels
        public Material progressBarMaterial;         // LuckiestGuy-Regular Progress Bar
        public Sprite tutorialFrame;                 // S_Tutorial_BG (TutorialView_Redesign message frame and panel)
        public Sprite tutorialHand;                  // HandHD_cartoon (Hand_SwipeTuto)
        public Sprite tutorialHandTrail;             // Hand_Trail
        public Sprite cardFrameWhite;                // S_OnboardingScreen_Frame_White (LdfTutorialPopup card)
        public Sprite cardFrameGold;                 // S_OnboardingScreen_Frame_Gold (BoosterTutorialPopup card)
        public Sprite cardPin;                       // S_OnboardingScreen_Pin
        public Sprite cardRibbon;                    // S_OnboardingScreen_Flag
        public Sprite speechBubble;                  // S_Background_SpeechBubble_02
        public Sprite speechTriangle;                // S_Background_Triangle_0
        public Sprite[] mechanicIcons;               // S_Icon_Bridge_2_0, Gate, Tunnel2, Rocks, SecretColectable, SwitchingArrow (IngredientUnlockConfig)
        public Sprite ribbonGreen;                   // S_Ribbon_Green (VictoryPopup feature unlocked)
        public Sprite glow;                          // Glow
        public Texture2D fxRay;                      // lightray3 (LdfTutorialPopup Back Rays 02)
        public Texture2D fxGlow;                     // TX_FX_glow1 (Center, Chest Pop FX)
        public Texture2D fxSparkle;                  // TX_FX_Sparkle (sparkles/stars)
        public Texture2D splash;                     // unity_static_splash (the APK's launch image)
        [UnityEngine.Tooltip("Home, settings and loading sprites, looked up by name (UiSkin.Get).")]
        public Sprite[] named;

        /// <summary>A sprite of <see cref="named"/> by its asset name (null when missing).</summary>
        public Sprite Get(string name)
        {
            if (named == null) return null;
            foreach (var sprite in named) if (sprite != null && sprite.name == name) return sprite;
            return null;
        }

        [UnityEngine.Tooltip("LilitaOne SDF, the Shop's font.")]
        public TMP_FontAsset shopFont;
        [UnityEngine.Tooltip("LilitaOne presets of the Shop, looked up by their original name (UiSkin.ShopMaterial).")]
        /// <summary>LuckiestGuy presets of the event popups (OriginalAssetImporter.EventFontPresets).</summary>
        public Material[] eventMaterials;

        /// <summary>UI/Shiny (Shader/UI_Shiny), the sweeping light of UiShiny.</summary>
        public Shader shinyShader;

        /// <summary>MT_FX_UI_Particle_Additive (UI/Additive): glows and rays added on top.</summary>
        public Material uiAdditive;

        /// <summary>An event preset by its original name, e.g. "LuckiestGuy-Regular weekly orange" (plain when missing).</summary>
        public Material EventMaterial(string original)
        {
            if (eventMaterials != null)
                foreach (var material in eventMaterials) if (material != null && material.name.EndsWith("(" + original + ")")) return material;
            return plainMaterial;
        }

        public Material[] shopMaterials;

        /// <summary>A Shop preset by its original name, e.g. "LilitaOne-Regular - Buy Btn".</summary>
        public Material ShopMaterial(string original)
        {
            if (shopMaterials == null) return null;
            foreach (var material in shopMaterials) if (material != null && material.name.EndsWith("(" + original + ")")) return material;
            return null;
        }
    }
}
