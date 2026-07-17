using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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

        public bool Pressed { get; private set; } = false;

        private const double PressDiff = 2.8;
        private const double PressedKeyTopHeight = 43.7; // 눌린 KeyTop 높이 (PressDiff와 무관하게 고정)
        private double originalKeyTopHeight;
        private Brush defaultKeyColor;
        private Brush defaultShadowColor;

        private static readonly Brush CorrectKeyColor = new SolidColorBrush(Color.FromRgb(140, 233, 154));
        private static readonly Brush CorrectKeyShadowColor = new SolidColorBrush(Color.FromRgb(105, 219, 124));

        private static readonly Brush IncorrectKeyColor = new SolidColorBrush(Color.FromRgb(255, 168, 168));
        private static readonly Brush IncorrectKeyShadowColor = new SolidColorBrush(Color.FromRgb(255, 135, 135));

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

        private void Press(Brush keyColor, Brush shadowColor)
        {
            if (!Pressed)
            {
                KeyTop.Height = PressedKeyTopHeight;
                Canvas.SetTop(KeyTop, PressDiff);
                KeyBack.Height -= PressDiff;
                Canvas.SetTop(KeyBack, PressDiff);
            }

            KeyColor = keyColor;
            ShadowColor = shadowColor;

            Pressed = true;
        }

        public void PressCorrect()
        {
            Press(CorrectKeyColor, CorrectKeyShadowColor);
        }

        public void PressIncorrect()
        {
            Press(IncorrectKeyColor, IncorrectKeyShadowColor);
        }

        public void Release()
        {
            if (Pressed)
            {
                KeyTop.Height = originalKeyTopHeight;
                Canvas.SetTop(KeyTop, 0);
                KeyBack.Height += PressDiff;
                Canvas.SetTop(KeyBack, 0);
            }

            KeyColor = defaultKeyColor;
            ShadowColor = defaultShadowColor;

            Pressed = false;
        }

        public void PressToggle()
        {
            if (Pressed) Release();
            else PressCorrect();
        }
    }
}
