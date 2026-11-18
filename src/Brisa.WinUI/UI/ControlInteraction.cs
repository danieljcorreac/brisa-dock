using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
namespace Brisa.WinUI;

internal sealed class ControlInteraction
{
    bool pointer, keyboard;
    public bool Active => pointer || keyboard;
    public ControlInteraction(Control control)
    {
        control.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, _) => pointer = true), true);
        control.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, _) => pointer = false), true);
        control.PointerCaptureLost += (_, _) => pointer = false;
        control.PointerCanceled += (_, _) => pointer = false;
        control.KeyDown += (_, e) => { if (e.Key is VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down or VirtualKey.Home or VirtualKey.End or VirtualKey.PageUp or VirtualKey.PageDown) keyboard = true; };
        control.KeyUp += (_, _) => keyboard = false;
        control.LostFocus += (_, _) => { pointer = keyboard = false; };
    }
}
