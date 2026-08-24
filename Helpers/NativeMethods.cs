using System.Runtime.InteropServices;

namespace AppLauncher.Helpers
{
    internal static class NativeMethods
    {
        public const int MOD_CONTROL = 0x0002;
        public const int WM_HOTKEY = 0x0312;
        public const uint VK_SPACE = 0x20;

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }
    }
}
