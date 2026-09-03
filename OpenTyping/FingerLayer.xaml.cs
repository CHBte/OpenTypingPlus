using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OpenTyping
{
    /// <summary>
    /// 연습 창의 렌더링 키보드 위에 겹쳐 그리는 손가락 레이어.
    /// 손 그림은 fingersmaple1 참고 그림(키별 실제 손 포즈 프레임)에서 추출한 실측 윤곽
    /// 라이브러리(HandContourData → ContourHand.BuildLibrary)를 키보드 좌표계에 정합해
    /// 그대로 그린다 (<260718_10>). 제시된 키에 해당 프레임의 실제 포즈가 표시되며(SetPose),
    /// 프레임이 없는 키(숫자 행, [ㅕ])만 기본자세에서 담당 손가락을 합성 변형한다.
    /// 누르는 손가락 외의 손끝은 기본 자리([ㅁ][ㄴ][ㅇ][ㄹ] / [ㅓ][ㅏ][ㅣ][;])에, 두 엄지는
    /// [Space] 위에 있다 (<260718_10-(2)>).
    /// 포즈 기하는 처음 쓰일 때 한 번만 만들어 Freeze 후 캐시하므로(비트맵 없음) 실시간
    /// 부담이 거의 없고, 부모 Viewbox 배율을 따라 창 크기 조절 시 자동으로 함께 커진다.
    /// </summary>
    public partial class FingerLayer : UserControl
    {
        // ===== 실측 손 포즈 라이브러리 (기본자세 + 키별 프레임, 본체·엄지 두 폐곡선) =====
        private static readonly ContourHand LeftHand = ContourHand.BuildLibrary(isRight: false);
        private static readonly ContourHand RightHand = ContourHand.BuildLibrary(isRight: true);

        private static bool TryGetFinger(KeyPos pos, out bool isRight, out int finger) =>
            ContourHand.TryGetFinger(pos.Row, pos.Column, out isRight, out finger);

        // ===== 포즈 기하 캐시 =====
        // key: (오른손 여부, row, col). row -1 = 홈 포즈, row -2 = 자기 쪽 [Shift] 포즈.
        // 두벌식 고정 좌표라 창이 여러 번 열려도 공유되고, 전부 만들어져도 수백 KB 수준이다.
        private static readonly Dictionary<(bool IsRight, int Row, int Col), Geometry> PoseCache =
            new Dictionary<(bool IsRight, int Row, int Col), Geometry>();

        public FingerLayer()
        {
            InitializeComponent();

            ShowHomePose();
            ApplySettings();
        }

        /// <summary>
        /// <260811_31-2> 마지막으로 적용한 손 그림의 출처(hands\ 파일 이름, 폴백이면 "(contour)").
        /// 설치·배선이 어긋나지 않았는지 밖에서 확인하기 위한 것으로, 화면 동작에는 영향이 없다.
        /// </summary>
        public string LastLeftSource { get; private set; } = "(none)";
        public string LastRightSource { get; private set; } = "(none)";

        /// <summary>hands\ 의 SVG 가 있으면 그것을, 없으면 기존 ContourHand 합성을 쓰고 출처를 알려 준다.</summary>
        private static Geometry LoadOrFallback(string fileName, Func<Geometry> fallback, out string source)
        {
            Geometry g = TryLoadHandSvg(fileName);
            source = g != null ? fileName : "(contour)";
            return g != null ? g : fallback();
        }

        /// <summary>
        /// 두 손을 기본 자리(홈 포지션) 포즈로 되돌린다.
        /// <260811_26>(2-1) 테스트: hands\home-left.svg / home-right.svg 가 있으면 그 벡터(손 모델
        /// 3D 로 "손 모양 json 추출"한 결과)를 쓰고, 없으면 기존 ContourHand 합성 결과로 그대로
        /// 폴백한다 — 아직 이 파일 하나뿐이라 나머지 포즈(SetPose)는 손대지 않았다. 다음 단계에서
        /// 물리 키별 SVG 가 갖춰지면 SetPose 도 같은 방식으로 확장하거나 ContourHand 를 대체한다.
        /// </summary>
        public void ShowHomePose()
        {
            string ls, rs;
            LeftHandPath.Data = LoadOrFallback("home-left.svg", () => GetGeometry(LeftHand, -1, -1), out ls);
            RightHandPath.Data = LoadOrFallback("home-right.svg", () => GetGeometry(RightHand, -1, -1), out rs);
            LastLeftSource = ls; LastRightSource = rs;
        }

        /// <summary>
        /// 실행 파일 옆 hands\&lt;fileName&gt; 을 읽어 그 안의 첫 &lt;path d="..."&gt; 값을 Geometry 로
        /// 만든다. 손 모델 3D 의 SVG 출력이 이미 WPF Path.Data 미니 언어와 같은 "M x y L x y ... Z"
        /// 형식이라 별도 변환 없이 그대로 Geometry.Parse 에 넣을 수 있다. 파일이 없거나 읽기 실패하면
        /// null(호출부가 기존 방식으로 폴백).
        /// </summary>
        private static readonly Dictionary<string, Geometry> SvgCache = new Dictionary<string, Geometry>();

        private static Geometry TryLoadHandSvg(string fileName)
        {
            // <260811_31> 키를 누를 때마다 호출되므로 파일별로 한 번만 읽어 캐시한다(없으면 null 캐시).
            if (SvgCache.TryGetValue(fileName, out Geometry cached)) return cached;
            Geometry result = null;
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "hands", fileName);
                if (File.Exists(path))
                {
                    string svg = File.ReadAllText(path);
                    Match m = Regex.Match(svg, "<path d=\"([^\"]*)\"");
                    if (m.Success)
                    {
                        // 손 모델 3D 의 SVG 는 조각이 여러 개여도 <path> 하나에 "M…Z M…L…" 로 담기므로
                        // (<260811_29>) 이 한 줄이 손 전체(실루엣+손가락 사이 선)를 담는다.
                        result = Geometry.Parse(m.Groups[1].Value);
                        result.Freeze();
                    }
                }
            }
            catch { result = null; }
            SvgCache[fileName] = result;
            return result;
        }

        /// <summary>
        /// 제시된 키를 담당 손가락이 누르는 포즈로 전환한다 (<260718_10>).
        /// 윗글쇠(isShift)면 반대손이 자기 쪽 [Shift]를 누르는 포즈가 된다
        /// (KeyPracticeWindow.IsRightShiftCorrect와 같은 손 분담).
        /// 배정에 없는 키(예: 연습 제외 키)면 홈 포즈로 되돌린다.
        /// </summary>
        public void SetPose(KeyPos pos, bool isShift)
        {
            if (pos == null || !TryGetFinger(pos, out bool isRight, out _))
            {
                ShowHomePose();
                return;
            }

            ContourHand acting = isRight ? RightHand : LeftHand;
            ContourHand other = isRight ? LeftHand : RightHand;

            // <260811_31> 손 모델 3D 로 만든 키별 손 모양(hands\{행}-{열}-{손}.svg)이 있으면 그것을 쓰고,
            // 없으면 기존 ContourHand 합성 결과로 폴백한다. 윗글쇠면 반대 손이 자기 쪽 [Shift]를 누르는
            // 모양(lshift/rshift.svg)이 되고, 아니면 기본자세 그대로 둔다.
            string actSide = isRight ? "right" : "left";
            string otherSide = isRight ? "left" : "right";
            string actSrc, otherSrc;
            Geometry actingGeometry = LoadOrFallback(
                $"{pos.Row}-{pos.Column}-{actSide}.svg",
                () => GetGeometry(acting, pos.Row, pos.Column), out actSrc);
            Geometry otherGeometry = isShift
                ? LoadOrFallback(isRight ? "lshift.svg" : "rshift.svg", () => GetGeometry(other, -2, -2), out otherSrc)
                : LoadOrFallback($"home-{otherSide}.svg", () => GetGeometry(other, -1, -1), out otherSrc);
            if (isRight) { LastRightSource = actSrc; LastLeftSource = otherSrc; }
            else { LastLeftSource = actSrc; LastRightSource = otherSrc; }

            if (isRight)
            {
                RightHandPath.Data = actingGeometry;
                LeftHandPath.Data = otherGeometry;
            }
            else
            {
                LeftHandPath.Data = actingGeometry;
                RightHandPath.Data = otherGeometry;
            }
        }

        /// <summary>사용자 설정(색·투명도·두께)을 두 손 윤곽선에 반영한다.</summary>
        public void ApplySettings()
        {
            Color color;
            try
            {
                color = (Color)ColorConverter.ConvertFromString(UserSettingsStore.FingerLayerColor);
            }
            catch (Exception ex) when (ex is FormatException || ex is NotSupportedException)
            {
                // 저장값이 null 이면 FormatException 이 아니라 NotSupportedException 이 난다 — 둘 다
                // '손상된 설정값'으로 보고 기본색으로 되돌린다 (<260812_16>).
                color = Color.FromRgb(0xF7, 0x67, 0x07);
            }

            double opacity = Math.Max(0.05, Math.Min(1.0, UserSettingsStore.FingerLayerOpacity));
            var brush = new SolidColorBrush(
                Color.FromArgb((byte)Math.Round(opacity * 255), color.R, color.G, color.B));
            brush.Freeze();

            double thickness = Math.Max(0.5, Math.Min(12.0, UserSettingsStore.FingerLayerThickness));

            LeftHandPath.Stroke = brush;
            RightHandPath.Stroke = brush;
            LeftHandPath.StrokeThickness = thickness;
            RightHandPath.StrokeThickness = thickness;
        }

        // ===== 포즈 기하 생성 =====

        private static Geometry GetGeometry(ContourHand hand, int row, int col)
        {
            bool isRight = ReferenceEquals(hand, RightHand);
            var key = (isRight, row, col);
            if (PoseCache.TryGetValue(key, out Geometry cached)) return cached;

            // 프레임이 있는 키는 실측 포즈, 없는 키는 기본자세에서 합성 변형 (<260718_10>)
            (ContourHand.Pt[] main, ContourHand.Pt[] thumb) = hand.GetPosePts(row, col);

            var sb = new StringBuilder();
            AppendFigure(sb, main);
            AppendFigure(sb, thumb); // 엄지는 [Space] 위 (<260718_10-(2)>)

            Geometry geometry = Geometry.Parse(sb.ToString());
            geometry.Freeze();
            PoseCache[key] = geometry;
            return geometry;
        }

        private static void AppendFigure(StringBuilder sb, ContourHand.Pt[] pts)
        {
            for (int i = 0; i < pts.Length; i++)
            {
                sb.Append(i == 0 ? "M " : "L ");
                sb.Append(string.Format(CultureInfo.InvariantCulture, "{0:0.#},{1:0.#} ",
                    pts[i].X, pts[i].Y));
            }
            sb.Append("Z ");
        }
    }
}
