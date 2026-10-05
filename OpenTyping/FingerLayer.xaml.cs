using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OpenTyping
{
    /// <summary>
    /// 연습 창의 렌더링 키보드 위에 겹쳐 그리는 손가락 레이어.
    /// 손 그림은 hands\*.svg(손 모델 3D 로 "손 모양 json 추출"한 결과)를 그대로 읽어 그린다.
    /// 제시된 키에 그 키 전용 SVG(hands\{행}-{열}-{손}.svg)가 있으면 그것을, 없으면 기본 자리
    /// 그림(hands\home-{손}.svg)을 그대로 쓴다 (<260927_8>) — 프레임이 없는 키를 위해 기본자세를
    /// 손가락별로 합성 변형해 그리던 예전 코드(ContourHand)는 걷어냈다: 물리 키별 SVG가 전부
    /// 갖춰지면서 그 코드가 이미 실행되지 않고 있었고, 남은 유일한 예외(배정 자체가 없는 키)는
    /// SetPose 가 애초에 <see cref="ShowHomePose"/>로 바로 보내 그 코드에 닿지 않았다.
    /// 누르는 손가락 외의 손끝은 기본 자리([ㅁ][ㄴ][ㅇ][ㄹ] / [ㅓ][ㅏ][ㅣ][;])에, 두 엄지는
    /// [Space] 위에 있다 (<260718_10-(2)>).
    /// 파일에서 읽은 Geometry는 Freeze 후 파일 이름으로 캐시하므로, 키를 반복해 눌러도 디스크를
    /// 다시 읽지 않는다.
    /// </summary>
    public partial class FingerLayer : UserControl
    {
        private static bool TryGetFinger(KeyPos pos, out bool isRight, out int finger) =>
            ContourHand.TryGetFinger(pos.Row, pos.Column, out isRight, out finger);

        /// <summary>SVG가 전혀 없을 때(설치 손상 등 최후의 경우) 그릴 빈 윤곽 — 화면엔 아무것도 안 보인다.</summary>
        private static readonly Geometry EmptyGeometry = Freeze(new PathGeometry());

        private static Geometry Freeze(Geometry g) { g.Freeze(); return g; }

        public FingerLayer()
        {
            InitializeComponent();

            ShowHomePose();
            ApplySettings();
        }

        /// <summary>
        /// 마지막으로 적용한 손 그림의 출처(hands\ 파일 이름, 아무 SVG도 없으면 "(없음)").
        /// 설치·배선이 어긋나지 않았는지 밖에서 확인하기 위한 것으로, 화면 동작에는 영향이 없다.
        /// </summary>
        public string LastLeftSource { get; private set; } = "(none)";
        public string LastRightSource { get; private set; } = "(none)";

        /// <summary>fileName 을 읽어 그리되, 없으면 그 손의 기본 자리 그림(home-{side}.svg)으로,
        /// 그것마저 없으면 빈 윤곽으로 폴백한다 (<260927_8>).</summary>
        private static Geometry LoadOrHome(string fileName, string side, out string source)
        {
            Geometry g = TryLoadHandSvg(fileName);
            if (g != null) { source = fileName; return g; }

            string homeFile = $"home-{side}.svg";
            Geometry home = TryLoadHandSvg(homeFile);
            source = home != null ? homeFile : "(없음)";
            return home ?? EmptyGeometry;
        }

        /// <summary>두 손을 기본 자리(홈 포지션) 포즈로 되돌린다.</summary>
        public void ShowHomePose()
        {
            LeftHandPath.Data = LoadOrHome("home-left.svg", "left", out string ls);
            RightHandPath.Data = LoadOrHome("home-right.svg", "right", out string rs);
            LastLeftSource = ls; LastRightSource = rs;
        }

        /// <summary>
        /// 실행 파일 옆 hands\&lt;fileName&gt; 을 읽어 그 안의 첫 &lt;path d="..."&gt; 값을 Geometry 로
        /// 만든다. 손 모델 3D 의 SVG 출력이 이미 WPF Path.Data 미니 언어와 같은 "M x y L x y ... Z"
        /// 형식이라 별도 변환 없이 그대로 Geometry.Parse 에 넣을 수 있다. 파일이 없거나 읽기 실패하면
        /// null(호출부가 기본 자리 그림으로 폴백).
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

            // <260927_8> 손 모델 3D 로 만든 키별 손 모양(hands\{행}-{열}-{손}.svg)이 있으면 그것을 쓰고,
            // 없으면 그 손의 기본 자리 그림(home-{손}.svg)을 그대로 쓴다(합성 변형은 더 이상 하지 않는다).
            // 윗글쇠면 반대 손이 자기 쪽 [Shift]를 누르는 모양(lshift/rshift.svg)이 되고, 아니면
            // 기본자세 그대로 둔다.
            string actSide = isRight ? "right" : "left";
            string otherSide = isRight ? "left" : "right";

            Geometry actingGeometry = LoadOrHome($"{pos.Row}-{pos.Column}-{actSide}.svg", actSide, out string actSrc);
            Geometry otherGeometry = isShift
                ? LoadOrHome(isRight ? "lshift.svg" : "rshift.svg", otherSide, out string otherSrc)
                : LoadOrHome($"home-{otherSide}.svg", otherSide, out otherSrc);

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
    }
}
