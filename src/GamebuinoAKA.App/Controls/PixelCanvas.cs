using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace GamebuinoAKA.App.Controls
{
    /// <summary>
    /// Affiche un Bitmap au zoom entier (nearest-neighbor) et gère une sélection
    /// rectangulaire au glisser (coordonnées image). Selection est bindable two-way.
    /// </summary>
    public class PixelCanvas : Control
    {
        public static readonly StyledProperty<Bitmap?> SourceProperty =
            AvaloniaProperty.Register<PixelCanvas, Bitmap?>(nameof(Source));

        public static readonly StyledProperty<int> ZoomProperty =
            AvaloniaProperty.Register<PixelCanvas, int>(nameof(Zoom), 4);

        public static readonly StyledProperty<bool> SelectionModeProperty =
            AvaloniaProperty.Register<PixelCanvas, bool>(nameof(SelectionMode));

        public static readonly StyledProperty<Rect> SelectionProperty =
            AvaloniaProperty.Register<PixelCanvas, Rect>(nameof(Selection),
                defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

        public Bitmap? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
        public int Zoom { get => GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }
        public bool SelectionMode { get => GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }
        public Rect Selection { get => GetValue(SelectionProperty); set => SetValue(SelectionProperty, value); }

        static PixelCanvas()
        {
            AffectsRender<PixelCanvas>(SourceProperty, ZoomProperty, SelectionProperty);
            AffectsMeasure<PixelCanvas>(SourceProperty, ZoomProperty);
        }

        public PixelCanvas()
        {
            RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        }

        private bool _dragging;
        private Point _start;

        protected override Size MeasureOverride(Size availableSize)
        {
            var s = Source;
            if (s is null) return default;
            int z = Math.Max(1, Zoom);
            return new Size(s.PixelSize.Width * z, s.PixelSize.Height * z);
        }

        public override void Render(DrawingContext ctx)
        {
            var s = Source;
            if (s is null) return;
            int z = Math.Max(1, Zoom);
            int w = s.PixelSize.Width, h = s.PixelSize.Height;

            ctx.DrawImage(s, new Rect(0, 0, w, h), new Rect(0, 0, w * z, h * z));

            var sel = Selection;
            if (sel.Width >= 1 && sel.Height >= 1)
            {
                var r = new Rect(sel.X * z, sel.Y * z, sel.Width * z, sel.Height * z);
                var fill = new SolidColorBrush(Color.FromArgb(60, 64, 196, 255));
                var pen = new Pen(Brushes.DeepSkyBlue, 1) { DashStyle = DashStyle.Dash };
                ctx.DrawRectangle(fill, pen, r);
            }
        }

        private Point ToImage(Point p)
        {
            var s = Source!;
            int z = Math.Max(1, Zoom);
            int x = Math.Clamp((int)(p.X / z), 0, s.PixelSize.Width - 1);
            int y = Math.Clamp((int)(p.Y / z), 0, s.PixelSize.Height - 1);
            return new Point(x, y);
        }

        private static Rect Normalize(Point a, Point b)
        {
            int x0 = (int)Math.Min(a.X, b.X), y0 = (int)Math.Min(a.Y, b.Y);
            int x1 = (int)Math.Max(a.X, b.X), y1 = (int)Math.Max(a.Y, b.Y);
            return new Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (SelectionMode && Source != null)
            {
                _dragging = true;
                _start = ToImage(e.GetPosition(this));
                Selection = new Rect(_start, new Size(1, 1));
                e.Pointer.Capture(this);
                e.Handled = true;
            }
            base.OnPointerPressed(e);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            if (_dragging && Source != null)
            {
                var end = ToImage(e.GetPosition(this));
                Selection = Normalize(_start, end);
            }
            base.OnPointerMoved(e);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            if (_dragging)
            {
                _dragging = false;
                e.Pointer.Capture(null);
            }
            base.OnPointerReleased(e);
        }
    }
}
