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

        public Sprite[] bigButton = new Sprite[4];   // S_Button_Sheet_3 (base), _2 (front), _1 (highlight), _0 (outline)
        public Sprite[] smallButton = new Sprite[4]; // S_Button_Small_Sheet_3, _2, _1, _0
        public Sprite buttonUnderlay;                // S_Button_Underlay_Sheet_1
        public Sprite bottomGradient;                // gradient01
        public Sprite topGradient;                   // VerticalGradient
        public Sprite bannerFrame;                   // BannerFrame00_04~06_Bg
        public Sprite titleBanner;                   // S_BG_Title (1)_0
        public Sprite brokenHeart;                   // S_Icon_BrokenHeart_Large_0
        public Sprite greenButton;                   // S_Button_Green
    }
}
