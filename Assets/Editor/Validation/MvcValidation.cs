using System.IO;
using Framework.Pool;
using Framework.Test;
using UnityEditor;
using UnityEngine;

namespace Framework.EditorValidation
{
    public static class MvcValidation
    {
        [MenuItem("Tools/Smesh Fest/Validation/MVC Runtime")]
        public static void Run()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("[MvcValidation] Enter Play Mode first.");
                return;
            }
            if (UnityEngine.Object.FindAnyObjectByType<MvcRuntimeProbe>() != null)
            {
                Debug.LogError("[MvcValidation] A probe is already running.");
                return;
            }

            GameObject fixtures = new GameObject("__MvcValidation_Fixtures");
            GameObject sourceRoot = new GameObject("Source");
            sourceRoot.transform.SetParent(fixtures.transform, false);
            sourceRoot.SetActive(false);
            PoolConfig[] configs = new PoolConfig[2];
            for (int i = 0; i < configs.Length; i++)
            {
                GameObject source = new GameObject("Source_" + i);
                source.transform.SetParent(sourceRoot.transform, false);
                MvcRuntimeProbe.PooledView view = source.AddComponent<MvcRuntimeProbe.PooledView>();
                PoolConfig config = ScriptableObject.CreateInstance<PoolConfig>();
                SerializedObject settings = new SerializedObject(config);
                settings.FindProperty("poolRootName").stringValue = "__MvcValidation_Pool_" + i;
                settings.FindProperty("prefab").objectReferenceValue = view;
                settings.FindProperty("minPool").intValue = 1;
                settings.FindProperty("maxPool").intValue = 1;
                settings.FindProperty("returnDelaySeconds").floatValue = 0;
                settings.ApplyModifiedPropertiesWithoutUndo();
                configs[i] = config;
            }

            PoolContainer container = fixtures.AddComponent<PoolContainer>();
            SerializedObject catalog = new SerializedObject(container);
            SerializedProperty entries = catalog.FindProperty("configs");
            entries.arraySize = configs.Length;
            for (int i = 0; i < configs.Length; i++) entries.GetArrayElementAtIndex(i).objectReferenceValue = configs[i];
            catalog.ApplyModifiedPropertiesWithoutUndo();
            GameObject host = new GameObject("__MvcValidation_Probe");
            MvcRuntimeProbe probe = host.AddComponent<MvcRuntimeProbe>();
            probe.Begin(fixtures, container, configs,
                Path.Combine(Application.dataPath, "../Docs/Prototype/evidence/raw/mvc-runtime-validation.json"));
        }
    }
}
