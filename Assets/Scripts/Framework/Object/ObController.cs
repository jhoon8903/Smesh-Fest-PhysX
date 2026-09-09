using System;
using System.Runtime.ExceptionServices;

namespace Framework.Object
{
    public abstract class ObController<TView, TModel> : IDisposable
        where TView : ObView
        where TModel : ObModel
    {
        private readonly Action<ObModel> _modelChangedHandler;
        private bool _active;
        private bool _observing;
        private bool _viewLifecycleSubscribed;
        private bool _disposed;

        protected ObController(TView view, TModel model)
        {
            View = view != null ? view : throw new ArgumentNullException(nameof(view));
            Model = model ?? throw new ArgumentNullException(nameof(model));
            _modelChangedHandler = HandleModelChanged;
        }

        protected TView View { get; }
        public TModel Model { get; }
        public bool IsObserving => _observing;
        public bool IsDisposed => _disposed;

        internal event Action<ObView> Disposed;

        protected void Activate()
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            if (_active) throw new InvalidOperationException($"{GetType().Name} was already activated.");
            if (View == null) throw new InvalidOperationException($"{GetType().Name} requires a live View.");

            SubscribeViewLifecycle();
            _active = true;
            if (!View.isActiveAndEnabled) return;

            try
            {
                StartObserving();
                OnViewEnabled();
            }
            catch
            {
                _active = false;
                StopObserving();
                UnsubscribeViewLifecycle();
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _active = false;
            Exception failure = null;

            try { StopObserving(); }
            catch (Exception exception) { failure = exception; }

            try { DisposeController(); }
            catch (Exception exception) { failure ??= exception; }

            UnsubscribeViewLifecycle();

            try { Disposed?.Invoke(View); }
            catch (Exception exception) { failure ??= exception; }

            Disposed = null;
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        protected virtual void OnViewEnabled() { }
        protected virtual void OnViewDisabled() { }
        protected virtual void DisposeController() { }
        protected abstract void RefreshView(TModel model);

        private void HandleViewEnabled()
        {
            if (!_active || _disposed) return;
            StartObserving();
            OnViewEnabled();
        }

        private void HandleViewDisabled()
        {
            if (!_active || _disposed) return;
            Exception failure = null;
            try { OnViewDisabled(); }
            catch (Exception exception) { failure = exception; }
            try { StopObserving(); }
            catch (Exception exception) { failure ??= exception; }
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private void HandleViewDestroying()
        {
            Dispose();
        }

        private void SubscribeViewLifecycle()
        {
            if (_viewLifecycleSubscribed) return;
            View.Enabled += HandleViewEnabled;
            View.Disabled += HandleViewDisabled;
            View.Destroying += HandleViewDestroying;
            _viewLifecycleSubscribed = true;
        }

        private void UnsubscribeViewLifecycle()
        {
            if (!_viewLifecycleSubscribed) return;
            _viewLifecycleSubscribed = false;
            View.Enabled -= HandleViewEnabled;
            View.Disabled -= HandleViewDisabled;
            View.Destroying -= HandleViewDestroying;
        }

        private void StartObserving()
        {
            if (_observing || _disposed || !_active || View == null || !View.isActiveAndEnabled) return;
            if (!Model.Subscribe(_modelChangedHandler)) throw new InvalidOperationException($"{GetType().Name} duplicated its Model observation.");
            _observing = true;

            try { RefreshView(Model); }
            catch
            {
                StopObserving();
                throw;
            }
        }

        private void StopObserving()
        {
            if (!_observing) return;
            _observing = false;
            Model.Unsubscribe(_modelChangedHandler);
        }

        private void HandleModelChanged(ObModel changedModel)
        {
            if (!_observing || _disposed || View == null || !View.isActiveAndEnabled
                || !ReferenceEquals(changedModel, Model)) return;
            RefreshView(Model);
        }
    }
}
