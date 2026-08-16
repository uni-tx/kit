using UniTx.Core;

namespace UniTx.Widgets
{
    /// <summary>
    /// Base contract for UI widget entities managed by the widgets stack.
    /// </summary>
    public interface IWidget : IInitializable, IResettable, ISceneEntity
    {
        // Empty
    }

    /// <summary>
    /// Contract for widgets that receive typed data on push.
    /// </summary>
    public interface IWidget<TData> : IWidget, IWidgetDataReceiver
        where TData : IWidgetData
    {
        /// <summary>
        /// Gets the data currently assigned to the widget.
        /// </summary>
        TData Data { get; }
    }
}
