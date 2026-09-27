using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OpenTyping
{
    /// <summary>
    /// KeyBox.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class KeyBox : UserControl
    {
        public Brush KeyColor
        {
            get => (Brush)GetValue(KeyColorProperty);
            set => SetValue(KeyColorProperty, value);
        }
        public static readonly DependencyProperty KeyColorProperty =
            DependencyProperty.Register("KeyColor", typeof(Brush), typeof(KeyBox), new PropertyMetadata(Brushes.White));

        public Brush ShadowColor
        {
            get => (Brush)GetValue(ShadowColorProperty);
            set => SetValue(ShadowColorProperty, value);
        }
        public static readonly DependencyProperty ShadowColorProperty =
            DependencyProperty.Register("ShadowColor", typeof(Brush), typeof(KeyBox), new PropertyMetadata(new SolidColorBrush(Color.FromRgb(206, 212, 218))));

        public Key Key
        {
            get => (Key)GetValue(KeyProperty);
            set => SetValue(KeyProperty, value);
        }
        public static readonly DependencyProperty KeyProperty =
            DependencyProperty.Register("Key", typeof(Key), typeof(KeyBox));

        // 검지손가락 홈 포지션(두벌식 표준 기준 ㄹ·ㅓ 자리, 즉 물리적 F/J 위치)인지 여부.
        // 어떤 자판이 로드되어 있든 이 자리에 나타나는 값에는 밑줄을 긋는다.
        public bool IsHomePosition
        {
            get => (bool)GetValue(IsHomePositionProperty);
            set => SetValue(IsHomePositionProperty, value);
        }
        public static readonly DependencyProperty IsHomePositionProperty =
            DependencyProperty.Register("IsHomePosition", typeof(bool), typeof(KeyBox), new PropertyMetadata(false));

        private const double PressDiff = 2.8;
        private const double PressedKeyTopHeight = 43.7; // 눌린 KeyTop 높이 (PressDiff와 무관하게 고정)
        private double originalKeyTopHeight;
        private Brush defaultKeyColor;
        private Brush defaultShadowColor;

        private static readonly Brush CorrectKeyColor = new SolidColorBrush(Color.FromRgb(140, 233, 154));
        private static readonly Brush CorrectKeyShadowColor = new SolidColorBrush(Color.FromRgb(105, 219, 124));

        private static readonly Brush IncorrectKeyColor = new SolidColorBrush(Color.FromRgb(255, 168, 168));
        private static readonly Brush IncorrectKeyShadowColor = new SolidColorBrush(Color.FromRgb(255, 135, 135));

        // 연습 창 타이핑 트리거용: 제시된 키를 맞게 눌렀을 때의 색 (짙은 노란색)
        private static readonly Brush PhysicalPressKeyColor = new SolidColorBrush(Color.FromRgb(250, 176, 5));
        private static readonly Brush PhysicalPressKeyShadowColor = new SolidColorBrush(Color.FromRgb(245, 159, 0));

        // 1단계 창 검지 자리 안내([ㄹ]·[ㅓ]) 강조색 (#1c7ed6)
        private static readonly Brush GuideKeyColor = new SolidColorBrush(Color.FromRgb(0x1C, 0x7E, 0xD6));
        private static readonly Brush GuideKeyShadowColor = new SolidColorBrush(Color.FromRgb(0x18, 0x64, 0xAB));

        public KeyBox()
        {
            InitializeComponent();

            originalKeyTopHeight = KeyTop.Height;
            defaultKeyColor = KeyColor;
            defaultShadowColor = ShadowColor;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            defaultKeyColor = KeyColor;
            defaultShadowColor = ShadowColor;
        }

        private void SetPressedShape(bool pressed)
        {
            if (pressed && !shapePressed)
            {
                KeyTop.Height = PressedKeyTopHeight;
                Canvas.SetTop(KeyTop, PressDiff);
                KeyBack.Height -= PressDiff;
                Canvas.SetTop(KeyBack, PressDiff);
            }
            else if (!pressed && shapePressed)
            {
                KeyTop.Height = originalKeyTopHeight;
                Canvas.SetTop(KeyTop, 0);
                KeyBack.Height += PressDiff;
                Canvas.SetTop(KeyBack, 0);
            }

            shapePressed = pressed;
        }

        private bool shapePressed; // 현재 키가 눌린 모양인지 (색과 별개로 추적)

        // ===== 연습 창 타이핑 트리거 (제시 키 = 녹색·안 눌림 / 물리로 누른 키 = 주황·눌림) =====

        private bool isTarget;                  // 상단에 제시된 키값의 키인지
        private bool physicallyPressed;         // 사용자가 지금 물리적으로 누르고 있는지
        private bool physicallyPressedIncorrect; // 눌린 키가 오답인지 (빨간색 표시)
        private bool guideHighlighted;          // 1단계 검지 자리 안내 강조인지 (#1c7ed6)

        // 색만 상태에 맞게 다시 칠한다.
        // 우선순위: 물리적으로 눌림(정답=주황, 오답=빨강) > 경고 깜빡임(빨강) > 안내 강조(파랑)
        //          > 제시 키(녹색) > 기본(흰색)
        private void ApplyTriggerColor()
        {
            if (physicallyPressed)
            {
                KeyColor = physicallyPressedIncorrect ? IncorrectKeyColor : PhysicalPressKeyColor;
                ShadowColor = physicallyPressedIncorrect ? IncorrectKeyShadowColor : PhysicalPressKeyShadowColor;
            }
            else if (warnBlinkLit)
            {
                KeyColor = IncorrectKeyColor;
                ShadowColor = IncorrectKeyShadowColor;
            }
            else if (guideHighlighted)
            {
                KeyColor = GuideKeyColor;
                ShadowColor = GuideKeyShadowColor;
            }
            else if (isTarget)
            {
                KeyColor = CorrectKeyColor;
                ShadowColor = CorrectKeyShadowColor;
            }
            else
            {
                KeyColor = defaultKeyColor;
                ShadowColor = defaultShadowColor;
            }
        }

        /// <summary>제시 키 여부를 지정한다. 제시 키는 녹색이지만 모양은 눌리지 않은 채로 둔다.</summary>
        public void SetTarget(bool target)
        {
            isTarget = target;
            ApplyTriggerColor();
        }

        /// <summary>연습할 키로 지정(선택)돼 녹색·안 눌린 모양인지 여부. 키 선택 UI가 선택 상태를 읽는 데 쓴다 (<260718_5-1>).</summary>
        public bool IsTarget => isTarget;

        /// <summary>1단계 검지 자리 안내 강조(#1c7ed6)를 켜거나 끈다.</summary>
        public void SetGuideHighlight(bool on)
        {
            guideHighlighted = on;
            ApplyTriggerColor();
        }

        /// <summary>제시된 키를 맞게 눌렀다: 눌린 모양 + 짙은 오렌지색.</summary>
        public void PressPhysical()
        {
            physicallyPressed = true;
            physicallyPressedIncorrect = false;
            SetPressedShape(true);
            ApplyTriggerColor();
        }

        /// <summary>제시된 키가 아닌 키를 눌렀다: 눌린 모양 + 빨간색 (원본 프로그램과 동일한 오답 표시).</summary>
        public void PressPhysicalIncorrect()
        {
            physicallyPressed = true;
            physicallyPressedIncorrect = true;
            SetPressedShape(true);
            ApplyTriggerColor();
        }

        /// <summary>사용자가 이 키에서 손을 뗐다: 안 눌린 모양 + 원래 색(제시 키면 녹색, 아니면 흰색).</summary>
        public void ReleasePhysical()
        {
            physicallyPressed = false;
            physicallyPressedIncorrect = false;
            SetPressedShape(false);
            ApplyTriggerColor();
        }

        /// <summary>홈 포지션 밑줄을 StopBlink()가 호출될 때까지 계속 깜빡인다 (1단계 창의 검지 자리 안내용, <260717_29-3>).</summary>
        public void BlinkHomeUnderline()
        {
            var blink = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
            blink.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            blink.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.3))));
            blink.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.6))));
            HomePositionUnderline.BeginAnimation(OpacityProperty, blink);
        }

        private System.Windows.Threading.DispatcherTimer warnBlinkTimer;
        private bool warnBlinkLit;   // 경고 깜빡임의 '켜진' 순간인지

        /// <summary>
        /// <260927_3>(2) 키를 오답 색(빨강)으로 깜빡이거나 멈춘다([Caps Lock]이 켜졌을 때 알림용).
        /// 오답 색은 기존 오답 표시와 같은 색을 쓴다(디자인 통일 원칙 1).
        /// </summary>
        public void SetWarnBlink(bool on)
        {
            if (on)
            {
                if (warnBlinkTimer != null) return;
                warnBlinkLit = true;
                warnBlinkTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(0.5) };
                warnBlinkTimer.Tick += (s, e) => { warnBlinkLit = !warnBlinkLit; ApplyTriggerColor(); };
                warnBlinkTimer.Start();
            }
            else
            {
                warnBlinkTimer?.Stop();
                warnBlinkTimer = null;
                warnBlinkLit = false;
            }
            ApplyTriggerColor();
        }

        /// <summary>검사용: 경고 깜빡임이 돌고 있는지.</summary>
        internal bool IsWarnBlinking => warnBlinkTimer != null;

        /// <summary>BlinkHomeUnderline()으로 시작한 깜빡임을 멈추고 밑줄을 원래(항상 보임) 상태로 되돌린다.</summary>
        public void StopBlink()
        {
            HomePositionUnderline.BeginAnimation(OpacityProperty, null);
        }
    }
}
