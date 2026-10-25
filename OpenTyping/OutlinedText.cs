using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace OpenTyping
{
    /// <summary>
    /// 글자 모양을 따라 흰 윤곽선을 두른 글자(썰렁이 지시 2026-10-25, 태그 없는 지시 — 산성비 단어가 짙은 연기·매연 구름 위에서도 읽히게).
    /// 글자를 도형으로 바꿔 굵은 흰 선(OutlinePen)을 먼저 그리고 그 위에 글자 색(Foreground)을 칠한다. 크기는 같은 글꼴의 TextBlock 과 같게
    /// 잡아(한 줄 너비·줄 높이) 산성비의 자리·착지 판정이 바뀌지 않는다. 윤곽선은 그 바깥으로 선 굵기의 절반만큼 삐져나온다.
    /// </summary>
    internal sealed class OutlinedText : FrameworkElement
    {
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(OutlinedText),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                                          OnShapeChanged));

        public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
            typeof(OutlinedText),
            new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));

        public static readonly DependencyProperty FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner(
            typeof(OutlinedText),
            new FrameworkPropertyMetadata(SystemFonts.MessageFontFamily,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits,
                OnShapeChanged));

        public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(
            typeof(OutlinedText),
            new FrameworkPropertyMetadata(SystemFonts.MessageFontSize,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits,
                OnShapeChanged));

        public static readonly DependencyProperty FontWeightProperty = TextElement.FontWeightProperty.AddOwner(
            typeof(OutlinedText),
            new FrameworkPropertyMetadata(FontWeights.Normal,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits,
                OnShapeChanged));

        public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
        public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
        public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
        public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
        public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

        /// <summary>윤곽선(검토에서 고른 ② 또렷한 흰 윤곽선: 흰색 4px). 모든 단어가 같은 펜을 쓴다.</summary>
        private static readonly Pen OutlinePen = CreateOutlinePen();

        private static Pen CreateOutlinePen()
        {
            var pen = new Pen(Brushes.White, 4) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            pen.Freeze();
            return pen;
        }

        private Geometry geometry;   // 글자 도형(글자·글꼴이 바뀌면 다시 만든다)
        private Size textSize;

        private static void OnShapeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((OutlinedText)d).geometry = null;

        // 캔버스에 넣기 전에 잰 도형은 그때의 DPI 로 만든 것이라, 화면 DPI 가 바뀌면 다시 만든다.
        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            geometry = null;
            InvalidateMeasure();
            InvalidateVisual();
        }

        private void EnsureGeometry()
        {
            if (geometry != null) return;
            var typeface = new Typeface(FontFamily, FontStyles.Normal, FontWeight, FontStretches.Normal);
            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var ft = new FormattedText(Text ?? "", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, FontSize,
                                       Brushes.Black, pixelsPerDip);
            textSize = new Size(ft.WidthIncludingTrailingWhitespace, ft.Height);
            geometry = ft.BuildGeometry(new Point(0, 0));
            geometry.Freeze();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            EnsureGeometry();
            return textSize;
        }

        protected override void OnRender(DrawingContext dc)
        {
            EnsureGeometry();
            dc.DrawGeometry(null, OutlinePen, geometry);
            dc.DrawGeometry(Foreground, null, geometry);
        }
    }
}
