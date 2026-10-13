using System;
using UnityEngine;

namespace SnackDash.UI
{
    /// <summary>
    /// GAME OVER (prefab <c>Prefabs/UI/Popups/GameOver</c>, gốc: LoseScreenFailedPopup): trái tim vỡ, "LEVEL FAILED!", TRY AGAIN chơi lại màn,
    /// nút X đỏ góc trên phải về Home (Cancel Button của bản gốc, video Man_Lose). Trước Level 6 chưa có Home thì ẩn X.
    /// </summary>
    public class GameOverPopup : BasePopup
    {
        [SerializeField] private UiButton tryAgainButton;
        [Tooltip("Nút X đỏ: về Home.")]
        [SerializeField] private UiButton homeButton;

        public event Action TryAgain;
        public event Action Home;

        protected override void OnInit()
        {
            tryAgainButton.Clicked += () => TryAgain?.Invoke();
            homeButton.Clicked += () => Home?.Invoke();
        }

        /// <param name="canGoHome">False trước Level 6 (chưa có Home): ẩn X.</param>
        public void Setup(bool canGoHome) => homeButton.gameObject.SetActive(canGoHome);
    }
}
