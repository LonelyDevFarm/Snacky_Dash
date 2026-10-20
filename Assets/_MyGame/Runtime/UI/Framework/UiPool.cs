using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Kho tái dùng ảnh UI sinh lặp lại (hạt sáng, pháo giấy, xu bay) từ một prefab mẫu (Prefabs/UI/Widgets/UiParticle hoặc
    /// FlyingCoin) [UNITY_WORK_RULES luật 5, 14: không dựng giao diện bằng code, thứ sinh liên tục lấy từ pool]. Lấy ra thì bật và
    /// trả về dáng mặc định của prefab (vị trí giữa, không xoay, cỡ 1); trả lại thì tắt, lần sau dùng lại, không tạo / huỷ liên tục.
    /// </summary>
    public sealed class UiPool
    {
        private readonly Image _prefab;
        private readonly Transform _parent;
        private readonly Stack<Image> _free = new Stack<Image>();

        public UiPool(Image prefab, Transform parent)
        {
            _prefab = prefab;
            _parent = parent;
        }

        public Image Get()
        {
            Image image = null;
            while (_free.Count > 0 && image == null) image = _free.Pop(); // ảnh có thể đã bị huỷ cùng cha
            if (image == null) image = Object.Instantiate(_prefab, _parent);
            var rect = image.rectTransform;
            rect.SetAsLastSibling();
            rect.anchoredPosition = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            image.gameObject.SetActive(true);
            return image;
        }

        public void Release(Image image)
        {
            if (image == null) return;
            image.gameObject.SetActive(false);
            _free.Push(image);
        }
    }
}
