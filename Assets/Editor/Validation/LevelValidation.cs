using System;
using System.Collections;
using System.Reflection;
using Editor.Level;
using Framework.Pool;
using InGame.Level;
using InGame.Obstacle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace Framework.EditorValidation
{
    /// <summary>Disposable Edit Mode checks for LevelConfig, LevelSpawner, and Level Editor capture safety.</summary>
    public static class LevelValidation
    {
        [MenuItem("Tools/Smesh Fest/Validation/Level Editor and Spawn")]
        public static void Run()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[LevelValidation] Leave Play Mode before running this Edit Mode-only validation.");
                return;
            }

            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject fixtures = null;
            PoolConfig poolConfig = null;
            PoolConfig smallPoolConfig = null;
            LevelConfig levelConfig = null;
            LevelConfig sentinelConfig = null;
            PoolFactory factory = null;
            IObjectResolver resolver = null;
            Exception primaryFailure = null;
            try
            {
                fixtures = new GameObject("__LevelValidation_Fixtures");
                SceneManager.MoveGameObjectToScene(fixtures, preview);
                Transform sourceRoot = CreateInactiveSourceRoot(fixtures.transform);
                LevelValidationPoolable source = CreatePoolableSource(sourceRoot);
                poolConfig = CreatePoolConfig("__LevelValidation_Pool", source, 2);
                smallPoolConfig = CreatePoolConfig("__LevelValidation_SmallPool", source, 8);
                PoolContainer container = fixtures.AddComponent<PoolContainer>();
                SetContainerConfigs(container, poolConfig);
                resolver = new ContainerBuilder().Build();
                factory = new PoolFactory(container, resolver, retainMinimum: true);
                factory.Initialize(startMaintenance: false);

                Transform runtimeRoot = new GameObject("RuntimeRoot").transform;
                runtimeRoot.SetParent(fixtures.transform, false);
                runtimeRoot.position = new Vector3(3f, -2f, 5f);
                runtimeRoot.rotation = Quaternion.Euler(12f, 37f, -8f);
                runtimeRoot.localScale = new Vector3(2f, 3f, .5f);

                levelConfig = CreateLevelConfig(poolConfig,
                    new Vector3(1f, 2f, -4f), Quaternion.Euler(0f, 25f, 0f), new Vector3(1.5f, .75f, 2f),
                    new Vector3(-3f, .5f, 2f), Quaternion.Euler(5f, -10f, 0f), new Vector3(.5f, 2f, 1f));
                LevelSpawner spawner = CreateSpawner(fixtures.transform, levelConfig, runtimeRoot, factory);
                Assert(spawner.TrySpawn(out string spawnFailure), "transformed-root spawn failed: " + spawnFailure);
                Assert(spawner.ActiveLeaseCount == 2, "spawn did not retain both leases.");
                Assert(runtimeRoot.childCount == 2, "spawn did not parent both rentals under the runtime root.");
                AssertPose(runtimeRoot.GetChild(0), runtimeRoot, levelConfig.GetEntry(0));
                AssertPose(runtimeRoot.GetChild(1), runtimeRoot, levelConfig.GetEntry(1));
                LevelValidationPoolable first = runtimeRoot.GetChild(0).GetComponent<LevelValidationPoolable>();
                AssertRentObservation(first, runtimeRoot, levelConfig.GetEntry(0));
                spawner.ReturnAll();
                Assert(factory.GetPool(poolConfig).CountActive == 0, "ReturnAll did not return every owned lease.");

                ValidateLevelSession(fixtures.transform, runtimeRoot, levelConfig, spawner, factory, poolConfig);

                // One external lease leaves capacity for only one of this level's two valid entries.
                Assert(factory.TryRent(poolConfig, new PoolSpawnArgs(Vector3.zero, Quaternion.identity), out PoolLease external),
                    "capacity fixture could not reserve its external lease.");
                Assert(!spawner.TrySpawn(out string capacityFailure) && !string.IsNullOrEmpty(capacityFailure),
                    "capacity failure unexpectedly succeeded.");
                Assert(factory.GetPool(poolConfig).CountActive == 1,
                    "capacity failure did not atomically roll back its first rental.");
                Assert(external.Return(), "capacity fixture could not return its external lease.");

                ValidateTwentyFourEntryCapacity(smallPoolConfig);
                ValidateOrderedCaptureAndBakeSafety(fixtures.transform, smallPoolConfig, poolConfig, out sentinelConfig);
                Debug.Log("[LevelValidation] Passed: inactive rent pose, final active local TRS, LevelSession handoff/restore/rejection, rollback, ordered 24-entry capture, and capacity-safe Bake.");
            }
            catch (Exception exception)
            {
                primaryFailure = exception;
            }
            finally
            {
                Exception cleanupFailure = null;
                TryCleanup(() => { if (fixtures != null) UnityEngine.Object.DestroyImmediate(fixtures); }, ref cleanupFailure);
                // The preview hierarchy is gone before PoolFactory.Dispose. Its Unity-null guards now
                // perform managed lease/pool cleanup without scheduling Edit Mode Object.Destroy calls.
                TryCleanup(() => factory?.Dispose(), ref cleanupFailure);
                TryCleanup(() => (resolver as IDisposable)?.Dispose(), ref cleanupFailure);
                TryCleanup(() => { if (levelConfig != null) UnityEngine.Object.DestroyImmediate(levelConfig); }, ref cleanupFailure);
                TryCleanup(() => { if (sentinelConfig != null) UnityEngine.Object.DestroyImmediate(sentinelConfig); }, ref cleanupFailure);
                TryCleanup(() => { if (poolConfig != null) UnityEngine.Object.DestroyImmediate(poolConfig); }, ref cleanupFailure);
                TryCleanup(() => { if (smallPoolConfig != null) UnityEngine.Object.DestroyImmediate(smallPoolConfig); }, ref cleanupFailure);
                TryCleanup(() => EditorSceneManager.ClosePreviewScene(preview), ref cleanupFailure);

                if (primaryFailure != null)
                {
                    if (cleanupFailure != null)
                        Debug.LogError("[LevelValidation] Failed.\n" + primaryFailure + "\nCleanup also failed.\n" + cleanupFailure);
                    else
                        Debug.LogError("[LevelValidation] Failed.\n" + primaryFailure);
                }
                else if (cleanupFailure != null)
                {
                    Debug.LogError("[LevelValidation] Cleanup failed after an otherwise successful validation.\n" + cleanupFailure);
                }
            }
        }

        private static void TryCleanup(Action action, ref Exception firstFailure)
        {
            try { action(); }
            catch (Exception exception) { firstFailure ??= exception; }
        }

        private static Transform CreateInactiveSourceRoot(Transform parent)
        {
            GameObject root = new GameObject("SourceRoot");
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            return root.transform;
        }

        private static LevelValidationPoolable CreatePoolableSource(Transform parent)
        {
            GameObject source = new GameObject("PoolableSource");
            source.transform.SetParent(parent, false);
            return source.AddComponent<LevelValidationPoolable>();
        }

        private static PoolConfig CreatePoolConfig(string rootName, MonoBehaviour prefab, int maxPool)
        {
            PoolConfig config = ScriptableObject.CreateInstance<PoolConfig>();
            SerializedObject serialized = new SerializedObject(config);
            serialized.FindProperty("poolRootName").stringValue = rootName;
            serialized.FindProperty("prefab").objectReferenceValue = prefab;
            serialized.FindProperty("minPool").intValue = 0;
            serialized.FindProperty("maxPool").intValue = maxPool;
            serialized.FindProperty("returnDelaySeconds").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        private static LevelConfig CreateLevelConfig(PoolConfig config, params object[] values)
        {
            LevelConfig level = ScriptableObject.CreateInstance<LevelConfig>();
            SerializedObject serialized = new SerializedObject(level);
            SerializedProperty entries = serialized.FindProperty("entries");
            entries.arraySize = values.Length / 3;
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("poolConfig").objectReferenceValue = config;
                entry.FindPropertyRelative("localPosition").vector3Value = (Vector3)values[i * 3];
                entry.FindPropertyRelative("localRotation").quaternionValue = (Quaternion)values[i * 3 + 1];
                entry.FindPropertyRelative("localScale").vector3Value = (Vector3)values[i * 3 + 2];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return level;
        }

        private static LevelConfig CreateEmptyLevelConfig()
        {
            return ScriptableObject.CreateInstance<LevelConfig>();
        }

        private static LevelSpawner CreateSpawner(Transform parent, LevelConfig level, Transform runtimeRoot, PoolFactory factory)
        {
            GameObject host = new GameObject("LevelSpawner");
            host.transform.SetParent(parent, false);
            LevelSpawner spawner = host.AddComponent<LevelSpawner>();
            SerializedObject serialized = new SerializedObject(spawner);
            serialized.FindProperty("levelConfig").objectReferenceValue = level;
            serialized.FindProperty("runtimeRoot").objectReferenceValue = runtimeRoot;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            typeof(LevelSpawner).GetMethod("Construct", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(spawner, new object[] { factory });
            return spawner;
        }

        private static void ValidateLevelSession(Transform parent, Transform runtimeRoot, LevelConfig level,
            LevelSpawner spawner, PoolFactory factory, PoolConfig poolConfig)
        {
            GameObject authored = new GameObject("AuthoredBlocks");
            authored.transform.SetParent(parent, false);
            LevelSession session = CreateSession(parent, authored.transform, spawner);

            Assert(session.TryStart(out string startFailure), "LevelSession start failed: " + startFailure);
            Assert(!authored.activeSelf && session.IsStarted && spawner.ActiveLeaseCount == level.Count,
                "Successful LevelSession start did not hide authored Blocks or retain every lease.");
            Assert(!session.TryStart(out string duplicateFailure) && !string.IsNullOrEmpty(duplicateFailure)
                   && spawner.ActiveLeaseCount == level.Count,
                "Duplicate LevelSession start changed the active leases.");
            session.ReturnAll();
            Assert(authored.activeSelf && !session.IsStarted && spawner.ActiveLeaseCount == 0,
                "LevelSession ReturnAll did not restore authored activeSelf or clear leases.");

            Assert(factory.TryRent(poolConfig, new PoolSpawnArgs(Vector3.zero, Quaternion.identity), out PoolLease external),
                "LevelSession capacity fixture could not reserve its external lease.");
            Assert(!session.TryStart(out string capacityFailure) && !string.IsNullOrEmpty(capacityFailure)
                   && authored.activeSelf && spawner.ActiveLeaseCount == 0
                   && factory.GetPool(poolConfig).CountActive == 1,
                "Failed LevelSession spawn did not restore authored Blocks or roll back its rentals.");
            Assert(external.Return(), "LevelSession capacity fixture could not return its external lease.");

            Transform invalidRuntimeRoot = new GameObject("InvalidRuntimeRoot").transform;
            invalidRuntimeRoot.SetParent(authored.transform, false);
            SetSpawnerRuntimeRoot(spawner, invalidRuntimeRoot);
            Assert(!session.TryStart(out string nestedFailure) && nestedFailure.Contains("cannot be the authored Blocks root")
                   && authored.activeSelf && spawner.ActiveLeaseCount == 0,
                "Nested RuntimeRoot was not rejected before authored Blocks changed.");
            SetSpawnerRuntimeRoot(spawner, runtimeRoot);
        }

        private static LevelSession CreateSession(Transform parent, Transform authoredBlocksRoot, LevelSpawner spawner)
        {
            GameObject host = new GameObject("LevelSession");
            host.transform.SetParent(parent, false);
            LevelSession session = host.AddComponent<LevelSession>();
            SerializedObject serialized = new SerializedObject(session);
            serialized.FindProperty("authoredBlocksRoot").objectReferenceValue = authoredBlocksRoot;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            typeof(LevelSession).GetMethod("Construct", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { spawner });
            return session;
        }

        private static void SetSpawnerRuntimeRoot(LevelSpawner spawner, Transform runtimeRoot)
        {
            SerializedObject serialized = new SerializedObject(spawner);
            serialized.FindProperty("runtimeRoot").objectReferenceValue = runtimeRoot;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateTwentyFourEntryCapacity(PoolConfig maxEightConfig)
        {
            LevelConfig twentyFour = CreateEmptyLevelConfig();
            try
            {
                SerializedObject serialized = new SerializedObject(twentyFour);
                SerializedProperty entries = serialized.FindProperty("entries");
                entries.arraySize = 24;
                for (int i = 0; i < entries.arraySize; i++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("poolConfig").objectReferenceValue = maxEightConfig;
                    entry.FindPropertyRelative("localPosition").vector3Value = new Vector3(i, 0f, 0f);
                    entry.FindPropertyRelative("localRotation").quaternionValue = Quaternion.identity;
                    entry.FindPropertyRelative("localScale").vector3Value = Vector3.one;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                try
                {
                    twentyFour.Validate();
                    throw new InvalidOperationException("24 entries sharing a max-8 PoolConfig were accepted.");
                }
                catch (InvalidOperationException exception)
                {
                    Assert(exception.Message.Contains("requires 24 rentals") && exception.Message.Contains("maximum is 8"),
                        "24-entry capacity rejection did not report the expected required-versus-maximum reason.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(twentyFour);
            }
        }

        private static void ValidateOrderedCaptureAndBakeSafety(Transform parent, PoolConfig maxEight,
            PoolConfig sentinelPool, out LevelConfig sentinel)
        {
            GameObject authoringRoot = new GameObject("AuthoringRoot");
            authoringRoot.transform.SetParent(parent, false);
            for (int i = 0; i < 24; i++)
            {
                GameObject child = new GameObject("Obstacle_" + i);
                child.transform.SetParent(authoringRoot.transform, false);
                child.transform.localPosition = new Vector3(i, i + 1f, -i);
                child.AddComponent<ObstacleView>();
            }

            sentinel = CreateLevelConfig(sentinelPool, Vector3.zero, Quaternion.identity, Vector3.one);
            LevelEditorWindow window = ScriptableObject.CreateInstance<LevelEditorWindow>();
            try
            {
                SetWindowField(window, "authoringRoot", authoringRoot.transform);
                SetWindowField(window, "defaultPoolConfig", maxEight);
                SetWindowField(window, "levelConfig", sentinel);
                InvokeWindow(window, "Capture");
                IList captured = (IList)typeof(LevelEditorWindow).GetField("captured", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(window);
                Assert(captured.Count == 24, "Capture did not retain all 24 direct child ObstacleViews.");
                for (int i = 0; i < captured.Count; i++)
                {
                    Transform source = (Transform)captured[i].GetType().GetField("Source").GetValue(captured[i]);
                    Assert(source == authoringRoot.transform.GetChild(i), "Capture did not preserve hierarchy order.");
                }
                InvokeWindow(window, "Bake");
                Assert(sentinel.Count == 1 && sentinel.GetEntry(0).PoolConfig == sentinelPool,
                    "Over-capacity Bake changed the target LevelConfig instead of rejecting it.");
                GameObject nextRoot = new GameObject("NextAuthoringRoot");
                nextRoot.transform.SetParent(parent, false);
                InvokeWindow(window, "ApplySelection", nextRoot.transform, maxEight, sentinel);
                Assert(captured.Count == 0, "Changing authoring inputs did not clear stale capture preview.");
                InvokeWindow(window, "Bake");
                Assert(sentinel.Count == 1 && sentinel.GetEntry(0).PoolConfig == sentinelPool,
                    "Bake after stale-selection clearing changed the target LevelConfig.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static void SetContainerConfigs(PoolContainer container, PoolConfig config)
        {
            SerializedObject serialized = new SerializedObject(container);
            SerializedProperty configs = serialized.FindProperty("configs");
            configs.arraySize = 1;
            configs.GetArrayElementAtIndex(0).objectReferenceValue = config;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetWindowField(LevelEditorWindow window, string name, object value) =>
            typeof(LevelEditorWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, value);

        private static void InvokeWindow(LevelEditorWindow window, string name, params object[] arguments) =>
            typeof(LevelEditorWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, arguments);

        private static void AssertPose(Transform spawned, Transform root, LevelConfig.Entry entry)
        {
            Assert(spawned.gameObject.activeSelf,
                "spawn completed without activating the rented object.");
            Assert((spawned.position - root.TransformPoint(entry.LocalPosition)).sqrMagnitude < .000001f,
                "spawned world position was not derived from the runtime root.");
            Assert(Quaternion.Angle(spawned.rotation, root.rotation * entry.LocalRotation) < .01f,
                "spawned world rotation was not derived from the runtime root.");
            Assert((spawned.localPosition - entry.LocalPosition).sqrMagnitude < .000001f
                && Quaternion.Angle(spawned.localRotation, entry.LocalRotation) < .01f
                && (spawned.localScale - entry.LocalScale).sqrMagnitude < .000001f,
                "spawned local transform did not exactly match the LevelConfig entry.");
        }

        private static void AssertRentObservation(LevelValidationPoolable value, Transform root, LevelConfig.Entry entry)
        {
            Vector3 expectedPosition = root.TransformPoint(entry.LocalPosition);
            Quaternion expectedRotation = root.rotation * entry.LocalRotation;
            Assert(value != null && value.RentObserved && value.RentSawInactiveSelf,
                "OnPoolRent did not observe the inactive pooled object.");
            Assert((value.RentPosition - expectedPosition).sqrMagnitude < .000001f
                   && Quaternion.Angle(value.RentRotation, expectedRotation) < .01f,
                "OnPoolRent did not observe the entry-derived world pose.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }

    /// <summary>Minimal disposable pool source used only by LevelValidation's preview scene.</summary>
    public sealed class LevelValidationPoolable : MonoBehaviour, IPoolable
    {
        public bool RentObserved { get; private set; }
        public bool RentSawInactiveSelf { get; private set; }
        public Vector3 RentPosition { get; private set; }
        public Quaternion RentRotation { get; private set; }

        public GameObject PoolObject => gameObject;
        public void OnPoolCreated(IPoolable owner) { }
        public void OnPoolRent(PoolLease lease)
        {
            RentSawInactiveSelf = !gameObject.activeSelf;
            RentPosition = transform.position;
            RentRotation = transform.rotation;
            RentObserved = true;
        }
        public void OnPoolReturn() { }
        public void OnPoolDestroy() { }

    }
}
