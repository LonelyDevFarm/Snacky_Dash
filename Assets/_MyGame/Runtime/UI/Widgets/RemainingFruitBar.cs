using SnackDash.Gameplay;
using SnackDash.LevelSystem;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Thanh đếm trái còn lại dưới thanh LEVEL (gốc RemainingFruitsView của Panel_InGame_HUD, bản mới; ảnh lv129 của user): mỗi loại
    /// trái có trong màn một ô <see cref="RemainingFruitCounter"/>, số = <see cref="GameSession.RemainingFruit"/>, cập nhật khi ăn,
    /// nam châm hút, hoàn tác. Nằm trong prefab HUD; HudView gọi <see cref="Bind"/>. Gốc bật bằng remote config, ở đây luôn bật [user].
    /// </summary>
    public class RemainingFruitBar : MonoBehaviour
    {
        [Tooltip("Một ô cho mỗi loại trái (đặt sẵn trong prefab, tắt sẵn); chỉ loại có trong màn được bật.")]
        [SerializeField] private RemainingFruitCounter[] counters;
        [SerializeField] private GridLayoutGroup grid;
        [Tooltip("Số cột tối đa của lưới ô; nhiều loại hơn thì xuống hàng (gốc DynamicGridColumns._maxColumns 6).")]
        [SerializeField] private int maxColumns = 6;
        [SerializeField] private FruitGraphicSet fruits;

        private GameSession _session;

        public void Bind(GameSession session)
        {
            _session = session;
            int shown = 0;
            foreach (var counter in counters)
            {
                bool inLevel = session.LevelHasFruit(counter.Kind);
                if (inLevel)
                {
                    shown++;
                    var entry = fruits != null ? fruits.Get(counter.Kind) : null;
                    if (entry != null) counter.SetIcon(entry.fieldSprite != null ? entry.fieldSprite : entry.sprite);
                    counter.SetCount(session.RemainingFruit(counter.Kind));
                }
                counter.gameObject.SetActive(inLevel);
            }
            grid.constraintCount = Mathf.Clamp(shown, 1, maxColumns);
            gameObject.SetActive(shown > 0);
            session.PillEaten += (cell, kind, segment) => Refresh(kind);
            session.PillMagnetized += (cell, kind, column, crate) => Refresh(kind);
            session.PillSpawned += (cell, kind) => Refresh(kind); // trái Snip thả lại bàn (hiếm)
            session.Rewound += RefreshAll;
        }

        private void Refresh(PillKind kind)
        {
            foreach (var counter in counters)
                if (counter.Kind == kind && counter.gameObject.activeSelf) counter.SetCount(_session.RemainingFruit(kind));
        }

        private void RefreshAll()
        {
            foreach (var counter in counters)
                if (counter.gameObject.activeSelf) counter.SetCount(_session.RemainingFruit(counter.Kind));
        }
    }
}
