using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace OpenTyping
{
    /// <summary>
    /// `자판 표시`(<260927_9>): 자판 이름(파랑)·문자 종류(초록) 사각형 한 쌍. 원래 '대문' 화면에만
    /// 있던 것을 '자리연습'·'음절연습'·'문장연습'·'긴글연습' 화면에도 두려고 뽑아낸 컨트롤이다
    /// (<260927_10>). 화면마다 두는 위치는 각 메뉴의 XAML이 정한다(전부 큰 제목 글씨의 오른쪽,
    /// 오른쪽·위쪽 정렬). <260927_13>: 클릭하면 '설정' 창을 열도록, MainWindow가 구독하는 Click
    /// 이벤트를 낸다(실제로 여는 동작은 MainWindow.SettingsButton_Click이 그대로 담당).
    /// </summary>
    public partial class KeyLayoutIndicator : UserControl
    {
        public event EventHandler Click;

        public KeyLayoutIndicator()
        {
            InitializeComponent();
            Refresh();
        }

        /// <summary>지금의 <see cref="MainWindow.CurrentKeyLayout"/> 기준으로 두 글자를 다시 묶는다.
        /// 설정에서 자판이 바뀔 때마다 MainWindow가 화면마다 하나씩 있는 이 컨트롤을 불러 다시 부른다
        /// (HomeMenu가 자기 텍스트 두 개를 직접 재바인딩하던 것과 같은 방식).</summary>
        public void Refresh()
        {
            CurrentKeyLayoutName.SetBinding(TextBlock.TextProperty,
                new Binding("Name") { Source = MainWindow.CurrentKeyLayout });
            CurrentKeyLayoutChar.SetBinding(TextBlock.TextProperty,
                new Binding("Character") { Source = MainWindow.CurrentKeyLayout });
        }

        // 버튼처럼: 이 위에서 누르고 이 위에서 뗐을 때만 클릭이다(다른 곳에서 누른 채 끌어와 놓은 것은 제외).
        private bool pressed;

        private void RootPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            pressed = ((UIElement)sender).CaptureMouse();
            e.Handled = true;
        }

        private void RootPanel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var panel = (UIElement)sender;
            bool wasPressed = pressed;
            pressed = false;
            panel.ReleaseMouseCapture();
            if (wasPressed && panel.IsMouseOver) Click?.Invoke(this, EventArgs.Empty);
        }

        // 누른 채 다른 창으로 전환되는 등 캡처를 잃으면 누름도 취소한다.
        private void RootPanel_LostMouseCapture(object sender, MouseEventArgs e) => pressed = false;
    }
}
