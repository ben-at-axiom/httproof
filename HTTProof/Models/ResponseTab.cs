namespace HTTProof.Models;

/// <summary>
/// Tabs of the response pane. Declaration order must match the TabItem order in
/// MainWindow.axaml, because the TabControl's SelectedIndex binds to the numeric value.
/// </summary>
public enum ResponseTab
{
    Body,
    Headers,
}
