using Microsoft.Maui.Graphics;

namespace LiftLog.App.Controls;

public sealed class TrainingChartView : GraphicsView
{
    public static readonly BindableProperty PointsProperty = BindableProperty.Create(
        nameof(Points),
        typeof(IReadOnlyList<ChartDataPoint>),
        typeof(TrainingChartView),
        Array.Empty<ChartDataPoint>(),
        propertyChanged: OnChartPropertyChanged);

    public static readonly BindableProperty KindProperty = BindableProperty.Create(
        nameof(Kind),
        typeof(TrainingChartKind),
        typeof(TrainingChartView),
        TrainingChartKind.Bars,
        propertyChanged: OnChartPropertyChanged);

    public IReadOnlyList<ChartDataPoint> Points
    {
        get => (IReadOnlyList<ChartDataPoint>)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public TrainingChartKind Kind
    {
        get => (TrainingChartKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public TrainingChartView()
    {
        Drawable = new TrainingChartDrawable(this);
        HeightRequest = 230;
        MinimumHeightRequest = 230;
    }

    private static void OnChartPropertyChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((TrainingChartView)bindable).Invalidate();

    private sealed class TrainingChartDrawable(TrainingChartView owner) : IDrawable
    {
        private static readonly Color Accent = Color.FromArgb("#3F7CFF");
        private static readonly Color Grid = Color.FromArgb("#292D33");
        private static readonly Color Muted = Color.FromArgb("#90939B");

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var points = owner.Points;
            if (points.Count == 0 || dirtyRect.Width < 80 || dirtyRect.Height < 80)
            {
                return;
            }

            const float left = 42;
            const float right = 8;
            const float top = 10;
            const float bottom = 30;
            var chartWidth = dirtyRect.Width - left - right;
            var chartHeight = dirtyRect.Height - top - bottom;
            var maximum = NiceMaximum(points.Max(point => point.Value));

            canvas.Antialias = true;
            canvas.FontSize = 10;
            canvas.FontColor = Muted;
            canvas.StrokeColor = Grid;
            canvas.StrokeSize = 1;

            for (var index = 0; index <= 3; index++)
            {
                var ratio = index / 3d;
                var y = top + chartHeight - (float)(chartHeight * ratio);
                canvas.DrawLine(left, y, left + chartWidth, y);
                canvas.DrawString(
                    FormatAxisValue(maximum * ratio),
                    0,
                    y - 8,
                    left - 6,
                    16,
                    HorizontalAlignment.Right,
                    VerticalAlignment.Center);
            }

            if (owner.Kind == TrainingChartKind.Line)
            {
                DrawLine(canvas, points, left, top, chartWidth, chartHeight, maximum);
            }
            else
            {
                DrawBars(canvas, points, left, top, chartWidth, chartHeight, maximum);
            }

            DrawLabels(canvas, points, left, top + chartHeight + 6, chartWidth);
        }

        private static void DrawBars(
            ICanvas canvas,
            IReadOnlyList<ChartDataPoint> points,
            float left,
            float top,
            float width,
            float height,
            double maximum)
        {
            var slotWidth = width / points.Count;
            var barWidth = Math.Max(4, slotWidth * 0.62f);
            canvas.FillColor = Accent;

            for (var index = 0; index < points.Count; index++)
            {
                var barHeight = points[index].Value <= 0
                    ? 0
                    : Math.Max(2, (float)(height * points[index].Value / maximum));
                var x = left + (slotWidth * index) + ((slotWidth - barWidth) / 2);
                canvas.FillRoundedRectangle(x, top + height - barHeight, barWidth, barHeight, 3);
            }
        }

        private static void DrawLine(
            ICanvas canvas,
            IReadOnlyList<ChartDataPoint> points,
            float left,
            float top,
            float width,
            float height,
            double maximum)
        {
            var slotWidth = width / points.Count;
            var coordinates = points
                .Select((point, index) => new PointF(
                    left + (slotWidth * index) + (slotWidth / 2),
                    top + height - (float)(height * point.Value / maximum)))
                .ToArray();

            canvas.StrokeColor = Accent;
            canvas.StrokeSize = 3;
            for (var index = 1; index < coordinates.Length; index++)
            {
                canvas.DrawLine(coordinates[index - 1], coordinates[index]);
            }

            canvas.FillColor = Accent;
            foreach (var point in coordinates)
            {
                canvas.FillCircle(point.X, point.Y, 4);
            }
        }

        private static void DrawLabels(
            ICanvas canvas,
            IReadOnlyList<ChartDataPoint> points,
            float left,
            float top,
            float width)
        {
            var slotWidth = width / points.Count;
            var labelStep = Math.Max(1, (int)Math.Ceiling(points.Count / 4d));
            canvas.FontColor = Muted;
            canvas.FontSize = 10;

            for (var index = 0; index < points.Count; index++)
            {
                if (index % labelStep != 0 && index != points.Count - 1)
                {
                    continue;
                }

                canvas.DrawString(
                    points[index].Label,
                    left + (slotWidth * index),
                    top,
                    slotWidth,
                    18,
                    HorizontalAlignment.Center,
                    VerticalAlignment.Top);
            }
        }

        private static double NiceMaximum(double value)
        {
            if (value <= 0)
            {
                return 1;
            }

            var magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));
            return Math.Ceiling(value / magnitude) * magnitude;
        }

        private static string FormatAxisValue(double value)
        {
            if (value >= 1_000_000)
            {
                return $"{value / 1_000_000:0.#}m";
            }

            if (value >= 1_000)
            {
                return $"{value / 1_000:0.#}k";
            }

            return value.ToString("0.#");
        }
    }
}
