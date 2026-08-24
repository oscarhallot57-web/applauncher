using System.Reflection;
using System.Runtime.InteropServices;

namespace AppLauncher.Services
{
    /// <summary>
    /// Resolves .lnk shortcut targets using late-bound COM ("WScript.Shell").
    /// Late binding avoids needing a COMReference entry in the .csproj / tlbimp step,
    /// which keeps the project simple to restore and build on any machine.
    /// </summary>
    internal static class ShellLinkResolver
    {
        private static readonly Type? ShellType;
        private static readonly object? ShellComObject;

        static ShellLinkResolver()
        {
            try
            {
                ShellType = Type.GetTypeFromProgID("WScript.Shell");
                ShellComObject = ShellType != null ? Activator.CreateInstance(ShellType) : null;
            }
            catch
            {
                ShellType = null;
                ShellComObject = null;
            }
        }

        public static bool TryResolve(string lnkPath, out string targetPath, out string arguments, out string workingDirectory)
        {
            targetPath = string.Empty;
            arguments = string.Empty;
            workingDirectory = string.Empty;

            if (ShellType == null || ShellComObject == null) return false;

            object? shortcut = null;
            try
            {
                shortcut = ShellType.InvokeMember(
                    "CreateShortcut", BindingFlags.InvokeMethod, null, ShellComObject, new object[] { lnkPath });

                if (shortcut == null) return false;

                Type shortcutType = shortcut.GetType();

                targetPath = shortcutType.InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null) as string ?? string.Empty;
                arguments = shortcutType.InvokeMember("Arguments", BindingFlags.GetProperty, null, shortcut, null) as string ?? string.Empty;
                workingDirectory = shortcutType.InvokeMember("WorkingDirectory", BindingFlags.GetProperty, null, shortcut, null) as string ?? string.Empty;

                return !string.IsNullOrWhiteSpace(targetPath);
            }
            catch
            {
                return false;
            }
            finally
            {
                if (shortcut != null && Marshal.IsComObject(shortcut))
                {
                    Marshal.FinalReleaseComObject(shortcut);
                }
            }
        }
    }
}
