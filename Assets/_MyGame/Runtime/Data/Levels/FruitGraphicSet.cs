using System;
using System.Collections.Generic;
using UnityEngine;

namespace SnackDash.LevelSystem
{
    /// <summary>
    /// Hình của từng loại trái (sprite, material khay, màu viền). Asset Data/FruitGraphicSet.asset, sinh từ
    /// GraphicSet_VO._graphicDatas gốc; CratesView, CurtainsView tham chiếu.
    /// </summary>
    [CreateAssetMenu(menuName = "SnackDash/Fruit Graphic Set")]
    public class FruitGraphicSet : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public PillKind kind;
            public Sprite sprite;
            public Sprite spriteNoOutline;
            [Tooltip("Icon theo hình trái trên sân (bản 1.15): bộ đếm trên rèm. Trống thì dùng Sprite.")]
            public Sprite fieldSprite;
            [Tooltip("Icon hình trái trên sân không viền trắng (gốc 1.15 GraphicSet_VO SpriteNoOutline): thanh đếm trái HUD. Trống thì dùng Field Sprite.")]
            public Sprite fieldSpriteNoOutline;
            public Material crateMaterial;
            public Color outlineColor = Color.black;
            [Tooltip("Cửa khay đóng (TOGGLE CRATE) và tấm phẳng dưới cửa theo màu trái (gốc GraphicSet_VO 1.15 MaterialShutterCrate / Plane).")]
            public Material shutterMaterial, shutterPlaneMaterial;
            [Tooltip("Nửa dây khay đôi phía khay màu này (sáng tạo): màu chính của khay (màu tại 0.45 trên gradient vật liệu khay).")]
            public Color ropeColor = Color.white;
            [Tooltip("Màu thành khay khi hiện trên màn (đo trên ảnh chụp, đã có ánh sáng): khoen xích khay đôi tô màu này cho liền với thùng.")]
            public Color crateRimColor = Color.white;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public Entry Get(PillKind kind)
        {
            foreach (var entry in entries)
                if (entry.kind == kind) return entry;
            return null;
        }

        public void SetEntries(List<Entry> newEntries) => entries = newEntries;
    }
}
