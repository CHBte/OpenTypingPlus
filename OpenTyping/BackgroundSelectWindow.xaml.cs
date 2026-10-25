using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls;

namespace OpenTyping
{
    /// <summary>
    /// <261025_4> 산성비 타자 오락의 배경(`배경 세트`)을 고르는 창. 지금 '설정'의 자판(한글/영문)마다 따로 고르고, 고른 것은 설정
    /// 파일(user_settings.json)에 저장해 다음에 여는 오락부터 쓴다. 자판의 기본 배경에는 '(기본)'을 붙이고, 기본 배경을 고르면 저장된
    /// 선택을 지워 기본 배경을 따르게 한다.
    /// </summary>
    public partial class BackgroundSelectWindow : MetroWindow
    {
        private readonly IStageSet keyboard;
        private readonly BackgroundSet defaultSet;

        public BackgroundSelectWindow()
        {
            InitializeComponent();
            Title = VersionInfo.WindowTitle; // <2600912_5-1>

            keyboard = StageSets.Current;
            defaultSet = BackgroundSets.DefaultFor(keyboard?.Key);
            BackgroundSet current = BackgroundSets.For(keyboard);
            KeyboardText.Text = (keyboard?.LayoutName ?? "이 자판") + " 오락에 쓸 배경을 고르세요.";

            foreach (BackgroundSet bg in BackgroundSets.Menu)
            {
                ChoicePanel.Children.Add(new RadioButton
                {
                    Content = bg == defaultSet ? bg.Name + "  (기본)" : bg.Name,
                    Tag = bg,
                    GroupName = "AcidRainBackground",
                    IsChecked = bg == current,
                    FontFamily = (System.Windows.Media.FontFamily)Application.Current.FindResource("NanumBarunGothic"),
                    FontSize = 15,
                    Margin = new Thickness(0, 6, 0, 0),
                });
            }
        }

        /// <summary>지금 골라 둔 배경 세트(검사용으로도 쓴다).</summary>
        internal BackgroundSet SelectedBackground =>
            ChoicePanel.Children.OfType<RadioButton>().FirstOrDefault(r => r.IsChecked == true)?.Tag as BackgroundSet;

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            BackgroundSet chosen = SelectedBackground;
            if (chosen != null && keyboard != null)
            {
                UserSettingsStore.SetAcidRainBackground(keyboard.Key, chosen == defaultSet ? null : chosen.Id);
                if (!UserSettingsStore.TrySave(out string error))
                {
                    MessageBox.Show(this, "배경 선택을 저장하지 못했습니다. 이번 실행 동안에는 고른 배경을 씁니다.\n\n" + error,
                                    "열린타자+", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
