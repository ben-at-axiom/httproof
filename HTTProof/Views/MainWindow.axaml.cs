using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using HTTProof.Models;
using HTTProof.ViewModels;
using TextMateSharp.Grammars;

namespace HTTProof.Views;

public partial class MainWindow : Window
{
    private static readonly RegistryOptions Registry = new(ThemeName.DarkPlus);

    private TextEditor? _bodyEditor;
    private TextEditor? _responseEditor;
    private TextEditor? _urlEditor;
    private TextMate.Installation? _bodyTm;
    private TextMate.Installation? _responseTm;
    private MainViewModel? _vm;
    private bool _syncingBody;
    private bool _syncingUrl;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnBodyEditorAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_bodyEditor is not null) return;
        _bodyEditor = (TextEditor)sender!;
        _bodyTm = _bodyEditor.InstallTextMate(Registry);
        _bodyEditor.TextChanged += OnBodyEditorTextChanged;
        UpdateBodyGrammar();
        SyncBodyFromVm();
    }

    private void UpdateBodyGrammar()
    {
        if (_bodyEditor is null || _bodyTm is null || _vm is null) return;
        if (_vm.Body == BodyKind.Json)
        {
            var scope = Registry.GetScopeByLanguageId("json");
            if (!string.IsNullOrEmpty(scope)) _bodyTm.SetGrammar(scope);
        }
        else
        {
            _bodyTm.SetGrammar(null);
        }
        _bodyEditor.TextArea.TextView.Redraw();
    }

    private void OnResponseEditorAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_responseEditor is not null) return;
        _responseEditor = (TextEditor)sender!;
        _responseTm = _responseEditor.InstallTextMate(Registry);
        var scope = Registry.GetScopeByLanguageId("json");
        if (!string.IsNullOrEmpty(scope)) _responseTm.SetGrammar(scope);
        SyncResponseFromVm();
    }

    private void OnUrlEditorAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_urlEditor is not null) return;
        _urlEditor = (TextEditor)sender!;
        _urlEditor.Options.EnableHyperlinks = false;
        _urlEditor.Options.EnableEmailHyperlinks = false;
        _urlEditor.Options.HighlightCurrentLine = false;
        _urlEditor.TextArea.TextView.LineTransformers.Add(new UrlColorizer());
        // Tunnel phase so we swallow Enter before AvaloniaEdit's TextArea inserts a newline.
        _urlEditor.TextArea.AddHandler(
            Avalonia.Input.InputElement.KeyDownEvent,
            (_, k) =>
            {
                if (k.Key != Avalonia.Input.Key.Enter) return;
                k.Handled = true;
                if (_vm?.SendCommand.CanExecute(null) == true) _vm.SendCommand.Execute(null);
            },
            Avalonia.Interactivity.RoutingStrategies.Tunnel,
            handledEventsToo: true);
        _urlEditor.TextChanged += OnUrlEditorTextChanged;
        SyncUrlFromVm();
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = DataContext as MainViewModel;
        if (_vm is not null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            SyncBodyFromVm();
            SyncResponseFromVm();
            SyncUrlFromVm();
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.BodyText): SyncBodyFromVm(); break;
            case nameof(MainViewModel.ResponseBody): SyncResponseFromVm(); break;
            case nameof(MainViewModel.Url): SyncUrlFromVm(); break;
            case nameof(MainViewModel.Body): UpdateBodyGrammar(); break;
        }
    }

    private void SyncBodyFromVm()
    {
        if (_bodyEditor is null || _vm is null) return;
        var target = _vm.BodyText ?? "";
        if ((_bodyEditor.Document?.Text ?? "") == target) return;
        _syncingBody = true;
        if (_bodyEditor.Document is not null) _bodyEditor.Document.Text = target;
        else _bodyEditor.Text = target;
        _syncingBody = false;
    }

    private void SyncResponseFromVm()
    {
        if (_responseEditor is null || _vm is null) return;
        var target = _vm.ResponseBody ?? "";
        if ((_responseEditor.Document?.Text ?? "") == target) return;
        if (_responseEditor.Document is not null) _responseEditor.Document.Text = target;
        else _responseEditor.Text = target;
    }

    private void OnBodyEditorTextChanged(object? sender, System.EventArgs e)
    {
        if (_syncingBody || _vm is null || _bodyEditor is null) return;
        _vm.BodyText = _bodyEditor.Document?.Text ?? "";
    }

    private void SyncUrlFromVm()
    {
        if (_urlEditor is null || _vm is null) return;
        var target = _vm.Url ?? "";
        if ((_urlEditor.Document?.Text ?? "") == target) return;
        _syncingUrl = true;
        if (_urlEditor.Document is not null) _urlEditor.Document.Text = target;
        else _urlEditor.Text = target;
        _syncingUrl = false;
    }

    private void OnUrlEditorTextChanged(object? sender, System.EventArgs e)
    {
        if (_syncingUrl || _vm is null || _urlEditor is null) return;
        var t = _urlEditor.Document?.Text ?? "";
        if (t.Contains('\n') || t.Contains('\r'))
        {
            t = t.Replace("\r", "").Replace("\n", "");
            _syncingUrl = true;
            if (_urlEditor.Document is not null) _urlEditor.Document.Text = t;
            _syncingUrl = false;
        }
        _vm.Url = t;
    }
}
