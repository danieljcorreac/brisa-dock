using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;
namespace Brisa.WinUI;
public sealed partial class MainWindow
{
    void ConfigureKeyboard(UIElement root)
    {
        root.KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
        void Shortcut(VirtualKey key, VirtualKeyModifiers modifiers, Action action)
        {
            var shortcut = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
            shortcut.Invoked += (_, e) => { e.Handled = true; action(); };
            root.KeyboardAccelerators.Add(shortcut);
        }
        Shortcut((VirtualKey)188, VirtualKeyModifiers.Control, ShowSettings);
        Shortcut(VirtualKey.T, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, ShowTasks);
        Shortcut(VirtualKey.C, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, ShowCalendar);
        Shortcut(VirtualKey.B, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, ShowBackupSettings);
        Shortcut(VirtualKey.O, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, ShowOrder);
        if (ReviewFixtures) Shortcut(VirtualKey.F12, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, NextVisualReview);
    }
    void EnsureDockKeyboardFocus()
    {
        if (closing || panel?.AppWindow.IsVisible == true || shell.XamlRoot == null) return;
        if (FocusManager.GetFocusedElement(shell.XamlRoot) is Microsoft.UI.Xaml.Controls.Button) return;
        if (FocusManager.FindFirstFocusableElement(row) is Microsoft.UI.Xaml.Controls.Control first) first.Focus(FocusState.Programmatic);
    }
    void RestoreDockFocus()
    {
        if (closing) return;
        Activate();
        DispatcherQueue.TryEnqueue(() => {
            if (closing || panel?.AppWindow.IsVisible == true) return;
            if (activeAnchor is Microsoft.UI.Xaml.Controls.Control control && control.XamlRoot == shell.XamlRoot && control.Focus(FocusState.Keyboard)) return;
            row.Children.OfType<Microsoft.UI.Xaml.Controls.Button>().LastOrDefault()?.Focus(FocusState.Keyboard);
        });
    }
}
