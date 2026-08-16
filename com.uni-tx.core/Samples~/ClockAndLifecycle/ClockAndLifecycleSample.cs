using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniTx.Core.Samples
{
    /// <summary>
    /// <see cref="IClock"/> for tamper-resistant timers, and <see cref="IUnityEventListener"/>
    /// for lifecycle callbacks without making every service a MonoBehaviour.
    /// </summary>
    public sealed class ClockAndLifecycleSample : MonoBehaviour
    {
        [Tooltip("Sync against server time instead of the device clock. Costs one HTTPS " +
                 "request at startup but survives the player changing their clock.")]
        [SerializeField] private bool _useServerClock = true;

        private IClock _clock;
        private UnityEventListener _listener;
        private CancellationTokenSource _cts;
        private long _rewardReadyAt;

        private async void Start()
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            // Bootstrap normally binds these; created inline so the sample runs standalone.
            _listener = new UnityEventListener();
            _listener.Initialize();

            if (_useServerClock)
            {
                var serverClock = new ServerClock();
                await serverClock.InitializeAsync(_cts.Token);

                // Falls back to device time rather than blocking startup forever, so check
                // this before trusting it for anything valuable.
                Debug.Log($"Server sync succeeded: {serverClock.IsSynchronized}");
                _clock = serverClock;
            }
            else
            {
                _clock = new LocalClock();
            }

            // A daily-reward gate. Storing an absolute timestamp rather than a countdown
            // means closing the app does not pause it.
            _rewardReadyAt = _clock.UnixTimestampNow + 60;

            _listener.OnUpdate += TickRewardTimer;
            _listener.OnPause += HandlePause;
            _listener.OnFocus += HandleFocus;
            _listener.OnQuit += HandleQuit;

            // Bound to the project-wide UI/Cancel action, which Unity 6 maps to the Android
            // hardware back button, Escape and the gamepad cancel button.
            _listener.OnBackButtonPressed += HandleBack;
        }

        private void OnDestroy()
        {
            if (_listener != null)
            {
                _listener.OnUpdate -= TickRewardTimer;
                _listener.OnPause -= HandlePause;
                _listener.OnFocus -= HandleFocus;
                _listener.OnQuit -= HandleQuit;
                _listener.OnBackButtonPressed -= HandleBack;
                _listener.Reset();
            }

            _cts.SafeCancelAndDispose();
        }

        private void TickRewardTimer()
        {
            var remaining = _rewardReadyAt - _clock.UnixTimestampNow;

            if (remaining > 0 && remaining % 10 == 0) Debug.Log($"Reward in {remaining}s");
        }

        // On mobile this is the last callback you can rely on before the OS may kill the
        // process — flush saves and pause audio here, not in OnQuit.
        private static void HandlePause(bool isPaused) => Debug.Log($"Paused: {isPaused}");

        private static void HandleFocus(bool hasFocus) => Debug.Log($"Focused: {hasFocus}");

        private static void HandleQuit() => Debug.Log("Quitting.");

        private static void HandleBack() => Debug.Log("Back pressed — pop a screen here.");
    }
}
