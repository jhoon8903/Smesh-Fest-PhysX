namespace Framework.Observer
{
    public sealed class Observable<T>
    {
        private readonly System.Collections.Generic.List<System.Action<T>> _listeners = new();
        private int _count;
        private bool _isPublishing;
        private bool _needsCompaction;
        public int Count => _count;

        public bool Subscribe(System.Action<T> listener)
        {
            if (listener == null) throw new System.ArgumentNullException(nameof(listener));
            for (int i = 0; i < _listeners.Count; i++)
            {
                System.Action<T> current = _listeners[i];
                if (current != null && current == listener) return false;
            }
            _listeners.Add(listener);
            _count++;
            return true;
        }

        public bool Unsubscribe(System.Action<T> listener)
        {
            if (listener == null) return false;
            for (int i = 0; i < _listeners.Count; i++)
            {
                if (_listeners[i] != listener) continue;
                _count--;
                if (_isPublishing)
                {
                    _listeners[i] = null;
                    _needsCompaction = true;
                }
                else _listeners.RemoveAt(i);
                return true;
            }
            return false;
        }

        public void Publish(T value)
        {
            if (_isPublishing) throw new System.InvalidOperationException("Observable does not support reentrant Publish calls.");
            _isPublishing = true;
            int publishCount = _listeners.Count;

            try
            {
                for (int i = 0; i < publishCount; i++)
                {
                    System.Action<T> listener = _listeners[i];
                    listener?.Invoke(value);
                }
            }
            finally
            {
                _isPublishing = false;
                if (_needsCompaction)
                {
                    CompactRemovedListeners();
                    _needsCompaction = false;
                }
            }
        }

        public void Clear()
        {
            _count = 0;

            if (!_isPublishing)
            {
                _listeners.Clear();
                return;
            }

            for (int i = 0; i < _listeners.Count; i++) _listeners[i] = null;
            _needsCompaction = true;
        }

        private void CompactRemovedListeners()
        {
            int writeIndex = 0;
            int listenerCount = _listeners.Count;
            for (int readIndex = 0; readIndex < listenerCount; readIndex++)
            {
                System.Action<T> listener = _listeners[readIndex];
                if (listener == null) continue;
                if (writeIndex != readIndex) _listeners[writeIndex] = listener;
                writeIndex++;
            }
            if (writeIndex < listenerCount) _listeners.RemoveRange(writeIndex, listenerCount - writeIndex);
        }
    }
}
