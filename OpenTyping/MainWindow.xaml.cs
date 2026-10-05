using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MahApps.Metro.Controls;

namespace OpenTyping
{
    /// <summary>
    ///     MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public static KeyLayout CurrentKeyLayout { get; private set; }

        /// <summary>검사용: 설정을 건드리지 않고 지금 자판만 바꾼다(헤드리스 진단 전용).</summary>
        internal static void SetCurrentKeyLayoutForTest(KeyLayout layout) => CurrentKeyLayout = layout;

        public const string KeyLayoutDataDirStr = "KeyLayoutDataDir";
        public const string KeyLayoutStr = "KeyLayout";
        public const string PracticeDataDirStr = "PracticeDataDir";

        /// <summary>
        /// 각 탭 화면에서 여는 2단계 창을 띄운다.
        ///
        /// <260811_34>: '정보'·'설정'을 뺀 창은 화면에 하나만 보이게 한다. 2단계 창은 메인 창과 중심이
        /// 겹치는 자리에 뜨고 그 동안 메인 창은 숨는다. 2단계 창을 닫으면 반대로, 그 창의 중심에 메인
        /// 창의 중심을 맞춰 다시 보여 준다. 이전에 쓰던 반투명 회색 오버레이(<260718_7-1>)는 메인 창이
        /// 아예 보이지 않게 되어 쓸모가 없어졌으므로 켜지 않는다.
        /// </summary>
        public static void ShowDialogDimmed(Window dialog)
        {
            var main = Application.Current?.MainWindow as MainWindow;
            if (main == null || !main.IsVisible) { dialog.ShowDialog(); return; }

            // 최대화 상태에서는 Left/Top 이 실제 위치가 아니므로 복원 좌표를 쓴다.
            Rect mainBounds = main.WindowState == WindowState.Normal
                ? new Rect(main.Left, main.Top, main.ActualWidth, main.ActualHeight)
                : main.RestoreBounds;
            Point center = new Point(mainBounds.X + mainBounds.Width / 2,
                                     mainBounds.Y + mainBounds.Height / 2);

            dialog.WindowStartupLocation = WindowStartupLocation.Manual;
            // SourceInitialized 시점엔 아직 ActualWidth 가 없을 수 있어 지정 크기로 한 번,
            // 배치가 끝난 Loaded 에서 실제 크기로 한 번 더 맞춘다(첫 렌더 전이라 깜빡임 없음).
            dialog.SourceInitialized += (s, e) => CenterAt(dialog, center, dialog.Width, dialog.Height);
            dialog.Loaded += (s, e) => CenterAt(dialog, center, dialog.ActualWidth, dialog.ActualHeight);

            // 닫히는 순간의 2단계 창 중심을 받아 두었다가 그 자리에 메인 창을 되돌린다.
            Point restoreCenter = center;
            dialog.Closing += (s, e) =>
            {
                if (dialog.WindowState == WindowState.Normal && dialog.ActualWidth > 0)
                    restoreCenter = new Point(dialog.Left + dialog.ActualWidth / 2,
                                              dialog.Top + dialog.ActualHeight / 2);
            };

            main.Hide();
            try
            {
                dialog.ShowDialog();
            }
            finally
            {
                // 예외로 빠져나가도 메인 창은 반드시 되살린다(안 그러면 조작할 창이 하나도 없다).
                if (main.WindowState == WindowState.Normal)
                    CenterAt(main, restoreCenter, main.ActualWidth, main.ActualHeight);
                main.Show();
                main.Activate();
            }
        }

        /// <summary>
        /// <260812_5-1> 메인 창을 앞으로 내며 '자리연습' 탭을 켠다(연습 종료 창의 '나가기').
        /// 탭 순서가 바뀌어도 따라가도록 번호가 아니라 KeyPracticeMenu 를 담고 있는 탭을 찾는다.
        /// </summary>
        public static void ShowKeyPracticeTab()
        {
            var main = Application.Current?.MainWindow as MainWindow;
            if (main?.MenuTabControl == null) return;

            foreach (object item in main.MenuTabControl.Items)
                if (item is TabItem tab && tab.Content is KeyPracticeMenu)
                {
                    main.MenuTabControl.SelectedItem = tab;
                    break;
                }

            main.Show();
            main.Activate();
        }

        /// <summary>창의 중심이 <paramref name="center"/>에 오도록 위치를 잡는다(화면 밖으로 나가지 않게 보정).</summary>
        private static void CenterAt(Window win, Point center, double width, double height)
        {
            if (double.IsNaN(width) || double.IsNaN(height) || width <= 0 || height <= 0) return;

            double left = center.X - width / 2;
            double top = center.Y - height / 2;

            // 작업 영역 밖으로 밀려나 제목 표시줄을 잡을 수 없게 되는 것만 막는다.
            Rect area = SystemParameters.WorkArea;
            left = Math.Max(area.Left, Math.Min(area.Right - width, left));
            top = Math.Max(area.Top, Math.Min(area.Bottom - height, top));

            win.Left = left;
            win.Top = top;
        }

        /// <summary>
        /// 실제 재현된 버그(<260830_2-4-3> 재설치 테스트): 저장된 경로가 "우리가 직접 만든 AppData
        /// 경로"인데 그 폴더가 없거나 비어 있으면(예: EnsureWritableDataDir이 한 번 빈 채로 만들었던
        /// 흔적이 그대로 저장값에 남은 경우) 다시 채우게 한다. 사용자가 설정 창에서 직접 고른, 우리가
        /// 만들지 않은 경로는(=이 경로와 다르면) 절대 손대지 않는다 — 원래 있던 "이미 값이 있으면
        /// 안 건드린다"는 설계를 그 경로에 대해서만 계속 지킨다.
        /// </summary>
        private static bool IsBrokenOwnAppDataDir(string configuredDir, string subFolder)
        {
            if (string.IsNullOrEmpty(configuredDir)) return true;

            string ourAppDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OTP", "OpenTypingPlus", subFolder);
            if (!string.Equals(configuredDir, ourAppDataDir, StringComparison.OrdinalIgnoreCase))
                return false;

            return !Directory.Exists(configuredDir) || Directory.GetFiles(configuredDir, "*.json").Length == 0;
        }

        /// <summary>
        /// exeDirectory 안의 번들 폴더(subFolder: "layouts"/"data")는 향후 Program Files처럼 쓰기
        /// 금지된 곳에 설치될 수 있다 (<260828_1>). %APPDATA%\OpenTypingPlus\&lt;subFolder&gt;\ 가 아직
        /// 없으면(첫 실행) 번들 폴더 내용을 그리로 복사해 그 경로를 기본값으로 쓴다 — 이후 자판·연습
        /// 데이터 추가/삭제 등 모든 쓰기가 항상 쓰기 가능한 이 경로에서 이뤄진다.
        ///
        /// 사용자가 설정 창에서 직접 고른 경로(우리가 만든 AppData 경로가 아닌 경로)를 저장해 둔
        /// 경우는 호출부의 IsBrokenOwnAppDataDir 검사가 걸러내 이 메서드를 타지 않는다 — 그 경로가
        /// 지금 비어 있어도 사용자의 선택을 존중해 손대지 않는다. 복사에 실패하면(권한 등) 안전하게
        /// 번들 경로로 되돌아간다 — 이 경우 자판 자체는 계속 읽히지만(읽기는 항상 허용됨) 추가/삭제는
        /// 예전처럼 실패할 수 있다.
        ///
        /// <260830_2-4-3 검증 중 발견한 버그>: 예전엔 "폴더가 있으면" 그냥 그 경로를 돌려줬는데, 그
        /// 폴더가 (번들 폴더를 아직 못 찾은 실행 등으로) 비어 있는 채로 한 번이라도 만들어지면, 그
        /// 뒤로는 매번 "있으니까 됐다"고 보고 다시는 채우지 않아 영구히 빈 채로 남았다(실제로 자리연습
        /// data 폴더가 이 상태로 재현됨). "있다"가 아니라 "있고 내용도 있다"를 봐서, 비어 있으면
        /// 다시 채우도록 고친다(이미 있는 파일은 건너뛰어 중복 복사 예외도 안 남).
        /// </summary>
        private static string EnsureWritableDataDir(string exeDirectory, string subFolder)
        {
            string bundledDir = Path.Combine(exeDirectory, subFolder);
            string appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OTP", "OpenTypingPlus", subFolder);

            if (Directory.Exists(appDataDir) && Directory.GetFiles(appDataDir, "*.json").Length > 0)
                return appDataDir;

            try
            {
                Directory.CreateDirectory(appDataDir);
                if (Directory.Exists(bundledDir))
                {
                    foreach (string file in Directory.GetFiles(bundledDir, "*.json"))
                    {
                        string dest = Path.Combine(appDataDir, Path.GetFileName(file));
                        if (!File.Exists(dest)) File.Copy(file, dest, overwrite: false);
                    }
                }
                return appDataDir;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                                       ex is System.Security.SecurityException)
            {
                try { Directory.Delete(appDataDir, recursive: true); } catch { /* 정리 실패는 무시 */ }
                return bundledDir;
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

            if (IsBrokenOwnAppDataDir((string)UserSettingsStore.Get(KeyLayoutDataDirStr), "layouts"))
            {
                UserSettingsStore.Set(KeyLayoutDataDirStr, EnsureWritableDataDir(exeDirectory, "layouts"));
            }

            // 자판을 불러오고, 저장된 자판 이름을 못 찾으면 기본 자판(연습 단계를 제공하는 자판)으로
            // 돌아간다. 이름이 바뀌어도 한 곳(KeyPracticeMenu.StageLayoutName)만 보면 되게 했다 (<260812_8>).
            // 돌아갈 때만 그 이름을 설정에 저장한다(정상적으로 찾은 경우는 기존 설정을 그대로 둔다).
            KeyLayout LoadAndSelect(string dir)
            {
                var keyLayouts = new List<KeyLayout>(KeyLayout.LoadFromDirectory(dir));

                var layoutName = (string)UserSettingsStore.Get(KeyLayoutStr);
                KeyLayout currentKeylayout = keyLayouts.FirstOrDefault(keyLayout => keyLayout.Name == layoutName);
                if (currentKeylayout != null) return currentKeylayout;

                KeyLayout dubeolsikLayout =
                    keyLayouts.Find(keyLayout => keyLayout.Name == KeyPracticeMenu.StageLayoutName);
                KeyLayout fallback = dubeolsikLayout ?? keyLayouts[0];
                UserSettingsStore.Set(KeyLayoutStr, fallback.Name);
                return fallback;
            }

            string configuredDir = (string)UserSettingsStore.Get(KeyLayoutDataDirStr);
            try
            {
                CurrentKeyLayout = LoadAndSelect(configuredDir);
            }
            catch (Exception ex)
            {
                // 사용자가 예전에 자판 데이터 경로를 바꿔 저장해 뒀는데 그 경로가 이후 비거나 손상되면,
                // 매번 여기서 그대로 종료돼 앱 안에서 되돌릴 방법이 전혀 없다(설정 창까지 가지도 못함).
                // 번들 기본 경로(실행 파일 옆 layouts/)로 한 번 더 시도해, 최소한 앱은 다시 뜨게 한다.
                string bundledDir = Path.Combine(exeDirectory, "layouts");
                bool alreadyBundled = string.Equals(configuredDir, bundledDir, StringComparison.OrdinalIgnoreCase);
                bool recovered = false;

                if (!alreadyBundled)
                {
                    try
                    {
                        CurrentKeyLayout = LoadAndSelect(bundledDir);
                        UserSettingsStore.Set(KeyLayoutDataDirStr, bundledDir); // 되돌린 경로를 저장해 다음 실행도 안전하게
                        recovered = true;
                    }
                    catch { /* 기본 경로도 실패하면 원래 오류로 종료(아래) */ }
                }

                if (!recovered)
                {
                    // 어떤 이유로든 자판을 못 불러오면 계속 진행할 수 없으므로(CurrentKeyLayout == null)
                    // 원인을 보여주고 종료한다. 조용히 넘어가면 이후 NullReferenceException으로 죽는다.
                    string message = ex is KeyLayoutLoadFail || ex is InvalidKeyLayoutDataException
                        ? ex.Message
                        : "자판 데이터를 불러오는 중 예상하지 못한 오류가 발생했습니다.\n" + ex.Message;

                    MessageBox.Show(message, "열린타자+", MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(-1);
                }
                else
                {
                    MessageBox.Show(
                        "자판 데이터 경로(" + configuredDir + ")를 불러올 수 없어 기본 경로로 되돌렸습니다.",
                        "열린타자+", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            if (IsBrokenOwnAppDataDir((string)UserSettingsStore.Get(PracticeDataDirStr), "data"))
            {
                UserSettingsStore.Set(PracticeDataDirStr, EnsureWritableDataDir(exeDirectory, "data"));
            }

            InitializeComponent();
            Title = VersionInfo.WindowTitle; // <2600912_5-1>

            this.Loaded += MainWindow_Loaded;
            this.Closed += MainWindow_Closed;

            // <260927_13> `자판 표시`를 클릭해도 '설정' 창이 열리게, 다섯 화면의 컨트롤 모두
            // '설정' 버튼과 같은 처리(SettingsButton_Click)를 구독한다.
            HomeMenu.KeyLayoutIndicator.Click += (s, e) => SettingsButton_Click(s, null);
            KeyPracticeMenu.KeyLayoutIndicator.Click += (s, e) => SettingsButton_Click(s, null);
            SyllablePracticeMenu.KeyLayoutIndicator.Click += (s, e) => SettingsButton_Click(s, null);
            SentencePracticeMenu.KeyLayoutIndicator.Click += (s, e) => SettingsButton_Click(s, null);
            ArticlePracticeMenu.KeyLayoutIndicator.Click += (s, e) => SettingsButton_Click(s, null);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            CheckSyllablePractice();
            WarnIfWordListUnreadable();
        }

        // 편집한 단어 목록(words.json)이 깨져 조용히 다른 목록으로 넘어갔으면 한 번 알린다. 단계 타일을
        // 만들면서 목록을 이미 읽은 뒤에 불러야 한다(아직 안 읽었으면 아무것도 하지 않는다).
        private void WarnIfWordListUnreadable()
        {
            string warning = WordCatalog.TakeUnreadableWarning();
            if (warning != null)
                MessageBox.Show(this, warning, "열린타자+", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private static void MainWindow_Closed(object sender, EventArgs e)
        {
            StageRecords.FlushPending();   // 디스크를 못 읽어 건너뛴 단계 기록 저장이 남아 있으면 마지막으로 한 번 더
            if (!KeyLayout.TrySaveKeyLayout(CurrentKeyLayout, out string error))
            {
                MessageBox.Show("연습 통계를 저장하지 못했습니다.\n" + error,
                                "열린타자+",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
            }
            // <260831 코드 검토>: 바로 위 통계 저장과 달리 이건 예외를 그대로 던지는 방침이라
            // (UserSettingsStore.Save 참고), 파일이 잠겼거나 디스크가 가득 차면 종료 도중 예외가
            // 전역 처리기까지 올라가 엉뚱한 오류창이 떴다. 통계 저장과 같은 방식으로 알리기만 한다
            // (TrySave — 설정 창·손 모양 창을 닫을 때의 저장도 같은 것을 쓴다).
            if (!UserSettingsStore.TrySave(out string settingsError))
            {
                MessageBox.Show("설정을 저장하지 못했습니다.\n" + settingsError,
                                "열린타자+",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
            }
        }

        private void InfoButton_Click(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new AboutWindow { Owner = this };
            aboutWindow.ShowDialog();
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

                // 옛 자판 파일로 만들어 둔 글자→키 규칙·단계·오락 단어를 버린다(새 자판 파일로 다시 만들게).
                StageSets.ResetCaches();

                // 자판이 바뀌면 '자리연습' 화면 UI(단계 타일 ↔ 키 선택)도 함께 전환된다.
                KeyPracticeMenu.RefreshLayout();
                WarnIfWordListUnreadable();
                // 기록·치트는 자판마다 따로이므로(<260927_5>(0)) 치트 표시(테마)도 새 자판 기준으로.
                StageRecords.NotifyLayoutChanged();

                // <260927_10>·<260927_10.1> `자판 표시`가 다섯 화면(대문·자리연습·음절연습·문장연습·
                // 긴글연습)에 하나씩 있으므로, 자판이 바뀌면 전부 다시 묶는다.
                HomeMenu.KeyLayoutIndicator.Refresh();
                KeyPracticeMenu.KeyLayoutIndicator.Refresh();
                SyllablePracticeMenu.KeyLayoutIndicator.Refresh();
                SentencePracticeMenu.KeyLayoutIndicator.Refresh();
                ArticlePracticeMenu.KeyLayoutIndicator.Refresh();

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