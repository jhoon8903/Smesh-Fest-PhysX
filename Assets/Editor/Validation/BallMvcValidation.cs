using System.IO;
using Framework.Pool;
using Framework.Test;
using InGame.Ball;
using UnityEditor;
using UnityEngine;

namespace Framework.EditorValidation
{
    public static class BallMvcValidation
    {
        [MenuItem("Tools/Smesh Fest/Validation/Ball MVC Runtime")]
        public static void Run()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("[BallMvcValidation] Enter Play Mode first.");
                return;
            }
            if (UnityEngine.Object.FindAnyObjectByType<BallMvcRuntimeProbe>() != null)
            {
                Debug.LogError("[BallMvcValidation] A probe is already running.");
                return;
            }

            GameObject fixtures = new GameObject("__BallMvcValidation_Fixtures");
            GameObject sourceRoot = new GameObject("Source");
            sourceRoot.transform.SetParent(fixtures.transform, false);
            sourceRoot.SetActive(false);
            GameObject source = new GameObject("Temporary_BallView_Source");
            source.transform.SetParent(sourceRoot.transform, false);
            source.AddComponent<Rigidbody>().useGravity = false;
            source.AddComponent<SphereCollider>();
            GroundFadeValidationFixture.Add(source, "Assets/Project/Prefabs/Ball.prefab", "Assets/Project/Materials/Ball_GroundFade.mat");
            BallView view = source.AddComponent<BallView>();

            PoolConfig config = ScriptableObject.CreateInstance<PoolConfig>();
            SerializedObject settings = new SerializedObject(config);
            settings.FindProperty("poolRootName").stringValue = "__BallMvcValidation_Pool";
            settings.FindProperty("prefab").objectReferenceValue = view;
            settings.FindProperty("minPool").intValue = 1;
            settings.FindProperty("maxPool").intValue = 1;
            settings.FindProperty("returnDelaySeconds").floatValue = 0f;
            settings.ApplyModifiedPropertiesWithoutUndo();

            PoolConfig hierarchyFirstConfig = ScriptableObject.CreateInstance<PoolConfig>();
            SerializedObject hierarchyFirstSettings = new SerializedObject(hierarchyFirstConfig);
            hierarchyFirstSettings.FindProperty("poolRootName").stringValue = "__BallMvcValidation_HierarchyFirstPool";
            hierarchyFirstSettings.FindProperty("prefab").objectReferenceValue = view;
            hierarchyFirstSettings.FindProperty("minPool").intValue = 1;
            hierarchyFirstSettings.FindProperty("maxPool").intValue = 1;
            hierarchyFirstSettings.FindProperty("returnDelaySeconds").floatValue = 0f;
            hierarchyFirstSettings.ApplyModifiedPropertiesWithoutUndo();

            PoolContainer container = fixtures.AddComponent<PoolContainer>();
            SerializedObject catalog = new SerializedObject(container);
            SerializedProperty configs = catalog.FindProperty("configs");
            configs.arraySize = 2;
            configs.GetArrayElementAtIndex(0).objectReferenceValue = config;
            configs.GetArrayElementAtIndex(1).objectReferenceValue = hierarchyFirstConfig;
            catalog.ApplyModifiedPropertiesWithoutUndo();

            GameObject host = new GameObject("__BallMvcValidation_Probe");
            BallMvcRuntimeProbe probe = host.AddComponent<BallMvcRuntimeProbe>();
            probe.Begin(fixtures, container, config, hierarchyFirstConfig,
                Path.Combine(Application.dataPath, "../Docs/Prototype/evidence/raw/ball-mvc-runtime-validation.json"));
        }
    }
}
