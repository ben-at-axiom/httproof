using System;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using HTTProof.Services;
using HTTProof.ViewModels;
using HTTProof.Views;

namespace HTTProof;

public partial class App : Application
{
    private DrivingServer? _server;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var opts = AppBootstrap.Options;
            var vm = new MainViewModel();
            CliBootstrap.ApplyTo(vm, opts);

            desktop.MainWindow = new MainWindow { DataContext = vm };
            desktop.Exit += (_, _) => _server?.Dispose();

            _server = new DrivingServer(vm);
            try
            {
                _server.Start(opts.Port);
                Console.WriteLine(JsonSerializer.Serialize(new { server = _server.BaseUrl }, DrivingServer.JsonOpts));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"server failed to start: {ex.Message}");
                _server = null;
            }

            if (opts.Send)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { ack = "sending", method = vm.Method, url = vm.Url }, DrivingServer.JsonOpts));

                Dispatcher.UIThread.Post(async () =>
                {
                    await vm.ExecuteAsync();
                    var dto = StateMapper.ToDto(vm);
                    Console.WriteLine(JsonSerializer.Serialize(dto, DrivingServer.JsonOpts));
                });
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
