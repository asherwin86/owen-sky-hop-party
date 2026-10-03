using UnityEditor;
using UnityEngine;

namespace SkyHop.EditorTools
{
    public static class HopMenu
    {
        [MenuItem("Sky Hop/Bot plays the course")]
        public static void BotRun()
        {
            PlayerPrefs.SetInt("hop_autotest", 1);
            PlayerPrefs.Save();
            EditorApplication.isPlaying = true;
        }
    }
}
