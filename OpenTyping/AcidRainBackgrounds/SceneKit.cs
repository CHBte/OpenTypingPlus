using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace OpenTyping
{
    /// <summary>모음 그림에서 떼어 낸 조각. Left·Top 은 떼어 낸 그림의 왼쪽 위가 모음 그림에서 어디인가(px).</summary>
    internal sealed class ScenePiece
    {
        public BitmapSource Bitmap;
        public int Left, Top;
    }

    /// <summary>나무 그림 안의 기준점: 줄기 그림의 왼쪽 위(OX·OY), 그 안에서 기준 원의 가운데(CX·CY)와 반지름(R).</summary>
    internal readonly struct TreeFrame
    {
        public TreeFrame(double ox, double oy, double cx, double cy, double r) { OX = ox; OY = oy; CX = cx; CY = cy; R = r; }
        public double OX { get; }
        public double OY { get; }
        public double CX { get; }
        public double CY { get; }
        public double R { get; }
    }

    /// <summary>
    /// 산성비 오락의 `배경 세트`(<261025_3>)들이 함께 쓰는 도구. 캔바 약관상 출처 정보(C2PA)를 지우면 안 되므로 받은 그림 파일은 다시
    /// 저장하지 않고, 조각 떼어 내기·색 바꾸기·크기·자리는 모두 실행 중에 메모리에서 한다. 그림은 Resources\AcidRain\&lt;배경 세트 Id&gt;\ 에 있다.
    /// </summary>
    internal static class SceneKit
    {
        internal static readonly Brush CrownBrush = Frozen(Color.FromRgb(0x7e, 0xa8, 0x50));             // 나무의 녹색 큰 원(잎)
        internal static readonly Brush TrailBrush = Frozen(Color.FromArgb(0x99, 0x6b, 0x82, 0x99));      // 날벌레·새가 지나간 길

        internal static Brush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        // ── 모음 그림 읽기와 조각 떼어 내기 ──

        private static readonly Dictionary<(string, string), BitmapSource> sheetBitmaps = new Dictionary<(string, string), BitmapSource>();
        private static readonly Dictionary<(string, string, Int32Rect, int, bool), ScenePiece> pieces =
            new Dictionary<(string, string, Int32Rect, int, bool), ScenePiece>();
        private static readonly Dictionary<(ScenePiece, bool, double, double), ScenePiece> shadedPieces =
            new Dictionary<(ScenePiece, bool, double, double), ScenePiece>();

        /// <summary>배경 세트 folder 의 그림 file(확장자 뺀 이름)의 주소.</summary>
        internal static Uri SheetUri(string folder, string file) =>
            new Uri("pack://application:,,,/Resources/AcidRain/" + folder + "/" + file + ".png");

        /// <summary>
        /// 모음 그림 한 장을 읽는다(Bgra32). 못 읽으면 null(그 그림만 빠지고 게임은 계속된다). 한 장이 약 6MB 라, 떼어 낸 조각만 남기고
        /// 모음 그림은 배경을 다 놓으면 놓아 준다(ReleaseSheets) — 안 그러면 오락 창을 닫아도 프로그램이 끝날 때까지 남는다.
        /// </summary>
        private static BitmapSource SheetBitmap(string folder, string file)
        {
            if (sheetBitmaps.TryGetValue((folder, file), out BitmapSource cached)) return cached;
            BitmapSource result = null;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = SheetUri(folder, file);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                var bgra = new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
                bgra.Freeze();
                result = bgra;
            }
            catch (Exception) { result = null; }
            sheetBitmaps[(folder, file)] = result;
            return result;
        }

        /// <summary>배경을 다 놓은 뒤 모음 그림을 놓아 준다. 떼어 낸 조각은 단계를 바꿀 때 다시 쓰므로 창이 닫힐 때까지 둔다.</summary>
        internal static void ReleaseSheets() => sheetBitmaps.Clear();

        /// <summary>
        /// 떼어 낸 조각도 놓아 준다(원본 해상도라 크다 — 모든 단계를 놓은 창을 닫은 뒤 메모리가 창 자체를 포함해 약 20MB 줄어든다,
        /// 2026-10-25 실측). 오락 창이 모두 닫힐 때 부른다. 다음에 창을 열면 다시 떼어 낸다.
        /// </summary>
        internal static void ReleasePieces()
        {
            pieces.Clear();
            shadedPieces.Clear();
        }

        /// <summary>검사용(-acidscenetest): 들고 있는 모음 그림 수(배경을 다 놓은 뒤에는 0이어야 한다).</summary>
        internal static int HeldSheetCountForTest => sheetBitmaps.Count;

        /// <summary>검사용(-acidscenetest): 잿빛으로 바꾼 조각의 그림들.</summary>
        internal static HashSet<BitmapSource> GrayBitmapsForTest =>
            new HashSet<BitmapSource>(shadedPieces.Where(kv => kv.Key.Item2).Select(kv => kv.Value.Bitmap));

        /// <summary>
        /// 배경 세트 folder 의 모음 그림 file 에서 rect 둘레(여유 3px)를 메모리에서 떼어 낸다. keepAll 이 아니면 그 안에서 가장 큰
        /// 덩어리(알파 8 넘는 픽셀이 8방향으로 이어진 것)만 남겨 떨어진 잡티를 빼고, 아래 cutBottom 줄(꽃·봉오리의 밑동 토막)을 지운 뒤,
        /// 그림이 있는 부분만 남긴다. 파일은 건드리지 않는다. 못 읽으면 null.
        /// </summary>
        internal static ScenePiece SheetPiece(string folder, string file, Int32Rect rect, int cutBottom = 0, bool keepAll = false)
        {
            var key = (folder, file, rect, cutBottom, keepAll);
            if (pieces.TryGetValue(key, out ScenePiece cached)) return cached;
            ScenePiece result = null;
            BitmapSource sheet = SheetBitmap(folder, file);
            if (sheet != null)
            {
                try { result = CutPiece(sheet, rect, cutBottom, keepAll); }
                catch (Exception) { result = null; }
            }
            pieces[key] = result;
            return result;
        }

        private static ScenePiece CutPiece(BitmapSource sheet, Int32Rect rect, int cutBottom, bool keepAll)
        {
            const int Margin = 3;
            int x0 = Math.Max(0, rect.X - Margin), y0 = Math.Max(0, rect.Y - Margin);
            int x1 = Math.Min(sheet.PixelWidth, rect.X + rect.Width + Margin);
            int y1 = Math.Min(sheet.PixelHeight, rect.Y + rect.Height + Margin);
            int w = x1 - x0, h = y1 - y0, stride = w * 4;
            if (w <= 0 || h <= 0) return null;
            var px = new byte[stride * h];
            sheet.CopyPixels(new Int32Rect(x0, y0, w, h), px, stride, 0);

            // 덩어리마다 번호를 매기며 가장 큰 것을 찾는다(keepAll 이면 알파 8 넘는 픽셀을 모두 남긴다).
            var label = new int[w * h];
            int best;
            if (keepAll)
            {
                best = 1;
                // 여유 3px 는 사각형 밖이라 옆 그림이 끼어들 수 있으므로, 사각형 안의 픽셀만 남긴다.
                for (int y = rect.Y - y0; y < Math.Min(h, rect.Y + rect.Height - y0); y++)
                    for (int x = rect.X - x0; x < Math.Min(w, rect.X + rect.Width - x0); x++)
                        if (px[(y * w + x) * 4 + 3] > 8) label[y * w + x] = 1;
            }
            else
            {
                var queue = new int[w * h];
                int next = 0, bestSize = 0;
                best = 0;
                for (int start = 0; start < w * h; start++)
                {
                    if (label[start] != 0 || px[start * 4 + 3] <= 8) continue;
                    next++;
                    int head = 0, tail = 0;
                    queue[tail++] = start;
                    label[start] = next;
                    while (head < tail)
                    {
                        int p = queue[head++], py = p / w, pxl = p % w;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int ny = py + dy, nx = pxl + dx;
                                if (ny < 0 || ny >= h || nx < 0 || nx >= w) continue;
                                int q = ny * w + nx;
                                if (label[q] != 0 || px[q * 4 + 3] <= 8) continue;
                                label[q] = next;
                                queue[tail++] = q;
                            }
                    }
                    if (tail > bestSize) { bestSize = tail; best = next; }
                }
                if (best == 0) return null;
            }

            int cutFrom = cutBottom > 0 ? rect.Y + rect.Height - cutBottom - y0 : h;   // 이 줄부터 아래는 지운다
            int bx0 = w, by0 = h, bx1 = -1, by1 = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int p = y * w + x;
                    if (label[p] != best || y >= cutFrom)
                    {
                        px[p * 4] = px[p * 4 + 1] = px[p * 4 + 2] = px[p * 4 + 3] = 0;
                        continue;
                    }
                    if (x < bx0) bx0 = x;
                    if (x > bx1) bx1 = x;
                    if (y < by0) by0 = y;
                    if (y > by1) by1 = y;
                }
            if (bx1 < 0) return null;

            int bw = bx1 - bx0 + 1, bh = by1 - by0 + 1, bstride = bw * 4;
            var cut = new byte[bstride * bh];
            for (int y = 0; y < bh; y++)
                Buffer.BlockCopy(px, (by0 + y) * stride + bx0 * 4, cut, y * bstride, bstride);
            return ToPiece(cut, bw, bh, x0 + bx0, y0 + by0);
        }

        /// <summary>Bgra32 픽셀로 조각을 만든다(얼려 둔다). left·top 은 모음 그림에서의 왼쪽 위.</summary>
        private static ScenePiece ToPiece(byte[] px, int w, int h, int left, int top)
        {
            BitmapSource bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, w * 4);
            bmp.Freeze();
            return new ScenePiece { Bitmap = bmp, Left = left, Top = top };
        }

        // ── 색 바꾸기 ──

        /// <summary>
        /// 조각의 색을 메모리에서 바꾼 사본: gray 면 잿빛(밝기만 남기고 조금 흐리게), shade 는 밝기를 곱한다(구름을 매연 구름으로).
        /// edge 가 1보다 크면 가장자리의 반투명 띠를 그만큼 좁힌다(SharpenAlpha). 파일은 그대로다.
        /// </summary>
        internal static ScenePiece Shaded(ScenePiece p, bool gray, double shade, double edge = 1)
        {
            if (p == null || (!gray && shade >= 1 && edge <= 1)) return p;
            if (shadedPieces.TryGetValue((p, gray, shade, edge), out ScenePiece cached)) return cached;
            BitmapSource src = p.Bitmap;
            int w = src.PixelWidth, h = src.PixelHeight, stride = w * 4;
            var px = new byte[stride * h];
            src.CopyPixels(px, stride, 0);
            for (int i = 0; i < px.Length; i += 4)
            {
                double b = px[i], g = px[i + 1], r = px[i + 2];
                if (gray)
                {
                    double v = (0.299 * r + 0.587 * g + 0.114 * b) * 0.78 + 28;
                    r = g = b = v;
                }
                px[i] = (byte)Math.Min(255, b * shade);
                px[i + 1] = (byte)Math.Min(255, g * shade);
                px[i + 2] = (byte)Math.Min(255, r * shade);
                if (edge > 1) px[i + 3] = SharpenAlpha(px[i + 3], edge);
            }
            ScenePiece result = ToPiece(px, w, h, p.Left, p.Top);
            shadedPieces[(p, gray, shade, edge)] = result;
            return result;
        }

        /// <summary>
        /// 가장자리의 반투명 띠를 k배 좁히도록 알파를 50% 지점을 중심으로 k배 가파르게 한다(k=1 이면 그대로). 50% 지점이 그대로라
        /// 윤곽선의 위치와 모양은 바뀌지 않는다.
        /// </summary>
        internal static byte SharpenAlpha(byte a, double k) => (byte)Math.Max(0, Math.Min(255, Math.Round(127.5 + (a - 127.5) * k)));

        // ── 지면 클립 아트 만들기 ──

        internal static Image PieceImage(ScenePiece p, double scale, bool flip)
        {
            var img = new Image
            {
                Source = p.Bitmap,
                Width = p.Bitmap.PixelWidth * scale,
                Height = p.Bitmap.PixelHeight * scale,
                Stretch = Stretch.Fill,
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            if (flip) img.RenderTransform = new ScaleTransform(-1, 1, img.Width / 2, 0);
            return img;
        }

        /// <summary>그림 한 장짜리 지면 클립 아트: 높이 height 로 줄여 놓는다. anchor 는 그림 가운데 x.</summary>
        internal static (Canvas Unit, double Anchor) SimpleUnit(ScenePiece p, double height, bool flip)
        {
            if (p == null) return (null, 0);
            Image img = PieceImage(p, height / p.Bitmap.PixelHeight, flip);
            var unit = new Canvas { Width = img.Width, Height = img.Height };
            unit.Children.Add(img);
            return (unit, img.Width / 2);
        }

        /// <summary>
        /// 나무 한 그루: 줄기·가지 그림(p, 높이 height) 뒤에 crown 이 있으면 녹색 큰 타원(잎)을 깐다. 타원은 가지가 난 부분을 덮고 가장
        /// 높은 가지 위로 여유가 있으며 줄기만 있는 곳도 조금 덮는다(썰렁이 검토 — 세로로 긴 타원, 너비는 crownWidth). drawn 은 실제로
        /// 그릴 조각(잿빛 등 색을 바꾼 사본 — 크기는 p 와 같다). anchor 는 그림 안의 줄기 가운데 x, frame 은 열매 같은 것을 더 그릴 기준점.
        /// </summary>
        internal static (Canvas Unit, double Anchor, TreeFrame Frame) Tree(ScenePiece p, ScenePiece drawn, double trunkX, double lowBranchY,
                                                                          double height, double crownWidth, Brush crown)
        {
            if (p == null) return (null, 0, default);
            double s = height / p.Bitmap.PixelHeight;
            double iw = p.Bitmap.PixelWidth * s, ih = height;
            double cx = (trunkX - p.Left) * s;
            double low = (lowBranchY - p.Top) * s;
            // 기준 원: 가지가 난 부분(그림 위 끝 ~ 가장 낮은 가지)의 가운데에 두고, 가지 끝까지 덮을 만큼 조금 넉넉한 반지름.
            double r = Math.Max(Math.Max(cx, iw - cx), low / 2) + ih * 0.036;
            double cy = low / 2;
            double rx = r * crownWidth, ry = r * 1.27, ey = cy + r * 0.18;
            double minX = Math.Min(0, cx - rx), maxX = Math.Max(iw, cx + rx), minY = Math.Min(0, ey - ry);
            var unit = new Canvas { Width = maxX - minX, Height = Math.Max(ih, ey + ry) - minY };
            double ox = -minX, oy = -minY;   // 줄기 그림의 왼쪽 위

            if (crown != null)
            {
                var ellipse = new Ellipse { Width = 2 * rx, Height = 2 * ry, Fill = crown };
                Canvas.SetLeft(ellipse, ox + cx - rx);
                Canvas.SetTop(ellipse, oy + ey - ry);
                unit.Children.Add(ellipse);
            }
            Image trunk = PieceImage(drawn ?? p, s, false);
            Canvas.SetLeft(trunk, ox);
            Canvas.SetTop(trunk, oy);
            unit.Children.Add(trunk);
            return (unit, ox + cx, new TreeFrame(ox, oy, cx, cy, r));
        }

        /// <summary>
        /// 꽃 식물 한 포기: 줄기와 잎(stem, 배율 s, 그림 속 줄기 x·줄기 끝 y) 위에 꽃·봉오리(head, 그림 속 밑동 x)를 겹친다. 꽃은 줄기보다
        /// headScale 배 크고, 아래 끝이 줄기 끝보다 overlap(px)만큼 내려와 줄기 끝을 덮는다. head 가 null 이면 줄기만. anchor 는 줄기 x.
        /// </summary>
        internal static (Canvas Unit, double Anchor) StemWithHead(ScenePiece stem, double stemX0, double topY0, bool stemFlip, double s,
                                                                   ScenePiece head, double stubX0, bool headFlip, double headScale, double overlap)
        {
            if (stem == null) return (null, 0);
            double iw = stem.Bitmap.PixelWidth * s, ih = stem.Bitmap.PixelHeight * s;
            double stemX = (stemX0 - stem.Left) * s;
            if (stemFlip) stemX = iw - stemX;
            double topY = (topY0 - stem.Top) * s;

            Image headImg = null;
            double headLeft = 0, headTop = 0;
            if (head != null)
            {
                double hs = s * headScale;
                headImg = PieceImage(head, hs, headFlip);
                double stubX = (stubX0 - head.Left) * hs;
                if (headFlip) stubX = headImg.Width - stubX;
                headLeft = stemX - stubX;
                headTop = topY + overlap - headImg.Height;
            }

            double minX = Math.Min(0, headImg != null ? headLeft : 0);
            double maxX = Math.Max(iw, headImg != null ? headLeft + headImg.Width : 0);
            double minY = Math.Min(0, headImg != null ? headTop : 0);
            var unit = new Canvas { Width = maxX - minX, Height = ih - minY };
            Image stemImg = PieceImage(stem, s, stemFlip);
            Canvas.SetLeft(stemImg, -minX);
            Canvas.SetTop(stemImg, -minY);
            unit.Children.Add(stemImg);
            if (headImg != null)
            {
                Canvas.SetLeft(headImg, headLeft - minX);
                Canvas.SetTop(headImg, headTop - minY);
                unit.Children.Add(headImg);
            }
            return (unit, stemX - minX);
        }

        /// <summary>
        /// 날벌레·새 한 마리와 지나간 길. 애니메이션이 아닌 그림이고, 날아온 모습은 바람처럼 물결치는 가로 점선으로 나타낸다. 그림은
        /// 너비 width(0이면 높이 height)로 줄이고, 선은 배 끝·꼬리 끝(그림 속 tipX·tipY)에서 시작해 옆으로(trailRight 면 오른쪽) 뻗는다 —
        /// 물결이 끝점에서 높이 0으로 시작하므로 선 끝이 정확히 만난다. 선은 그림 뒤에 깔린다. 그림은 모두 오른쪽을 본다. anchor 는 그림
        /// 가운데 x.
        /// </summary>
        internal static (Canvas Unit, double Anchor) Flyer(ScenePiece p, double tipX, double tipY, double width, double height,
                                                           bool facingRight, bool trailRight, double trail)
        {
            if (p == null) return (null, 0);
            double s = width > 0 ? width / p.Bitmap.PixelWidth : height / p.Bitmap.PixelHeight;
            Image img = PieceImage(p, s, !facingRight);
            double iw = img.Width, ih = img.Height;
            double tx = (tipX - p.Left) * s, ty = (tipY - p.Top) * s;
            if (!facingRight) tx = iw - tx;
            const double Amp = 2.2, Wave = 13;
            double endX = trailRight ? tx + trail : tx - trail;
            double minX = Math.Min(0, endX), maxX = Math.Max(iw, endX);
            double minY = Math.Min(0, ty - Amp - 1), maxY = Math.Max(ih, ty + Amp + 1);
            var unit = new Canvas { Width = maxX - minX, Height = maxY - minY };
            double left = -minX, top = -minY;

            var geo = new StreamGeometry();
            using (StreamGeometryContext g = geo.Open())
            {
                const int Steps = 40;
                double dir = trailRight ? 1 : -1;
                for (int i = 0; i <= Steps; i++)
                {
                    double d = trail * i / Steps;   // 끝점에서 떨어진 거리
                    var pt = new Point(left + tx + dir * d, top + ty + Amp * Math.Sin(2 * Math.PI * d / Wave));
                    if (i == 0) g.BeginFigure(pt, false, false);
                    else g.LineTo(pt, true, true);
                }
            }
            geo.Freeze();
            unit.Children.Add(new System.Windows.Shapes.Path
            {
                Data = geo,
                Stroke = TrailBrush,
                StrokeThickness = 1.1,
                StrokeDashArray = new DoubleCollection { 2, 2 },
                StrokeDashCap = PenLineCap.Round,
            });
            Canvas.SetLeft(img, left);
            Canvas.SetTop(img, top);
            unit.Children.Add(img);
            return (unit, left + iw / 2);
        }

        internal static Rectangle PlacedRect(double x, double y, double w, double h, Brush fill)
        {
            var r = new Rectangle { Width = w, Height = h, Fill = fill };
            Canvas.SetLeft(r, x);
            Canvas.SetTop(r, y);
            return r;
        }
    }
}
