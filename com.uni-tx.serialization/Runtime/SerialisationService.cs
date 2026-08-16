using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.IoC;

namespace UniTx.Serialization
{
    /// <summary>
    /// Batches dirty saves and flushes them on an interval, on pause and on quit.
    /// </summary>
    public sealed class SerialisationService : ISerialisationService, IInjectable, IInitializable, IResettable
    {
        private readonly Serialiser _serialiser = new();

        private IUnityEventListener _eventListener;
        private CancellationTokenSource _cts;
        private float _interval = 5f;

        /// <summary>
        /// Gets how many entries are waiting to be written.
        /// </summary>
        public int PendingCount => _serialiser.DirtyCount;

        /// <summary>
        /// Gets the directory save files live in.
        /// </summary>
        public string SaveDirectoryPath => Serialiser.SaveDirectoryPath;

        /// <inheritdoc />
        public void Inject(IResolver resolver)
        {
            if (resolver != null) resolver.TryResolve(out _eventListener);
        }

        /// <summary>
        /// Starts the autosave loop and hooks the mobile lifecycle flush points.
        /// </summary>
        public void Initialize()
        {
            _interval = UniStatics.Config?.SaveInterval ?? 5f;
            _cts = new CancellationTokenSource();
            SaveLoopAsync(_cts.Token).Forget();

            if (_eventListener == null)
            {
                UniStatics.LogWarning(
                    "No IUnityEventListener bound, so saves will not be flushed on pause or quit. " +
                    "Pending changes can be lost when the OS kills a backgrounded app.", this);
                return;
            }

            // OnPause is the last callback Android and iOS reliably deliver before the
            // process may be killed — flushing only on an interval loses up to one interval
            // of progress every time the player backgrounds the game.
            _eventListener.OnPause += OnPause;
            _eventListener.OnQuit += OnQuit;
        }

        /// <summary>
        /// Flushes pending saves, stops the loop and clears the cache.
        /// </summary>
        public void Reset()
        {
            if (_eventListener != null)
            {
                _eventListener.OnPause -= OnPause;
                _eventListener.OnQuit -= OnQuit;
            }

            // Flush before tearing down, or everything written since the last batch is lost.
            Flush();

            _cts.SafeCancelAndDispose();
            _cts = null;
            _serialiser.Reset();
        }

        /// <inheritdoc />
        public void Save(ISavedData data)
        {
            if (data?.Id == null)
            {
                UniStatics.LogError("Cannot save: data or its Id is null.", this);
                return;
            }

            _serialiser.MarkDirty(data);
        }

        /// <inheritdoc />
        public T Load<T>(string id)
            where T : ISavedData, new()
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Save id cannot be null or empty.", nameof(id));

            return _serialiser.Deserialise<T>(id);
        }

        /// <inheritdoc />
        public int Flush() => _serialiser.SerialiseDirty();

        /// <inheritdoc />
        public void Delete(string id) => _serialiser.Delete(id);

        private void OnPause(bool isPaused)
        {
            if (isPaused) Flush();
        }

        // Adapters: Flush returns a count, the lifecycle events are plain Action.
        private void OnQuit() => Flush();

        private async UniTaskVoid SaveLoopAsync(CancellationToken cToken)
        {
            try
            {
                while (!cToken.IsCancellationRequested)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(_interval), DelayType.UnscaledDeltaTime,
                        cancellationToken: cToken);

                    // Async here, blocking in Flush. The periodic autosave must not stall a
                    // frame; the pause/quit paths must finish before the process can die.
                    await _serialiser.SerialiseDirtyAsync(cToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
            catch (Exception ex)
            {
                UniStatics.LogError($"Autosave loop stopped: {ex.Message}", this);
                UniStatics.LogException(ex, this);
            }
        }
    }
}
