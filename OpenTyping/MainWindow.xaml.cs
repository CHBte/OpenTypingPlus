using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MahApps.Metro.Controls;
using OpenTyping.Properties;

namespace OpenTyping
{
    /// <summary>
    ///     MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public static KeyLayout CurrentKeyLayout { get; private set; }

        public const string KeyLayoutDataDirStr = "KeyLayoutDataDir";
        public const string KeyLayoutStr = "KeyLayout";
        public const string PracticeDataDirStr = "PracticeDataDir";

        /// <summary>
        /// 자식 모달 대화상자(각 탭 화면에서 여는 2단계 창)를 띄우는 동안 메인 창 내용에 반투명 회색
        /// 오버레이를 덮어 채도를 낮춰(비활성 표시) 보여 준다. ShowDialog가 닫힐 때까지 블로킹하므로
        /// 효과는 그 사이에만 적용되고 끝나면 반드시 해제된다 (<260718_7-1>, <260718_7-2>).
        /// </summary>
        public static void ShowDialogDimmed(Window dialog)
        {
            var main = Application.Current?.MainWindow as MainWindow;
            if (main?.DesaturateOverlay != null)
            {
                main.DesaturateOverlay.Visibility = Visibility.Visible;
                try { dialog.ShowDialog(); }
                finally { main.DesaturateOverlay.Visibility = Visibility.Collapsed; }
            }
            else
            {
                dialog.ShowDialog();
            }
        }

        public MainWindow()
        {
            // 단일 파일 게시(PublishSingleFile)에서는 Assembly.Location이 빈 문자열이므로
            // AppContext.BaseDirectory를 사용한다.
            string exeDirectory = AppContext.BaseDirectory;
            if (string.IsNullOrEmpty(exeDirectory))
            {
                MessageBox.Show("응용 프로그램 경로를 찾는 도중 에러가 발생했습니다.",
                                "열린타자+",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                Environment.Exit(-1);
            }

            if (string.IsNullOrEmpty((string)Settings.Default[KeyLayoutDataDirStr]))
            {
                string layoutsDirectory = Path.Combine(exeDirectory, "layouts");
                Settings.Default[KeyLayoutDataDirStr] = layoutsDirectory;
            }

            try
            {
                var keyLayouts =
                    new List<KeyLayout>(KeyLayout.LoadFromDirectory((string)Settings.Default[KeyLayoutDataDirStr]));

                var layoutName = (string)Settings.Default[KeyLayoutStr];
                KeyLayout currentKeylayout = keyLayouts.FirstOrDefault(keyLayout => keyLayout.Name == layoutName);

                if (currentKeylayout == null)
                {
                    KeyLayout dubeolsikLayout = keyLayouts.Find(keyLayout => keyLayout.Name == "두벌식 표준");

                    if (dubeolsikLayout != null)
                    {
                        Settings.Default[KeyLayoutStr] = dubeolsikLayout.Name;
                        CurrentKeyLayout = dubeolsikLayout;
                    }
                    else
                    {
                        Settings.Default[KeyLayoutStr] = keyLayouts[0].Name;
                        CurrentKeyLayout = keyLayouts[0];
                    }
                }
                else
                {
                    CurrentKeyLayout = currentKeylayout;
                }
            }
            catch (Exception ex)
            {
                // 어떤 이유로든 자판을 못 불러오면 계속 진행할 수 없으므로(CurrentKeyLayout == null)
                // 원인을 보여주고 종료한다. 조용히 넘어가면 이후 NullReferenceException으로 죽는다.
                string message = ex is KeyLayoutLoadFail || ex is InvalidKeyLayoutDataException
                    ? ex.Message
                    : "자판 데이터를 불러오는 중 예상하지 못한 오류가 발생했습니다.\n" + ex.Message;

                MessageBox.Show(message, "열린타자+", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(-1);
            }

            if (string.IsNullOrEmpty((string)Settings.Default[PracticeDataDirStr]))
            {
                string dataDirectory = Path.Combine(exeDirectory, "data");
                Settings.Default[PracticeDataDirStr] = dataDirectory;
            }
            
            InitializeComponent();

            this.Loaded += MainWindow_Loaded;
            this.Closed += MainWindow_Closed;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            CheckSyllablePractice();
        }

        private static void MainWindow_Closed(object sender, EventArgs e)
        {
            if (!KeyLayout.TrySaveKeyLayout(CurrentKeyLayout, out string error))
            {
                MessageBox.Show("연습 통계를 저장하지 못했습니다.\n" + error,
                                "열린타자+",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
            }
            Settings.Default.Save();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // 설정창이 자판 목록을 파일에서 다시 읽으므로, 현재 통계를 먼저 파일에 반영해 둔다.
            // 저장 실패는 설정창 이용을 막을 이유가 아니므로 알리지 않고 계속 진행한다 (종료 시 다시 시도됨).
            KeyLayout.TrySaveKeyLayout(CurrentKeyLayout, out _);

            SettingsWindow settingsWindow;
            try
            {
                settingsWindow = new SettingsWindow();
            }
            catch (Exception ex) when (ex is KeyLayoutLoadFail || ex is InvalidKeyLayoutDataException ||
                                       ex is PracticeDataLoadFail || ex is InvalidPracticeDataException)
            {
                // 앱 실행 중 데이터 폴더의 파일이 손상된 경우: 설정창만 열지 못하게 하고 앱은 유지한다.
                MessageBox.Show(ex.Message, "열린타자+", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            settingsWindow.ShowDialog();

            if ((settingsWindow.KeyLayoutUpdated || settingsWindow.KeyLayoutDataDirUpdated) &&
                settingsWindow.SelectedKeyLayout != null) // null이 대입되면 이후 모든 연습 기능이 죽는다
            {
                CurrentKeyLayout = settingsWindow.SelectedKeyLayout;

                // 자판이 바뀌면 '자리연습' 화면 UI(단계 타일 ↔ 키 선택)도 함께 전환된다.
                KeyPracticeMenu.RefreshLayout();

                var currentKeyLayoutNameBinding = new Binding
                {
                    Path = new PropertyPath("Name"),
                    Source = CurrentKeyLayout,
                };
                HomeMenu.CurrentKeyLayoutName.SetBinding(TextBlock.TextProperty, currentKeyLayoutNameBinding);

                var currentKeyLayoutCharBinding = new Binding
                {
                    Path = new PropertyPath("Character"),
                    Source = CurrentKeyLayout,
                };
                HomeMenu.CurrentKeyLayoutChar.SetBinding(TextBlock.TextProperty, currentKeyLayoutCharBinding);

                var mostIncorrectBinding = new Binding
                {
                    Path = new PropertyPath("Stats.MostIncorrect.Key"),
                    Source = CurrentKeyLayout,
                    Converter = new KeyPosToKeyConverter()
                };
                HomeMenu.MostIncorrectKey.SetBinding(KeyBox.KeyProperty, mostIncorrectBinding);

                var averageSpeedBinding = new Binding
                {
                    Path = new PropertyPath("Stats.AverageTypingSpeed"),
                    Source = CurrentKeyLayout,
                };
                HomeMenu.AverageTypingSpeed.SetBinding(TextBlock.TextProperty, averageSpeedBinding);

                var averageAccuracyBinding = new Binding
                {
                    Path = new PropertyPath("Stats.AverageAccuracy"),
                    Source = CurrentKeyLayout,
                    StringFormat = "{0}%"
                };
                HomeMenu.AverageAccuracy.SetBinding(TextBlock.TextProperty, averageAccuracyBinding);

                var sentencePracticeCountBinding = new Binding
                {
                    Path = new PropertyPath("Stats.SentencePracticeCount"),
                    Source = CurrentKeyLayout,
                };
                HomeMenu.SentencePracticeCount.SetBinding(TextBlock.TextProperty, sentencePracticeCountBinding);

                CheckSyllablePractice();
            }

            SentencePracticeMenu.LoadData();
            ArticlePracticeMenu.LoadData();
        }

        private void CheckSyllablePractice()
        {
            if (CurrentKeyLayout.Character == "한글")
            {
                SyllablePracticeTabItem.Visibility = Visibility.Visible;
            }
            else
            {
                SyllablePracticeTabItem.Visibility = Visibility.Collapsed;

                if (SyllablePracticeTabItem.IsSelected)
                {
                    MenuTabControl.SelectedIndex = 0;
                }
            }
        }
    }
}