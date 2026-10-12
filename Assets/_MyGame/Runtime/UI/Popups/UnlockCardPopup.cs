using System.Collections;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Thẻ "NEW ITEM!" / "NEW BOOSTER!" lúc vào màn có cơ chế hoặc booster mới (prefab <c>Prefabs/UI/Popups/UnlockCardPopup</c>, gốc:
    /// LdfTutorialPopup / BoosterTutorialPopup). Video (5 fps): màn tối đi, tiêu đề bay vào từ trên phải, loé tím, thẻ bật ra nghiêng
    /// rồi thẳng lại, tia sáng quay sau thẻ, rồi tên trên dải và bóng nói; khoảng 2 giây sau "TAP TO CONTINUE!" và chạm thì đóng.
    /// Mở bằng <c>UIManager.Show&lt;UnlockCardPopup&gt;()</c> rồi <see cref="Setup"/>.
    /// </summary>
    public class UnlockCardPopup : BasePopup
    {
        [SerializeField] private Image background;
        [SerializeField] private Button tapArea;
        [SerializeField] private RectTransform title;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private RectTransform card;
        [SerializeField] private Image cardFrame;
        [SerializeField] private Image icon;
        [SerializeField] private RectTransform ribbon;
        [SerializeField] private RectTransform nameRect;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private RectTransform bubble;
        [SerializeField] private TMP_Text description;
        [SerializeField] private CanvasGroup rays;
        [SerializeField] private RawImage flash;
        [SerializeField] private Transform sparkles;
        [SerializeField] private RectTransform tap;
        [Header("Thẻ cơ chế / thẻ booster")]
        [SerializeField] private string itemTitle = "NEW ITEM!";
        [SerializeField] private string boosterTitle = "NEW BOOSTER!";
        [SerializeField] private Sprite frameWhite, frameGold;
        [Tooltip("Hình cơ chế (theo UnlockPopups.Mechanics) và booster (xẻng, boomerang, undo, nam châm).")]
        [SerializeField] private Sprite[] mechanicIcons = new Sprite[0];
        [SerializeField] private Sprite[] boosterIcons = new Sprite[4];
        [Tooltip("TAP TO CONTINUE! khi thẻ đã vào và yên (khoảng 1.6 s).")]
        [SerializeField] private float tapDelay = 2f;

        private bool _canClose;

        protected override void OnInit() => tapArea.onClick.AddListener(() => { if (_canClose) Close(); });

        public void Setup(UnlockPopups.Entry entry)
        {
            titleText.text = entry.booster ? boosterTitle : itemTitle;
            cardFrame.sprite = entry.booster ? frameGold : frameWhite;
            icon.sprite = entry.booster ? boosterIcons[Mathf.Clamp(entry.icon, 0, boosterIcons.Length - 1)] : entry.icon < mechanicIcons.Length ? mechanicIcons[entry.icon] : null;
            nameText.text = entry.name + "!";
            description.text = entry.description;
            tap.gameObject.SetActive(false);
            StartCoroutine(Animate());
            StartCoroutine(ShowTap());
        }

        private IEnumerator ShowTap()
        {
            yield return new WaitForSeconds(tapDelay);
            tap.gameObject.SetActive(true);
            _canClose = true;
            yield return TutorialView.Bounce(tap);
        }

        private IEnumerator Animate()
        {
            float alpha = background.color.a;
            var color = background.color;
            var flashRect = flash.rectTransform;
            title.localScale = card.localScale = ribbon.localScale = bubble.localScale = flashRect.localScale = nameRect.localScale = Vector3.zero;
            rays.alpha = 0f;
            sparkles.gameObject.SetActive(false);
            StartCoroutine(Spin());

            yield return Tween.Run(0.2f, Tween.Linear, t => background.color = new Color(color.r, color.g, color.b, alpha * t));
            var titleEnd = title.anchoredPosition;
            var titleStart = titleEnd + new Vector2(260f, 80f);
            yield return Tween.Run(0.3f, Tween.OutBack(2f), t =>
            {
                title.anchoredPosition = Vector2.LerpUnclamped(titleStart, titleEnd, t);
                title.localScale = Vector3.one * t;
                title.localRotation = Quaternion.Euler(0f, 0f, 12f * (1f - t));
            });
            StartCoroutine(Tween.Run(0.4f, Tween.OutQuad, t =>
            {
                flashRect.localScale = Vector3.one * (0.2f + 2f * t);
                flash.color = new Color(flash.color.r, flash.color.g, flash.color.b, 1f - t);
            }));
            yield return new WaitForSeconds(0.1f);
            yield return Tween.Run(0.35f, Tween.OutBack(2.5f), t =>
            {
                card.localScale = ribbon.localScale = Vector3.one * t;
                card.localRotation = Quaternion.Euler(0f, 0f, -15f * (1f - t));
            });
            sparkles.gameObject.SetActive(true);
            StartCoroutine(Twinkle());
            StartCoroutine(Tween.Run(0.4f, Tween.Linear, t => rays.alpha = t));
            yield return new WaitForSeconds(0.3f);
            yield return Tween.Run(0.25f, Tween.OutBack(3f), t => nameRect.localScale = Vector3.one * t);
            yield return Tween.Run(0.25f, Tween.OutBack(2f), t => bubble.localScale = Vector3.one * t);
        }

        private IEnumerator Spin()
        {
            while (true)
            {
                rays.transform.Rotate(0f, 0f, -18f * Time.deltaTime);
                yield return null;
            }
        }

        private IEnumerator Twinkle()
        {
            float time = 0f;
            while (true)
            {
                time += Time.deltaTime;
                for (int i = 0; i < sparkles.childCount; i++)
                    sparkles.GetChild(i).localScale = Vector3.one * Mathf.Abs(Mathf.Sin(time * 2.2f + i * 1.3f));
                yield return null;
            }
        }
    }
}
