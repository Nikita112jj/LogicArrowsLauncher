using Avalonia;
using CefNet;
using LogicArrowsLauncher.Linux;
using LogicArrowsLauncher.Linux.Cef;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Субпроцессы CEF обрабатываются внутри CefEngine.Start (ExecuteProcess) и там же завершаются.
        if (!CefEngine.Start(args, out var error))
        {
            Console.Error.WriteLine("LogicArrowsLauncher: " + error);
            return 1;
        }

        try
        {
            var builder = BuildAvaloniaApp();
            builder.StartWithClassicDesktopLifetime(args);
            return 0;
        }
        finally
        {
            CefApi.Shutdown();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
