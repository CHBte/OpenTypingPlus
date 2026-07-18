using System;
using System.Globalization;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;

namespace OpenTyping
{
    /// <summary>
    /// 연습 창의 렌더링 키보드 위에 겹쳐 그리는 손가락 레이어.
    /// '타자몬 참고 그림'의 손 모양(윤곽선만, 내부 투명)을 따르되 다소 마른 손을 그린다.
    /// 손 기하는 시작 시 한 번만 만들어 Freeze하므로(비트맵 없음) 실시간 메모리 부담이
    /// 거의 없고, 부모 Viewbox 배율을 그대로 따라 창 크기 조절 시 자동으로 함께 커진다.
    /// 두 검지는 [ㄹ]·[ㅓ](물리적 F/J), 두 엄지는 [Space] 위에 놓인 모양이다.
    /// </summary>
    public partial class FingerLayer : UserControl
    {
        // 키보드 좌표계(777 x 260) 기준 좌표
        private const double SpaceCenterX = 388.5; // 하단 행 Space 중심

        public FingerLayer()
        {
            InitializeComponent();

            // 왼손: 새끼 ㅁ(127) → 약지 ㄴ(179) → 중지 ㅇ(231) → 검지 ㄹ(283), 엄지는 Space 왼쪽 절반
            LeftHandPath.Data = BuildHandGeometry(
                new[] { 127.0, 179.0, 231.0, 283.0 },
                new[] { 122.0, 112.0, 108.0, 112.0 },
                dir: +1,
                thumbTipX: SpaceCenterX - 24,
                thumbTipY: 228);

            // 오른손: 새끼 ;(595) → 약지 ㅣ(543) → 중지 ㅏ(491) → 검지 ㅓ(439).
            // 오른 손바닥은 [Space] 오른쪽 끝과 가까워 엄지가 왼쪽 아래로 비스듬히 향한다.
            RightHandPath.Data = BuildHandGeometry(
                new[] { 595.0, 543.0, 491.0, 439.0 },
                new[] { 122.0, 112.0, 108.0, 112.0 },
                dir: -1,
                thumbTipX: SpaceCenterX + 4,
                thumbTipY: 246);

            ApplySettings();
        }

        /// <summary>사용자 설정(색·투명도·두께)을 두 손 윤곽선에 반영한다.</summary>
        public void ApplySettings()
        {
            Properties.Settings settings = Properties.Settings.Default;

            Color color;
            try
            {
                color = (Color)ColorConverter.ConvertFromString(settings.FingerLayerColor);
            }
            catch (FormatException)
            {
                color = Color.FromRgb(0x3B, 0xC9, 0xDB); // 손상된 설정값이면 기본색
            }

            double opacity = Math.Max(0.05, Math.Min(1.0, settings.FingerLayerOpacity));
            var brush = new SolidColorBrush(
                Color.FromArgb((byte)Math.Round(opacity * 255), color.R, color.G, color.B));
            brush.Freeze();

            double thickness = Math.Max(0.5, Math.Min(12.0, settings.FingerLayerThickness));

            LeftHandPath.Stroke = brush;
            RightHandPath.Stroke = brush;
            LeftHandPath.StrokeThickness = thickness;
            RightHandPath.StrokeThickness = thickness;
        }

        /// <summary>
        /// 손 하나의 윤곽 기하를 만든다. 새끼손가락 쪽 손바닥 밑에서 시작해
        /// 새끼→약지→중지→검지 순서로 손가락을 그리고 엄지를 지나 손바닥 밑으로 끝난다
        /// (밑변은 열어 두어 화면 아래로 잘린 듯 보이게 한다).
        /// </summary>
        /// <param name="fingerXs">손끝 x 4개: 새끼, 약지, 중지, 검지 순.</param>
        /// <param name="fingerTipYs">각 손끝의 y (작을수록 위).</param>
        /// <param name="dir">왼손 +1(새끼→검지가 오른쪽 방향), 오른손 -1.</param>
        /// <param name="thumbTipX">엄지 손끝 x ([Space] 위).</param>
        /// <param name="thumbTipY">엄지 손끝 y ([Space] 위).</param>
        private static Geometry BuildHandGeometry(double[] fingerXs, double[] fingerTipYs,
                                                  int dir, double thumbTipX, double thumbTipY)
        {
            const double fingerHalf = 13;  // 손가락 반폭 (마른 손)
            const double valleyY = 170;    // 손가락 사이 골 y
            const double palmBottomY = 315; // 손바닥이 잘리는 y (키보드 아래 밖)

            var sb = new StringBuilder();
            int sweep = dir > 0 ? 1 : 0; // 반대쪽 손은 호의 방향도 반대

            double pinkyX = fingerXs[0];

            // 손바닥 새끼손가락 쪽 밑에서 시작해 손날을 따라 올라간다
            Move(sb, pinkyX - dir * 30, palmBottomY);
            Cubic(sb,
                pinkyX - dir * 33, 255,
                pinkyX - dir * 27, 205,
                pinkyX - dir * 20, valleyY);

            for (int i = 0; i < 4; i++)
            {
                double x = fingerXs[i];
                double tipY = fingerTipYs[i];

                if (i > 0)
                {
                    // 골: 이전 손가락 안쪽에서 이번 손가락 바깥쪽으로
                    double prevInner = fingerXs[i - 1] + dir * (fingerHalf + 3);
                    double outer = x - dir * (fingerHalf + 3);
                    Quad(sb, (prevInner + outer) / 2, valleyY + 12, outer, valleyY);
                }

                // 바깥쪽 모서리를 따라 손끝까지
                Cubic(sb,
                    x - dir * (fingerHalf + 1), valleyY - 24,
                    x - dir * fingerHalf, tipY + 26,
                    x - dir * fingerHalf, tipY + 10);
                // 손끝 (둥근 호)
                Arc(sb, fingerHalf, sweep, x + dir * fingerHalf, tipY + 10);
                // 안쪽 모서리를 따라 골까지
                Cubic(sb,
                    x + dir * fingerHalf, tipY + 26,
                    x + dir * (fingerHalf + 1), valleyY - 24,
                    x + dir * (fingerHalf + 3), valleyY);
            }

            double indexX = fingerXs[3];

            // ---- 엄지: 손바닥 안쪽 가장자리의 갈림점 S에서 [Space] 위 손끝 T까지,
            //      엄지 축(u)과 위쪽 법선(n)으로 양변을 계산해 그린다 ----
            const double thumbHalf = 9;  // 엄지 반폭 (마른 손)

            var thumbStart = new System.Windows.Point(indexX + dir * 27, 218); // S
            var thumbTip = new System.Windows.Point(thumbTipX, thumbTipY);     // T

            System.Windows.Vector axis = thumbTip - thumbStart;
            axis.Normalize();
            var normal = new System.Windows.Vector(-axis.Y, axis.X); // 법선
            if (normal.Y > 0) normal = -normal;                      // 항상 위쪽(손등 쪽)을 향하게

            System.Windows.Point upperStart = thumbStart + normal * thumbHalf;
            System.Windows.Point upperEnd = thumbTip - axis * thumbHalf + normal * thumbHalf;
            System.Windows.Point lowerStart = thumbTip - axis * thumbHalf - normal * thumbHalf;
            System.Windows.Point lowerEnd = thumbStart - normal * thumbHalf + axis * 6;

            // 검지 안쪽에서 손바닥을 따라 엄지 갈림점(윗변 시작점)까지
            Cubic(sb,
                indexX + dir * 18, valleyY + 26,
                indexX + dir * 23, 200,
                upperStart.X, upperStart.Y);
            // 엄지 윗변 (살짝 바깥으로 볼록하게)
            System.Windows.Point upperMid = upperStart + (upperEnd - upperStart) * 0.5 + normal * 2;
            Quad(sb, upperMid.X, upperMid.Y, upperEnd.X, upperEnd.Y);
            // 손끝 반원. 호의 볼록한 쪽이 손끝(axis) 방향이 되도록, 호의 중간 지점
            // 후보(±)를 실제로 계산해 axis 쪽에 가까운 감는 방향을 고른다.
            System.Windows.Point chordMid = upperEnd + (lowerStart - upperEnd) * 0.5;
            System.Windows.Point convexTarget = thumbTip + axis * 2;
            var chordDir = lowerStart - upperEnd;
            chordDir.Normalize();
            var chordPerp = new System.Windows.Vector(-chordDir.Y, chordDir.X);
            // sweep=1(시계 방향)일 때 호의 중간이 진행 방향의 왼쪽(+perp)인지 오른쪽(-perp)인지는
            // WPF 좌표(아래로 증가)에서 오른쪽(-perp)이다.
            System.Windows.Point midIfSweep1 = chordMid - chordPerp * (thumbHalf + 2) * 0.6;
            System.Windows.Point midIfSweep0 = chordMid + chordPerp * (thumbHalf + 2) * 0.6;
            int thumbSweep =
                (midIfSweep1 - convexTarget).LengthSquared <= (midIfSweep0 - convexTarget).LengthSquared
                    ? 1 : 0;
            Arc(sb, thumbHalf + 2, thumbSweep, lowerStart.X, lowerStart.Y);
            // 엄지 아랫변으로 되돌아온다
            System.Windows.Point lowerMid = lowerStart + (lowerEnd - lowerStart) * 0.5 - normal * 2;
            Quad(sb, lowerMid.X, lowerMid.Y, lowerEnd.X, lowerEnd.Y);
            // 손바닥 안쪽 가장자리를 따라 아래로
            Cubic(sb,
                lowerEnd.X - dir * 6, lowerEnd.Y + 24,
                lowerEnd.X - dir * 9, lowerEnd.Y + 55,
                lowerEnd.X - dir * 10, palmBottomY);

            var geometry = Geometry.Parse(sb.ToString());
            geometry.Freeze();
            return geometry;
        }

        private static void Move(StringBuilder sb, double x, double y)
        {
            sb.Append(string.Format(CultureInfo.InvariantCulture, "M {0:0.#},{1:0.#} ", x, y));
        }

        private static void Cubic(StringBuilder sb, double c1X, double c1Y,
                                  double c2X, double c2Y, double x, double y)
        {
            sb.Append(string.Format(CultureInfo.InvariantCulture,
                "C {0:0.#},{1:0.#} {2:0.#},{3:0.#} {4:0.#},{5:0.#} ",
                c1X, c1Y, c2X, c2Y, x, y));
        }

        private static void Quad(StringBuilder sb, double cX, double cY, double x, double y)
        {
            sb.Append(string.Format(CultureInfo.InvariantCulture,
                "Q {0:0.#},{1:0.#} {2:0.#},{3:0.#} ", cX, cY, x, y));
        }

        private static void Arc(StringBuilder sb, double radius, int sweep, double x, double y)
        {
            sb.Append(string.Format(CultureInfo.InvariantCulture,
                "A {0:0.#},{0:0.#} 0 0 {1} {2:0.#},{3:0.#} ", radius, sweep, x, y));
        }
    }
}
