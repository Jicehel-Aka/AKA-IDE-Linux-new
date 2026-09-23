using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace GamebuinoAKA.App.Controls
{
    /// <summary>
    /// Affiche un bitmap découpé en cellules (au zoom entier). Un clic/glisser sur
    /// une cellule invoque CellCommand avec un Point(colonne, ligne). SelectedCell
    /// (col,row) dessine un cadre de surbrillance (-1,-1 = aucune).
    /// </summary>
    public class GridCanvas : Control
    {
        public static readonly StyledProperty<Bitmap?> SourceProperty =
            AvaloniaProperty.Register<GridCanvas, Bitmap?>(nameof(Source));
        public static readonly StyledProperty<int> CellWidthProperty =
            AvaloniaProperty.Register<GridCanvas, int>(nameof(CellWidth), 16);
        public static readonly StyledProperty<int> CellHeightProperty =
            AvaloniaProperty.Register<GridCanvas, int>(nameof(CellHeight), 16);
        public static readonly StyledProperty<int> ZoomProperty =
            AvaloniaProperty.Register<GridCanvas, int>(nameof(Zoom), 2);
        public static readonly StyledProperty<bool> ShowGridProperty =
            AvaloniaProperty.Register<GridCanvas, bool>(nameof(ShowGrid), true);
        public static readonly StyledProperty<Point> SelectedCellProperty =
            AvaloniaProperty.Register<GridCanvas, Point>(nameof(SelectedCell), new Point(-1, -1));
        public static readonly StyledProperty<ICommand?> CellCommandProperty =
            AvaloniaProperty.Register<GridCanvas, ICommand?>(nameof(CellCommand));

        public Bitmap? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
        public int CellWidth { get => GetValue(CellWidthProperty); set => SetValue(CellWidthProperty, value); }
        public int CellHeight { get => GetValue(CellHeightProperty); set => SetValue(CellHeightProperty, value); }
        public int Zoom { get => GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }
        public bool ShowGrid { get => GetValue(ShowGridProperty); set => SetValue(ShowGridProperty, value); }
        public Point SelectedCell { get => GetValue(SelectedCellProperty); set => SetValue(SelectedCellProperty, value); }
        public ICommand? CellCommand { get => GetValue(CellCommandProperty); set => SetValue(CellCommandProperty, value); }

        static GridCanvas()
        {
            AffectsRender<GridCanvas>(SourceProperty, ZoomProperty, ShowGridProperty, SelectedCellProperty,
                                      CellWidthProperty, CellHeightProperty);
            AffectsMeasure<GridCanvas>(SourceProperty, ZoomProperty);
        }

        public GridCanvas()
        {
            RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        }

        private bool _pressing;

        protected override Size MeasureOverride(Size a)
        {
            var s = Source; if (s is null) return default;
            int z = Math.Max(1, Zoom);
            return new Size(s.PixelSize.Width * z, s.PixelSize.Height * z);
        }

        public override void Render(DrawingContext ctx)
        {
            var s = Source; if (s is null) return;
            int z = Math.Max(1, Zoom);
            int w = s.PixelSize.Width, h = s.PixelSize.Height;
            ctx.DrawImage(s, new Rect(0, 0, w, h), new Rect(0, 0, w * z, h * z));

            if (ShowGrid && CellWidth > 0 && CellHeight > 0)
            {
                var pen = new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 1);
                for (int x = 0; x <= w; x += CellWidth) ctx.DrawLine(pen, new Point(x * z, 0), new Point(x * z, h * z));
                for (int y = 0; y <= h; y += CellHeight) ctx.DrawLine(pen, new Point(0, y * z), new Point(w * z, y * z));
            }

            var sel = SelectedCell;
            if (sel.X >= 0 && sel.Y >= 0 && CellWidth > 0 && CellHeight > 0)
            {
                var r = new Rect(sel.X * CellWidth * z, sel.Y * CellHeight * z, CellWidth * z, CellHeight * z);
                ctx.DrawRectangle(null, new Pen(Brushes.Yellow, 2), r);
            }
        }

        private void Hit(Point p)
        {
            var s = Source; if (s is null || CellWidth <= 0 || CellHeight <= 0) return;
            int z = Math.Max(1, Zoom);
            int col = (int)(p.X / (CellWidth * z));
            int row = (int)(p.Y / (CellHeight * z));
            int maxCol = s.PixelSize.Width / CellWidth - 1;
            int maxRow = s.PixelSize.Height / CellHeight - 1;
            if (col < 0 || row < 0 || col > maxCol || row > maxRow) return;
            var cmd = CellCommand;
            var arg = new Point(col, row);
            if (cmd != null && cmd.CanExecute(arg)) cmd.Execute(arg);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            _pressing = true;
            Hit(e.GetPosition(this));
            e.Pointer.Capture(this);
            base.OnPointerPressed(e);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            if (_pressing) Hit(e.GetPosition(this));
            base.OnPointerMoved(e);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            _pressing = false;
            e.Pointer.Capture(null);
            base.OnPointerReleased(e);
        }
    }
}
