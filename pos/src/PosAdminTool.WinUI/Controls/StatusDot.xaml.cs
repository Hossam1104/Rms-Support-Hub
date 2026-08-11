using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace PosAdminTool.WinUI.Controls;

public sealed partial class StatusDot : UserControl
{
    public static readonly DependencyProperty DotBrushProperty = DependencyProperty.Register(
        nameof(DotBrush),
        typeof(Brush),
        typeof(StatusDot),
        new PropertyMetadata(null));

    private Storyboard? _pulseStoryboard;

    public StatusDot()
    {
        InitializeComponent();
        Loaded += (_, _) => StartPulse();
        Unloaded += (_, _) => _pulseStoryboard?.Stop();
    }

    public Brush DotBrush
    {
        get => (Brush)GetValue(DotBrushProperty);
        set => SetValue(DotBrushProperty, value);
    }

    private void StartPulse()
    {
        if (_pulseStoryboard is not null)
        {
            return;
        }

        var scaleX = new DoubleAnimation
        {
            From = 1,
            To = 1.9,
            Duration = TimeSpan.FromMilliseconds(620),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        var scaleY = new DoubleAnimation
        {
            From = 1,
            To = 1.9,
            Duration = TimeSpan.FromMilliseconds(620),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        var opacity = new DoubleAnimation
        {
            From = 0.25,
            To = 0.05,
            Duration = TimeSpan.FromMilliseconds(620),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };

        Storyboard.SetTarget(scaleX, OuterGlowScale);
        Storyboard.SetTargetProperty(scaleX, "ScaleX");
        Storyboard.SetTarget(scaleY, OuterGlowScale);
        Storyboard.SetTargetProperty(scaleY, "ScaleY");
        Storyboard.SetTarget(opacity, OuterGlow);
        Storyboard.SetTargetProperty(opacity, "Opacity");

        _pulseStoryboard = new Storyboard();
        _pulseStoryboard.Children.Add(scaleX);
        _pulseStoryboard.Children.Add(scaleY);
        _pulseStoryboard.Children.Add(opacity);
        _pulseStoryboard.Begin();
    }
}
