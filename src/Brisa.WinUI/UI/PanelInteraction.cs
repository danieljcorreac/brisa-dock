namespace Brisa.WinUI;

public sealed partial class MainWindow
{
    async Task<T?> PickForPanel<T>(object picker, Func<Windows.Foundation.IAsyncOperation<T>> choose) where T : class
    {
        using var interaction = panel?.KeepOpenForPicker();
        Microsoft.UI.Xaml.Window owner = panel?.AppWindow.IsVisible == true ? panel : this;
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(owner));
        return await choose();
    }
}
