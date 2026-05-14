using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ShadowPilot;

public sealed class LoadingDotsControl : Panel
{
    private readonly Ellipse[] _dots  = new Ellipse[3];
    private readonly DispatcherTimer _timer;
    private int _phase;

    private static readonly SolidColorBrush AmberBrush   = new(Color.FromRgb(0xFC, 0xC2, 0x47));
    private static readonly SolidColorBrush SubtextBrush = new(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF));

    public LoadingDotsControl()
    {
        for (int i = 0; i < 3; i++)
        {
            _dots[i] = new Ellipse { Width = 5, Height = 5, Fill = SubtextBrush };
            Children.Add(_dots[i]);
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(380) };
        _timer.Tick += (_, _) =>
        {
            for (int i = 0; i < 3; i++)
                _dots[i].Fill = i == _phase ? AmberBrush : SubtextBrush;
            _phase = (_phase + 1) % 3;
        };

        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue) _timer.Start();
            else _timer.Stop();
        };
    }

    protected override Size MeasureOverride(Size available)
    {
        foreach (UIElement c in Children) c.Measure(available);
        return new Size(29, 5);
    }

    protected override Size ArrangeOverride(Size final)
    {
        double x = 0;
        foreach (UIElement c in Children)
        {
            c.Arrange(new Rect(x, 0, 5, 5));
            x += 5 + 4.5;
        }
        return final;
    }
}
