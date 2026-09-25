using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Lazo
{
    internal static class WindowPlacement
    {
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpNoActivate = 0x0010;

        public static void CenterOnCursor(Window window)
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            Rect bounds;
            if (!GetWindowRect(hwnd, out bounds)) return;
            Rectangle area = System.Windows.Forms.Screen.FromPoint(
                System.Windows.Forms.Cursor.Position).WorkingArea;
            int width = bounds.Right - bounds.Left;
            int height = bounds.Bottom - bounds.Top;
            int x = area.Left + Math.Max(0, (area.Width - width) / 2);
            int y = area.Top + Math.Max(0, (area.Height - height) / 2);
            SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out Rect bounds);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y,
            int width, int height, uint flags);
    }
}
