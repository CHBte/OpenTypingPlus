using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MahApps.Metro.Controls;

namespace OpenTyping
{
    /// <summary>
    /// 손가락 레이어 설정 창 (연습 창의 "손 모양" 버튼으로 여는 3단계 창).
    /// 켬/끔·두께·색·투명도를 조절하며, 바꾸는 즉시 연습 창의 레이어에 반영된다.
    /// 값은 사용자 설정으로 저장되어 다음 실행에도 유지된다.
    /// </summary>
    public partial class HandSettingsWindow : MetroWindow
    {
        private readonly FingerLayer fingerLayer;
        private bool initializing = true;

        // 프리셋 색. 기본값은 7번째(주황 #F76707)다 (<260812_16>) — Settings 의 FingerLayerColor
        // 기본값과 같아야 하며, 처음 설치한 PC 에서 그 색으로 시작한다.
        private static readonly string[] SwatchColors =
        {
            "#3BC9DB", // 청록
            "#FFFFFF", // 흰색
            "#212529", // 먹색
            "#F03E3E", // 빨강
            "#1C7ED6", // 파랑
            "#37B24D", // 초록
            "#F76707", // 주황 ← 기본값 (7번째)
            "#AE3EC9", // 보라
        };

        public HandSettingsWindow(FingerLayer fingerLayer)
        {
            InitializeComponent();
            Title = VersionInfo.WindowTitle; // <2600912_5-1>

            this.fingerLayer = fingerLayer;

            EnabledToggle.IsOn = UserSettingsStore.FingerLayerEnabled;
            ThicknessSlider.Value = UserSettingsStore.FingerLayerThickness;
            OpacitySlider.Value = Math.Round(UserSettingsStore.FingerLayerOpacity * 100);
            BuildColorSwatches(UserSettingsStore.FingerLayerColor);
            UpdateLabels();

            initializing = false;
        }

        private void BuildColorSwatches(string currentColor)
        {
            ColorSwatchPanel.Children.Clear();

            foreach (string colorText in SwatchColors)
            {
                var brushColor = (Color)ColorConverter.ConvertFromString(colorText);
                var swatch = new Button
                {
                    Width = 34,
                    Height = 34,
                    Margin = new Thickness(0, 0, 8, 0),
                    Background = new SolidColorBrush(brushColor),
                    BorderBrush = Brushes.Black,
                    BorderThickness = IsSameColor(colorText, currentColor)
                        ? new Thickness(3)
                        : new Thickness(1),
                    Tag = colorText,
                    // <260812_16> 마우스를 올려도 견본 색이 바뀌지 않게 하는 전용 모양
                    Style = (Style)FindResource("ColorSwatchButton")
                };
                swatch.Click += Swatch_Click;
                ColorSwatchPanel.Children.Add(swatch);
            }
        }

        private static bool IsSameColor(string lhs, string rhs)
        {
            return string.Equals(lhs, rhs, StringComparison.OrdinalIgnoreCase);
        }

        private void Swatch_Click(object sender, RoutedEventArgs e)
        {
            string colorText = (string)((Button)sender).Tag;
            UserSettingsStore.FingerLayerColor = colorText;

            foreach (Button swatch in ColorSwatchPanel.Children)
            {
                swatch.BorderThickness = IsSameColor((string)swatch.Tag, colorText)
                    ? new Thickness(3)
                    : new Thickness(1);
            }

            ApplyToLayer();
        }

        private void EnabledToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (initializing) return;

            UserSettingsStore.FingerLayerEnabled = EnabledToggle.IsOn;
            fingerLayer.Visibility = EnabledToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (initializing) return;

            UserSettingsStore.FingerLayerThickness = ThicknessSlider.Value;
            UpdateLabels();
            ApplyToLayer();
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (initializing) return;

            UserSettingsStore.FingerLayerOpacity = OpacitySlider.Value / 100.0;
            UpdateLabels();
            ApplyToLayer();
        }

        private void UpdateLabels()
        {
            ThicknessLabel.Text = ThicknessSlider.Value.ToString("0.0");
            OpacityLabel.Text = OpacitySlider.Value.ToString("0") + "%";
        }

        private void ApplyToLayer()
        {
            fingerLayer.ApplySettings();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void HandSettingsWindow_Closed(object sender, EventArgs e)
        {
            if (!UserSettingsStore.TrySave(out string error))
            {
                MessageBox.Show("설정을 저장하지 못했습니다.\n" + error,
                                "열린타자+",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
            }
        }
    }
}
