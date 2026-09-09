using System.IO;
using Framework.Pool;
using Framework.Test;
using InGame.Obstacle;
using UnityEditor;
using UnityEngine;

namespace Framework.EditorValidation
{
    public static class ObstacleMvcValidation
    {
        [MenuItem("Tools/Smesh Fest/Validation/Obstacle MVC Runtime")]
        public static void Run()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("[ObstacleMvcValidation] Enter Play Mode first.");
                return;
            }
            if (UnityEngine.Object.FindAnyObjectByType<ObstacleMvcRuntimeProbe>() != null)
            {
                Debug.LogError("[ObstacleMvcValidation] A probe is already running.");
                return;
            }

            GameObject fixtures = new GameObject("__ObstacleMvcValidation_Fixtures");
            GameObject sourceRoot = new GameObject("Source");
            sourceRoot.transform.SetParent(fixtures.transform, false);
            sourceRoot.SetActive(false);
            GameObject source = new GameObject("Temporary_ObstacleView_Source");
            source.transform.SetParent(sourceRoot.transform, false);
            source.AddComponent<Rigidbody>().useGravity = false;
            source.AddComponent<BoxCollider>();
            ObstacleView view = source.AddComponent<ObstacleView>();

            PoolConfig config = CreateConfig("__ObstacleMvcValidation_Pool", view);
            PoolConfig hierarchyFirstConfig = CreateConfig("__ObstacleMvcValidation_HierarchyFirstPool", view);

            PoolContainer container = fixtures.AddComponent<PoolContainer>();
            SerializedObject catalog = new SerializedObject(container);
            SerializedProperty configs = catalog.FindProperty("configs");
            configs.arraySize = 2;
            configs.GetArrayElementAtIndex(0).objectReferenceValue = config;
            configs.GetArrayElementAtIndex(1).objectReferenceValue = hierarchyFirstConfig;
            catalog.ApplyModifiedPropertiesWithoutUndo();

            GameObject host = new GameObject("__ObstacleMvcValidation_Probe");
            ObstacleMvcRuntimeProbe probe = host.AddComponent<ObstacleMvcRuntimeProbe>();
            probe.Begin(fixtures, container, config, hierarchyFirstConfig,
                Path.Combine(Application.dataPath, "../Docs/Prototype/evidence/raw/obstacle-mvc-runtime-validation.json"));
        }

        private static PoolConfig CreateConfig(string rootName, ObstacleView view)
        {
            PoolConfig config = ScriptableObject.CreateInstance<PoolConfig>();
            SerializedObject settings = new SerializedObject(config);
            settings.FindProperty("poolRootName").stringValue = rootName;
            settings.FindProperty("prefab").objectReferenceValue = view;
            settings.FindProperty("minPool").intValue = 1;
            settings.FindProperty("maxPool").intValue = 1;
            settings.FindProperty("returnDelaySeconds").floatValue = 0f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }
    }
}
