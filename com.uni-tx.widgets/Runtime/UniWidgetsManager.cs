using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;
using UniTx.IoC;
using UniTx.Resources;
using UnityEngine;

namespace UniTx.Widgets
{
    /// <summary>
    /// Default widget manager: a stack of screens spawned from an <see cref="AssetData"/> map.
    /// </summary>
    internal sealed class UniWidgetsManager : IWidgetsManager, IDisposable
    {
        // Instance-scoped, not static. A static semaphore shared by every manager was
        // disposed by the first Reset(), so any manager created afterwards threw
        // ObjectDisposedException on its first push.
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly Stack<IWidget> _stack = new();
        private readonly IResolver _resolver;

        private AssetData _assetData;
        private Transform _spawnPoint;
        private bool _isDisposed;

        /// <inheritdoc />
        public event Action<Type> OnPush;

        /// <inheritdoc />
        public event Action<Type> OnPop;

        /// <inheritdoc />
        public int Count => _stack.Count;

        /// <summary>
        /// Creates the manager, resolving dependencies from the global container.
        /// </summary>
        public UniWidgetsManager() : this(IoCStatics.IsInitialized ? IoCStatics.Resolver : null)
        {
        }

        /// <summary>
        /// Creates the manager with an explicit resolver.
        /// </summary>
        /// <param name="resolver">Resolver used to inject spawned widgets. May be null.</param>
        public UniWidgetsManager(IResolver resolver) => _resolver = resolver;

        /// <inheritdoc />
        public async UniTask InitializeAsync(CancellationToken cToken = default)
        {
            var key = UniStatics.Config?.WidgetsAssetDataKey;

            if (string.IsNullOrEmpty(key))
            {
                throw new InvalidOperationException(
                    "UniTxConfig.WidgetsAssetDataKey is not set, so widget prefabs cannot be resolved. " +
                    "Assign a UniTxConfig to UniTxStep or place one at Resources/UniTxConfig.");
            }

            _assetData = await UniResources.LoadAssetAsync<AssetData>(key, cToken: cToken);
        }

        /// <inheritdoc />
        public UniTask PushAsync<TWidgetType>(CancellationToken cToken = default)
            where TWidgetType : IWidget
            => PushInternalAsync<TWidgetType>(null, cToken);

        /// <inheritdoc />
        public UniTask PushAsync<TWidgetType>(IWidgetData widgetData, CancellationToken cToken = default)
            where TWidgetType : IWidget, IWidgetDataReceiver
            => PushInternalAsync<TWidgetType>(widgetData, cToken);

        /// <inheritdoc />
        public async UniTask PopAsync(CancellationToken cToken = default)
        {
            ThrowIfDisposed();

            await _semaphore.WaitAsync(cToken);

            try
            {
                if (!_stack.TryPop(out var widget)) return;

                var widgetType = widget.GetType();
                widget.Reset();
                UniResources.DisposeInstance(widget.GameObject);
                OnPop.SafeInvoke(widgetType);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc />
        public async UniTask PopAllAsync(CancellationToken cToken = default)
        {
            // Loop on PopAsync rather than draining inside one lock, so each pop still raises
            // OnPop and a cancellation midway leaves the stack in a consistent state.
            while (_stack.Count > 0)
            {
                cToken.ThrowIfCancellationRequested();
                await PopAsync(cToken);
            }
        }

        /// <inheritdoc />
        public IWidget Peek() => _stack.TryPeek(out var widget) ? widget : null;

        /// <inheritdoc />
        public bool IsOpen<TWidgetType>()
            where TWidgetType : IWidget
        {
            foreach (var widget in _stack)
            {
                if (widget is TWidgetType) return true;
            }

            return false;
        }

        /// <summary>
        /// Releases the push/pop lock.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            _semaphore.Dispose();
        }

        private async UniTask PushInternalAsync<TWidgetType>(IWidgetData widgetData, CancellationToken cToken)
            where TWidgetType : IWidget
        {
            ThrowIfDisposed();

            if (_assetData == null)
            {
                throw new InvalidOperationException(
                    "UniWidgetsManager was not initialized. Await InitializeAsync() first.");
            }

            await _semaphore.WaitAsync(cToken);

            try
            {
                var widgetType = typeof(TWidgetType);
                var asset = _assetData.GetAsset(widgetType.Name);
                var parent = GetSpawnPoint();

                var widget = await UniResources.CreateInstanceAsync<IWidget>(
                    asset.RuntimeKey, parent, null, cToken);

                if (_resolver != null && widget is IInjectable injectable)
                {
                    injectable.Inject(_resolver);
                }

                if (widgetData != null && widget is IWidgetDataReceiver receiver)
                {
                    receiver.SetData(widgetData);
                }

                widget.Initialize();
                _stack.Push(widget);
                OnPush.SafeInvoke(widgetType);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private Transform GetSpawnPoint()
        {
            if (_spawnPoint != null) return _spawnPoint;

            var parentTag = UniStatics.Config?.WidgetsParentTag;

            if (string.IsNullOrEmpty(parentTag)) return null;

            // FindGameObjectWithTag throws UnityException for a tag that is not defined in
            // the Tag Manager, which is a different failure from "tag exists but nothing
            // carries it" — both need to degrade to spawning at the canvas root.
            GameObject go;

            try
            {
                go = GameObject.FindGameObjectWithTag(parentTag);
            }
            catch (UnityException)
            {
                UniStatics.LogWarning(
                    $"Tag '{parentTag}' is not defined in the Tag Manager; widgets will spawn unparented.", this);
                return null;
            }

            if (go == null)
            {
                UniStatics.LogWarning(
                    $"No GameObject tagged '{parentTag}' found; widgets will spawn unparented.", this);
                return null;
            }

            return _spawnPoint = go.transform;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(UniWidgetsManager));
        }
    }
}
