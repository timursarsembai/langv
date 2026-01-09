using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LangVPlayer.Helpers
{
    /// <summary>
    /// Helper class for window-related WinAPI operations.
    /// Вспомогательный класс для операций WinAPI с окнами.
    /// </summary>
    public static class WindowHelper
    {
        #region WinAPI Constants / Константы WinAPI

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        #endregion

        #region WinAPI Imports / Импорты WinAPI

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        // For showing window on all virtual desktops
        // Для отображения окна на всех виртуальных рабочих столах
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;

        #endregion

        /// <summary>
        /// Sets window to always be on top of other windows.
        /// Устанавливает окно всегда поверх других окон.
        /// </summary>
        public static void SetAlwaysOnTop(Window window, bool isTopmost)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            var insertAfter = isTopmost ? HWND_TOPMOST : HWND_NOTOPMOST;
            SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        /// <summary>
        /// Gets the window handle.
        /// Получает дескриптор окна.
        /// </summary>
        public static IntPtr GetHandle(Window window)
        {
            return new WindowInteropHelper(window).Handle;
        }
    }
}
