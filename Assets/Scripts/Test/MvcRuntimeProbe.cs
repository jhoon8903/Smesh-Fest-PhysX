#if UNITY_EDITOR || SMESH_VALIDATION
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Framework.Loop;
using Framework.Object;
using Framework.Pool;
using UnityEngine;
using VContainer;

namespace Framework.Test
{
    /// <summary>Play Mode evidence for Controller-owned model observation and pool composition.</summary>
    public sealed class MvcRuntimeProbe : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public string recordedAtUtc;
            public string unityVersion;
            public bool success;
            public int assertions;
            public string error;
            public long notificationAllocationBytes;
            public long reconnectionAllocationBytes;
            public bool allocationMeasured;
            public string allocationCondition;
        }

        public sealed class StateModel : ObModel
        {
            public int Value { get; private set; }
            public int SelectionSubscribers { get; private set; }
            public event Action Selected;

            public void SetValue(int value)
            {
                if (Value == value) return;
                Value = value;
                NotifyChanged();
            }

            public void AddSelection(Action callback)
            {
                Selected += callback;
                SelectionSubscribers++;
            }

            public void RemoveSelection(Action callback)
            {
                Selected -= callback;
                SelectionSubscribers--;
            }

            public void Select() => Selected?.Invoke();
        }

        public class ProbeView : ObView
        {
            public int RefreshCount { get; private set; }
            public int ShownValue { get; private set; }
            public int SelectionCount { get; private set; }
            public Action<StateModel> RefreshAction { get; set; }

            internal void Show(StateModel model)
            {
                RefreshCount++;
                ShownValue = model.Value;
                RefreshAction?.Invoke(model);
            }

            internal void ShowSelection() => SelectionCount++;
        }

        public sealed class ProbeController : ObController<ProbeView, StateModel>
        {
            private readonly ILoopEvents _loop;
            private readonly Action<float> _tick;
            private readonly Action _selected;
            private readonly PooledView _pooledView;
            private bool _running;

            public ProbeController(ProbeView view, StateModel model, ILoopEvents loop)
                : base(view, model)
            {
                _loop = loop ?? throw new ArgumentNullException(nameof(loop));
                _tick = HandleTick;
                _selected = HandleSelected;
                _pooledView = view as PooledView;
                if (_pooledView != null) _pooledView.Created += HandlePoolCreated;

                try
                {
                    Activate();
                }
                catch
                {
                    try { Dispose(); }
                    catch { }
                    throw;
                }
            }

            public int TickCount { get; private set; }
            public bool IsRunning => _running;
            public bool PoolCreationObserved { get; private set; }

            protected override void OnViewEnabled()
            {
                if (_running) throw new InvalidOperationException("ProbeController duplicated its active subscriptions.");
                _loop.UpdateTick += _tick;
                Model.AddSelection(_selected);
                _running = true;
            }

            protected override void OnViewDisabled() => StopRunning();

            protected override void DisposeController()
            {
                StopRunning();
                if (_pooledView != null) _pooledView.Created -= HandlePoolCreated;
            }

            protected override void RefreshView(StateModel model) => View.Show(model);

            private void HandleTick(float deltaTime)
            {
                if (!_running) return;
                TickCount++;
                Model.SetValue(Model.Value + 1);
            }

            private void HandleSelected()
            {
                if (_running) View.ShowSelection();
            }

            private void HandlePoolCreated() => PoolCreationObserved = true;

            private void StopRunning()
            {
                if (!_running) return;
                _running = false;
                _loop.UpdateTick -= _tick;
                Model.RemoveSelection(_selected);
            }
        }

        public sealed class PooledView : ProbeView, IPoolable
        {
            public bool FailNextRent { get; set; }
            public GameObject PoolObject => gameObject;
            internal event Action Created;

            public void OnPoolCreated(IPoolable owner)
            {
                if (!ReferenceEquals(owner, this)) throw new InvalidOperationException("PooledView must own its PoolObject.");
                if (Created == null) throw new InvalidOperationException("PooledView requires Controller composition before OnPoolCreated.");
                Created.Invoke();
            }

            public void OnPoolRent(PoolLease lease)
            {
                if (!lease.IsValid) throw new InvalidOperationException("PooledView requires the current PoolLease.");
                if (!FailNextRent) return;
                FailNextRent = false;
                throw new InvalidOperationException("Expected rental preparation failure.");
            }

            public void OnPoolReturn() { }
            public void OnPoolDestroy() => NotifyDestroying();
        }

        private readonly Result _result = new Result();
        private readonly Dictionary<ProbeView, ProbeController> _controllers = new Dictionary<ProbeView, ProbeController>();
        private GameObject _fixtureRoot;
        private PoolFactory _factory;
        private PoolConfig[] _configs;
        private IObjectResolver _resolver;
        private LoopDispatcher _loop;
        private string _outputPath;

        public void Begin(GameObject root, PoolContainer container, PoolConfig[] poolConfigs, string output)
        {
            _fixtureRoot = root;
            _configs = poolConfigs;
            _outputPath = output;
            StartCoroutine(Run(container));
        }

        private IEnumerator Run(PoolContainer container)
        {
            _result.recordedAtUtc = DateTime.UtcNow.ToString("O");
            _result.unityVersion = Application.unityVersion;
            try
            {
                CheckStandaloneControllerLifetime();
                CheckRefreshFailureAndReentrantDispose();
                CheckAllocation();
                _factory = new PoolFactory(container, _resolver, new TestPoolObjectComposer(Compose), true);
                _factory.Initialize(false);
                CheckComposerRequiredBeforeCreate();
                CheckPoolReuse(_configs[0]);
                CheckPoolReuse(_configs[1]);
                CheckInvalidRentRollback(_configs[1]);
                CheckHierarchyFirstDestroy(_configs[1]);
                CheckActiveFactoryDispose(_configs[0]);
                _result.success = true;
            }
            catch (Exception exception)
            {
                _result.success = false;
                _result.error = exception.ToString();
            }
            finally
            {
                try { _factory?.Dispose(); }
                catch (Exception exception) { RecordCleanupFailure("PoolFactory", exception); }
                DisposeRemainingControllers();
                _loop?.Dispose();
                _resolver?.Dispose();
                if (_fixtureRoot != null) Destroy(_fixtureRoot);
                if (_configs != null)
                {
                    for (int i = 0; i < _configs.Length; i++)
                    {
                        if (_configs[i] != null) Destroy(_configs[i]);
                    }
                }
            }

            yield return null;
            yield return null;
            Directory.CreateDirectory(Path.GetDirectoryName(_outputPath));
            File.WriteAllText(_outputPath, JsonUtility.ToJson(_result, true));
            if (_result.success) Debug.Log("[MvcValidation] Passed " + _result.assertions + " assertions.");
            else Debug.LogError("[MvcValidation] " + _result.error);
            Destroy(gameObject);
        }

        private void Compose(IPoolable owner)
        {
            if (!(owner is PooledView view)) throw new InvalidOperationException("MVC validation only composes PooledView owners.");
            if (_controllers.ContainsKey(view)) throw new InvalidOperationException("PooledView already has a Controller.");
            ProbeController controller = new ProbeController(view, new StateModel(), _loop);
            try
            {
                _controllers.Add(view, controller);
                controller.Disposed += HandleControllerDisposed;
            }
            catch
            {
                controller.Dispose();
                throw;
            }
        }

        private void HandleControllerDisposed(ObView view) => _controllers.Remove((ProbeView)view);

        private ProbeView NewView(string name)
        {
            GameObject target = new GameObject(name);
            target.transform.SetParent(_fixtureRoot.transform, false);
            target.SetActive(false);
            return target.AddComponent<ProbeView>();
        }

        private void CheckStandaloneControllerLifetime()
        {
            ProbeView view = NewView("__MvcValidation_Standalone");
            Assert(!(view is IPoolable), "Standalone ObView unexpectedly requires pooling.");
            StateModel model = new StateModel();
            model.SetValue(7);
            ProbeController controller = new ProbeController(view, model, RequireLoop());
            Assert(ReferenceEquals(controller.Model, model) && !controller.IsObserving && !controller.IsRunning
                   && model.ObserverCount == 0 && model.SelectionSubscribers == 0 && view.RefreshCount == 0,
                "An inactive View began Controller-owned observation or refresh early.");

            view.gameObject.SetActive(true);
            Assert(controller.IsObserving && controller.IsRunning && model.ObserverCount == 1
                   && model.SelectionSubscribers == 1 && view.ShownValue == 7 && view.RefreshCount == 1,
                "Enable did not establish one Controller-owned observation and initial refresh.");
            model.SetValue(7);
            Assert(view.RefreshCount == 1, "An unchanged Model value triggered a duplicate refresh.");
            model.SetValue(8);
            Assert(view.ShownValue == 8 && view.RefreshCount == 2, "Model notification did not flow through Controller to View.");
            model.Select();
            Assert(view.SelectionCount == 1, "Controller-owned custom Model observation was not connected.");

            view.gameObject.SetActive(false);
            Assert(ReferenceEquals(controller.Model, model) && !controller.IsObserving && !controller.IsRunning
                   && model.ObserverCount == 0 && model.SelectionSubscribers == 0,
                "Disable did not suspend standard and custom Controller observation.");
            int disabledRefreshes = view.RefreshCount;
            model.SetValue(42);
            model.Select();
            _loop.TickUpdate(.02f);
            Assert(view.RefreshCount == disabledRefreshes && view.SelectionCount == 1 && controller.TickCount == 0,
                "A disabled View still received Model or Loop work.");

            view.gameObject.SetActive(true);
            Assert(view.ShownValue == 42 && view.RefreshCount == disabledRefreshes + 1 && controller.IsObserving
                   && model.ObserverCount == 1 && model.SelectionSubscribers == 1,
                "Re-enable did not restore one observation of the latest state.");
            view.enabled = false;
            Assert(!controller.IsObserving && model.ObserverCount == 0 && model.SelectionSubscribers == 0,
                "Component disable leaked Controller observation.");
            view.enabled = true;
            Assert(controller.IsObserving && model.ObserverCount == 1 && model.SelectionSubscribers == 1,
                "Component re-enable duplicated or omitted Controller observation.");

            int shownBeforeDispose = view.ShownValue;
            controller.Dispose();
            controller.Dispose();
            model.SetValue(100);
            model.Select();
            _loop.TickUpdate(.02f);
            Assert(controller.IsDisposed && !controller.IsObserving && !controller.IsRunning
                   && model.ObserverCount == 0 && model.SelectionSubscribers == 0 && view.ShownValue == shownBeforeDispose,
                "Controller Dispose was not idempotent or left active subscriptions.");
            Destroy(view.gameObject);
        }

        private void CheckRefreshFailureAndReentrantDispose()
        {
            ProbeView failedView = NewView("__MvcValidation_RefreshFailure");
            failedView.gameObject.SetActive(true);
            StateModel failedModel = new StateModel();
            failedView.RefreshAction = _ => throw new InvalidOperationException("Expected initial refresh failure.");
            Expect<InvalidOperationException>(() => new ProbeController(failedView, failedModel, RequireLoop()),
                "Initial Controller refresh failure did not propagate.");
            Assert(failedModel.ObserverCount == 0 && failedModel.SelectionSubscribers == 0,
                "Failed Controller activation retained Model subscriptions.");
            failedView.RefreshAction = null;
            ProbeController recovered = new ProbeController(failedView, failedModel, _loop);
            Assert(recovered.IsObserving && failedModel.ObserverCount == 1,
                "View could not reconnect after a failed Controller activation.");
            recovered.Dispose();
            Destroy(failedView.gameObject);

            ProbeView reentrantView = NewView("__MvcValidation_ReentrantDispose");
            reentrantView.gameObject.SetActive(true);
            StateModel reentrantModel = new StateModel();
            ProbeController reentrantController = new ProbeController(reentrantView, reentrantModel, _loop);
            reentrantView.RefreshAction = _ =>
            {
                reentrantView.RefreshAction = null;
                reentrantController.Dispose();
            };
            reentrantModel.SetValue(1);
            Assert(reentrantController.IsDisposed && reentrantModel.ObserverCount == 0
                   && reentrantModel.SelectionSubscribers == 0,
                "Dispose during Model notification left Controller observation connected.");
            Destroy(reentrantView.gameObject);
        }

        private void CheckAllocation()
        {
            ProbeView view = NewView("__MvcValidation_Allocation");
            view.gameObject.SetActive(true);
            StateModel model = new StateModel();
            ProbeController controller = new ProbeController(view, model, RequireLoop());
            for (int i = 0; i < 100; i++) model.SetValue(i);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 100; i < 1100; i++) model.SetValue(i);
            _result.notificationAllocationBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            for (int i = 0; i < 100; i++)
            {
                view.enabled = false;
                view.enabled = true;
            }
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                view.enabled = false;
                view.enabled = true;
            }
            _result.reconnectionAllocationBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            _result.allocationMeasured = true;
            _result.allocationCondition = "Unity Editor current thread; precreated Model/View/Controller and cached callbacks; 100 warmups, then 1000 synchronous notifications or View disable/enable observation pairs; excludes object creation, pool rent/return, logging/assertions, and rendering.";
            Assert(_result.notificationAllocationBytes == 0, "Stable Controller-owned Model notifications allocated managed memory.");
            Assert(_result.reconnectionAllocationBytes == 0, "Warmed View observation reconnection allocated managed memory.");
            controller.Dispose();
            Destroy(view.gameObject);
        }

        private void CheckComposerRequiredBeforeCreate()
        {
            GameObject target = new GameObject("__MvcValidation_Uncomposed");
            target.transform.SetParent(_fixtureRoot.transform, false);
            target.SetActive(false);
            PooledView view = target.AddComponent<PooledView>();
            Expect<InvalidOperationException>(() => view.OnPoolCreated(view),
                "PooledView accepted OnPoolCreated before Controller composition.");
            Destroy(target);
        }

        private void CheckPoolReuse(PoolConfig config)
        {
            PoolSpawnArgs args = new PoolSpawnArgs(Vector3.zero, Quaternion.identity);
            IPool pool = _factory.GetPool(config);
            Assert(pool.CountAll == 1 && pool.CountInactive == 1 && pool.CountActive == 0,
                "Prewarm did not create one inactive composed PooledView.");
            Assert(_factory.TryRent(config, args, out PoolLease first), "Initial pool rent failed.");
            PooledView view = first.Value as PooledView;
            ProbeController controller = null;
            Assert(view != null && _controllers.TryGetValue(view, out controller), "Pool composer did not expose the Controller it owns.");
            StateModel model = controller.Model;
            Assert(controller.PoolCreationObserved && controller.IsObserving && controller.IsRunning
                   && model.ObserverCount == 1 && model.SelectionSubscribers == 1,
                "Controller composition did not precede creation and activation.");
            _loop.TickUpdate(.02f);
            Assert(model.Value == 1 && view.ShownValue == 1 && controller.TickCount == 1,
                "Loop to Controller to Model to View path failed.");
            model.Select();
            Assert(view.SelectionCount == 1, "Pooled Controller did not own the custom Model observation.");

            Assert(first.Return(), "Initial PoolLease return failed.");
            Assert(!first.IsValid && !controller.IsObserving && !controller.IsRunning
                   && model.ObserverCount == 0 && model.SelectionSubscribers == 0,
                "Pool return did not suspend Controller-owned observations.");
            int refreshes = view.RefreshCount;
            int ticks = controller.TickCount;
            model.SetValue(90);
            model.Select();
            _loop.TickUpdate(.02f);
            Assert(view.RefreshCount == refreshes && view.SelectionCount == 1 && controller.TickCount == ticks,
                "Returned MVC bundle still received Model or Loop work.");

            Assert(_factory.TryRent(config, args, out PoolLease second), "Pool re-rent failed.");
            Assert(ReferenceEquals(second.Value, view) && _controllers.TryGetValue(view, out ProbeController reused)
                   && ReferenceEquals(reused, controller) && ReferenceEquals(reused.Model, model),
                "Re-rent did not reuse the same View, Controller, and Model.");
            int beforeRefresh = view.RefreshCount;
            int beforeTicks = controller.TickCount;
            _loop.TickUpdate(.02f);
            Assert(model.Value == 91 && view.ShownValue == 91 && view.RefreshCount == beforeRefresh + 1
                   && controller.TickCount == beforeTicks + 1,
                "Re-rent caused duplicate work or lost retained Model state.");
            Assert(!first.Return() && second.IsValid, "A stale PoolLease returned the newer rental.");
            Assert(second.Return(), "Current re-rent PoolLease return failed.");
        }

        private void CheckInvalidRentRollback(PoolConfig config)
        {
            PoolSpawnArgs args = new PoolSpawnArgs(Vector3.zero, Quaternion.identity);
            Assert(_factory.TryRent(config, args, out PoolLease first), "Invalid-rent fixture initial rent failed.");
            PooledView view = first.Value as PooledView;
            ProbeController controller = null;
            Assert(view != null && _controllers.TryGetValue(view, out controller),
                "Invalid-rent fixture was not composed.");
            StateModel model = controller.Model;
            view.FailNextRent = true;
            Assert(first.Return(), "Invalid-rent fixture could not be returned before failure injection.");

            PoolLease failedLease = default;
            Expect<InvalidOperationException>(() => _factory.TryRent(config, args, out failedLease),
                "Rental preparation failure did not propagate.");
            Assert(!failedLease.IsValid && controller.IsDisposed && model.ObserverCount == 0
                   && model.SelectionSubscribers == 0 && !_controllers.ContainsKey(view)
                   && _factory.GetPool(config).CountAll == 0,
                "Failed rent was not rolled back and quarantined with Controller cleanup.");
            Assert(_factory.TryRent(config, args, out PoolLease recoveredLease),
                "Pool did not recover after quarantining the failed rental.");
            PooledView recoveredView = recoveredLease.Value as PooledView;
            Assert(recoveredView != null && !ReferenceEquals(recoveredView, view)
                   && _controllers.TryGetValue(recoveredView, out ProbeController recoveredController)
                   && !ReferenceEquals(recoveredController, controller) && recoveredController.IsObserving,
                "Recovered rent reused the quarantined bundle or missed composition.");
            Assert(recoveredLease.Return(), "Recovered rental return failed.");
        }

        private void CheckHierarchyFirstDestroy(PoolConfig config)
        {
            Assert(_factory.TryRent(config, new PoolSpawnArgs(Vector3.zero, Quaternion.identity), out PoolLease lease),
                "Hierarchy-first rent failed.");
            PooledView view = lease.Value as PooledView;
            ProbeController controller = null;
            Assert(view != null && _controllers.TryGetValue(view, out controller),
                "Hierarchy-first fixture was not composed.");
            StateModel model = controller.Model;
            DestroyImmediate(view.gameObject);
            Assert(controller.IsDisposed && !controller.IsObserving && !controller.IsRunning
                   && model.ObserverCount == 0 && model.SelectionSubscribers == 0
                   && !_controllers.ContainsKey(view) && !lease.IsValid && !lease.Return(),
                "Hierarchy-first destruction left Controller, Model, or stale lease state alive.");
        }

        private void CheckActiveFactoryDispose(PoolConfig config)
        {
            Assert(_factory.TryRent(config, new PoolSpawnArgs(Vector3.zero, Quaternion.identity), out PoolLease lease),
                "Active-dispose rent failed.");
            PooledView view = lease.Value as PooledView;
            ProbeController controller = null;
            Assert(view != null && _controllers.TryGetValue(view, out controller),
                "Active-dispose fixture was not composed.");
            StateModel model = controller.Model;

            _factory.Dispose();
            _factory = null;
            Assert(!lease.IsValid && controller.IsDisposed && !controller.IsObserving
                   && !controller.IsRunning && model.ObserverCount == 0
                   && model.SelectionSubscribers == 0,
                "Active PoolFactory disposal left MVC state alive.");
            Assert(_controllers.Count == 0, "Active PoolFactory disposal left Controller ownership entries alive.");
        }

        private LoopDispatcher RequireLoop()
        {
            if (_loop != null) return _loop;
            _loop = new LoopDispatcher();
            _loop.StartLoop();
            _resolver = new ContainerBuilder().Build();
            return _loop;
        }

        private void DisposeRemainingControllers()
        {
            ProbeController[] controllers = new ProbeController[_controllers.Count];
            _controllers.Values.CopyTo(controllers, 0);
            for (int i = 0; i < controllers.Length; i++)
            {
                try { controllers[i].Dispose(); }
                catch (Exception exception) { RecordCleanupFailure("ProbeController", exception); }
            }
            _controllers.Clear();
        }

        private void Assert(bool condition, string message)
        {
            _result.assertions++;
            if (!condition) throw new InvalidOperationException(message);
        }

        private void Expect<T>(Action action, string message) where T : Exception
        {
            try { action(); }
            catch (T)
            {
                _result.assertions++;
                return;
            }
            throw new InvalidOperationException(message);
        }

        private void RecordCleanupFailure(string label, Exception exception)
        {
            _result.success = false;
            _result.error = (_result.error ?? string.Empty) + "\nCleanup " + label + ": " + exception;
        }
    }
}
#endif
