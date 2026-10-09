using System;
using System.Collections;
using System.Collections.Generic;
using SnackDash.Economy;
using SnackDash.Presentation;
using UnityEngine;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;

namespace SnackDash.UI
{
    /// <summary>
    /// The chest of a weekly milestone, from the original ChestRewardPopupView and video sk1 (33-41 s): the screen
    /// darkens under the WEEKLY ADVENTURE / REWARDS title, the dashed path appears, the chest leaves its marker on
    /// the bar and glides down the path growing to the middle, shakes and bursts open in rays, its rewards pop out
    /// in a row (170 each, 50 apart) and TAP TO CLAIM; a tap sends them off and closes. The rewards were already
    /// given by <see cref="WeeklyMissions.Claim"/>.
    /// </summary>
    public class WeeklyChestReward : MonoBehaviour
    {
        private bool _tapped;

        public static void Open(RectTransform parent, UiSkin skin, WeeklyMissionsPopup popup, int milestone, Vector3 from, Action done)
        {
            var root = Stretch(parent, "ChestRewardPopupView");
            var view = root.gameObject.AddComponent<WeeklyChestReward>();
            Action<RectTransform> title = header => { WeeklyMissionsPopup.Title(header, skin); WeeklyMissionsPopup.Ribbon(header, skin, "Rewards"); };
            view.StartCoroutine(view.Play(root, skin, title, popup.ChestSprite(milestone), popup.Extras(WeeklyMissionsData.Milestones[milestone].reward, "x"), from, done, true));
        }

        /// <summary>The same screen for another event's chest (Snacky Pass): its own title, no path; the chest grows from where it was.</summary>
        public static void OpenChest(RectTransform parent, UiSkin skin, Action<RectTransform> title, Sprite chest, List<(Sprite icon, string amount)> rewards, Vector3 from, Action done)
        {
            var root = Stretch(parent, "ChestRewardPopupView");
            var view = root.gameObject.AddComponent<WeeklyChestReward>();
            view.StartCoroutine(view.Play(root, skin, title, chest, rewards, from, done, false));
        }

        private IEnumerator Play(RectTransform root, UiSkin skin, Action<RectTransform> title, Sprite chestSprite, List<(Sprite icon, string amount)> rewards, Vector3 from, Action done, bool withPath)
        {
            var cover = Image(Stretch(root, "Background"), null, new Color(0f, 0f, 0f, 0f), raycast: true);
            cover.gameObject.AddComponent<Button>().onClick.AddListener(() => _tapped = true);

            var header = Rect(root, "Header", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(881.13f, 144.3f));
            header.localScale = Vector3.one * 0.933f;
            title(header);
            var headerGroup = header.gameObject.AddComponent<CanvasGroup>();

            // The dashed path (S_Path_0, filled) and the target ring at its end.
            var path = Rect(root, "SP_Path", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-55f, 221f), new Vector2(720f, 575f));
            var dashes = Image(path, skin.Get("S_Path_0"), new Color(1f, 1f, 1f, 0.6f));
            dashes.type = UnityEngine.UI.Image.Type.Filled;
            dashes.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical;
            dashes.fillOrigin = (int)UnityEngine.UI.Image.OriginVertical.Top;
            dashes.fillAmount = 0f;
            var target = Image(Rect(root, "Sp_Target", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -161.4f), new Vector2(150f, 150f)), skin.Get("S_Weekly_Target_0"), new Color(1f, 1f, 1f, 0f));
            path.gameObject.SetActive(withPath);
            target.gameObject.SetActive(withPath);

            var rays = Rect(root, "Chest Back Rays", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -140.6f), new Vector2(700f, 700f));
            var raysImage = Image(rays, skin.Get("GlowRays"), new Color(1f, 0.8f, 0.3f, 0f));
            var chest = Rect(root, "S_Chest", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 500f));
            var chestImage = Image(chest, chestSprite, Color.white);
            chestImage.preserveAspect = true;
            chest.position = from;
            chest.localScale = Vector3.one * 0.2f;

            // Darken, title and path.
            yield return Tween.Run(0.3f, Tween.Linear, t =>
            {
                cover.color = new Color(0f, 0f, 0f, 0.97f * t);
                headerGroup.alpha = t;
                target.color = new Color(1f, 1f, 1f, t);
                dashes.fillAmount = t;
            }, true);

            // Down the path to the middle, growing.
            Vector3 start = chest.localPosition, end = new Vector3(0f, -127f, 0f);
            Vector3 control = new Vector3(start.x - 260f, (start.y + end.y) * 0.5f + 120f, 0f);
            yield return Tween.Run(0.9f, Tween.InOutCubic, t =>
            {
                float u = 1f - t;
                chest.localPosition = u * u * start + 2f * u * t * control + t * t * end;
                chest.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, t);
                dashes.color = new Color(1f, 1f, 1f, 0.6f * (1f - t));
                target.color = new Color(1f, 1f, 1f, 1f - t);
            }, true);

            // Shake, then open in rays.
            SnackDash.Audio.Haptics.Light();
            yield return Tween.Run(0.45f, Tween.Linear, t =>
            {
                chest.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 8f) * 6f * (1f - t));
                chest.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(t * Mathf.PI * 4f));
            }, true);
            SnackDash.Audio.Haptics.Medium();
            SnackDash.Audio.SoundPlayer.Play("CollectCoin");
            var items = new RectTransform[rewards.Count];
            for (int i = 0; i < items.Length; i++)
            {
                // One row of up to four; more go in rows of three (a Snacky Pass chest holds up to six).
                int perRow = items.Length <= 4 ? items.Length : 3, rowIndex = i / perRow, inRow = Mathf.Min(perRow, items.Length - rowIndex * perRow);
                float x = (i % perRow - (inRow - 1) * 0.5f) * 220f;
                float y = items.Length <= 4 ? 0f : 130f - rowIndex * 260f;
                items[i] = Rect(root, "RewardView " + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(170f, 170f));
                Image(Stretch(items[i], "Icon"), rewards[i].icon, Color.white).preserveAspect = true;
                Text(Rect(items[i], "Amount", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, -20f), new Vector2(0f, 80f)), rewards[i].amount.ToUpperInvariant(), skin.font, skin.EventMaterial("LuckiestGuy-Regular Brown Cartoon"), 60f, Color.white);
                items[i].localScale = Vector3.zero;
            }
            yield return Tween.Run(0.45f, Tween.Linear, t =>
            {
                chest.localScale = Vector3.one * Mathf.Lerp(1f, 1.4f, t);
                chestImage.color = new Color(1f, 1f, 1f, 1f - t);
                raysImage.color = new Color(1f, 0.8f, 0.3f, 0.8f * Mathf.Sin(t * Mathf.PI * 0.5f));
                rays.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, Tween.OutQuad(t));
                for (int i = 0; i < items.Length; i++)
                {
                    float k = Mathf.Clamp01((t - i * 0.15f) / 0.6f);
                    items[i].localScale = Vector3.one * Tween.OutBack(1.7f)(k);
                }
            }, true);

            var tap = Text(Rect(root, "TAP TO CLAIM", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 232f), new Vector2(0f, 300f)), "TAP TO CLAIM", skin.font, skin.plainMaterial, 68f, Color.white);
            _tapped = false;
            float spin = 0f;
            while (!_tapped)
            {
                spin += Time.unscaledDeltaTime * 20f;
                rays.localRotation = Quaternion.Euler(0f, 0f, spin);
                tap.alpha = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f);
                yield return null;
            }

            // Off they go, and the screen clears.
            Destroy(tap.gameObject);
            Vector3[] starts = new Vector3[items.Length];
            for (int i = 0; i < items.Length; i++) starts[i] = items[i].localPosition;
            yield return Tween.Run(0.4f, Tween.Linear, t =>
            {
                float e = Tween.InOutCubic(t);
                for (int i = 0; i < items.Length; i++)
                {
                    items[i].localPosition = Vector3.Lerp(starts[i], starts[i] + Vector3.up * 700f, e);
                    items[i].localScale = Vector3.one * (1f - e);
                }
                cover.color = new Color(0f, 0f, 0f, 0.97f * (1f - t));
                headerGroup.alpha = 1f - t;
                raysImage.color = new Color(1f, 0.8f, 0.3f, 0.8f * (1f - t));
            }, true);
            Destroy(gameObject);
            done();
        }
    }
}
