using System.Windows;
using System.Windows.Threading;
using AppLauncher.Helpers;
using AppLauncher.Services;
using Application = System.Windows.Application;

namespace AppLauncher
{
    public partial class App : Application
    {
        private SingleInstanceService? _singleInstance;
        private TrayIconService? _trayIcon;
        private StartupService? _startupService;
        private MainWindow? _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Never let an unhandled exception crash the app silently (spec §21).
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                Logger.LogError(args.ExceptionObject as Exception, "AppDomain.UnhandledException");
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _singleInstance = new SingleInstanceService();
            if (!_singleInstance.TryAcquire())
            {
                // Another instance is already running: ask it to show the overlay, then exit (spec §16).
                SingleInstanceService.SignalPrimaryInstance();
                Shutdown();
                return;
            }

            _startupService = new StartupService();

            _mainWindow = new MainWindow();
            _mainWindow.HotkeyRegistrationFailed += () =>
                _trayIcon?.ShowBalloon("App Launcher", "Le raccourci Ctrl+Espace est déjà utilisé par une autre application.");

            _trayIcon = new TrayIconService(_startupService.IsEnabled());
            _trayIcon.OpenRequested += () => _mainWindow.ShowOverlay();
            _trayIcon.StartupToggleRequested += () => _startupService.SetEnabled(!_startupService.IsEnabled());
            _trayIcon.ExitRequested += () => Shutdown();

            _singleInstance.ShowRequested += () => Current.Dispatcher.Invoke(() => _mainWindow.ShowOverlay());
            _singleInstance.StartListening();

            // Create the window handle immediately so the global hotkey is live right away,
            // without ever showing the overlay until the user actually presses Ctrl+Space.
            _mainWindow.PrepareHiddenWindow();
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Logger.LogError(e.Exception, "DispatcherUnhandledException");
            e.Handled = true; // keep the app running instead of crashing
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _trayIcon?.Dispose();
            _singleInstance?.Dispose();
            base.OnExit(e);
        }
    }
}
