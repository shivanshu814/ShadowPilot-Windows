using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace ShadowPilot;

public sealed class WaveformControl : Panel
{
    private static readonly SolidColorBrush RedBrush    = new(Color.FromRgb(0xFB, 0x52, 0x52));
    private static readonly SolidColorBrush SubtextBrush = new(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF));
    private static readonly double[] Heights = [0.3, 0.5, 0.8, 0.4, 0.65];

    private readonly Rectangle[] _bars = new Rectangle[5];
    private readonly DoubleAnimation[] _anims = new DoubleAnimation[5];
    private readonly Storyboard _storyboard = new();
    private bool _active;

    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(WaveformControl),
            new PropertyMetadata(false, OnActiveChanged));

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public WaveformControl()
    {
        for (int i = 0; i < 5; i++)
        {
            _bars[i] = new Rectangle
            {
                Width          = 2.5,
                Height         = 3,
                Fill           = SubtextBrush,
                RadiusX        = 1.25,
                RadiusY        = 1.25,
                VerticalAlignment = VerticalAlignment.Center
            };
            Children.Add(_bars[i]);
        }
    }

    private static void OnActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WaveformControl w)
        {
            if ((bool)e.NewValue) w.StartAnimation();
            else w.StopAnimation();
        }
    }

    private void StartAnimation()
    {
        _storyboard.Stop(this);
        _storyboard.Children.Clear();
        var rng = new Random();

        for (int i = 0; i < 5; i++)
        {
            _bars[i].Fill = RedBrush;
            var anim = new DoubleAnimation
            {
                From           = 3,
                To             = rng.NextDouble() * 14 + 4,
                Duration       = TimeSpan.FromSeconds(rng.NextDouble() * 0.28 + 0.22),
                AutoReverse    = true,
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime      = TimeSpan.FromMilliseconds(i * 60),
            };
            Storyboard.SetTarget(anim, _bars[i]);
            Storyboard.SetTargetProperty(anim, new PropertyPath(HeightProperty));
            _storyboard.Children.Add(anim);
        }
        _storyboard.Begin(this, true);
    }

    private void StopAnimation()
    {
        _storyboard.Stop(this);
        for (int i = 0; i < 5; i++)
        {
            _bars[i].Fill   = SubtextBrush;
            _bars[i].Height = 3;
        }
    }

    protected override Size MeasureOverride(Size available)
    {
        foreach (UIElement child in Children) child.Measure(available);
        return new Size(22, 18);
    }

    protected override Size ArrangeOverride(Size final)
    {
        double x = 0;
        for (int i = 0; i < _bars.Length; i++)
        {
            var bar = _bars[i];
            bar.Arrange(new Rect(x, (final.Height - bar.Height) / 2, 2.5, bar.Height));
            x += 2.5 + 2.5;
        }
        return final;
    }
}
