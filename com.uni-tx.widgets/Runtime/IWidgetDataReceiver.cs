namespace UniTx.Widgets
{
    /// <summary>
    /// Receives widget data and applies it to the widget.
    /// </summary>
    public interface IWidgetDataReceiver
    {
        /// <summary>
        /// Sets or updates the data for this widget.
        /// </summary>
        /// <param name="widgetData">The widget data to apply.</param>
        void SetData(IWidgetData widgetData);
    }
}
