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
