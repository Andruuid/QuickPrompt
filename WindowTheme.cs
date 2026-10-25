using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace QuickPrompt;

internal static class WindowTheme
{
    // Native frame colors retain the standard window buttons, resizing, and dragging.
    // https://learn.microsoft.com/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute
    private const int UseImmersiveDarkMode = 20, CaptionColor = 35, TextColor = 36;

    internal static void UseDarkTitleBar(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            IntPtr handle = new WindowInteropHelper(window).Handle;
            int dark = 1;
            NativeMethods.DwmSetWindowAttribute(handle, UseImmersiveDarkMode, ref dark, sizeof(int));
            SetColor(handle, CaptionColor, ((SolidColorBrush)window.FindResource("Panel")).Color);
            SetColor(handle, TextColor, ((SolidColorBrush)window.FindResource("Text")).Color);
        };
    }

    private static void SetColor(IntPtr handle, int attribute, Color color)
    {
        int colorRef = color.R | color.G << 8 | color.B << 16;
        // Older Windows versions may not support explicit colors; dark mode remains enabled.
        NativeMethods.DwmSetWindowAttribute(handle, attribute, ref colorRef, sizeof(int));
    }
}
