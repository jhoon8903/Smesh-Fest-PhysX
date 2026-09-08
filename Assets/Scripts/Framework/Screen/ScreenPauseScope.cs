using System;
using Framework.Loop;
using UnityEngine;
using VContainer;

namespace Framework.Screen
{
    /// <summary>Put on a screen root: active screens own a game-pause request, with no UI clock.</summary>
    [DisallowMultipleComponent]
    public sealed class ScreenPauseScope : MonoBehaviour
    {
        private IGamePause _pause;
        private bool _held;

        [Inject, UnityEngine.Scripting.Preserve]
        private void Construct(IGamePause pause)
        {
            if (pause == null) throw new ArgumentNullException(nameof(pause));
            if (!ReferenceEquals(_pause, pause))
            {
                Release();
                _pause = pause;
            }
            AcquireIfOpen();
        }

        private void OnEnable()
        {
            AcquireIfOpen();
        }

        private void OnDisable()
        {
            Release();
        }

        private void OnDestroy()
        {
            Release();
        }

        private void AcquireIfOpen()
        {
            if (_held || _pause == null || !isActiveAndEnabled) return;
            // Notification can synchronously close this screen; OnDisable must already own the request.
            _held = true;
            _pause.Pause(this);
        }

        private void Release()
        {
            if (!_held) return;
            _held = false;
            _pause.Resume(this);
        }
    }
}
