using System.Windows;
using System.Windows.Interop;
using AppLauncher.Helpers;

namespace AppLauncher.Services
{
    /// <summary>
    /// Registers the real Windows global hotkey (RegisterHotKey), so Ctrl+Space is caught
    /// even when another application has focus - not just a WPF KeyDown handler (spec §15).
    /// </summary>
    public class HotkeyService : IDisposable
    {
        private const int HotkeyId = 0x4000;

        private HwndSource? _source;
        private IntPtr _windowHandle;
        private bool _registered;

        public event Action? HotkeyPressed;
        public bool IsRegistered => _registered;

        public void Register(Window window)
        {
            _windowHandle = new WindowInteropHelper(window).Handle;
            _source = HwndSource.FromHwnd(_windowHandle);
            _source?.AddHook(WndProc);

            _registered = NativeMethods.RegisterHotKey(
                _windowHandle, HotkeyId, NativeMethods.MOD_CONTROL, NativeMethods.VK_SPACE);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {
                HotkeyPressed?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_registered)
            {
                NativeMethods.UnregisterHotKey(_windowHandle, HotkeyId);
                _registered = false;
            }
            _source?.RemoveHook(WndProc);
        }
    }
}
