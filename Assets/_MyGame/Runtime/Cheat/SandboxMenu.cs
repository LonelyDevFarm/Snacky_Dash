using System;
using System.Collections.Generic;
using SnackDash.Economy;
using SnackDash.UI;
using UnityEngine;

namespace SnackDash.Cheat
{
    /// <summary>
    /// Menu thử giao diện của scene <c>Scenes/_Sandbox</c>: bấm Play, cột nút bên trái mở từng popup của Home và 3 sự kiện
    /// mà không phải chơi tới nơi. Mở prefab của popup trong Project, sửa, Play lại để xem.
    /// Popup trong màn chơi (NO WAY OUT, VICTORY…) xem ở scene Game qua bảng cheat (WIN / LOSE).
    /// Chỉ là công cụ nội bộ, không có trong game (scene không nằm trong Build Settings).
    /// </summary>
    public class SandboxMenu : MonoBehaviour
    {
        [Tooltip("Canvas của scene (1080x1920, khớp chiều cao như Home): popup mở trong đây.")]
        [SerializeField] private RectTransform canvas;

        [Header("Khối dùng chung (Prefabs/UI/Widgets, Popups/JungleHeist, Popups/WeeklyMissions) — mục \"Khối dùng chung\" xếp chúng từ trên xuống")]
        [SerializeField] private GameObject[] widgetPrefabs = new GameObject[0];

        private List<(string name, Action open)> _entries;
        private Vector2 _scroll;
        private GUIStyle _button;
        private bool _shown = true;

        private void Start()
        {
            // Dữ liệu để popup sự kiện có nội dung: level 40, 3 sự kiện đã mở.
            int level = 40;
            WeeklyMissions.Shared.StartIfUnlocked(level);
            SeasonPass.Shared.StartIfUnlocked(level);
            JungleHeist.Shared.StartIfUnlocked(level);
            LuckyLadybug.Shared.StartIfUnlocked(level);
            _entries = new List<(string, Action)>
            {
                ("Khối dùng chung", ShowWidgets),
                ("Settings", () => UI.Show<SettingsPopup>()),
                ("Lives", () => UI.Show<LivesPopup>()),
                ("Shop", () => UI.Show<ShopView>()),
                ("Weekly", () => UI.Show<WeeklyMissionsPopup>()),
                ("Weekly rules", () => UI.Show<WeeklyMissionsRules>()),
                ("Snacky Pass", () => UI.Show<SnackyPassView>()),
                ("Snacky rules", () => UI.Show<SnackyPassRules>()),
                ("Snacky offer", () => UI.Show<SnackyPassOffer>()),
                ("JH Join", () => UI.Show<JungleHeistJoin>()),
                ("JH Finding players", () => UI.Show<JungleHeistMatch>()),
                ("JH Rules", () => UI.Show<JungleHeistRules>()),
                ("JH River (step 3)", () => { SetHeist(JungleHeist.State.Running, 3); OpenRiver(); }),
                ("JH River (won)", () => { SetHeist(JungleHeist.State.Won, JungleHeist.Steps); OpenRiver(); }),
                ("JH Rewards", () => UI.Show<JungleHeistRewards>()),
                ("JH Ended won", () => UI.Show<JungleHeistEnded>()?.Setup(true)),
                ("JH Ended lost", () => UI.Show<JungleHeistEnded>()?.Setup(false)),
                ("Lucky Ladybug", () => UI.Show<LuckyLadybugPopup>()),
                ("Ladybug rules", () => UI.Show<LuckyLadybugRules>()),
                ("Ladybug chest (claim)", () => { LuckyLadybug.Shared.CheatAdd(LuckyLadybug.Target); UI.Show<LuckyLadybugPopup>()?.ClaimNow(); }),
            };
        }

        /// <summary>Đặt từng khối dùng chung từ trên xuống giữa màn để xem; khối to (khung) thu nhỏ cho vừa.</summary>
        private void ShowWidgets()
        {
            float y = 860f;
            foreach (var prefab in widgetPrefabs)
            {
                if (prefab == null) continue;
                var item = (RectTransform)Instantiate(prefab, canvas).transform;
                item.anchorMin = item.anchorMax = new Vector2(0.5f, 0.5f);
                float height = Mathf.Max(item.rect.height, 80f);
                float scale = Mathf.Min(1f, 260f / height);
                item.localScale = Vector3.one * scale;
                y -= height * scale * 0.5f;
                item.anchoredPosition = new Vector2(120f, y);
                y -= height * scale * 0.5f + 40f;
            }
        }

        private static SnackDash.Core.UIManager UI => SnackDash.Core.Services.Get<SnackDash.Core.UIManager>();

        private void OpenRiver() => UI.Show<JungleHeistMain>();

        /// <summary>Đặt trạng thái Jungle Heist chỉ để xem (không cộng xu); bước đã xem bằng bước hiện tại để không chạy hoạt ảnh nhảy.</summary>
        private static void SetHeist(JungleHeist.State state, int step)
        {
            PlayerPrefs.SetInt("jh_state", (int)state);
            PlayerPrefs.SetInt("jh_step", step);
            PlayerPrefs.SetInt("jh_seen_step", step);
            PlayerPrefs.SetInt("jh_claimed", 1);
        }

        private void CloseAll()
        {
            Time.timeScale = 1f;
            for (int i = canvas.childCount - 1; i >= 0; i--) Destroy(canvas.GetChild(i).gameObject);
            foreach (var shop in FindObjectsOfType<ShopView>()) Destroy(shop.gameObject);
            UI.CloseAll();
        }

        private void OnGUI()
        {
            if (_entries == null) return;
            float unit = Screen.height / 60f;
            if (_button == null) _button = new GUIStyle(GUI.skin.button);
            _button.fontSize = Mathf.RoundToInt(unit * 1.1f);
            float width = unit * 12f, height = unit * 2.4f;
            // Thu gọn sau khi mở popup để không che; nút nhỏ góc trên trái mở lại menu.
            if (!_shown)
            {
                if (GUI.Button(new Rect(unit * 0.5f, unit * 0.5f, unit * 6f, height), "☰ Menu", _button)) _shown = true;
                return;
            }
            GUILayout.BeginArea(new Rect(unit * 0.5f, unit * 0.5f, width, Screen.height - unit));
            _scroll = GUILayout.BeginScrollView(_scroll);
            if (GUILayout.Button("✕ Đóng hết", _button, GUILayout.Height(height))) CloseAll();
            foreach (var (name, open) in _entries)
                if (GUILayout.Button(name, _button, GUILayout.Height(height))) { CloseAll(); open(); _shown = false; }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
