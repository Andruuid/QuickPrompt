using System;
using System.Linq;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Windows;

[assembly: InternalsVisibleTo("QuickPrompt.Ui.Tests")]

namespace QuickPrompt;

public partial class App : Application
{
    private Mutex? _instance;
    // Integration tests use the shared resources without creating a second launcher.
    internal bool LaunchOnStartup { get; init; } = true;
    protected override void OnStartup(StartupEventArgs e)
    {
        if (!LaunchOnStartup) { base.OnStartup(e); return; }
        NativeMethods.SetCurrentProcessExplicitAppUserModelID(NativeMethods.AppUserModelId);
        base.OnStartup(e);
        _instance = new Mutex(true, "Local\\QuickPrompt.v1", out bool created);
        if (!created)
        {
            if (!e.Args.Contains("--hidden"))
            {
                // An app launcher grants foreground permission to this new process.
                // Pass that permission to the existing tray instance before showing it.
                using var current = System.Diagnostics.Process.GetCurrentProcess();
                foreach (var process in System.Diagnostics.Process.GetProcessesByName(current.ProcessName))
                {
                    using (process)
                    {
                        try
                        {
                            if (process.Id != current.Id && process.SessionId == current.SessionId)
                                NativeMethods.AllowSetForegroundWindow((uint)process.Id);
                        }
                        catch (InvalidOperationException) { }
                        catch (System.ComponentModel.Win32Exception) { }
                    }
                }
                NativeMethods.PostMessage(new IntPtr(0xffff), NativeMethods.ShowMessage, IntPtr.Zero, IntPtr.Zero);
            }
            Shutdown();
            return;
        }
        try
        {
            string? dataDirectory = null;
            int dataFlag = Array.IndexOf(e.Args, "--data-dir");
            if (dataFlag >= 0 && dataFlag + 1 < e.Args.Length) dataDirectory = e.Args[dataFlag + 1];
            var store = new LibraryStore(dataDirectory);
            if (e.Args.Contains("--diagnostics"))
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(store.FilePath)!);
                System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(store.FilePath + ".log"));
                System.Diagnostics.Trace.AutoFlush = true;
            }
            var library = store.Load();
            var window = new MainWindow(store, library);
            MainWindow = window;
            window.InitializeLauncher(!e.Args.Contains("--hidden"));
            if (store.LoadWarning is not null) MessageBox.Show(store.LoadWarning, "QuickPrompt — recovery", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show("QuickPrompt could not start.\n\n" + ex.Message, "QuickPrompt", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        base.OnExit(e);
    }
}
