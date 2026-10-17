using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Cần gạt cạnh vạch qua đường (prefab Prefabs/InGame/ldfs/visualoverhaul/SwitchUniversal): hiện View_Switch_ON hoặc
    /// View_Switch_OFF theo công tắc (gốc SwitchView._meshOn / _meshOff). BoardView gọi theo GameSession.SwitchToggled.
    /// </summary>
    public class SwitchLeverView : MonoBehaviour
    {
        [SerializeField] private GameObject meshOn, meshOff;

        public void Show(bool on)
        {
            if (meshOn != null) meshOn.SetActive(on);
            if (meshOff != null) meshOff.SetActive(!on);
        }
    }
}
