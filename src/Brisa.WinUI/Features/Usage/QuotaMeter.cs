using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;
namespace Brisa.WinUI;

/// <summary>Animate only real quota changes, starting at the currently displayed percentage.</summary>
internal sealed class QuotaMeter : Grid
{
    readonly ScaleTransform scale = new() { ScaleX = 0, ScaleY = 1 };
    readonly Border ink = new() { CornerRadius = new CornerRadius(2) };
    Storyboard? transition;
    string? color;
    public double Value { get; private set; }
    public QuotaMeter(double value, double height, string foreground)
    {
        Height = height; VerticalAlignment = VerticalAlignment.Center;
        Background = Ui.Brush("#33A0AAB8"); CornerRadius = new CornerRadius(2);
        ink.RenderTransform = scale; ink.RenderTransformOrigin = new Point(0, .5); Children.Add(ink);
        Unloaded += (_, _) => SnapToValue();
        Set(value, foreground);
    }
    public void Set(double value, string foreground)
    {
        value = double.IsFinite(value) ? Math.Clamp(value, 0, 100) : 0;
        if (Value != value)
        {
            double previous = Value; Value = value;
            double visible = scale.ScaleX;
            if (Ui.ReducedMotion || !IsLoaded || ActualWidth <= 0) SnapToValue();
            else
            {
                transition?.Stop(); scale.ScaleX = visible;
                var storyboard = new Storyboard();
                var animation = new DoubleAnimation
                {
                    From = visible, To = value / 100,
                    Duration = new Duration(TimeSpan.FromMilliseconds(220)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(animation, scale); Storyboard.SetTargetProperty(animation, "ScaleX");
                storyboard.Children.Add(animation); transition = storyboard;
                storyboard.Completed += (_, _) => { if (ReferenceEquals(transition, storyboard)) SnapToValue(); };
                storyboard.Begin();
            }
            if (FrameworkElementAutomationPeer.FromElement(this) is QuotaMeterPeer peer) peer.Notify(previous, value);
        }
        if (color != foreground) { color = foreground; ink.Background = Ui.Brush(foreground); }
    }
    void SnapToValue() { var active = transition; transition = null; active?.Stop(); scale.ScaleX = Value / 100; }
    protected override AutomationPeer OnCreateAutomationPeer() => new QuotaMeterPeer(this);
    sealed class QuotaMeterPeer(QuotaMeter owner) : FrameworkElementAutomationPeer(owner), IRangeValueProvider
    {
        protected override string GetClassNameCore() => "QuotaMeter";
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;
        protected override object GetPatternCore(PatternInterface patternInterface) => patternInterface == PatternInterface.RangeValue ? this : base.GetPatternCore(patternInterface);
        public void Notify(double oldValue, double newValue) => RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty, oldValue, newValue);
        public bool IsReadOnly => true;
        public double Value => owner.Value;
        public double Minimum => 0;
        public double Maximum => 100;
        public double SmallChange => 0;
        public double LargeChange => 0;
        public void SetValue(double value) => throw new InvalidOperationException("La cuota es de sólo lectura.");
    }
}
