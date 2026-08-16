using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.IoC;
using UniTx.Widgets;
using UnityEngine;

namespace UniTx.Widgets.Samples
{
    /// <summary>
    /// Data handed to the confirm dialog when it is pushed.
    /// </summary>
    public sealed class ConfirmDialogData : IWidgetData
    {
        public string Message;
        public Action OnConfirmed;
    }

    /// <summary>
    /// A screen with no data. Put this on the prefab root.
    /// </summary>
    /// <remarks>
    /// <b>Setup:</b> the prefab must be Addressable, and an <c>AssetData</c> asset must map
    /// the widget's <b>type name</b> to it — the manager looks up <c>nameof(MainMenuWidget)</c>.
    /// </remarks>
    public sealed class MainMenuWidget : MonoBehaviour, IWidget
    {
        /// <inheritdoc />
        public GameObject GameObject => gameObject;

        /// <inheritdoc />
        public Transform Transform => transform;

        /// <summary>
        /// Called after the prefab is spawned, injected and given its data.
        /// </summary>
        public void Initialize() => Debug.Log("Main menu opened");

        /// <summary>
        /// Called just before the instance is released. Undo everything Initialize did.
        /// </summary>
        public void Reset() => Debug.Log("Main menu closed");
    }

    /// <summary>
    /// A screen that receives typed data and resolves services.
    /// </summary>
    public sealed class ConfirmDialogWidget : MonoBehaviour, IWidget<ConfirmDialogData>, IInjectable
    {
        private IClock _clock;

        /// <inheritdoc />
        public ConfirmDialogData Data { get; private set; }

        /// <inheritdoc />
        public GameObject GameObject => gameObject;

        /// <inheritdoc />
        public Transform Transform => transform;

        /// <inheritdoc />
        public void SetData(IWidgetData widgetData) => Data = (ConfirmDialogData)widgetData;

        /// <inheritdoc />
        public void Inject(IResolver resolver) => resolver.TryResolve(out _clock);

        /// <inheritdoc />
        public void Initialize() => Debug.Log($"Confirm: {Data?.Message} (at {_clock?.UtcNow:T})");

        /// <inheritdoc />
        public void Reset() => Data = null;

        /// <summary>
        /// Wire this to the dialog's confirm button.
        /// </summary>
        public void OnConfirmPressed()
        {
            Data?.OnConfirmed?.Invoke();
            UniWidgets.PopAsync().Forget();
        }
    }

    /// <summary>
    /// Driving the widget stack, including back-button integration.
    /// </summary>
    public sealed class WidgetStackSample : MonoBehaviour
    {
        private IUnityEventListener _listener;
        private CancellationTokenSource _cts;

        private async void Start()
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

            // UniTxStep does this during bootstrap. It needs UniTxConfig.WidgetsAssetDataKey
            // to point at an Addressable AssetData asset.
            if (!UniWidgets.IsInitialized) await UniWidgets.InitializeAsync(_cts.Token);

            UniWidgets.OnPush += type => Debug.Log($"Pushed {type.Name} (depth {UniWidgets.Count})");
            UniWidgets.OnPop += type => Debug.Log($"Popped {type.Name} (depth {UniWidgets.Count})");

            // The hardware back button should pop the stack, not quit the app.
            if (IoCStatics.Resolver.TryResolve(out _listener))
            {
                _listener.OnBackButtonPressed += HandleBack;
            }

            await UniWidgets.PushAsync<MainMenuWidget>(_cts.Token);
        }

        private void OnDestroy()
        {
            if (_listener != null) _listener.OnBackButtonPressed -= HandleBack;

            _cts.SafeCancelAndDispose();
        }

        /// <summary>
        /// Pushes the confirm dialog with typed data.
        /// </summary>
        [ContextMenu("Push Confirm Dialog")]
        public void PushConfirm()
        {
            var data = new ConfirmDialogData
            {
                Message = "Spend 100 coins to continue?",
                OnConfirmed = () => Debug.Log("Purchase confirmed"),
            };

            UniWidgets.PushAsync<ConfirmDialogWidget>(data, _cts.Token).Forget();
        }

        private void HandleBack()
        {
            // Nothing open means the back press belongs to the app, not the UI.
            if (UniWidgets.Count == 0)
            {
                Debug.Log("Nothing to pop — show a quit prompt.");
                return;
            }

            // Guard against popping the root screen and leaving a blank canvas.
            if (UniWidgets.Count == 1 && UniWidgets.IsOpen<MainMenuWidget>()) return;

            UniWidgets.PopAsync(_cts.Token).Forget();
        }

        /// <summary>
        /// Closes every open screen, e.g. before a scene change.
        /// </summary>
        [ContextMenu("Pop All")]
        public void PopAll() => UniWidgets.PopAllAsync(_cts.Token).Forget();
    }
}
