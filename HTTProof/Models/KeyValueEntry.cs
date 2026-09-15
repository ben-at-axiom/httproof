using CommunityToolkit.Mvvm.ComponentModel;

namespace HTTProof.Models;

public partial class KeyValueEntry : ObservableObject
{
    [ObservableProperty]
    public partial bool Enabled { get; set; } = true;

    [ObservableProperty]
    public partial string Key { get; set; } = "";

    [ObservableProperty]
    public partial string Value { get; set; } = "";
}
