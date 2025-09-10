using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using TinyUnrealPackerExtended.Interfaces;

namespace TinyUnrealPackerExtended.Helpers
{
    public sealed class WindowActions : IWindowActions
    {
        private readonly WeakReference<Window> _windowRef;

        public WindowActions(Window window)
        {
            _windowRef = new WeakReference<Window>(window);

            window.SourceInitialized += (_, __) =>
            {
                if (PresentationSource.FromVisual(window) is HwndSource src)
                {
                    var hwnd = new WindowInteropHelper(window).Handle;

                    EnableNativeFrameAndTransitions(hwnd);  
                    src.AddHook(WndProc);              
                }
            };
        }

        public void Minimize()
        {
            if (TryGetWindow(out var w)) SystemCommands.MinimizeWindow(w);
        }

        public void ToggleMaximizeRestore()
        {
            if (!TryGetWindow(out var w)) return;
            if (w.WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(w);
            else SystemCommands.MaximizeWindow(w);
        }

        public void Close()
        {
            if (TryGetWindow(out var w)) SystemCommands.CloseWindow(w);
        }

        private bool TryGetWindow(out Window w) => _windowRef.TryGetTarget(out w);


        private const int GWL_STYLE = -16;
        private const long WS_CAPTION = 0x00C00000;
        private const long WS_THICKFRAME = 0x00040000;
        private const long WS_MINIMIZEBOX = 0x00020000;
        private const long WS_MAXIMIZEBOX = 0x00010000;
        private const long WS_SYSMENU = 0x00080000;

        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_FRAMECHANGED = 0x0020;

        private const int DWMWA_TRANSITIONS_FORCEDISABLED = 3; 

        private static void EnableNativeFrameAndTransitions(IntPtr hwnd)
        {
            int off = 0;
            DwmSetWindowAttribute(hwnd, DWMWA_TRANSITIONS_FORCEDISABLED, ref off, sizeof(int));

            var style = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
            long desired =
                style
                | WS_CAPTION
                | WS_THICKFRAME
                | WS_MINIMIZEBOX
                | WS_MAXIMIZEBOX
                | WS_SYSMENU;

            if (desired != style)
            {
                SetWindowLongPtr(hwnd, GWL_STYLE, new IntPtr(desired));
                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
            }
        }

        private const int WM_GETMINMAXINFO = 0x0024;
        private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_GETMINMAXINFO)
            {
                AdjustMaximizedSizeAndPosition(hwnd, lParam);
                handled = false;
            }
            return IntPtr.Zero;
        }

        private static void AdjustMaximizedSizeAndPosition(IntPtr hwnd, IntPtr lParam)
        {
            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero) return;

            var mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            if (!GetMonitorInfo(monitor, ref mi)) return;

            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

            var rcWork = mi.rcWork;
            var rcMonitor = mi.rcMonitor;

            mmi.ptMaxPosition.x = Math.Abs(rcWork.left - rcMonitor.left);
            mmi.ptMaxPosition.y = Math.Abs(rcWork.top - rcMonitor.top);
            mmi.ptMaxSize.x = Math.Abs(rcWork.right - rcMonitor.left);
            mmi.ptMaxSize.y = Math.Abs(rcWork.bottom - rcMonitor.top);

            Marshal.StructureToPtr(mmi, lParam, true);
        }

        // ---------- P/Invoke ----------

        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
            => IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
            => IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }
    }
}
