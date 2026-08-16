using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniTx.Core;

namespace UniTx.Widgets
{
    /// <summary>
    /// Manages the UI widget stack: push, pop and peek.
    /// </summary>
    public interface IWidgetsManager : IInitializableAsync
    {
        /// <summary>
        /// Raised when a widget is pushed onto the stack.
        /// </summary>
        event Action<Type> OnPush;

        /// <summary>
        /// Raised when a widget is popped off the stack.
        /// </summary>
        event Action<Type> OnPop;

        /// <summary>
        /// Gets how many widgets are on the stack.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Pushes a widget of the given type.
        /// </summary>
        /// <typeparam name="TWidgetType">The widget type to push.</typeparam>
        /// <param name="cToken">Token to cancel the push.</param>
        UniTask PushAsync<TWidgetType>(CancellationToken cToken = default)
            where TWidgetType : IWidget;

        /// <summary>
        /// Pushes a widget of the given type with data.
        /// </summary>
        /// <typeparam name="TWidgetType">The widget type to push.</typeparam>
        /// <param name="widgetData">Data handed to the widget before it initializes.</param>
        /// <param name="cToken">Token to cancel the push.</param>
        UniTask PushAsync<TWidgetType>(IWidgetData widgetData, CancellationToken cToken = default)
            where TWidgetType : IWidget, IWidgetDataReceiver;

        /// <summary>
        /// Pops the top widget off the stack.
        /// </summary>
        /// <param name="cToken">Token to cancel the pop.</param>
        UniTask PopAsync(CancellationToken cToken = default);

        /// <summary>
        /// Pops every widget off the stack, top first.
        /// </summary>
        /// <param name="cToken">Token to cancel the operation.</param>
        UniTask PopAllAsync(CancellationToken cToken = default);

        /// <summary>
        /// Returns the top widget without removing it, or null when the stack is empty.
        /// </summary>
        IWidget Peek();

        /// <summary>
        /// Indicates whether a widget of the given type is anywhere on the stack.
        /// </summary>
        /// <typeparam name="TWidgetType">The widget type to look for.</typeparam>
        bool IsOpen<TWidgetType>()
            where TWidgetType : IWidget;
    }
}
