using System;
using System.IO;
using Framework.Loop;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Framework.EditorValidation
{
    // Explicit Editor-only checks; this menu never creates, saves, reloads, or changes a scene.
    public static class GameClockValidation
    {
        private sealed class EqualOwner
        {
            public override bool Equals(object other)
            {
                return other is EqualOwner;
            }

            public override int GetHashCode()
            {
                return 1;
            }
        }

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
            public long managementAllocatedBytes;
            public int warmupCycles = 100;
            public int measuredCycles = 1000;
        }

        [MenuItem("Tools/Smesh Fest/Validation/Game Clock")]
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
                "../Docs/Prototype/evidence/raw/game-clock-validation.json");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, JsonUtility.ToJson(result, true));
            if (result.success)
                Debug.Log("[GameClockValidation] " + result.summary);
            else
                Debug.LogError("[GameClockValidation] " + result.error);
        }

        private static string RunChecks(ValidationResult result)
        {
            Action<bool, string> assert = delegate(bool condition, string message)
            {
                if (!condition) throw new InvalidOperationException(message);
            };

            var clock = new GameClock();
            int stateChanges = 0;
            clock.StateChanged += delegate { stateChanges++; };
            object firstUi = new object();
            object secondUi = new object();
            assert(!clock.IsRunning && !clock.IsPaused && clock.PauseCount == 0 &&
                clock.RequestedTimeScale == 1f && clock.EffectiveTimeScale == 0f, "initial clock state is incorrect");
            clock.StartLoop();
            assert(clock.IsRunning && clock.EffectiveTimeScale == 1f && stateChanges == 1, "StartLoop state is incorrect");
            assert(clock.Pause(firstUi), "first UI Pause failed");
            assert(!clock.Pause(firstUi), "duplicate UI Pause was accepted");
            assert(clock.Pause(secondUi) && clock.IsPaused && clock.PauseCount == 2 && clock.EffectiveTimeScale == 0f,
                "nested UI Pause state is incorrect");
            assert(clock.Resume(firstUi) && clock.IsPaused && clock.PauseCount == 1, "first UI Resume released another owner");
            assert(clock.Resume(secondUi) && !clock.IsPaused && clock.PauseCount == 0 && clock.EffectiveTimeScale == 1f,
                "final UI Resume did not restore the running clock");
            assert(!clock.Resume(secondUi), "missing UI Resume was accepted");
            bool nullPauseRejected = false;
            try { clock.Pause(null); }
            catch (ArgumentNullException) { nullPauseRejected = true; }
            assert(nullPauseRejected && !clock.Resume(null) && stateChanges == 5,
                "Pause/Resume null or duplicate state notification behavior is incorrect");

            var equalOwnerClock = new GameClock();
            object equalFirst = new EqualOwner();
            object equalSecond = new EqualOwner();
            assert(equalOwnerClock.Pause(equalFirst) && equalOwnerClock.Pause(equalSecond) && equalOwnerClock.PauseCount == 2,
                "owners with equal values were not tracked by reference");
            assert(equalOwnerClock.Resume(equalFirst) && equalOwnerClock.PauseCount == 1 && equalOwnerClock.IsPaused,
                "reference owner release removed the wrong equal owner");
            assert(equalOwnerClock.Resume(equalSecond) && equalOwnerClock.PauseCount == 0, "second equal owner did not release");

            var stoppedClock = new GameClock();
            object stoppedOwner = new object();
            stoppedClock.StartLoop();
            stoppedClock.Pause(stoppedOwner);
            stoppedClock.StopLoop();
            stoppedClock.Resume(stoppedOwner);
            assert(!stoppedClock.IsRunning && !stoppedClock.IsPaused && stoppedClock.EffectiveTimeScale == 0f,
                "closing UI while stopped restarted the game");

            var scaleClock = new GameClock();
            object scaleOwner = new object();
            scaleClock.StartLoop();
            scaleClock.Pause(scaleOwner);
            scaleClock.SetTimeScale(0.5f);
            assert(scaleClock.RequestedTimeScale == 0.5f && scaleClock.EffectiveTimeScale == 0f,
                "paused scale change changed effective pause state");
            scaleClock.Resume(scaleOwner);
            assert(scaleClock.EffectiveTimeScale == 0.5f, "resuming did not restore the requested scale");
            scaleClock.SetTimeScale(0f);
            assert(scaleClock.RequestedTimeScale == 0f && scaleClock.EffectiveTimeScale == 0f,
                "zero time scale was not retained as a valid requested value");
            scaleClock.SetTimeScale(1.5f);
            bool negativeRejected = false;
            bool nanRejected = false;
            bool infinityRejected = false;
            try { scaleClock.SetTimeScale(-0.01f); }
            catch (ArgumentOutOfRangeException) { negativeRejected = true; }
            try { scaleClock.SetTimeScale(float.NaN); }
            catch (ArgumentOutOfRangeException) { nanRejected = true; }
            try { scaleClock.SetTimeScale(float.PositiveInfinity); }
            catch (ArgumentOutOfRangeException) { infinityRejected = true; }
            assert(scaleClock.RequestedTimeScale == 1.5f && negativeRejected && nanRejected && infinityRejected,
                "time scale validation is incorrect");

            var exceptionClock = new GameClock();
            bool throwOnce = true;
            Action throwingHandler = delegate
            {
                if (throwOnce)
                {
                    throwOnce = false;
                    throw new ApplicationException("expected state notification failure");
                }
            };
            exceptionClock.StateChanged += throwingHandler;
            bool notificationExceptionPropagated = false;
            try { exceptionClock.StartLoop(); }
            catch (ApplicationException) { notificationExceptionPropagated = true; }
            exceptionClock.StateChanged -= throwingHandler;
            exceptionClock.SetTimeScale(0.5f);
            assert(notificationExceptionPropagated && exceptionClock.IsRunning && exceptionClock.RequestedTimeScale == 0.5f,
                "StateChanged exception did not preserve changed state or later use");

            var disposedClock = new GameClock();
            int disposeNotifications = 0;
            Action disposeHandler = delegate { disposeNotifications++; };
            object disposedOwner = new object();
            disposedClock.StateChanged += disposeHandler;
            disposedClock.StartLoop();
            disposedClock.Pause(disposedOwner);
            int notificationsBeforeDispose = disposeNotifications;
            disposedClock.Dispose();
            disposedClock.Dispose();
            disposedClock.StopLoop();
            disposedClock.StateChanged -= disposeHandler;
            bool disposedStartRejected = false;
            bool disposedScaleRejected = false;
            bool disposedPauseRejected = false;
            try { disposedClock.StartLoop(); }
            catch (ObjectDisposedException) { disposedStartRejected = true; }
            try { disposedClock.SetTimeScale(0.5f); }
            catch (ObjectDisposedException) { disposedScaleRejected = true; }
            try { disposedClock.Pause(disposedOwner); }
            catch (ObjectDisposedException) { disposedPauseRejected = true; }
            assert(!disposedClock.IsRunning && !disposedClock.IsPaused && disposedClock.PauseCount == 0 &&
                disposedClock.RequestedTimeScale == 1f && disposedClock.EffectiveTimeScale == 0f && !disposedClock.Resume(disposedOwner) &&
                disposeNotifications == notificationsBeforeDispose && disposedStartRejected && disposedScaleRejected && disposedPauseRejected,
                "Dispose did not perform final silent cleanup");

            var allocationClock = new GameClock();
            object allocationOwner = new object();
            Action allocationHandler = delegate { };
            allocationClock.StateChanged += allocationHandler;
            allocationClock.StartLoop();
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
                    allocationClock.Pause(allocationOwner);
                    allocationClock.Resume(allocationOwner);
                    allocationClock.SetTimeScale((i & 1) == 0 ? 0.5f : 1f);
                }
                long before = getAllocatedBytes();
                for (int i = 0; i < result.measuredCycles; i++)
                {
                    allocationClock.Pause(allocationOwner);
                    allocationClock.Resume(allocationOwner);
                    allocationClock.SetTimeScale((i & 1) == 0 ? 0.5f : 1f);
                }
                long after = getAllocatedBytes();
                result.allocationMeasured = true;
                result.managementAllocatedBytes = after - before;
                assert(result.managementAllocatedBytes == 0,
                    "stable Pause/Resume/scale management allocated " + result.managementAllocatedBytes + " bytes");
            }

            string allocationSummary = result.allocationMeasured
                ? result.managementAllocatedBytes + " bytes"
                : "unmeasured (GC.GetAllocatedBytesForCurrentThread unsupported)";
            return "Game Clock checks passed (owners, pause lifecycle, scale validation, disposal; management allocation: " + allocationSummary + ").";
        }
    }
}
