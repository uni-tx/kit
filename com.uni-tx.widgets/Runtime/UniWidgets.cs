using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace UniTx.Widgets
{
    /// <summary>
    /// Static facade over the kit's widget stack.
    /// </summary>
    public static class UniWidgets
    {
        private static IWidgetsManager _widgetsManager;

        /// <summary>
        /// Indicates whether the widget manager has been initialized.
        /// </summary>
        public static bool IsInitialized => _widgetsManager != null;

        /// <summary>
        /// Gets how many widgets are on the stack.
        /// </summary>
        public static int Count => _widgetsManager?.Count ?? 0;

        /// <summary>
        /// Raised when a widget is pushed onto the stack.
        /// </summary>
        public static event Action<Type> OnPush
        {
            add => Manager.OnPush += value;
            remove
            {
                if (_widgetsManager != null) _widgetsManager.OnPush -= value;
            }
        }

        /// <summary>
        /// Raised when a widget is popped off the stack.
        /// </summary>
        public static event Action<Type> OnPop
        {
            add => Manager.OnPop += value;
            remove
            {
                if (_widgetsManager != null) _widgetsManager.OnPop -= value;
            }
        }

        /// <summary>
        /// Initializes the default widget manager.
        /// </summary>
        /// <param name="cToken">Token to cancel initialization.</param>
        public static UniTask InitializeAsync(CancellationToken cToken = default)
            => InitializeAsync(new UniWidgetsManager(), cToken);

        /// <summary>
        /// Initializes with a custom widget manager.
        /// </summary>
        /// <param name="widgetsManager">The manager to install.</param>
        /// <param name="cToken">Token to cancel initialization.</param>
        public static UniTask InitializeAsync(IWidgetsManager widgetsManager, CancellationToken cToken = default)
        {
            if (_widgetsManager != null)
            {
                throw new InvalidOperationException(
                    "UniWidgets is already initialized. Call Reset() before initializing again.");
            }

            _widgetsManager = widgetsManager ?? throw new ArgumentNullException(nameof(widgetsManager));

            return _widgetsManager.InitializeAsync(cToken);
        }

        /// <summary>
        /// Releases the widget manager.
        /// </summary>
        public static void Reset()
        {
            if (_widgetsManager == null) return;

            if (_widgetsManager is IDisposable disposable) disposable.Dispose();

            _widgetsManager = null;
        }

        /// <summary>
        /// Pushes a widget of the given type.
        /// </summary>
        /// <typeparam name="TWidgetType">The widget type to push.</typeparam>
        /// <param name="cToken">Token to cancel the push.</param>
        public static UniTask PushAsync<TWidgetType>(CancellationToken cToken = default)
            where TWidgetType : IWidget
            => Manager.PushAsync<TWidgetType>(cToken);

        /// <summary>
        /// Pushes a widget of the given type with data.
        /// </summary>
        /// <typeparam name="TWidgetType">The widget type to push.</typeparam>
        /// <param name="widgetData">Data handed to the widget before it initializes.</param>
        /// <param name="cToken">Token to cancel the push.</param>
        public static UniTask PushAsync<TWidgetType>(IWidgetData widgetData, CancellationToken cToken = default)
            where TWidgetType : IWidget, IWidgetDataReceiver
            => Manager.PushAsync<TWidgetType>(widgetData, cToken);

        /// <summary>
        /// Pops the top widget off the stack.
        /// </summary>
        /// <param name="cToken">Token to cancel the pop.</param>
        public static UniTask PopAsync(CancellationToken cToken = default) => Manager.PopAsync(cToken);

        /// <summary>
        /// Pops every widget off the stack, top first.
        /// </summary>
        /// <param name="cToken">Token to cancel the operation.</param>
        public static UniTask PopAllAsync(CancellationToken cToken = default) => Manager.PopAllAsync(cToken);

        /// <summary>
        /// Returns the top widget without removing it, or null when the stack is empty.
        /// </summary>
        public static IWidget Peek() => _widgetsManager?.Peek();

        /// <summary>
        /// Indicates whether a widget of the given type is anywhere on the stack.
        /// </summary>
        /// <typeparam name="TWidgetType">The widget type to look for.</typeparam>
        public static bool IsOpen<TWidgetType>()
            where TWidgetType : IWidget
            => _widgetsManager?.IsOpen<TWidgetType>() ?? false;

        private static IWidgetsManager Manager => _widgetsManager
            ?? throw new InvalidOperationException(
                "UniWidgets is not initialized. Call UniWidgets.InitializeAsync() first — " +
                "UniTxStep does this during bootstrap.");
    }
}
