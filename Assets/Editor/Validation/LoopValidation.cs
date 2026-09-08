using System;
using System.IO;
using Framework.Loop;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Framework.EditorValidation
{
    // Explicit Editor-only checks; this menu never creates, saves, reloads, or changes a scene.
    public static class LoopValidation
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
            public bool allocationMeasured;
            public long stableTickAllocatedBytes;
            public int warmupCycles = 100;
            public int measuredCycles = 1000;
        }

        [MenuItem("Tools/Smesh Fest/Validation/Loop")]
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
                result.summary = RunChecks(result);
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
                "../Docs/Prototype/evidence/raw/loop-validation.json");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, JsonUtility.ToJson(result, true));
            if (result.success)
                Debug.Log("[LoopValidation] " + result.summary);
            else
                Debug.LogError("[LoopValidation] " + result.error);
        }

        private static string RunChecks(ValidationResult result)
        {
            Action<bool, string> assert = delegate(bool condition, string message)
            {
                if (!condition) throw new InvalidOperationException(message);
            };

            var phaseDispatcher = new LoopDispatcher();
            int updateCalls = 0;
            int fixedCalls = 0;
            int lateCalls = 0;
            float updateDelta = -1f;
            float fixedDelta = -1f;
            float lateDelta = -1f;
            phaseDispatcher.UpdateTick += delegate(float value) { updateCalls++; updateDelta = value; };
            phaseDispatcher.FixedTick += delegate(float value) { fixedCalls++; fixedDelta = value; };
            phaseDispatcher.LateTick += delegate(float value) { lateCalls++; lateDelta = value; };
            phaseDispatcher.TickUpdate(1f);
            phaseDispatcher.TickFixed(1f);
            phaseDispatcher.TickLate(1f);
            assert(updateCalls == 0 && fixedCalls == 0 && lateCalls == 0, "ticks ran before StartLoop");
            phaseDispatcher.StartLoop();
            phaseDispatcher.StartLoop();
            phaseDispatcher.TickUpdate(0.25f);
            phaseDispatcher.TickFixed(0.5f);
            phaseDispatcher.TickLate(0.75f);
            assert(updateCalls == 1 && fixedCalls == 1 && lateCalls == 1, "phases were not dispatched separately");
            assert(updateDelta == 0.25f && fixedDelta == 0.5f && lateDelta == 0.75f, "delta values were changed");
            phaseDispatcher.StopLoop();
            phaseDispatcher.StopLoop();
            phaseDispatcher.TickUpdate(1f);
            phaseDispatcher.TickFixed(1f);
            phaseDispatcher.TickLate(1f);
            assert(updateCalls == 1 && fixedCalls == 1 && lateCalls == 1, "stopped ticks did not no-op for every phase");
            phaseDispatcher.StartLoop();
            phaseDispatcher.TickUpdate(0f);
            assert(updateCalls == 2 && updateDelta == 0f, "StopLoop or restart behavior is incorrect");

            var duplicateDispatcher = new LoopDispatcher();
            int duplicateCalls = 0;
            Action<float> duplicateListener = delegate(float value) { duplicateCalls++; };
            duplicateDispatcher.UpdateTick += duplicateListener;
            duplicateDispatcher.UpdateTick += duplicateListener;
            duplicateDispatcher.StartLoop();
            duplicateDispatcher.TickUpdate(1f);
            assert(duplicateCalls == 1, "duplicate subscription dispatched more than once");

            var stopDuringDispatch = new LoopDispatcher();
            int stopFirstCalls = 0;
            int stopSecondCalls = 0;
            int stoppedFixedCalls = 0;
            int stoppedLateCalls = 0;
            stopDuringDispatch.UpdateTick += delegate(float value)
            {
                stopFirstCalls++;
                stopDuringDispatch.StopLoop();
                stopDuringDispatch.TickFixed(value);
                stopDuringDispatch.TickLate(value);
            };
            stopDuringDispatch.UpdateTick += delegate(float value) { stopSecondCalls++; };
            stopDuringDispatch.FixedTick += delegate(float value) { stoppedFixedCalls++; };
            stopDuringDispatch.LateTick += delegate(float value) { stoppedLateCalls++; };
            stopDuringDispatch.StartLoop();
            stopDuringDispatch.TickUpdate(1f);
            stopDuringDispatch.TickUpdate(1f);
            stopDuringDispatch.TickFixed(1f);
            stopDuringDispatch.TickLate(1f);
            assert(stopFirstCalls == 1 && stopSecondCalls == 1 && stoppedFixedCalls == 0 && stoppedLateCalls == 0 &&
                !stopDuringDispatch.IsRunning,
                "StopLoop changed the current dispatch or failed to suppress the next Tick");

            var disposeDuringDispatch = new LoopDispatcher();
            int disposeFirstCalls = 0;
            int disposeSecondCalls = 0;
            int disposedFixedCalls = 0;
            int disposedLateCalls = 0;
            Action<float> disposeFirst = delegate(float value) { disposeFirstCalls++; disposeDuringDispatch.Dispose(); };
            Action<float> disposeSecond = delegate(float value) { disposeSecondCalls++; };
            Action<float> disposeFixed = delegate(float value) { disposedFixedCalls++; };
            Action<float> disposeLate = delegate(float value) { disposedLateCalls++; };
            disposeDuringDispatch.UpdateTick += disposeFirst;
            disposeDuringDispatch.UpdateTick += disposeSecond;
            disposeDuringDispatch.FixedTick += disposeFixed;
            disposeDuringDispatch.LateTick += disposeLate;
            disposeDuringDispatch.StartLoop();
            disposeDuringDispatch.TickUpdate(1f);
            disposeDuringDispatch.TickFixed(1f);
            disposeDuringDispatch.TickLate(1f);
            disposeDuringDispatch.Dispose();
            disposeDuringDispatch.StopLoop();
            disposeDuringDispatch.TickUpdate(1f);
            disposeDuringDispatch.TickFixed(1f);
            disposeDuringDispatch.TickLate(1f);
            disposeDuringDispatch.UpdateTick -= disposeFirst;
            disposeDuringDispatch.UpdateTick -= disposeSecond;
            disposeDuringDispatch.FixedTick -= disposeFixed;
            disposeDuringDispatch.LateTick -= disposeLate;
            bool disposedStartRejected = false;
            bool disposedUpdateSubscribeRejected = false;
            bool disposedFixedSubscribeRejected = false;
            bool disposedLateSubscribeRejected = false;
            try { disposeDuringDispatch.StartLoop(); }
            catch (ObjectDisposedException) { disposedStartRejected = true; }
            try { disposeDuringDispatch.UpdateTick += delegate(float value) { }; }
            catch (ObjectDisposedException) { disposedUpdateSubscribeRejected = true; }
            try { disposeDuringDispatch.FixedTick += delegate(float value) { }; }
            catch (ObjectDisposedException) { disposedFixedSubscribeRejected = true; }
            try { disposeDuringDispatch.LateTick += delegate(float value) { }; }
            catch (ObjectDisposedException) { disposedLateSubscribeRejected = true; }
            assert(disposeFirstCalls == 1 && disposeSecondCalls == 0 && disposedFixedCalls == 0 && disposedLateCalls == 0 &&
                disposedStartRejected && disposedUpdateSubscribeRejected && disposedFixedSubscribeRejected &&
                disposedLateSubscribeRejected && !disposeDuringDispatch.IsRunning,
                "Dispose did not immediately clear pending listeners or reject later setup");

            var mutationDispatcher = new LoopDispatcher();
            int mutationTargetCalls = 0;
            bool mutated = false;
            Action<float> mutationTarget = delegate(float value) { mutationTargetCalls++; };
            mutationDispatcher.UpdateTick += delegate(float value)
            {
                if (!mutated)
                {
                    mutated = true;
                    mutationDispatcher.UpdateTick -= mutationTarget;
                    mutationDispatcher.UpdateTick += mutationTarget;
                }
            };
            mutationDispatcher.UpdateTick += mutationTarget;
            mutationDispatcher.StartLoop();
            mutationDispatcher.TickUpdate(1f);
            mutationDispatcher.TickUpdate(1f);
            assert(mutationTargetCalls == 1, "subscription mutation did not preserve Observable timing");

            var exceptionDispatcher = new LoopDispatcher();
            bool throwOnce = true;
            int recoveredCalls = 0;
            exceptionDispatcher.UpdateTick += delegate(float value)
            {
                if (throwOnce)
                {
                    throwOnce = false;
                    throw new ApplicationException("expected tick failure");
                }
            };
            exceptionDispatcher.UpdateTick += delegate(float value) { recoveredCalls++; };
            exceptionDispatcher.StartLoop();
            bool listenerExceptionPropagated = false;
            try { exceptionDispatcher.TickUpdate(1f); }
            catch (ApplicationException) { listenerExceptionPropagated = true; }
            exceptionDispatcher.TickUpdate(1f);
            assert(listenerExceptionPropagated && recoveredCalls == 1, "listener exception did not recover Tick state");

            var reentrantDispatcher = new LoopDispatcher();
            bool reentrantRejected = false;
            int updateAfterReentrant = 0;
            int fixedAfterReentrant = 0;
            Action<float> recursiveTick = delegate(float value) { reentrantDispatcher.TickFixed(value); };
            Action<float> recoveredUpdate = delegate(float value) { updateAfterReentrant++; };
            reentrantDispatcher.UpdateTick += recursiveTick;
            reentrantDispatcher.FixedTick += delegate(float value) { fixedAfterReentrant++; };
            reentrantDispatcher.StartLoop();
            try { reentrantDispatcher.TickUpdate(1f); }
            catch (InvalidOperationException) { reentrantRejected = true; }
            reentrantDispatcher.UpdateTick -= recursiveTick;
            reentrantDispatcher.UpdateTick += recoveredUpdate;
            reentrantDispatcher.TickUpdate(1f);
            reentrantDispatcher.TickFixed(1f);
            assert(reentrantRejected && updateAfterReentrant == 1 && fixedAfterReentrant == 1,
                "uncaught reentrant Tick did not reject or recover");

            var deltaDispatcher = new LoopDispatcher();
            int zeroDeltaCalls = 0;
            deltaDispatcher.UpdateTick += delegate(float value) { zeroDeltaCalls++; };
            deltaDispatcher.StartLoop();
            deltaDispatcher.TickUpdate(0f);
            bool negativeRejected = false;
            bool nanRejected = false;
            bool infinityRejected = false;
            try { deltaDispatcher.TickUpdate(-0.01f); }
            catch (ArgumentOutOfRangeException) { negativeRejected = true; }
            try { deltaDispatcher.TickUpdate(float.NaN); }
            catch (ArgumentOutOfRangeException) { nanRejected = true; }
            try { deltaDispatcher.TickUpdate(float.PositiveInfinity); }
            catch (ArgumentOutOfRangeException) { infinityRejected = true; }
            assert(zeroDeltaCalls == 1 && negativeRejected && nanRejected && infinityRejected,
                "running delta boundary validation is incorrect");

            var allocationDispatcher = new LoopDispatcher();
            float allocationSink = 0f;
            allocationDispatcher.UpdateTick += delegate(float value) { allocationSink += value; };
            allocationDispatcher.FixedTick += delegate(float value) { allocationSink += value; };
            allocationDispatcher.LateTick += delegate(float value) { allocationSink += value; };
            allocationDispatcher.StartLoop();
            System.Reflection.MethodInfo allocationMethod = typeof(GC).GetMethod(
                "GetAllocatedBytesForCurrentThread",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (allocationMethod == null)
            {
                result.allocationMeasured = false;
            }
            else
            {
                Func<long> getAllocatedBytes = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), allocationMethod);
                for (int i = 0; i < result.warmupCycles; i++)
                {
                    allocationDispatcher.TickUpdate(i);
                    allocationDispatcher.TickFixed(i);
                    allocationDispatcher.TickLate(i);
                }
                long before = getAllocatedBytes();
                for (int i = 0; i < result.measuredCycles; i++)
                {
                    allocationDispatcher.TickUpdate(i);
                    allocationDispatcher.TickFixed(i);
                    allocationDispatcher.TickLate(i);
                }
                long after = getAllocatedBytes();
                result.allocationMeasured = true;
                result.stableTickAllocatedBytes = after - before;
                assert(result.stableTickAllocatedBytes == 0,
                    "stable three-phase Tick allocated " + result.stableTickAllocatedBytes + " bytes");
            }

            string allocationSummary = result.allocationMeasured
                ? result.stableTickAllocatedBytes + " bytes"
                : "unmeasured (GC.GetAllocatedBytesForCurrentThread unsupported)";
            return "Loop checks passed (lifecycle, phase routing, mutation, recovery, delta bounds; stable three-phase Tick allocation: " + allocationSummary + ").";
        }
    }
}
