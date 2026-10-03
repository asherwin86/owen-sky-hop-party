using UnityEngine;
using UnityEngine.EventSystems;

namespace SkyHop
{
    /// <summary>Builds the whole game at runtime so the scene file can stay as it is.</summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            // clear the template's camera and light; we make our own
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.GetComponent<Camera>() != null || root.GetComponent<Light>() != null) Object.Destroy(root);
            new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            GameFlow.Create();
#if UNITY_EDITOR
            int at = PlayerPrefs.GetInt("hop_autotest", 0);
            PlayerPrefs.SetInt("hop_autotest", 0);
            if (at == 1) new GameObject("AutoTest").AddComponent<AutoTest>();
#endif
        }
    }
}
