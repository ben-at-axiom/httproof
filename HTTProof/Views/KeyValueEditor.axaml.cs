using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using HTTProof.Models;

namespace HTTProof.Views;

public partial class KeyValueEditor : UserControl
{
    public static readonly DirectProperty<KeyValueEditor, ObservableCollection<KeyValueEntry>?> ItemsProperty =
        AvaloniaProperty.RegisterDirect<KeyValueEditor, ObservableCollection<KeyValueEntry>?>(
            nameof(Items),
            o => o.Items,
            (o, v) => o.Items = v);

    private ObservableCollection<KeyValueEntry>? _items;

    public ObservableCollection<KeyValueEntry>? Items
    {
        get => _items;
        set => SetAndRaise(ItemsProperty, ref _items, value);
    }

    public KeyValueEditor()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ObservableCollection<KeyValueEntry> src)
                Items = src;
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnAddClicked(object? sender, RoutedEventArgs e)
    {
        Items?.Add(new KeyValueEntry());
    }

    private void OnDeleteClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: KeyValueEntry entry } && Items is not null)
            Items.Remove(entry);
    }
}
