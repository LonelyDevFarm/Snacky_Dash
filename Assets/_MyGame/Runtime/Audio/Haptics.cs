using UnityEngine;

namespace SnackDash.Audio
{
    /// <summary>
    /// Short vibrations when the Haptics switch is on (GameSettings). Android: the system vibrator for a
    /// few milliseconds (Handheld.Vibrate is a long buzz and only the fallback); iOS: Handheld.Vibrate.
    /// Nothing in the editor.
    /// </summary>
    public static class Haptics
    {
        public static void Light() => Vibrate(15);
        public static void Medium() => Vibrate(35);
        public static void Heavy() => Vibrate(70);

        private static void Vibrate(long milliseconds)
        {
            if (!GameSettings.Haptics) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                    vibrator.Call("vibrate", milliseconds);
            }
            catch { Handheld.Vibrate(); } // also makes Unity add the VIBRATE permission
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }
    }
}
