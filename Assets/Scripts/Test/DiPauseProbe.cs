#if UNITY_EDITOR || SMESH_VALIDATION
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DG.Tweening;
using Framework.Loop;
using Framework.Screen;
using InGame.DI;
using InGame.Flow;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Framework.Test
{
    /// <summary>Explicit Play Mode probe for the DI pause chain. Excluded from ordinary Player builds.</summary>
    public sealed class DiPauseProbe : MonoBehaviour
    {
        [Serializable]
        private sealed class ValidationResult
        {
            public string recordedAtUtc;
            public string unityVersion;
            public bool isEditor;
            public bool success;
            public int assertions;
            public string error;
            public int updateTicks;
            public int fixedTicks;
            public int lateTicks;
            public int elapsedFrames;
            public float initialTimeScale;
            public float finalTimeScale;
        }

        public GameLifetimeScope Scope;
        public bool RunOnStart;
        public bool QuitOnCompletion;
        public string OutputPath;

        private ValidationResult _result;
        private GameClock _clock;
        private ILoopEvents _loopEvents;
        private LoopDispatcher _dispatcher;
        private UpdateLoop _updateLoop;
        private Action<float> _updateHandler;
        private Action<float> _fixedHandler;
        private Action<float> _lateHandler;
        private GameObject _rigidbodyObject;
        private GameObject _particleObject;
        private Tween _tween;
        private float _tweenValue;
        private bool _completed;
        private int _startFrame;
        private readonly List<GameObject> _uiRoots = new();

        private void Start()
        {
            if (RunOnStart) Begin(Scope, OutputPath, QuitOnCompletion);
        }

        public void Begin(GameLifetimeScope scope, string outputPath, bool quitOnCompletion = false)
        {
            if (_completed) return;

            Scope = scope;
            OutputPath = ResolveOutputPath(outputPath);
            QuitOnCompletion = ResolveQuitOnCompletion(quitOnCompletion);
            _startFrame = Time.frameCount;
            StartCoroutine(GuardedRun());
        }

        private IEnumerator GuardedRun()
        {
            IEnumerator routine = RunChecks();
            while (true)
            {
                object current;
                bool moved;
                try
                {
                    moved = routine.MoveNext();
                    current = moved ? routine.Current : null;
                }
                catch (Exception exception)
                {
                    Complete(false, exception.ToString());
                    yield break;
                }

                if (!moved) break;
                yield return current;
            }

            Complete(true, null);
        }

        private IEnumerator RunChecks()
        {
            _result = new ValidationResult
            {
                recordedAtUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                isEditor = Application.isEditor,
                initialTimeScale = Time.timeScale
            };
            Assert(Scope != null, "GameLifetimeScope is required.");
            Assert(Scope.Container != null, "GameLifetimeScope container was not built.");

            _clock = Scope.Container.Resolve<GameClock>();
            _loopEvents = Scope.Container.Resolve<ILoopEvents>();
            _dispatcher = Scope.Container.Resolve<LoopDispatcher>();
            _updateLoop = Scope.Container.Resolve<UpdateLoop>();
            GameFlow flow = Scope.Container.Resolve<GameFlow>();
            Assert(_clock != null && _loopEvents != null && _dispatcher != null && _updateLoop != null && flow != null,
                "Required DI registrations did not resolve.");

            _updateHandler = delegate(float value) { _result.updateTicks++; };
            _fixedHandler = delegate(float value) { _result.fixedTicks++; };
            _lateHandler = delegate(float value) { _result.lateTicks++; };
            _loopEvents.UpdateTick += _updateHandler;
            _loopEvents.FixedTick += _fixedHandler;
            _loopEvents.LateTick += _lateHandler;

            yield return null;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert(_clock.IsRunning && _updateLoop.IsRunning && _dispatcher.IsRunning,
                "GameFlow bootstrap did not start the injected loop.");
            Assert(_result.updateTicks > 0 && _result.fixedTicks > 0 && _result.lateTicks > 0,
                "All three Unity loop phases did not reach ILoopEvents.");

            int updatesBeforeLoopDisable = _result.updateTicks;
            int fixedBeforeLoopDisable = _result.fixedTicks;
            int lateBeforeLoopDisable = _result.lateTicks;
            _updateLoop.enabled = false;
            yield return null;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert(_clock.IsRunning && !_updateLoop.IsRunning && !_dispatcher.IsRunning &&
                _result.updateTicks == updatesBeforeLoopDisable && _result.fixedTicks == fixedBeforeLoopDisable &&
                _result.lateTicks == lateBeforeLoopDisable,
                "Disabling UpdateLoop changed game intent or continued dispatching.");
            _updateLoop.enabled = true;
            yield return null;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert(_clock.IsRunning && _updateLoop.IsRunning && _dispatcher.IsRunning &&
                _result.updateTicks > updatesBeforeLoopDisable && _result.fixedTicks > fixedBeforeLoopDisable &&
                _result.lateTicks > lateBeforeLoopDisable,
                "Re-enabling UpdateLoop did not resume all phase dispatches.");

            flow.enabled = false;
            yield return null;
            Assert(!_clock.IsRunning && !_updateLoop.IsRunning && !_dispatcher.IsRunning,
                "Disabling GameFlow did not stop the game loop.");
            flow.enabled = true;
            yield return null;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert(_clock.IsRunning && _updateLoop.IsRunning && _dispatcher.IsRunning,
                "Re-enabling GameFlow did not restart the game loop.");

            Rigidbody body = CreateRuntimeFixtures();
            GameObject firstUi = CreateInjectedPauseRoot("__DiPauseValidation_UI_One", false);
            GameObject secondUi = CreateInjectedPauseRoot("__DiPauseValidation_UI_Two", false);
            yield return null;
            yield return null;
            Assert(_clock.PauseCount == 2 && Mathf.Approximately(Time.timeScale, 0f),
                "Two injected UI roots did not pause game time.");

            Vector3 pausedPosition = body.position;
            float pausedTween = _tweenValue;
            float pausedParticleTime = GetParticleSystem().time;
            int pausedTickCount = _result.updateTicks + _result.fixedTicks + _result.lateTicks;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert(body.position == pausedPosition && Mathf.Approximately(_tweenValue, pausedTween) &&
                Mathf.Approximately(GetParticleSystem().time, pausedParticleTime) &&
                _result.updateTicks + _result.fixedTicks + _result.lateTicks == pausedTickCount,
                "Loop ticks, physics, scaled tween, or scaled particle advanced while paused.");

            firstUi.SetActive(false);
            yield return null;
            Assert(_clock.PauseCount == 1 && Mathf.Approximately(Time.timeScale, 0f),
                "Closing one nested UI root released the remaining pause.");
            _clock.SetTimeScale(1.5f);
            Assert(Mathf.Approximately(Time.timeScale, 0f), "Paused scale change changed effective Unity time.");
            secondUi.SetActive(false);
            yield return null;
            Assert(_clock.PauseCount == 0 && Mathf.Approximately(Time.timeScale, 1.5f),
                "Closing the final UI root did not restore requested scale.");

            int updateBeforeResume = _result.updateTicks;
            Vector3 positionBeforeResume = body.position;
            float tweenBeforeResume = _tweenValue;
            float particleBeforeResume = GetParticleSystem().time;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert(_result.updateTicks > updateBeforeResume && body.position.x > positionBeforeResume.x &&
                _tweenValue > tweenBeforeResume && GetParticleSystem().time > particleBeforeResume,
                "Ticks or scaled runtime fixtures did not resume.");

            GameObject activeInjectedUi = CreateInjectedPauseRoot("__DiPauseValidation_UI_ActiveInject", true);
            yield return null;
            Assert(_clock.PauseCount == 1 && Mathf.Approximately(Time.timeScale, 0f),
                "Active UI injection did not acquire pause ownership.");
            _clock.SetTimeScale(2f);
            Assert(Mathf.Approximately(Time.timeScale, 0f), "Paused speed change changed effective Unity time.");
            activeInjectedUi.SetActive(false);
            yield return null;
            Assert(_clock.PauseCount == 0 && Mathf.Approximately(Time.timeScale, 2f),
                "Disabled active-injected UI did not release pause ownership.");
            activeInjectedUi.SetActive(true);
            yield return null;
            Assert(_clock.PauseCount == 1 && Mathf.Approximately(Time.timeScale, 0f),
                "Paused speed change was not retained while reopening UI.");
            activeInjectedUi.SetActive(false);
            yield return null;
            Assert(_clock.PauseCount == 0 && Mathf.Approximately(Time.timeScale, 2f),
                "Final UI release did not restore the paused requested scale.");

            GameObject synchronousCloseUi = CreateInjectedPauseRoot("__DiPauseValidation_UI_SynchronousClose", false);
            synchronousCloseUi.SetActive(false);
            Action closeOnPause = delegate
            {
                if (_clock.IsPaused && synchronousCloseUi.activeSelf) synchronousCloseUi.SetActive(false);
            };
            _clock.StateChanged += closeOnPause;
            synchronousCloseUi.SetActive(true);
            yield return null;
            _clock.StateChanged -= closeOnPause;
            Assert(!synchronousCloseUi.activeSelf && _clock.PauseCount == 0,
                "Synchronous UI close during Pause notification retained ownership.");

            GameObject destroyedUi = CreateInjectedPauseRoot("__DiPauseValidation_UI_Destroy", false);
            yield return null;
            Assert(_clock.PauseCount == 1, "Destroy fixture did not acquire pause ownership.");
            Destroy(destroyedUi);
            yield return null;
            Assert(_clock.PauseCount == 0, "Destroying UI holder did not release pause ownership.");

            int ticksBeforeDispose = _result.updateTicks + _result.fixedTicks + _result.lateTicks;
            Scope.DisposeCore();
            _dispatcher.TickUpdate(1f);
            _dispatcher.TickFixed(1f);
            _dispatcher.TickLate(1f);
            yield return null;
            Assert(Mathf.Approximately(Time.timeScale, 1f), "Scope DisposeCore did not restore Unity time scale to 1.");
            Assert(_result.updateTicks + _result.fixedTicks + _result.lateTicks == ticksBeforeDispose,
                "Disposed dispatcher accepted a Tick.");
        }

        private Rigidbody CreateRuntimeFixtures()
        {
            _rigidbodyObject = new GameObject("__DiPauseValidation_Rigidbody");
            Rigidbody body = _rigidbodyObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.linearVelocity = Vector3.right;

            _particleObject = new GameObject("__DiPauseValidation_Particle");
            ParticleSystem particle = _particleObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particle.main;
            main.useUnscaledTime = false;
            ParticleSystem.EmissionModule emission = particle.emission;
            emission.enabled = true;
            emission.rateOverTime = 1f;
            particle.Play();

            _tweenValue = 0f;
            _tween = DOTween.To(delegate { return _tweenValue; }, delegate(float value) { _tweenValue = value; }, 10f, 10f)
                .SetUpdate(false);
            return body;
        }

        private GameObject CreateInjectedPauseRoot(string name, bool injectWhileActive)
        {
            GameObject root = new GameObject(name);
            if (!injectWhileActive) root.SetActive(false);
            root.AddComponent<ScreenPauseScope>();
            Scope.Container.InjectGameObject(root);
            if (!injectWhileActive) root.SetActive(true);
            _uiRoots.Add(root);
            return root;
        }

        private ParticleSystem GetParticleSystem()
        {
            return _particleObject.GetComponent<ParticleSystem>();
        }

        private void Assert(bool condition, string message)
        {
            _result.assertions++;
            if (!condition) throw new InvalidOperationException(message);
        }

        private void Complete(bool success, string error)
        {
            if (_completed) return;
            _completed = true;
            if (_result == null)
                _result = new ValidationResult { recordedAtUtc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion, isEditor = Application.isEditor };
            _result.success = success;
            _result.error = error;
            _result.elapsedFrames = Time.frameCount - _startFrame;
            _result.finalTimeScale = Time.timeScale;
            CleanupFixtures();
            WriteResult();
            if (QuitOnCompletion) Application.Quit(success ? 0 : 1);
        }

        private void CleanupFixtures()
        {
            if (_loopEvents != null)
            {
                _loopEvents.UpdateTick -= _updateHandler;
                _loopEvents.FixedTick -= _fixedHandler;
                _loopEvents.LateTick -= _lateHandler;
            }
            if (_tween != null) _tween.Kill();
            if (_rigidbodyObject != null) Destroy(_rigidbodyObject);
            if (_particleObject != null) Destroy(_particleObject);
            for (int i = 0; i < _uiRoots.Count; i++)
            {
                if (_uiRoots[i] != null) Destroy(_uiRoots[i]);
            }
        }

        private void WriteResult()
        {
            if (string.IsNullOrEmpty(OutputPath)) return;
            string directory = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(OutputPath, JsonUtility.ToJson(_result, true));
        }

        private static string ResolveOutputPath(string configuredPath)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (arguments[i] == "-smesh-validation-output") return arguments[i + 1];
            }
            if (!string.IsNullOrEmpty(configuredPath)) return configuredPath;
            return Path.Combine(Application.dataPath, "../Docs/Prototype/evidence/raw/di-pause-playmode.json");
        }

        private static bool ResolveQuitOnCompletion(bool configuredValue)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length; i++)
            {
                if (arguments[i] == "-smesh-validation-quit") return true;
            }
            return configuredValue;
        }
    }
}
#endif
