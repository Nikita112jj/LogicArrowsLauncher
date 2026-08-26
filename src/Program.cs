namespace LogicArrowsLauncher;

internal static class Program
{
    private static Mutex? singleInstanceMutex;

    [STAThread]
    private static void Main()
    {
        singleInstanceMutex = new Mutex(
            initiallyOwned: true,
            name: "LogicArrowsLauncher.SingleInstance.v2",
            createdNew: out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "Logic Arrows Launcher уже запущен. Закрой старое окно перед повторным запуском.",
                "Logic Arrows Launcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            singleInstanceMutex.Dispose();
            return;
        }

        try
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            ApplicationConfiguration.Initialize();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, args) => ShowFatalError(args.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception exception)
                {
                    ShowFatalError(exception);
                }
            };

            Application.Run(new LauncherForm());
        }
        finally
        {
            try
            {
                singleInstanceMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The mutex can already be released during process shutdown.
            }
            singleInstanceMutex.Dispose();
        }
    }

    private static void ShowFatalError(Exception exception)
    {
        try
        {
            MessageBox.Show(
                $"Logic Arrows Launcher остановлен.\n\n{exception.Message}",
                "Logic Arrows Launcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
            // The process may already be shutting down.
        }
    }
}
