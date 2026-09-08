using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Framework.EditorValidation
{
    // Explicit Editor-only checks; this menu never creates, saves, or reloads a scene.
    public static class ObservableValidation
    {
        [Serializable]
        private sealed class ValidationResult
        {
            public string recordedAtUtc;
            public string unityVersion;
            public string execution = "Editor menu; plain C# instances; no Play Mode";
            public bool success;
            public string summary;
            public string error;
            public string sceneBefore;
            public string sceneAfter;
            public bool dirtyBefore;
            public bool dirtyAfter;
            public bool sceneStateUnchanged;
        }

        [MenuItem("Tools/Smesh Fest/Validation/Observable")]
        public static void Run()
        {
            Scene before = SceneManager.GetActiveScene();
            int sceneHandle = before.handle;
            int rootCount = before.rootCount;
            var result = new ValidationResult
            {
                recordedAtUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                sceneBefore = before.path,
                dirtyBefore = before.isDirty
            };

            try
            {
                result.summary = RunChecks();
                result.success = true;
            }
            catch (Exception exception)
            {
                result.error = exception.ToString();
            }

            Scene after = SceneManager.GetActiveScene();
            result.sceneAfter = after.path;
            result.dirtyAfter = after.isDirty;
            result.sceneStateUnchanged = sceneHandle == after.handle &&
                result.sceneBefore == result.sceneAfter &&
                result.dirtyBefore == result.dirtyAfter && rootCount == after.rootCount;
            if (!result.sceneStateUnchanged)
            {
                result.success = false;
                result.error = (result.error ?? "") + "\nActive scene state changed during validation.";
            }

            string outputPath = Path.Combine(Application.dataPath,
                "../Docs/Prototype/evidence/raw/observable-validation.json");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, JsonUtility.ToJson(result, true));
            if (result.success)
                Debug.Log("[ObservableValidation] " + result.summary);
            else
                Debug.LogError("[ObservableValidation] " + result.error);
        }

        private static string RunChecks()
        {
            System.Action<bool, string> assert = delegate(bool condition, string message)
            {
                if (!condition)
                {
                    throw new System.InvalidOperationException(message);
                }
            };
            
            Framework.Observer.Observable<int> ordered = new Framework.Observer.Observable<int>();
            System.Collections.Generic.List<int> order = new System.Collections.Generic.List<int>();
            System.Action<int> first = delegate(int value) { order.Add(1); };
            System.Action<int> second = delegate(int value) { order.Add(2); };
            assert(ordered.Subscribe(first), "order first subscription failed");
            assert(ordered.Subscribe(second), "order second subscription failed");
            ordered.Publish(0);
            assert(order.Count == 2 && order[0] == 1 && order[1] == 2, "listeners did not publish in registration order");
            assert(!ordered.Subscribe(first) && ordered.Count == 2, "duplicate subscription changed Count");
            try
            {
                ordered.Subscribe(null);
                throw new System.InvalidOperationException("null Subscribe did not throw");
            }
            catch (System.ArgumentNullException)
            {
            }
            
            Framework.Observer.Observable<int> selfRemoval = new Framework.Observer.Observable<int>();
            int selfCalls = 0;
            System.Action<int> self = null;
            self = delegate(int value)
            {
                selfCalls++;
                assert(selfRemoval.Unsubscribe(self), "self Unsubscribe failed");
            };
            selfRemoval.Subscribe(self);
            selfRemoval.Publish(0);
            selfRemoval.Publish(0);
            assert(selfCalls == 1 && selfRemoval.Count == 0, "self removal affected a later Publish incorrectly");
            
            Framework.Observer.Observable<int> otherRemoval = new Framework.Observer.Observable<int>();
            int otherCalls = 0;
            System.Action<int> other = delegate(int value) { otherCalls++; };
            otherRemoval.Subscribe(delegate(int value) { assert(otherRemoval.Unsubscribe(other), "other Unsubscribe failed"); });
            otherRemoval.Subscribe(other);
            otherRemoval.Publish(0);
            assert(otherCalls == 0 && otherRemoval.Count == 1, "removed later listener ran during the same Publish");
            
            Framework.Observer.Observable<int> addDuringPublish = new Framework.Observer.Observable<int>();
            int addedCalls = 0;
            System.Action<int> added = delegate(int value) { addedCalls++; };
            addDuringPublish.Subscribe(delegate(int value) { addDuringPublish.Subscribe(added); });
            addDuringPublish.Publish(0);
            assert(addedCalls == 0, "listener added during Publish ran too early");
            addDuringPublish.Publish(0);
            assert(addedCalls == 1, "listener added during Publish did not run next time");
            
            Framework.Observer.Observable<int> resubscribe = new Framework.Observer.Observable<int>();
            int targetCalls = 0;
            bool changed = false;
            System.Action<int> target = delegate(int value) { targetCalls++; };
            resubscribe.Subscribe(delegate(int value)
            {
                if (!changed)
                {
                    changed = true;
                    assert(resubscribe.Unsubscribe(target), "target Unsubscribe failed");
                    assert(resubscribe.Subscribe(target), "target re-subscribe failed");
                }
            });
            resubscribe.Subscribe(target);
            resubscribe.Publish(0);
            assert(targetCalls == 0, "re-subscribed listener ran during the original Publish");
            resubscribe.Publish(0);
            assert(targetCalls == 1, "re-subscribed listener did not wait until the next Publish");
            
            Framework.Observer.Observable<int> clearDuringPublish = new Framework.Observer.Observable<int>();
            int clearedLaterCalls = 0;
            int afterClearCalls = 0;
            bool cleared = false;
            System.Action<int> afterClear = delegate(int value) { afterClearCalls++; };
            clearDuringPublish.Subscribe(delegate(int value)
            {
                if (!cleared)
                {
                    cleared = true;
                    clearDuringPublish.Clear();
                    assert(clearDuringPublish.Subscribe(afterClear), "Subscribe after Clear failed");
                }
            });
            clearDuringPublish.Subscribe(delegate(int value) { clearedLaterCalls++; });
            clearDuringPublish.Publish(0);
            assert(clearedLaterCalls == 0 && afterClearCalls == 0 && clearDuringPublish.Count == 1, "Clear during Publish did not tombstone current listeners");
            clearDuringPublish.Publish(0);
            assert(afterClearCalls == 1, "listener added after Clear did not wait for the next Publish");
            
            Framework.Observer.Observable<int> exceptionRecovery = new Framework.Observer.Observable<int>();
            bool throwOnce = true;
            int recoveredCalls = 0;
            exceptionRecovery.Subscribe(delegate(int value)
            {
                if (throwOnce)
                {
                    throwOnce = false;
                    throw new System.ApplicationException("expected listener failure");
                }
            });
            exceptionRecovery.Subscribe(delegate(int value) { recoveredCalls++; });
            try
            {
                exceptionRecovery.Publish(0);
                throw new System.InvalidOperationException("listener exception was hidden");
            }
            catch (System.ApplicationException)
            {
            }
            exceptionRecovery.Publish(0);
            assert(recoveredCalls == 1, "Publish did not recover after a listener exception");
            
            Framework.Observer.Observable<int> reentrant = new Framework.Observer.Observable<int>();
            reentrant.Subscribe(delegate(int value) { reentrant.Publish(value); });
            bool reentrantRejected = false;
            try
            {
                reentrant.Publish(0);
            }
            catch (System.InvalidOperationException)
            {
                reentrantRejected = true;
            }
            assert(reentrantRejected, "reentrant Publish was accepted");
            reentrant.Clear();
            int postReentrantCalls = 0;
            reentrant.Subscribe(delegate(int value) { postReentrantCalls++; });
            reentrant.Publish(0);
            assert(postReentrantCalls == 1, "reentrant rejection did not restore Publish state");
            
            Framework.Observer.Observable<int> countChecks = new Framework.Observer.Observable<int>();
            int countListenerCalls = 0;
            System.Action<int> countListener = delegate(int value) { countListenerCalls++; };
            for (int i = 0; i < 32; i++)
            {
                assert(countChecks.Subscribe(countListener), "repeated Subscribe failed");
                assert(countChecks.Count == 1, "Count after Subscribe is incorrect");
                assert(countChecks.Unsubscribe(countListener), "repeated Unsubscribe failed");
                assert(countChecks.Count == 0, "Count after Unsubscribe is incorrect");
            }
            assert(!countChecks.Unsubscribe(countListener), "Unsubscribe reported a missing listener as present");
            
            Framework.Observer.Observable<int> allocationCheck = new Framework.Observer.Observable<int>();
            int allocationSink = 0;
            System.Action<int> stableListener = delegate(int value) { allocationSink += value; };
            allocationCheck.Subscribe(stableListener);
            System.Reflection.MethodInfo allocationMethod = typeof(System.GC).GetMethod(
                "GetAllocatedBytesForCurrentThread",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            string allocationSummary;
            if (allocationMethod == null)
            {
                allocationSummary = "unmeasured (GC.GetAllocatedBytesForCurrentThread unsupported)";
            }
            else
            {
                System.Func<long> getAllocatedBytes = (System.Func<long>)System.Delegate.CreateDelegate(
                    typeof(System.Func<long>),
                    allocationMethod);
                for (int i = 0; i < 100; i++)
                {
                    allocationCheck.Publish(i);
                }
                long before = getAllocatedBytes();
                for (int i = 0; i < 1000; i++)
                {
                    allocationCheck.Publish(i);
                }
                long after = getAllocatedBytes();
                long allocatedBytes = after - before;
                assert(allocatedBytes == 0, "stable Publish allocated " + allocatedBytes + " bytes");
                allocationSummary = allocatedBytes + " bytes";
            }
            
            return "Observable checks passed (10 behavioral cases; stable Publish allocation: " + allocationSummary + ").";
        }
    }
}

