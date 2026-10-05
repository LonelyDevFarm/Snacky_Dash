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
        public Sprite boosterCountBadge;             // S_BoostersNumber_Bg (1)_0
        public Sprite boosterPlus;                   // S_BoostersBuy_Bg (2)_0
        public Sprite lockIcon;                      // S_Icon_Lock_0_0
        public Sprite whitePlate;                    // JU-Texture-Common-ReosurcesBG
        public Sprite bluePlate;                     // background (tinted 2C64CF)
        public Sprite settingsBackground;            // S_Settings_Bg_0
        public Sprite settingsIcon;                  // Icon_Settings
        public Sprite overlayVignette;               // S_BG_Overlay_0
        public Sprite closeSquare;                   // S_Bg_Close_btn
        public Sprite tutorialArrow;                 // PictoIcon_Arrow_Up (BoosterTutorialView)
        public Material homeScreenLevelsMaterial;    // LuckiestGuy-Regular HomeScreen Levels
        public Material progressBarMaterial;         // LuckiestGuy-Regular Progress Bar
    }
}
