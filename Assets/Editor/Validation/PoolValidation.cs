using System.IO;
using Framework.Pool;
using Framework.Test;
using InGame.DI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace Framework.EditorValidation
{
    public static class PoolValidation
    {
        [MenuItem("Tools/Smesh Fest/Validation/Pool Runtime")]
        public static void Run()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("[PoolValidation] Enter Play Mode first; this menu does not start Play Mode.");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            GameLifetimeScope[] scopes = UnityEngine.Object.FindObjectsByType<GameLifetimeScope>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            GameLifetimeScope scope = null;
            int matchingScopes = 0;
            for (int i = 0; i < scopes.Length; i++)
            {
                if (scopes[i].gameObject.scene != activeScene) continue;
                scope = scopes[i];
                matchingScopes++;
            }
            if (matchingScopes != 1 || scope == null || scope.Container == null)
            {
                Debug.LogError("[PoolValidation] Active scene must contain one built GameLifetimeScope; found " +
                               matchingScopes + ".");
                return;
            }
            PoolFactory scopedFactory = scope.Container.Resolve<PoolFactory>();

            GameObject sourceParent = new GameObject("__PoolValidation_SourceParent");
            sourceParent.SetActive(false);
            GameObject prefabObject = new GameObject("__PoolValidation_Prefab");
            prefabObject.transform.SetParent(sourceParent.transform, false);
            PoolRuntimeProbe.ProbePoolable prefab = prefabObject.AddComponent<PoolRuntimeProbe.ProbePoolable>();
            prefabObject.AddComponent<PoolRuntimeProbe.CleanupPart>();
            prefabObject.AddComponent<ParticleSystem>();
            PoolConfig config = CreateConfig(prefab, 1, 2, 0.05f);
            PoolConfig maintenanceConfig = CreateConfig(prefab, 1, 2, 0.05f);
            GameObject containerObject = new GameObject("__PoolValidation_Container");
            GameObject maintenanceContainerObject = new GameObject("__PoolValidation_MaintenanceContainer");
            PoolContainer container = containerObject.AddComponent<PoolContainer>();
            PoolContainer maintenanceContainer = maintenanceContainerObject.AddComponent<PoolContainer>();
            SetConfigs(container, config);
            SetConfigs(maintenanceContainer, maintenanceConfig);

            GameObject probeObject = new GameObject("__PoolValidation_Probe");
            PoolRuntimeProbe probe = probeObject.AddComponent<PoolRuntimeProbe>();
            string output = Path.Combine(Application.dataPath, "../Docs/Prototype/evidence/raw/pool-runtime-validation.json");
            probe.Begin(container, config, maintenanceContainer, maintenanceConfig, scopedFactory, output);
        }

        private static PoolConfig CreateConfig(MonoBehaviour prefab, int min, int max, float delay)
        {
            PoolConfig config = ScriptableObject.CreateInstance<PoolConfig>();
            SerializedObject serialized = new SerializedObject(config);
            serialized.FindProperty("poolRootName").stringValue = "__PoolValidation_Root";
            serialized.FindProperty("prefab").objectReferenceValue = prefab;
            serialized.FindProperty("minPool").intValue = min;
            serialized.FindProperty("maxPool").intValue = max;
            serialized.FindProperty("returnDelaySeconds").floatValue = delay;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        private static void SetConfigs(PoolContainer container, PoolConfig config)
        {
            SerializedObject serialized = new SerializedObject(container);
            SerializedProperty configs = serialized.FindProperty("configs");
            configs.arraySize = 1;
            configs.GetArrayElementAtIndex(0).objectReferenceValue = config;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
