using System.Drawing;
using System.Windows.Forms;

namespace AppLauncher.Services
{
    /// <summary>Notification-area icon with a real context menu (spec §18): open, startup toggle, quit.</summary>
    public class TrayIconService : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ToolStripMenuItem _startupItem;

        public event Action? OpenRequested;
        public event Action? StartupToggleRequested;
        public event Action? ExitRequested;

        public TrayIconService(bool startupEnabled)
        {
            var openItem = new ToolStripMenuItem("Ouvrir le lanceur");
            openItem.Click += (_, _) => OpenRequested?.Invoke();

            _startupItem = new ToolStripMenuItem("Démarrer avec Windows") { CheckOnClick = true, Checked = startupEnabled };
            _startupItem.Click += (_, _) => StartupToggleRequested?.Invoke();

            var exitItem = new ToolStripMenuItem("Quitter");
            exitItem.Click += (_, _) => ExitRequested?.Invoke();

            var menu = new ContextMenuStrip();
            menu.Items.Add(openItem);
            menu.Items.Add(_startupItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            _notifyIcon = new NotifyIcon
            {
                Icon = CreateFallbackIcon(),
                Visible = true,
                Text = "App Launcher (Ctrl+Espace)",
                ContextMenuStrip = menu
            };
            _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke();
        }

        public void ShowBalloon(string title, string text)
        {
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = text;
            _notifyIcon.ShowBalloonTip(4000);
        }

        /// <summary>Small generic icon drawn at runtime, so the app ships with zero image assets.</summary>
        private static Icon CreateFallbackIcon()
        {
            using var bitmap = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var brush = new SolidBrush(Color.FromArgb(255, 41, 121, 255));
                g.FillEllipse(brush, 2, 2, 28, 28);
                using var pen = new Pen(Color.White, 3);
                g.DrawEllipse(pen, 9, 9, 10, 10);
                g.DrawLine(pen, 17, 17, 23, 23);
            }
            return Icon.FromHandle(bitmap.GetHicon());
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
}
