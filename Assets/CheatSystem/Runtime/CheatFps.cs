using UnityEngine;

namespace CheatSystem
{
    /// <summary>
    /// The FPS counter of the System tab: frames per second over the last half second, top left inside the safe area,
    /// green from 55, yellow from 30, red below. Stays across scenes; remembered between runs (PlayerPrefs cheat_fps).
    /// </summary>
    public class CheatFps : MonoBehaviour
    {
        private const string Key = "cheat_fps";
        private const float Window = 0.5f;

        private static CheatFps _instance;

        public static bool Shown
        {
            get => _instance != null;
            set
            {
                PlayerPrefs.SetInt(Key, value ? 1 : 0);
                if (value == Shown) return;
                if (value)
                {
                    var go = new GameObject("CheatFps");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<CheatFps>();
                }
                else
                {
                    Destroy(_instance.gameObject);
                    _instance = null;
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Restore()
        {
            if (CheatManager.Enabled && PlayerPrefs.GetInt(Key, 0) != 0) Shown = true;
        }

        private int _frames;
        private float _time, _fps, _worstMs, _worstNow;
        private GUIStyle _style;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _frames++;
            _time += dt;
            _worstNow = Mathf.Max(_worstNow, dt * 1000f);
            if (_time < Window) return;
            _fps = _frames / _time;
            _worstMs = _worstNow;
            _frames = 0;
            _time = 0f;
            _worstNow = 0f;
        }

        private void OnGUI()
        {
            if (_style == null) _style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            _style.fontSize = Mathf.Max(14, Screen.height / 55);
            _style.normal.textColor = _fps >= 55f ? Color.green : _fps >= 30f ? Color.yellow : Color.red;
            var safe = Screen.safeArea;
            var rect = new Rect(safe.xMin + 12f, Screen.height - safe.yMax + 12f, _style.fontSize * 12f, _style.fontSize * 1.5f);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 8f, rect.y, rect.width, rect.height), $"{_fps:0} FPS  max {_worstMs:0} ms", _style);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Awake() => _instance = this;
    }
}
