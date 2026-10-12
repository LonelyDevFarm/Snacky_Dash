using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CheatSystem.Editor
{
    /// <summary>
    /// Makes Assets/CheatSystem/Resources/CheatPanel.prefab once (Tools/Cheat System/Create Cheat Panel Prefab).
    /// After that the prefab is yours to restyle; the builder only makes it again when it is missing (or when asked to
    /// replace it). Names and parents must stay: CheatPanel finds its parts by them.
    /// Laid out for a 1080 wide portrait screen (CanvasScaler 1080x1920, scaled to the width).
    /// </summary>
    public static class CheatPanelBuilder
    {
        private const string Folder = "Assets/CheatSystem/Resources";
        private const string PrefabPath = Folder + "/CheatPanel.prefab";

        private const float RowHeight = 100f, LabelWidth = 300f, FontSize = 38f;
        private static readonly Color Window = new Color(0.11f, 0.12f, 0.16f, 1f);
        private static readonly Color Header = new Color(0.18f, 0.20f, 0.27f, 1f);
        private static readonly Color TabColor = new Color(0.24f, 0.26f, 0.34f, 1f);
        private static readonly Color Selected = new Color(0.96f, 0.58f, 0.10f, 1f);
        private static readonly Color ButtonColor = new Color(0.20f, 0.45f, 0.85f, 1f);
        private static readonly Color RowColor = new Color(0.17f, 0.18f, 0.23f, 1f);
        private static readonly Color CloseColor = new Color(0.85f, 0.25f, 0.22f, 1f);
        private static readonly Color LabelColor = new Color(1f, 0.92f, 0.65f, 1f);

        [MenuItem("Tools/Cheat System/Create Cheat Panel Prefab")]
        public static void Create()
        {
            if (File.Exists(PrefabPath) && !EditorUtility.DisplayDialog("Cheat Panel",
                    "CheatPanel.prefab đã có. Dựng lại sẽ mất mọi chỉnh sửa tay trong prefab và các ô đang kéo prefab này (CheatButton) phải gán lại.",
                    "Dựng lại", "Hủy"))
                return;
            Directory.CreateDirectory(Folder);
            var root = Build();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.Refresh();
            Debug.Log("[Cheat] Created " + PrefabPath);
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        }

        private static GameObject Build()
        {
            var root = new GameObject("CheatPanel", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000; // above every popup
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0f;
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<CheatPanel>();

            // Dim: tap outside the window to close.
            var dim = Stretch(root.transform, "Dim");
            Image(dim, new Color(0f, 0f, 0f, 0.55f));
            dim.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

            var window = Stretch(root.transform, "Window", new Vector2(30f, 60f), new Vector2(-30f, -110f));
            Image(window, Window);

            var header = Rect(window, "Header", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 110f));
            Image(header, Header);
            var title = Text(Stretch(header, "Title", new Vector2(30f, 0f), new Vector2(-130f, 0f)), "CHEAT", 52f, TextAlignmentOptions.MidlineLeft, Color.white);
            title.fontStyle = FontStyles.Bold;
            var close = Rect(header, "Close", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(90f, 90f));
            Image(close, CloseColor);
            close.gameObject.AddComponent<Button>();
            Text(Stretch(close, "Text"), "X", 48f, TextAlignmentOptions.Center, Color.white);

            var tabs = Rect(window, "Tabs", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, -110f), new Vector2(0f, 100f));
            var tabsLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabsLayout.padding = new RectOffset(10, 10, 10, 10);
            tabsLayout.spacing = 8f;
            tabsLayout.childControlWidth = tabsLayout.childControlHeight = true;
            tabsLayout.childForceExpandWidth = tabsLayout.childForceExpandHeight = true;

            var scroll = Stretch(window, "Scroll", Vector2.zero, new Vector2(0f, -215f));
            var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.scrollSensitivity = 40f;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Stretch(scroll, "Viewport");
            Image(viewport, new Color(0f, 0f, 0f, 0f)); // catches drags between rows
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect(viewport, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(20, 20, 20, 40);
            contentLayout.spacing = 12f;
            contentLayout.childControlWidth = contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.viewport = viewport;
            scrollRect.content = content;

            var templates = Rect(root.transform, "Templates", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            BuildTab(templates);
            BuildLabel(templates);
            BuildButton(templates);
            BuildButtons(templates);
            BuildInput(templates);
            BuildToggle(templates);
            templates.gameObject.SetActive(false);
            return root;
        }

        // ------------------------------------------------------------------ templates

        private static void BuildTab(RectTransform parent)
        {
            var tab = Rect(parent, "Tab", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 80f));
            Image(tab, TabColor);
            tab.gameObject.AddComponent<Button>();
            var selected = Stretch(tab, "Selected");
            Image(selected, Selected).raycastTarget = false;
            var text = Text(Stretch(tab, "Text", new Vector2(6f, 0f), new Vector2(-6f, 0f)), "Tab", 34f, TextAlignmentOptions.Center, Color.white);
            text.enableAutoSizing = true;
            text.fontSizeMin = 18f;
            text.fontSizeMax = 34f;
        }

        private static void BuildLabel(RectTransform parent)
        {
            var label = Rect(parent, "Label", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 60f));
            var text = Text(label, "Info", 34f, TextAlignmentOptions.MidlineLeft, LabelColor);
            text.margin = new Vector4(10f, 6f, 10f, 6f); // TMP gives the row its height (one line or more)
        }

        private static void BuildButton(RectTransform parent)
        {
            var row = Row(parent, "Button");
            Image(row, ButtonColor);
            row.gameObject.AddComponent<Button>();
            Text(Stretch(row, "Text"), "Button", FontSize, TextAlignmentOptions.Center, Color.white);
        }

        private static void BuildButtons(RectTransform parent)
        {
            var row = Row(parent, "Buttons");
            var layout = HorizontalRow(row);
            layout.spacing = 8f;
            Label(row);
            var line = Rect(row, "Row", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, RowHeight));
            line.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var lineLayout = line.gameObject.AddComponent<HorizontalLayoutGroup>();
            lineLayout.spacing = 8f;
            lineLayout.childControlWidth = lineLayout.childControlHeight = true;
            lineLayout.childForceExpandWidth = lineLayout.childForceExpandHeight = true;
            var button = Rect(line, "Button", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, RowHeight));
            Image(button, ButtonColor);
            button.gameObject.AddComponent<Button>();
            var text = Text(Stretch(button, "Text", new Vector2(4f, 0f), new Vector2(-4f, 0f)), "+1", 34f, TextAlignmentOptions.Center, Color.white);
            text.enableAutoSizing = true;
            text.fontSizeMin = 18f;
            text.fontSizeMax = 34f;
        }

        private static void BuildInput(RectTransform parent)
        {
            var row = Row(parent, "Input");
            HorizontalRow(row).spacing = 8f;
            Label(row);

            var field = Rect(row, "Field", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, RowHeight));
            field.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Image(field, Color.white);
            var area = Stretch(field, "Text Area", new Vector2(16f, 6f), new Vector2(-16f, -6f));
            area.gameObject.AddComponent<RectMask2D>();
            var placeholder = Text(Stretch(area, "Placeholder"), "...", FontSize, TextAlignmentOptions.MidlineLeft, new Color(0.4f, 0.4f, 0.4f));
            placeholder.fontStyle = FontStyles.Italic;
            var text = Text(Stretch(area, "Text"), "", FontSize, TextAlignmentOptions.MidlineLeft, Color.black);
            var input = field.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.pointSize = FontSize;
            input.fontAsset = text.font;

            var go = Rect(row, "Go", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180f, RowHeight));
            go.gameObject.AddComponent<LayoutElement>().preferredWidth = 180f;
            Image(go, ButtonColor);
            go.gameObject.AddComponent<Button>();
            Text(Stretch(go, "Text"), "GO", FontSize, TextAlignmentOptions.Center, Color.white);
        }

        private static void BuildToggle(RectTransform parent)
        {
            var row = Row(parent, "Toggle");
            Image(row, RowColor);
            row.gameObject.AddComponent<Button>();
            Text(Stretch(row, "Label", new Vector2(24f, 0f), new Vector2(-200f, 0f)), "Toggle", FontSize, TextAlignmentOptions.MidlineLeft, Color.white);
            var state = Text(Rect(row, "State", new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(170f, 0f)), "OFF", FontSize, TextAlignmentOptions.MidlineRight, Color.white);
            state.fontStyle = FontStyles.Bold;
        }

        // ------------------------------------------------------------------ helpers

        private static RectTransform Row(RectTransform parent, string name)
        {
            var row = Rect(parent, name, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, RowHeight));
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = RowHeight;
            return row;
        }

        private static HorizontalLayoutGroup HorizontalRow(RectTransform row)
        {
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            return layout;
        }

        private static void Label(RectTransform row)
        {
            var label = Rect(row, "Label", Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(LabelWidth, RowHeight));
            var element = label.gameObject.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = LabelWidth;
            var text = Text(label, "Label", 34f, TextAlignmentOptions.MidlineLeft, Color.white);
            text.enableAutoSizing = true;
            text.fontSizeMin = 20f;
            text.fontSizeMax = 34f;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform Stretch(Transform parent, string name) => Stretch(parent, name, Vector2.zero, Vector2.zero);

        private static RectTransform Stretch(Transform parent, string name, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = Rect(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        private static Image Image(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI Text(RectTransform rect, string text, float size, TextAlignmentOptions alignment, Color color)
        {
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = alignment;
            tmp.color = color;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = true;
            return tmp;
        }
    }
}
