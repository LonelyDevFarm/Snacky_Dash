using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Nút dùng chung (nút xanh, nút X…): đổi hình / chữ / cỡ ngay trong prefab của nút. Script popup giữ ô
    /// <c>[SerializeField] UiButton</c> và gắn việc bằng <see cref="Clicked"/>. Có nảy khi bấm (PressFeedback).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class UiButton : MonoBehaviour
    {
        [Tooltip("Component Button của nút (tự lấy nếu trống).")]
        [SerializeField] private Button button;
        [Tooltip("Chữ trên nút (để trống nếu nút chỉ có hình).")]
        [SerializeField] private TMP_Text label;

        /// <summary>Bấm nút.</summary>
        public event Action Clicked;

        public Button Button => button;

        /// <summary>Chữ trên nút.</summary>
        public string Label
        {
            get => label != null ? label.text : "";
            set { if (label != null) label.text = value; }
        }

        public bool Interactable { get => button.interactable; set => button.interactable = value; }

        private void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            button.onClick.AddListener(() => Clicked?.Invoke());
        }

        private void OnValidate()
        {
            if (button == null) button = GetComponent<Button>();
        }
    }
}
