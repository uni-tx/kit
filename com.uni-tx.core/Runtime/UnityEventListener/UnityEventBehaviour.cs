using UnityEngine;
using UnityEngine.InputSystem;

namespace UniTx.Core
{
    /// <summary>
    /// MonoBehaviour that forwards Unity lifecycle events to a <see cref="UnityEventListener"/>.
    /// </summary>
    /// <remarks>
    /// The back button is read from the project-wide <c>UI/Cancel</c> action
    /// (<see cref="InputSystem.actions"/>), which Unity 6 binds to the Android hardware
    /// back button, the Escape key and the gamepad cancel button out of the box. That
    /// avoids requiring a <c>PlayerInput</c> component, which would otherwise be added
    /// with no actions asset assigned and never fire.
    /// </remarks>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    internal sealed class UnityEventBehaviour : MonoBehaviour
    {
        private const string CancelActionPath = "UI/Cancel";

        private UnityEventListener _listener;
        private InputAction _backAction;

        public void SetListener(UnityEventListener listener) => _listener = listener;

        private void OnEnable()
        {
            _backAction = ResolveBackAction();

            if (_backAction == null) return;

            // Project-wide actions are enabled by default, but a project may have disabled
            // the UI map — enable just the one action rather than the whole asset.
            if (!_backAction.enabled) _backAction.Enable();
            _backAction.performed += OnBackPerformed;
        }

        private void OnDisable()
        {
            if (_backAction == null) return;

            _backAction.performed -= OnBackPerformed;
            _backAction = null;
        }

        private static InputAction ResolveBackAction()
        {
            var actions = InputSystem.actions;

            if (actions == null)
            {
                UniStatics.LogInfo(
                    "No project-wide Input Actions asset assigned " +
                    "(Edit ▸ Project Settings ▸ Input System Package), so IUnityEventListener." +
                    "OnBackButtonPressed will never fire.", null, Color.yellow);
                return null;
            }

            var action = actions.FindAction(CancelActionPath);

            if (action == null)
            {
                UniStatics.LogInfo(
                    $"Project-wide Input Actions has no '{CancelActionPath}' action, so " +
                    "IUnityEventListener.OnBackButtonPressed will never fire.", null, Color.yellow);
            }

            return action;
        }

        private void OnBackPerformed(InputAction.CallbackContext context)
            => _listener.BroadcastOnBackButtonPressed();

        private void Update() => _listener.BroadcastOnUpdate();
        private void LateUpdate() => _listener.BroadcastOnLateUpdate();
        private void FixedUpdate() => _listener.BroadcastOnFixedUpdate();
        private void OnApplicationPause(bool pauseStatus) => _listener.BroadcastOnPause(pauseStatus);
        private void OnApplicationFocus(bool hasFocus) => _listener.BroadcastOnFocus(hasFocus);
        private void OnApplicationQuit() => _listener.BroadcastOnQuit();
    }
}
