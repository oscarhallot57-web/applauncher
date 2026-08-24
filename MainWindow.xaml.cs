using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AppLauncher.Helpers;
using AppLauncher.Models;
using AppLauncher.Services;
using AppLauncher.ViewModels;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace AppLauncher
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel = new();
        private readonly HotkeyService _hotkeyService = new();
        private bool _isOverlayVisible;

        public event Action? HotkeyRegistrationFailed;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _viewModel;

            _viewModel.RequestClose += HideOverlay;
            _viewModel.RequestLaunchAndClose += HideOverlay;

            _hotkeyService.HotkeyPressed += () => Dispatcher.Invoke(ToggleOverlay);
        }

        /// <summary>
        /// Creates the Win32 window handle and registers the global hotkey without ever
        /// showing the window, then kicks off the (cache-first) application scan.
        /// </summary>
        public void PrepareHiddenWindow()
        {
            var helper = new WindowInteropHelper(this);
            helper.EnsureHandle();

            _hotkeyService.Register(this);
            if (!_hotkeyService.IsRegistered)
            {
                HotkeyRegistrationFailed?.Invoke();
            }

            _ = _viewModel.InitializeAsync();
        }

        private void ToggleOverlay()
        {
            if (_isOverlayVisible) HideOverlay();
            else ShowOverlay();
        }

        public void ShowOverlay()
        {
            PositionOnActiveScreen();

            _viewModel.ResetSearch();
            Show();
            Activate();
            SearchBox.Focus();
            Keyboard.Focus(SearchBox);
            NativeMethods.SetForegroundWindow(new WindowInteropHelper(this).Handle);

            AnimateIn();
            _isOverlayVisible = true;
        }

        public void HideOverlay()
        {
            if (!_isOverlayVisible)
            {
                Visibility = Visibility.Hidden;
                return;
            }

            _isOverlayVisible = false;
            AnimateOut(() => Visibility = Visibility.Hidden);
        }

        /// <summary>Centers the overlay (near the top) on the monitor currently holding the mouse cursor.</summary>
        private void PositionOnActiveScreen()
        {
            NativeMethods.GetCursorPos(out var cursor);
            var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(cursor.X, cursor.Y));
            var area = screen.WorkingArea;

            double dpiX = 1.0, dpiY = 1.0;
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }

            double screenLeft = area.Left / dpiX;
            double screenTop = area.Top / dpiY;
            double screenWidth = area.Width / dpiX;
            double screenHeight = area.Height / dpiY;

            Left = screenLeft + (screenWidth - Width) / 2;
            Top = screenTop + screenHeight * 0.22;
        }

        private void AnimateIn()
        {
            RootBorder.Opacity = 0;
            RootScale.ScaleX = 0.96;
            RootScale.ScaleY = 0.96;

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140));
            var scaleX = new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(160)) { EasingFunction = new QuadraticEase() };
            var scaleY = new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(160)) { EasingFunction = new QuadraticEase() };

            RootBorder.BeginAnimation(OpacityProperty, fadeIn);
            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
        }

        private void AnimateOut(Action onComplete)
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(100));
            fadeOut.Completed += (_, _) => onComplete();
            RootBorder.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    HideOverlay();
                    e.Handled = true;
                    break;
                case Key.Down:
                    _viewModel.MoveSelectionDownCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.Up:
                    _viewModel.MoveSelectionUpCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.Enter:
                    _viewModel.LaunchSelectedCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }

        private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResultsList.SelectedItem is ApplicationItem item)
            {
                _viewModel.Launch(item);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _hotkeyService.Dispose();
            base.OnClosed(e);
        }
    }
}
