using UnityEngine;

namespace SnackDash.Core
{
    /// <summary>
    /// Object "[Managers]" trên cùng Hierarchy của scene Home và Game: giữ các Manager (kéo thả trong Inspector) và khởi động
    /// chúng theo thứ tự Save → Game → Economy → Event → Level → Audio → UI. Bản của scene mở đầu sống suốt game, bản ở scene sau
    /// tự xoá vì đã có. Mỗi scene có bản riêng: đổi thông số (FPS, danh sách màn, âm thanh) thì sửa ở cả hai scene.
    /// Scene mới phải chép "[Managers]" từ scene Home sang (thiếu thì Console báo lỗi khi Play). Chạy trước mọi script khác.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class ManagersRoot : MonoBehaviour
    {
        [SerializeField] private SaveManager save;
        [SerializeField] private GameManager game;
        [SerializeField] private EconomyManager economy;
        [SerializeField] private EventManager events;
        [SerializeField] private LevelManager level;
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private UIManager ui;

        private static ManagersRoot _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        /// <summary>Bản đặt trong scene: bản đầu tiên làm Manager cho cả game, bản ở scene sau tự xoá.</summary>
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Become(this);
        }

        /// <summary>Scene mở đầu không có [Managers]: báo ngay, không để lỗi âm thầm.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CheckPresent()
        {
            if (_instance == null)
                Debug.LogError("[Managers] Scene này thiếu object [Managers] — hãy chép [Managers] từ scene Home sang (Copy / Paste) rồi lưu scene.");
        }

        private static void Become(ManagersRoot root)
        {
            _instance = root;
            root.name = "[Managers]";
            root.transform.SetParent(null);
            DontDestroyOnLoad(root.gameObject);
            root.InitAll();
        }

        private void InitAll()
        {
            foreach (ManagerBase manager in new ManagerBase[] { save, game, economy, events, level, audioManager, ui })
            {
                if (manager == null) { Debug.LogError("[Managers] Một ô Manager trên [Managers] đang trống (kéo component tương ứng vào).", this); continue; }
                manager.Init();
            }
        }

        private void OnValidate()
        {
            if (save == null) save = GetComponent<SaveManager>();
            if (game == null) game = GetComponent<GameManager>();
            if (economy == null) economy = GetComponent<EconomyManager>();
            if (events == null) events = GetComponent<EventManager>();
            if (level == null) level = GetComponent<LevelManager>();
            if (audioManager == null) audioManager = GetComponent<AudioManager>();
            if (ui == null) ui = GetComponent<UIManager>();
        }
    }
}
