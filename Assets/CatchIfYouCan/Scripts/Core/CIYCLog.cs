using UnityEngine;

namespace CatchIfYouCan.Core
{
    public static class CIYCLog
    {
        public static bool Enabled = true;
#if !DEVELOPMENT_BUILD && !UNITY_EDITOR
        public static bool Detailed = false;
#else
        public static bool Detailed = true;
#endif

        public static void Info(string msg)
        {
            if (Enabled) Debug.Log($"[CIYC] {msg}");
        }

        public static void Warn(string msg)
        {
            if (Enabled) Debug.LogWarning($"[CIYC] {msg}");
        }

        public static void Error(string msg)
        {
            Debug.LogError($"[CIYC] {msg}");
        }

        public static void Detail(string msg)
        {
            if (Enabled && Detailed) Debug.Log($"[CIYC:D] {msg}");
        }
    }
}
