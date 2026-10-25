using System;
using System.Runtime.InteropServices;

namespace QuickPrompt;

internal static class NativeMethods
{
    internal const string AppUserModelId = "QuickPrompt.Desktop";
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] internal static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
    [DllImport("user32.dll")] internal static extern bool AllowSetForegroundWindow(uint processId);
    internal const int HotkeyId = 0x5150;
    internal const int WmHotkey = 0x0312;
    internal static readonly uint ShowMessage = RegisterWindowMessage("QuickPrompt.Show.v1");
    internal static readonly string[] Hotkeys = ["Alt+Space", "Ctrl+Space", "Ctrl+Alt+Space", "Ctrl+Shift+Space"];
    internal static uint Modifiers(string hotkey) => hotkey switch
    {
        "Ctrl+Space" => 0x0002,
        "Ctrl+Alt+Space" => 0x0003,
        "Ctrl+Shift+Space" => 0x0006,
        _ => 0x0001
    };
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] internal static extern IntPtr MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("shcore.dll")] internal static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct MonitorInfo
    {
        public int Size;
        public Rect Monitor, Work;
        public uint Flags;
    }
}
