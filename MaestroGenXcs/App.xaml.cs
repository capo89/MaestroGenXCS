using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace MaestroGenXcs;

public partial class App : System.Windows.Application
{
    public App()
    {
        DispatcherUnhandledException += OnUnhandled;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            LogException(args.ExceptionObject as Exception, "AppDomain");
    }

    private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _ = sender;
        LogException(e.Exception, "Dispatcher");
        MessageBox.Show(
            e.Exception.ToString(),
            "Neočakávaná chyba",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void LogException(Exception? ex, string source)
    {
        if (ex == null) return;
        try
        {
            var path = Path.Combine(
                Path.GetDirectoryName(typeof(App).Assembly.Location) ?? ".",
                "crash.log");
            File.AppendAllText(path,
                $"[{DateTime.Now:O}] [{source}] {ex}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
