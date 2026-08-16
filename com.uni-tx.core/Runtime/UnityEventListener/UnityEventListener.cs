using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UniTx.Core
{
    /// <summary>
    /// Default <see cref="IUnityEventListener"/>, backed by one hidden DontDestroyOnLoad object.
    /// </summary>
    public sealed class UnityEventListener : IUnityEventListener, IInitializable, IResettable
    {
        private GameObject _sceneObject;

        /// <inheritdoc />
        public event Action OnUpdate;

        /// <inheritdoc />
        public event Action OnLateUpdate;

        /// <inheritdoc />
        public event Action OnFixedUpdate;

        /// <inheritdoc />
        public event Action<bool> OnPause;

        /// <inheritdoc />
        public event Action<bool> OnFocus;

        /// <inheritdoc />
        public event Action OnQuit;

        /// <inheritdoc />
        public event Action OnLowMemory;

        /// <inheritdoc />
        public event Action OnBackButtonPressed;

        /// <summary>
        /// Creates the hidden driver object. Safe to call twice.
        /// </summary>
        public void Initialize()
        {
            if (_sceneObject != null) return;

            _sceneObject = new GameObject("[UniTx] UnityEventListener")
            {
                // Keep it out of the hierarchy and out of saved scenes: it is an
                // implementation detail, not something a designer should find or edit.
                hideFlags = HideFlags.HideAndDontSave,
            };
            Object.DontDestroyOnLoad(_sceneObject);

            var behaviour = _sceneObject.AddComponent<UnityEventBehaviour>();
            behaviour.SetListener(this);

            // Application.lowMemory is a plain static event, not a MonoBehaviour message, so
            // it is hooked here rather than on the driver object.
            Application.lowMemory += BroadcastOnLowMemory;
        }

        /// <summary>
        /// Destroys the driver object and drops every subscriber.
        /// </summary>
        public void Reset()
        {
            Application.lowMemory -= BroadcastOnLowMemory;

            if (_sceneObject != null)
            {
                Object.Destroy(_sceneObject);
                _sceneObject = null;
            }

            // Without this, a service that forgot to unsubscribe keeps the listener — and
            // everything it closes over — alive across a scene reload.
            OnUpdate = null;
            OnLateUpdate = null;
            OnFixedUpdate = null;
            OnPause = null;
            OnFocus = null;
            OnQuit = null;
            OnLowMemory = null;
            OnBackButtonPressed = null;
        }

        internal void BroadcastOnUpdate() => OnUpdate.SafeInvoke();

        internal void BroadcastOnLateUpdate() => OnLateUpdate.SafeInvoke();

        internal void BroadcastOnFixedUpdate() => OnFixedUpdate.SafeInvoke();

        internal void BroadcastOnPause(bool isPaused) => OnPause.SafeInvoke(isPaused);

        internal void BroadcastOnFocus(bool hasFocus) => OnFocus.SafeInvoke(hasFocus);

        internal void BroadcastOnQuit() => OnQuit.SafeInvoke();

        internal void BroadcastOnLowMemory() => OnLowMemory.SafeInvoke();

        internal void BroadcastOnBackButtonPressed() => OnBackButtonPressed.SafeInvoke();
    }
}
