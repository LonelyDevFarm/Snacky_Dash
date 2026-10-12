using System;
using System.Collections.Generic;
using UnityEngine;

namespace SnackDash.LevelSystem
{
    /// <summary>Per-fruit visuals. Generated from the original GraphicSet_VO._graphicDatas.</summary>
    [CreateAssetMenu(menuName = "SnackDash/Fruit Graphic Set")]
    public class FruitGraphicSet : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public PillKind kind;
            public Sprite sprite;
            public Sprite spriteNoOutline;
            public Material crateMaterial;
            public Color outlineColor = Color.black;
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
