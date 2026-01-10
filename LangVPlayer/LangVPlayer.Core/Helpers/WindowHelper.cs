using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LangVPlayer.Core.Helpers;

/// <summary>
/// Helper class for window-related WinAPI operations.
/// Вспомогательный класс для операций WinAPI с окнами.
/// </summary>
public static class WindowHelper
{
    #region WinAPI Constants / Константы WinAPI

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;

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
    private const uint SWP_FRAMECHANGED = 0x0020;

    #endregion

    #region Virtual Desktop Interfaces / Интерфейсы виртуальных рабочих столов

    // Windows 10/11 Virtual Desktop COM interfaces
    // These GUIDs may change between Windows versions
    
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
    private interface IVirtualDesktopManager
    {
        [PreserveSig]
        int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, out bool onCurrentDesktop);
        
        [PreserveSig]
        int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);
        
        [PreserveSig]
        int MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
    }

    private static readonly Guid CLSID_VirtualDesktopManager = new Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a");

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

    /// <summary>
    /// Set window as tool window (hide from taskbar and Alt+Tab).
    /// This also makes the window show on all virtual desktops in Windows 10/11.
    /// Установить окно как tool window (скрыть из панели задач и Alt+Tab).
    /// Это также делает окно видимым на всех виртуальных рабочих столах в Windows 10/11.
    /// </summary>
    public static bool SetToolWindow(Window window, bool isToolWindow)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return false;

            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            
            if (isToolWindow)
            {
                // Add WS_EX_TOOLWINDOW, remove WS_EX_APPWINDOW
                // Tool windows appear on all virtual desktops
                exStyle |= WS_EX_TOOLWINDOW;
                exStyle &= ~WS_EX_APPWINDOW;
            }
            else
            {
                // Remove WS_EX_TOOLWINDOW, add WS_EX_APPWINDOW
                exStyle &= ~WS_EX_TOOLWINDOW;
                exStyle |= WS_EX_APPWINDOW;
            }
            
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
            
            // Force window to update / Принудительно обновить окно
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, 
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            
            System.Diagnostics.Debug.WriteLine($"SetToolWindow: {isToolWindow} - Success");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SetToolWindow failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Pin or unpin window to show on all virtual desktops (Windows 10/11).
    /// Uses WS_EX_TOOLWINDOW style which makes window appear on all desktops.
    /// Закрепить или открепить окно для показа на всех виртуальных рабочих столах (Windows 10/11).
    /// Использует стиль WS_EX_TOOLWINDOW который делает окно видимым на всех рабочих столах.
    /// </summary>
    public static bool SetShowOnAllVirtualDesktops(Window window, bool pin)
    {
        // Use tool window style - it shows on all virtual desktops
        // Используем стиль tool window - он показывается на всех виртуальных рабочих столах
        return SetToolWindow(window, pin);
    }
    
    /// <summary>
    /// Convert screen pixels to WPF device-independent units.
    /// Конвертировать пиксели экрана в WPF независимые от устройства единицы.
    /// </summary>
    public static void GetDpiScale(Window window, out double scaleX, out double scaleY)
    {
        var source = PresentationSource.FromVisual(window);
        if (source?.CompositionTarget != null)
        {
            scaleX = source.CompositionTarget.TransformToDevice.M11;
            scaleY = source.CompositionTarget.TransformToDevice.M22;
        }
        else
        {
            scaleX = 1.0;
            scaleY = 1.0;
        }
    }
}
