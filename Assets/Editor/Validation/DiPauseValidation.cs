using System.IO;
using Framework.Test;
using InGame.DI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Framework.EditorValidation
{
    public static class DiPauseValidation
    {
        [MenuItem("Tools/Smesh Fest/Validation/DI Pause Runtime")]
        public static void Run()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("[DiPauseValidation] Enter Play Mode first; this menu does not start Play Mode.");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            GameLifetimeScope[] allScopes = UnityEngine.Object.FindObjectsByType<GameLifetimeScope>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            GameLifetimeScope scope = null;
            int matchingScopes = 0;
            for (int i = 0; i < allScopes.Length; i++)
            {
                if (allScopes[i].gameObject.scene != activeScene) continue;
                scope = allScopes[i];
                matchingScopes++;
            }

            if (matchingScopes != 1)
            {
                Debug.LogError("[DiPauseValidation] Active scene must contain exactly one GameLifetimeScope; found " + matchingScopes + ".");
                return;
            }

            GameObject probeObject = new GameObject("__DiPauseValidation_Probe");
            DiPauseProbe probe = probeObject.AddComponent<DiPauseProbe>();
            string outputPath = Path.Combine(Application.dataPath,
                "../Docs/Prototype/evidence/raw/di-pause-playmode.json");
            probe.Begin(scope, outputPath);
        }
    }
}
