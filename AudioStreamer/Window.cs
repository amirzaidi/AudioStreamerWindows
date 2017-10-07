using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;

namespace AudioStreamer
{
    class Window
    {
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        static extern IntPtr GetDesktopWindow();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);


        delegate bool ConsoleEventDelegate(int eventType);

        static NotifyIcon notifyIcon = new NotifyIcon();
        static IntPtr processHandle = Process.GetCurrentProcess().MainWindowHandle;
        static IntPtr WinShell;
        static IntPtr WinDesktop;
        static ConsoleEventDelegate CloseHandler;
        static bool Shown = true;

        public static void Init()
        {
            notifyIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            notifyIcon.Visible = true;
            notifyIcon.Click += (s, e) => SetWindowShown(!Shown);

            WinShell = GetShellWindow();
            WinDesktop = GetDesktopWindow();

            SetConsoleCtrlHandler(CloseHandler = new ConsoleEventDelegate(e =>
            {
                if (e == 2)
                    notifyIcon.Visible = false;
                return false;
            }), true);

            Application.Run();
        }

        public static void SetWindowShown(bool Show = true)
        {
            Shown = Show;
            SetParent(processHandle, Show ? WinDesktop : WinShell);
        }
    }
}
