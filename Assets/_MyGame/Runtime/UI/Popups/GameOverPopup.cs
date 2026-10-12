using System;
using UnityEngine;

namespace SnackDash.UI
{
    /// <summary>GAME OVER (prefab <c>Prefabs/UI/Popups/GameOver</c>, gốc: LoseScreenFailedPopup): trái tim vỡ, "LEVEL FAILED!", TRY AGAIN chơi lại màn.</summary>
    public class GameOverPopup : BasePopup
    {
        [SerializeField] private UiButton tryAgainButton;

        public event Action TryAgain;

        protected override void OnInit() => tryAgainButton.Clicked += () => TryAgain?.Invoke();
    }
}
