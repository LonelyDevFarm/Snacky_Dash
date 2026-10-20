using UnityEngine;

namespace SnackDash.Audio
{
    /// <summary>
    /// Rung ngắn khi công tắc Rung bật (GameSettings). Bản gốc dùng Nice Vibrations có giới hạn nhịp (HapticService,
    /// HapticThrottleConfigurator); ở đây dùng bộ rung Android có độ mạnh (VibrationEffect, Android 8+; trước đó rung ngắn
    /// thường) và tối đa một lần mỗi <see cref="MinInterval"/>, nên ăn một hàng trái hay giao liền mạch sẽ "tích tích" chứ
    /// không rung rền. iOS: Handheld.Vibrate. Trong Editor không rung.
    /// </summary>
    public static class Haptics
    {
        private const float MinInterval = 0.05f;
        private static float _last = -1f;

        /// <summary>A fruit delivered, a progress step.</summary>
        public static void Tick() => Vibrate(12, 60);
        /// <summary>A fruit eaten.</summary>
        public static void Selection() => Vibrate(15, 90);
        /// <summary>A stop, a button.</summary>
        public static void Light() => Vibrate(20, 110);
        public static void Medium() => Vibrate(35, 170);
        public static void Heavy() => Vibrate(60, 255);

        private static void Vibrate(long milliseconds, int amplitude)
        {
            if (!GameSettings.Haptics) return;
            float now = Time.unscaledTime;
            if (now - _last < MinInterval) return;
            _last = now;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    if (version.GetStatic<int>("SDK_INT") >= 26)
                    {
                        using (var effects = new AndroidJavaClass("android.os.VibrationEffect"))
                        using (var effect = effects.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, amplitude))
                            vibrator.Call("vibrate", effect);
                    }
                    else vibrator.Call("vibrate", milliseconds);
                }
            }
            catch { Handheld.Vibrate(); } // also makes Unity add the VIBRATE permission
#elif UNITY_IOS && !UNITY_EDITOR
            if (amplitude >= 170) Handheld.Vibrate(); // the only iOS buzz is long: kept for the strong ones
#endif
        }
    }
}
