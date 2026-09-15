using System;
using Avalonia;
using HTTProof.Services;

namespace HTTProof;

sealed class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var opts = CliOptions.Parse(args);
        if (opts.Help)
        {
            Console.WriteLine(CliOptions.HelpText());
            return 0;
        }
        if (opts.ParseError is not null)
        {
            Console.Error.WriteLine($"error: {opts.ParseError}");
            Console.Error.WriteLine();
            Console.Error.WriteLine(CliOptions.HelpText());
            return 2;
        }

        AppBootstrap.Options = opts;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
