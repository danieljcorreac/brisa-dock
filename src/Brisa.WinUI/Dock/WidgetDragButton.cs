using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Input;
namespace Brisa.WinUI;

internal sealed class WidgetDragButton : Button
{
    static readonly InputSystemCursor idleCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
    static readonly InputSystemCursor dragCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
    public WidgetDragButton() => SetDragging(false);
    public void SetDragging(bool dragging) => ProtectedCursor = dragging ? dragCursor : idleCursor;
}
