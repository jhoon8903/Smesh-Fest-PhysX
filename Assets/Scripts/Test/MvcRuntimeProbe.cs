#if UNITY_EDITOR || SMESH_VALIDATION
using System;
using System.Collections;
using System.IO;
using Framework.Loop;
using Framework.Object;
using Framework.Pool;
using UnityEngine;
using VContainer;

namespace Framework.Test
{
    /// <summary>Transient, explicit checks. No game scene, prefab or gameplay state is configured here.</summary>
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
            public event Action Selected;
            public int SelectionSubscribers { get; private set; }

            public void SetValue(int value)
            {
                if (Value == value) return;
                Value = value;
                NotifyChanged();
            }

            public void AddSelection(Action callback) { Selected += callback; SelectionSubscribers++; }
            public void RemoveSelection(Action callback) { Selected -= callback; SelectionSubscribers--; }
            public void Select() => Selected?.Invoke();
        }

        public class ProbeView : ObView<StateModel>
        {
            private Action selected;
            public int RefreshCount { get; private set; }
            public int ShownValue { get; private set; }
            public int SelectionCount { get; private set; }
            public Action<StateModel> BoundAction;
            public Action<StateModel> UnboundAction;
            public Action<StateModel> RefreshAction;

            protected override void OnModelBound(StateModel model)
            {
                selected ??= OnSelected;
                model.AddSelection(selected);
                BoundAction?.Invoke(model);
            }

            protected override void OnModelUnbound(StateModel model)
            {
                model.RemoveSelection(selected);
                UnboundAction?.Invoke(model);
            }

            protected override void RefreshView(StateModel model)
            {
                RefreshCount++;
                ShownValue = model.Value;
                RefreshAction?.Invoke(model);
            }

            private void OnSelected() => SelectionCount++;
        }

        // Object-specific controller lifetime; the common Model/View code does not own this policy.
        public sealed class ProbeController : IDisposable
        {
            private readonly ILoopEvents loop;
            private readonly StateModel model;
            private readonly Action<float> tick;
            public bool IsRunning { get; private set; }
            public int TickCount { get; private set; }

            public ProbeController(ILoopEvents loop, StateModel model)
            {
                this.loop = loop;
                this.model = model;
                tick = OnTick;
            }

            public void Start()
            {
                if (IsRunning) return;
                loop.UpdateTick += tick;
                IsRunning = true;
            }

            public void Stop()
            {
                if (!IsRunning) return;
                IsRunning = false;
                loop.UpdateTick -= tick;
            }

            public void Dispose() => Stop();
            private void OnTick(float delta)
            {
                if (!IsRunning) return;
                TickCount++;
                model.SetValue(model.Value + 1);
            }
        }

        public sealed class PooledView : ProbeView, IPoolable
        {
            private ILoopEvents loop;
            public bool RecreateOnRent;
            public StateModel OwnedModel { get; private set; }
            public ProbeController Controller { get; private set; }
            public bool InjectedBeforeCreation { get; private set; }
            public GameObject PoolObject => gameObject;

            [Inject, UnityEngine.Scripting.Preserve]
            private void Construct(ILoopEvents events) => loop = events;

            public void OnPoolCreated(IPoolable owner)
            {
                InjectedBeforeCreation = loop != null;
                if (!InjectedBeforeCreation) throw new InvalidOperationException("Missing DI before creation.");
                CreateBundle();
            }

            public void OnPoolRent(PoolLease lease)
            {
                if (RecreateOnRent) { Controller.Dispose(); CreateBundle(); }
                OwnedModel.SetValue(0);
                Bind(OwnedModel);
                Controller.Start();
            }

            public void OnPoolReturn()
            {
                Controller.Stop();
                Unbind();
                OwnedModel.SetValue(0);
            }

            public void OnPoolDestroy()
            {
                Controller?.Dispose();
                Unbind();
            }

            private void CreateBundle()
            {
                OwnedModel = new StateModel();
                Controller = new ProbeController(loop, OwnedModel);
            }
        }

        private readonly Result result = new Result();
        private GameObject fixtureRoot;
        private PoolFactory factory;
        private PoolConfig[] configs;
        private IObjectResolver resolver;
        private LoopDispatcher loop;
        private string outputPath;

        public void Begin(GameObject root, PoolContainer container, PoolConfig[] poolConfigs, string output)
        {
            fixtureRoot = root;
            configs = poolConfigs;
            outputPath = output;
            StartCoroutine(Run(container));
        }

        private IEnumerator Run(PoolContainer container)
        {
            result.recordedAtUtc = DateTime.UtcNow.ToString("O");
            result.unityVersion = Application.unityVersion;
            try
            {
                CheckViewLifetime();
                CheckHooksAndExceptions();
                CheckAllocation();
                loop = new LoopDispatcher();
                loop.StartLoop();
                ContainerBuilder builder = new ContainerBuilder();
                builder.RegisterInstance<ILoopEvents>(loop);
                resolver = builder.Build();
                factory = new PoolFactory(container, resolver, true);
                factory.Initialize(false);
                CheckPool(configs[0], false);
                CheckPool(configs[1], true);

                Assert(factory.TryRent(configs[0], new PoolSpawnArgs(Vector3.zero, Quaternion.identity), out PoolLease active), "Final rent failed.");
                PooledView view = (PooledView)active.Value;
                StateModel model = view.OwnedModel;
                ProbeController controller = view.Controller;
                factory.Dispose();
                factory = null;
                Assert(!active.IsValid && model.ObserverCount == 0 && model.SelectionSubscribers == 0 && !controller.IsRunning,
                    "Factory disposal left model/controller subscriptions alive.");
                result.success = true;
            }
            catch (Exception exception) { result.error = exception.ToString(); }
            finally
            {
                try { factory?.Dispose(); }
                catch (Exception exception) { result.success = false; result.error += "\nCleanup: " + exception; }
                loop?.Dispose();
                resolver?.Dispose();
                if (fixtureRoot != null) Destroy(fixtureRoot);
                if (configs != null)
                    for (int i = 0; i < configs.Length; i++) if (configs[i] != null) Destroy(configs[i]);
            }

            yield return null;
            yield return null;
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, JsonUtility.ToJson(result, true));
            if (result.success) Debug.Log("[MvcValidation] Passed " + result.assertions + " assertions.");
            else Debug.LogError("[MvcValidation] " + result.error);
            Destroy(gameObject);
        }

        private ProbeView NewView()
        {
            GameObject target = new GameObject("__MvcValidation_View");
            target.transform.SetParent(fixtureRoot.transform, false);
            target.SetActive(false);
            return target.AddComponent<ProbeView>();
        }

        private void CheckViewLifetime()
        {
            GameObject standalone = new GameObject("__MvcValidation_Standalone");
            standalone.transform.SetParent(fixtureRoot.transform, false);
            Assert(!(standalone.AddComponent<ObView>() is IPoolable), "Base ObView requires pooling.");
            ProbeView view = NewView();
            StateModel first = new StateModel();
            StateModel second = new StateModel();
            first.SetValue(7);
            view.Bind(first);
            Assert(view.Model == first && !view.IsObserving && first.ObserverCount == 0 && view.RefreshCount == 0,
                "Inactive Bind observed or refreshed early.");
            view.gameObject.SetActive(true);
            Assert(view.IsObserving && first.ObserverCount == 1 && first.SelectionSubscribers == 1 && view.ShownValue == 7 && view.RefreshCount == 1,
                "Enable did not observe and display the current state exactly once.");
            view.Bind(first);
            Assert(first.ObserverCount == 1 && view.RefreshCount == 1, "Same Model Bind duplicated work.");
            first.SetValue(8);
            Assert(view.ShownValue == 8 && view.RefreshCount == 2, "State notification did not refresh View.");
            first.Select();
            Assert(view.SelectionCount == 1, "Custom model subscription was not connected.");
            view.Bind(second);
            Assert(first.ObserverCount == 0 && first.SelectionSubscribers == 0 && second.ObserverCount == 1 && view.Model == second,
                "Model replacement leaked the old model subscription.");
            int refreshes = view.RefreshCount;
            first.SetValue(9);
            first.Select();
            Assert(view.RefreshCount == refreshes && view.SelectionCount == 1, "Old model still drove the View.");
            view.gameObject.SetActive(false);
            Assert(view.Model == second && !view.IsObserving && second.ObserverCount == 0 && second.SelectionSubscribers == 0,
                "Disable did not suspend both standard and custom observation.");
            second.SetValue(42);
            second.Select();
            Assert(view.RefreshCount == refreshes && view.SelectionCount == 1, "Disabled View received notifications.");
            view.gameObject.SetActive(true);
            Assert(view.ShownValue == 42 && view.RefreshCount == refreshes + 1 && second.ObserverCount == 1 && second.SelectionSubscribers == 1,
                "Re-enable did not restore a single observation of latest state.");
            view.enabled = false;
            Assert(second.ObserverCount == 0 && second.SelectionSubscribers == 0, "Component disable leaked observation.");
            view.enabled = true;
            Assert(second.ObserverCount == 1 && second.SelectionSubscribers == 1, "Component re-enable duplicated observation.");
            view.Unbind();
            view.Unbind();
            Assert(view.Model == null && second.ObserverCount == 0 && second.SelectionSubscribers == 0, "Unbind was not idempotent.");
            Expect<ArgumentNullException>(() => view.Bind(null), "Null Bind was accepted.");
            view.Bind(first);
            view.gameObject.SetActive(false);
            // Destruction is deferred; OnDisable must already have disconnected both subscriptions.
            Destroy(view.gameObject);
            Assert(first.ObserverCount == 0 && first.SelectionSubscribers == 0, "Destroy/disable leaked subscription.");
        }

        private void CheckHooksAndExceptions()
        {
            ProbeView view = NewView();
            view.gameObject.SetActive(true);
            StateModel first = new StateModel();
            StateModel replacement = new StateModel();
            replacement.SetValue(21);
            view.BoundAction = model => { view.BoundAction = null; view.Unbind(); };
            view.Bind(first);
            Assert(view.Model == null && first.ObserverCount == 0 && first.SelectionSubscribers == 0 && view.RefreshCount == 0,
                "Unbind during OnModelBound produced stale observation/refresh.");
            view.Bind(first);
            view.RefreshAction = model => { view.RefreshAction = null; view.Bind(replacement); };
            first.SetValue(1);
            Assert(view.Model == replacement && first.ObserverCount == 0 && replacement.ObserverCount == 1 && view.ShownValue == 21,
                "Replacing the model inside Refresh retained the old model.");
            view.RefreshAction = model => { view.RefreshAction = null; view.Unbind(); };
            replacement.SetValue(22);
            Assert(view.Model == null && replacement.ObserverCount == 0 && replacement.SelectionSubscribers == 0,
                "Unbind during notification leaked the listener.");

            view.BoundAction = model => throw new InvalidOperationException("Expected bound failure.");
            Expect<InvalidOperationException>(() => view.Bind(first), "Binding failure did not propagate.");
            Assert(view.Model == null && first.ObserverCount == 0 && first.SelectionSubscribers == 0, "Failed binding retained subscriptions.");
            view.BoundAction = null;
            view.RefreshAction = model => throw new InvalidOperationException("Expected initial refresh failure.");
            Expect<InvalidOperationException>(() => view.Bind(first), "Initial refresh failure did not propagate.");
            Assert(view.Model == null && first.ObserverCount == 0 && first.SelectionSubscribers == 0, "Failed initial refresh retained subscriptions.");
            view.RefreshAction = null;
            view.Bind(first);
            view.UnboundAction = model => throw new InvalidOperationException("Expected unbound failure.");
            Expect<InvalidOperationException>(() => view.Unbind(), "Unbind failure did not propagate.");
            Assert(view.Model == null && first.ObserverCount == 0 && first.SelectionSubscribers == 0, "Unbind exception retained subscriptions.");
            view.UnboundAction = null;
            view.Bind(replacement);
            Assert(view.ShownValue == replacement.Value && replacement.ObserverCount == 1, "View could not reconnect after hook failure.");
            view.Unbind();

            int beforeRefresh = view.RefreshCount;
            view.BoundAction = model => { view.BoundAction = null; view.Unbind(); view.Bind(model); };
            view.Bind(first);
            Assert(view.RefreshCount == beforeRefresh + 1 && first.ObserverCount == 1 && first.SelectionSubscribers == 1,
                "Same-model reconnect during binding caused an obsolete initial refresh.");
            view.Unbind();
            beforeRefresh = view.RefreshCount;
            view.BoundAction = model => { view.BoundAction = null; view.enabled = false; view.enabled = true; };
            view.Bind(first);
            Assert(view.RefreshCount == beforeRefresh + 1 && first.ObserverCount == 1 && first.SelectionSubscribers == 1,
                "Disable/re-enable during binding caused an obsolete initial refresh.");
            view.Unbind();
            view.BoundAction = model =>
            {
                view.BoundAction = null;
                view.Unbind();
                view.Bind(model);
                throw new InvalidOperationException("Expected obsolete binding failure.");
            };
            Expect<InvalidOperationException>(() => view.Bind(first), "Obsolete binding failure did not propagate.");
            Assert(view.Model == first && view.IsObserving && first.ObserverCount == 1 && first.SelectionSubscribers == 1,
                "Obsolete failure destroyed the newly established observation.");
            view.Unbind();
        }

        private void CheckAllocation()
        {
            ProbeView view = NewView();
            view.gameObject.SetActive(true);
            StateModel model = new StateModel();
            view.Bind(model);
            for (int i = 0; i < 100; i++) model.SetValue(i);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 100; i < 1100; i++) model.SetValue(i);
            result.notificationAllocationBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            for (int i = 0; i < 100; i++) { view.Unbind(); view.Bind(model); }
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) { view.Unbind(); view.Bind(model); }
            result.reconnectionAllocationBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            result.allocationMeasured = true;
            result.allocationCondition = "Unity Editor current thread; precreated model/view/cached callbacks; 100 warmups, 1000 synchronous updates or Unbind/Bind pairs; includes a single extra model event; excludes object creation, pool rent/return, logging/assertions and real game rendering.";
            Assert(result.notificationAllocationBytes == 0, "Stable notifications allocated managed memory.");
            Assert(result.reconnectionAllocationBytes == 0, "Warmed model reconnection allocated managed memory.");
            view.Unbind();
        }

        private void CheckPool(PoolConfig config, bool recreate)
        {
            PoolSpawnArgs args = new PoolSpawnArgs(Vector3.zero, Quaternion.identity);
            Assert(factory.TryRent(config, args, out PoolLease first), "Initial pool rent failed.");
            PooledView view = (PooledView)first.Value;
            StateModel model = view.OwnedModel;
            ProbeController controller = view.Controller;
            Assert(view.InjectedBeforeCreation && view.Model == model && model.ObserverCount == 1 && controller.IsRunning,
                "DI or pooled MVC connection was incomplete before activation.");
            loop.TickUpdate(0.02f);
            Assert(model.Value == 1 && view.ShownValue == 1 && controller.TickCount == 1, "Loop -> Controller -> Model -> View path failed.");
            Assert(first.Return(), "Pool return failed.");
            Assert(view.Model == null && model.ObserverCount == 0 && model.SelectionSubscribers == 0 && !controller.IsRunning && model.Value == 0,
                "Return did not disconnect/reset the MVC bundle.");
            int refreshes = view.RefreshCount;
            int ticks = controller.TickCount;
            model.SetValue(90);
            loop.TickUpdate(0.02f);
            Assert(view.RefreshCount == refreshes && controller.TickCount == ticks, "Returned MVC still received model/loop work.");
            Assert(factory.TryRent(config, args, out PoolLease next), "Pool re-rent failed.");
            Assert(ReferenceEquals(next.Value, view), "View was not reused.");
            Assert(ReferenceEquals(model, view.OwnedModel) != recreate && ReferenceEquals(controller, view.Controller) != recreate,
                "Object-specific retain/recreate policy was overridden.");
            int beforeRefresh = view.RefreshCount;
            int beforeTicks = view.Controller.TickCount;
            loop.TickUpdate(0.02f);
            Assert(view.OwnedModel.Value == 1 && view.ShownValue == 1 && view.RefreshCount == beforeRefresh + 1 && view.Controller.TickCount == beforeTicks + 1,
                "Re-rent caused duplicate update/refresh or stale state.");
            Assert(!first.Return() && next.IsValid, "Old lease returned the new rental.");
            Assert(next.Return(), "Second return failed.");
        }

        private void Assert(bool condition, string message)
        {
            result.assertions++;
            if (!condition) throw new InvalidOperationException(message);
        }

        private void Expect<T>(Action action, string message) where T : Exception
        {
            try { action(); }
            catch (T) { result.assertions++; return; }
            throw new InvalidOperationException(message);
        }
    }
}
#endif
