using UnityEditor;
using UnityEngine;

namespace SkyHop.EditorTools
{
    public static class HopMenu
    {
        [MenuItem("Sky Hop/Bot plays the course")]
        public static void BotRun()
        {
            PlayerPrefs.SetInt("hop_autocourse", 0);
            PlayerPrefs.SetInt("hop_autotest", 1);
            PlayerPrefs.Save();
            EditorApplication.isPlaying = true;
        }


        private static void BotCourse(int id)
        {
            PlayerPrefs.SetInt("hop_autocourse", id);
            PlayerPrefs.SetInt("hop_autotest", 1);
            PlayerPrefs.Save();
            EditorApplication.isPlaying = true;
        }
        [MenuItem("Sky Hop/Bot plays course 2 (Candy Canyon)")] public static void Bot1() { BotCourse(1); }
        [MenuItem("Sky Hop/Bot plays course 3 (Frosty Peaks)")] public static void Bot2() { BotCourse(2); }
        [MenuItem("Sky Hop/Bot plays course 4 (Twilight Spire)")] public static void Bot3() { BotCourse(3); }

        [MenuItem("Sky Hop/Build Windows (WinBuild)")]
        public static void BuildWindows()
        {
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                locationPathName = "WinBuild/SkyHopParty.exe",
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            var rep = BuildPipeline.BuildPlayer(opts);
            Debug.Log("[winbuild] " + rep.summary.result + " size=" + rep.summary.totalSize);
        }
    }
}
