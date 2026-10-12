using System.Collections;
using SnackDash.Economy;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Nút "Level N" của Home (Button_Play_Level): 4 lớp (Base, Front, Highlight, Outline) tô màu theo độ khó như Btn_Nomal / Btn_Hard /
    /// Btn_SuperHard của bản gốc, chữ độ khó dưới tên ở level khó; bấm thì vào màn, hết tim thì mở popup nạp tim [user].
    /// Thanh streak (CoreStreak, từ Level 23): mỗi trận thắng tô vàng một ô, ô mới tô lần lượt; đủ 5 thì cả 5 cam, x2 đỏ và nút cháy
    /// (StreakActive: mặt nút và đế mang hoa văn lửa, quầng lửa sau nút và sau thanh, viền cháy quanh nút) [user].
    /// </summary>
    public class PlayButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [Tooltip("Base, Front, Highlight, Outline.")]
        [SerializeField] private Image[] layers = new Image[4];
        [Tooltip("Màu 4 lớp: thường / khó / siêu khó.")]
        [SerializeField] private Color[] normal = new Color[4], hard = new Color[4], superHard = new Color[4];
        [SerializeField] private TMP_Text title;
        [Tooltip("\"Hard\" / \"Super Hard\" dưới tên, chỉ ở level khó.")]
        [SerializeField] private TMP_Text difficultyLabel;
        [Tooltip("Chữ tên và chữ độ khó: khó / siêu khó.")]
        [SerializeField] private Material hardTitle, superHardTitle, hardLabel, superHardLabel;
        [Tooltip("Level khó: tên lên cao 5.5 và nhỏ lại 68.8 / 80; Super Hard nhỏ 38.4 / 40.")]
        [SerializeField] private float hardTitleRaise = 5.5f, hardTitleScale = 68.8f / 80f, superHardLabelScale = 38.4f / 40f;

        [Header("Streak")]
        [SerializeField] private GameObject streak;
        [Tooltip("Filler_Yellow của 5 ô (tô ngang).")]
        [SerializeField] private Image[] yellow = new Image[5];
        [Tooltip("Filler_Orange của 5 ô (khi cháy).")]
        [SerializeField] private GameObject[] orange = new GameObject[5];
        [SerializeField] private RectTransform coin;
        [SerializeField] private TMP_Text multiplier;
        [SerializeField] private Color fireMultiplier = new Color(0.863f, 0.024f, 0f);
        [Tooltip("Mặt nút / đế khi cháy: thường và siêu khó.")]
        [SerializeField] private Material fireFront, fireFrontSuperHard, fireUnderlay, fireUnderlaySuperHard;
        [Tooltip("Streak_FX_Container (prefab FX_StreakPlayButton) và StreakGlow_Outline, tắt sẵn.")]
        [SerializeField] private GameObject fireFx, fireEdge;

        private const string StreakShownKey = "home_streak_shown"; // số trận thanh đã hiện lần trước, để tô các ô mới
        private bool _superHard;

        private static Core.UIManager UI => Core.Services.Get<Core.UIManager>();

        private void Awake() => button.onClick.AddListener(() =>
        {
            if (PlayerEconomy.Shared.CanPlay) LoadingScreen.Load(LoadingScreen.GameScene);
            else UI.Show<LivesPopup>(); // hết tim: popup nạp tim [user]
        });

        /// <summary>difficulty: 0-1 thường, 2-3 khó, từ 4 siêu khó (LevelData.difficulty).</summary>
        public void Setup(int level, int difficulty)
        {
            bool isHard = difficulty >= 2;
            _superHard = difficulty >= 4;
            var colours = _superHard ? superHard : isHard ? hard : normal;
            for (int i = 0; i < layers.Length; i++) layers[i].color = colours[i];
            title.text = "Level " + level;
            if (isHard)
            {
                title.fontSharedMaterial = _superHard ? superHardTitle : hardTitle;
                title.rectTransform.anchoredPosition += new Vector2(0f, hardTitleRaise);
                title.fontSize *= hardTitleScale;
                difficultyLabel.gameObject.SetActive(true);
                difficultyLabel.text = _superHard ? "Super Hard" : "Hard";
                difficultyLabel.fontSharedMaterial = _superHard ? superHardLabel : hardLabel;
                if (_superHard) difficultyLabel.fontSize *= superHardLabelScale;
            }
            bool shown = PlayerEconomy.StreakShown(level);
            streak.SetActive(shown);
            if (shown) Streak();
        }

        private void Streak()
        {
            var economy = PlayerEconomy.Shared;
            bool fire = economy.StreakOnFire;
            int wins = fire ? yellow.Length : economy.StreakWins;
            int shownWins = Mathf.Clamp(PlayerPrefs.GetInt(StreakShownKey, 0), 0, wins); // sau khi mất chuỗi thanh trống luôn
            for (int i = 0; i < yellow.Length; i++) { yellow[i].fillAmount = i < shownWins ? 1f : 0f; orange[i].SetActive(false); }
            PlayerPrefs.SetInt(StreakShownKey, wins);
            PlayerPrefs.Save();
            StartCoroutine(Fill(shownWins, wins, fire));
        }

        /// <summary>Fillbar_Progress: từng ô mới tô trái sang phải; Fillbar_Progress_Full: tất cả chuyển cam và nảy.</summary>
        private IEnumerator Fill(int from, int to, bool fire)
        {
            if (to > from) yield return new WaitForSeconds(0.4f);
            while (LoadingScreen.Busy) yield return null; // sau khi màn chờ tan
            for (int i = from; i < to; i++)
            {
                var step = yellow[i];
                yield return Tween.Run(0.3f, Tween.OutQuad, t => step.fillAmount = t);
                StartCoroutine(Tween.Punch(0.08f, t => step.transform.parent.localScale = Vector3.one * (1f + 0.2f * t)));
            }
            if (!fire) yield break;
            foreach (var step in orange) step.SetActive(true);
            multiplier.color = fireMultiplier;
            OnFire();
            if (to > from) yield return Tween.Punch(0.12f, t => coin.localScale = Vector3.one * (1f + 0.3f * t));
        }

        private void OnFire()
        {
            layers[1].material = _superHard ? fireFrontSuperHard : fireFront;
            layers[0].material = _superHard ? fireUnderlaySuperHard : fireUnderlay;
            fireFx.SetActive(true);
            fireEdge.SetActive(true);
        }
    }
}
