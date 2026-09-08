using System;
using System.Runtime.ExceptionServices;
using UnityEngine;

namespace Framework.Object
{
    /// <summary>
    /// An <see cref="ObView"/> that observes one model while this component is active and enabled.
    /// Bind while inactive only retains the model reference; OnEnable begins observation and performs the first refresh.
    /// Derived Unity lifecycle callbacks must call their base implementation. Bind, Unbind, and model callbacks are
    /// for the Unity main thread only.
    /// </summary>
    public abstract class ObView<TModel> : ObView where TModel : ObModel
    {
        private TModel _model;
        private TModel _observedModel;
        private Action<ObModel> _modelChangedHandler;
        private int _bindingVersion;
        private uint _observationEpoch;

        /// <summary>The retained model, including while this component is disabled.</summary>
        public TModel Model => _model;

        /// <summary>Whether this active component currently has its model change listener registered.</summary>
        public bool IsObserving => _observedModel != null;

        /// <summary>
        /// Retains <paramref name="model"/> and starts observing it immediately when active and enabled.
        /// Rebinding the same instance is a no-op.
        /// </summary>
        public void Bind(TModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (ReferenceEquals(_model, model)) return;

            int bindingVersion = ++_bindingVersion;
            TModel previousModel = _model;
            _model = null;

            if (previousModel != null)
                StopObserving(previousModel);

            // A lifecycle hook may have selected another model. Its nested request owns the final state.
            if (bindingVersion != _bindingVersion)
                return;

            _model = model;
            if (isActiveAndEnabled)
                StartObserving(model);
        }

        /// <summary>
        /// Stops observation and releases the retained model reference. This is separate from disabling the component,
        /// which intentionally keeps <see cref="Model"/> for a later OnEnable.
        /// </summary>
        public void Unbind()
        {
            TModel model = _model;
            if (model == null) return;

            ++_bindingVersion;
            _model = null;
            StopObserving(model);
        }

        /// <summary>
        /// Starts observing a retained model after the component becomes active. Derived implementations must call base.
        /// </summary>
        protected virtual void OnEnable()
        {
            TModel model = _model;
            if (model != null)
                StartObserving(model);
        }

        /// <summary>
        /// Stops observing without releasing the retained model. Derived implementations must call base.
        /// </summary>
        protected virtual void OnDisable()
        {
            TModel model = _model;
            if (model != null)
                StopObserving(model);
        }

        /// <summary>Releases the model as the component is destroyed. Derived implementations must call base.</summary>
        protected virtual void OnDestroy() => Unbind();

        /// <summary>Called once whenever active observation of a model begins.</summary>
        protected virtual void OnModelBound(TModel model) { }

        /// <summary>Called once whenever active observation of a model ends.</summary>
        protected virtual void OnModelUnbound(TModel model) { }

        /// <summary>Renders the model's latest state after binding and after each change notification.</summary>
        protected abstract void RefreshView(TModel model);

        private void StartObserving(TModel model)
        {
            if (!isActiveAndEnabled || !ReferenceEquals(_model, model) || _observedModel != null)
                return;

            _modelChangedHandler ??= HandleModelChanged;
            model.Subscribe(_modelChangedHandler);
            _observedModel = model;
            uint observationEpoch = ++_observationEpoch;

            try
            {
                OnModelBound(model);
                if (CanRefresh(model, observationEpoch))
                    RefreshView(model);
            }
            catch (Exception exception)
            {
                CleanupFailedInitialObservation(model, observationEpoch, exception);
            }
        }

        private void StopObserving(TModel model)
        {
            if (!ReferenceEquals(_observedModel, model))
                return;

            // Clear state first: hooks can Bind/Unbind or disable this component without leaving an old subscription.
            _observedModel = null;
            ++_observationEpoch;
            Exception failure = null;

            try { model.Unsubscribe(_modelChangedHandler); }
            catch (Exception exception) { failure = exception; }

            try { OnModelUnbound(model); }
            catch (Exception exception) { failure ??= exception; }

            if (failure != null)
                ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private void CleanupFailedInitialObservation(TModel model, uint observationEpoch, Exception initialFailure)
        {
            // Hooks may have ended and restarted observation of the same model (ABA). A failed older setup must not
            // tear down that newer connection or clear its retained reference.
            if (!IsCurrentObservation(model, observationEpoch))
            {
                ExceptionDispatchInfo.Capture(initialFailure).Throw();
                return;
            }

            // The retained reference is cleared before hooks run, so a nested Bind becomes the new owner.
            if (ReferenceEquals(_model, model))
                _model = null;

            try
            {
                StopObserving(model);
            }
            catch
            {
                // The initial setup error is the actionable failure; cleanup has already cleared internal state.
            }

            ExceptionDispatchInfo.Capture(initialFailure).Throw();
        }

        private void HandleModelChanged(ObModel changedModel)
        {
            TModel model = _model;
            if (model == null || !ReferenceEquals(changedModel, model) ||
                !ReferenceEquals(_observedModel, model) || !isActiveAndEnabled)
                return;

            RefreshView(model);
        }

        private bool CanRefresh(TModel model, uint observationEpoch) =>
            isActiveAndEnabled && ReferenceEquals(_model, model) && IsCurrentObservation(model, observationEpoch);

        private bool IsCurrentObservation(TModel model, uint observationEpoch) =>
            observationEpoch == _observationEpoch && ReferenceEquals(_observedModel, model);
    }
}
