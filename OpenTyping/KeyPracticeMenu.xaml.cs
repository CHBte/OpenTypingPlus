using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MahApps.Metro.Controls;
// OpenTyping 네임스페이스에 키보드 레이아웃용 'Key' 클래스가 있어, WPF 입력키 열거형은 별칭으로 구분한다.
using WinKey = System.Windows.Input.Key;

namespace OpenTyping
{
    /// <summary>
    /// KeyPracticeMenu.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class KeyPracticeMenu : UserControl
    {
        public bool NoShiftMode { get; set; }

        // 연습 단계(타일 UI)를 제공하는 자판. 자판 이름은 layouts\*.json 의 "Name" 과 같아야 한다
        // (<260812_8>에서 '두벌식 표준' → '두벌식 표준 한글'로 바뀌었다).
        internal const string StageLayoutName = "두벌식 표준 한글";

        // 타일 크기·간격은 예전 단계 타일과 같다 (<260718_2>, <260717_28-2>). 가장 긴 문구
        // "(오른쪽 아랫자리)"도 두 줄로 나눠 이 크기에 들어간다.
        private const double TileWidth = 130;
        private const double TileHeight = 64;
        private const double TileGapX = 22;   // 타일 좌우 간격
        private const double TileGapY = 22;   // 행 사이(상하) 간격

        // 행당 타일 수와 왼쪽부터의 색은 단계 정의 파일의 "tiles"가 정한다 (<260927_5>(2)(3), <260927_6>(2)(3)).
        private static IReadOnlyList<Brush> TileBrushes(IStageSet set)
        {
            var brushes = new List<Brush>();
            foreach (string hex in set.TileColors)
            {
                try { brushes.Add(new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex))); }
                catch { /* 잘못 적힌 색은 건너뛴다 */ }
            }
            if (brushes.Count == 0) brushes.Add(new SolidColorBrush(Color.FromRgb(28, 126, 214)));
            return brushes;
        }
        // 비활성(앞 단계 미통과) 타일의 글자 색: 회색 (<260724_2>(2), 값은 <260812_11>)
        private static readonly Brush InactiveTileTextBrush = new SolidColorBrush(Color.FromRgb(190, 190, 190));

        public KeyPracticeMenu()
        {
            InitializeComponent();
            RefreshLayout(); // 단계가 있는 자판이면 이 안에서 타일·오락 목록을 만든다

            // 치트코드 입력을 받도록: 컨트롤이 보일 때 포커스를 잡고, 키 입력을 엿본다 (<260724_1>(2)).
            Focusable = true;
            PreviewKeyDown += KeyPracticeMenu_PreviewKeyDown;
            IsVisibleChanged += (s, e) => { if (IsVisible) Focus(); };
        }

        // ── 치트코드: 자리연습·오락 전 단계 해금 (<260724_2>(3)) ──
        private readonly CheatCodeDetector cheatDetector = new CheatCodeDetector();

        private void KeyPracticeMenu_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            WinKey k = e.Key == WinKey.ImeProcessed ? e.ImeProcessedKey : e.Key;
            if (cheatDetector.Feed(k)) ApplyCheat();
        }

        /// <summary>
        /// 치트코드 처리 (<260724_2>(3), <260812_10>, <260812_10-2>). 몇 단계까지 통과한 것으로 칠지
        /// (또는 '원래대로' 되돌릴지) 고르는 창을 늘 띄운다. 실제 기록은 건드리지 않는다.
        /// </summary>
        private void ApplyCheat()
        {
            // <260812_10-2> 되돌리기('원래대로')도 목록의 한 항목이 되었으므로, 치트가 이미 걸려
            // 있든 아니든 늘 이 창을 띄운다. 그래야 걸려 있는 값을 다른 값으로 바꿀 수도 있다.
            // 치트 창은 다른 2단계 창과 달리 메인 창을 숨기지 않는다(사용자 지정).
            // <260927_5>(0), <260927_6>(0.1) 치트는 지금 '설정'에 지정된 자판의 기록에만 적용된다.
            var dialog = new CheatStageWindow(StageSets.Current.Stages) { Owner = Window.GetWindow(this) };
            dialog.ShowDialog();
            if (!dialog.Confirmed) return;   // 취소

            StageRecords.SetCheatUpTo(dialog.Stages);

            BuildStageTiles();    // 자리연습 타일 활성 갱신
            BuildGameDropdown();  // 오락 드롭다운 갱신

            string message;
            if (!dialog.Stages.HasValue) message = "자리연습과 오락의 단계 제한이 다시 적용됩니다.";
            else if (dialog.Stages.Value == 0) message = "1단계도 통과하지 않은 것으로 처리했습니다.";
            else message = dialog.Stages.Value + "단계까지 통과한 것으로 처리했습니다.";
            MessageBox.Show(message, "열린타자+", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 현재 자판에 맞는 UI를 보여준다: 단계가 있는 자판(두벌식 표준 한글·QWERTY 영문)이면 연습
        /// 단계 타일, 그 외 자판이면 기존 키 선택 UI. 설정에서 자판이 바뀔 때마다 다시 호출된다.
        /// </summary>
        public void RefreshLayout()
        {
            bool useStages = MainWindow.CurrentKeyLayout != null &&
                             StageSets.HasStages(MainWindow.CurrentKeyLayout.Name);

            StageTilePanel.Visibility = useStages ? Visibility.Visible : Visibility.Collapsed;
            ClassicBody.Visibility = useStages ? Visibility.Collapsed : Visibility.Visible;
            ClassicBottomBar.Visibility = ClassicBody.Visibility;

            // 오락 드롭다운은 단계가 있는 자판에서만 보인다 (<260723_2>, <260927_6>(0)).
            GameStageCombo.Visibility = useStages ? Visibility.Visible : Visibility.Collapsed;
            if (useStages)
            {
                BuildStageTiles();   // 자판이 바뀌면 단계 구성·기록이 달라진다
                BuildGameDropdown();
            }

            if (!useStages)
            {
                KeyLayoutBox.LoadKeyLayout();
            }
        }

        // ── 오락(산성비 게임) 드롭다운 (<260723_2>) ──

        private bool suppressGameComboEvent;

        // 기본 오락(레지스트리의 대표 게임)의 단계를 드롭다운에 채운다 (<260724_1>(1)). 자리연습 목표
        // 타수로 해금된 단계만 고를 수 있고, 잠긴 단계는 회색·비활성으로 보인다. 첫 항목은 '메뉴' 성격의 placeholder.
        private void BuildGameDropdown()
        {
            suppressGameComboEvent = true;
            GameStageCombo.Items.Clear();
            // <260812_9> 닫혀 있을 때 보이는 안내 글. 펼친 목록에는 나오지 않아야 하므로(목록은
            // '1단계(기본 자리)'부터 시작) 항목 자체를 감춘다 — ComboBox 는 선택된 항목을 그릴 때
            // ComboBoxItem 의 Content 만 꺼내 쓰므로, 항목이 Collapsed 여도 닫힌 상태 글자는 보인다.
            GameStageCombo.Items.Add(new ComboBoxItem
            {
                Content = "오락 열기",
                Tag = null,
                Visibility = Visibility.Collapsed,
            });

            IArcadeGame game = ArcadeGames.Default;
            if (game != null)
            {
                foreach (int stageId in game.StageIdsFor(StageSets.Current))
                {
                    bool unlocked = StageRecords.IsGameStageUnlocked(stageId);
                    string label = game.StageName(StageSets.Current, stageId);
                    // <260812_3> 잠긴 단계도 고를 수는 있게 두고(비활성이면 클릭 자체가 먹지 않아
                    // 이유를 알려 줄 수 없다), 고르면 경고 문구를 띄운다.
                    var item = new ComboBoxItem
                    {
                        Content = unlocked ? label : label + "  (잠김)",
                        Tag = stageId,
                    };
                    if (!unlocked) item.Foreground = Brushes.Gray;
                    GameStageCombo.Items.Add(item);
                }
            }

            // <260812_20>(2) 목록 맨 아래의 '최고 기록'. 단계가 아니므로 Tag 로 구분한다.
            GameStageCombo.Items.Add(new ComboBoxItem { Content = "최고 기록", Tag = BestRecordTag });

            GameStageCombo.SelectedIndex = 0;
            suppressGameComboEvent = false;
        }

        /// <summary>'최고 기록' 항목 표시용(단계 id 와 겹치지 않는 값) (<260812_20>(2)).</summary>
        private const string BestRecordTag = "bestrecord";

        private void GameStageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressGameComboEvent) return;
            if (!(GameStageCombo.SelectedItem is ComboBoxItem item)) return;

            if (BestRecordTag.Equals(item.Tag))
            {
                suppressGameComboEvent = true;
                GameStageCombo.SelectedIndex = 0;   // 메뉴처럼 곧바로 원래 자리로
                suppressGameComboEvent = false;
                // 최고 기록 창은 다른 2단계 창과 달리 메인 창을 숨기지 않는다(사용자 지정).
                new BestRecordWindow { Owner = Window.GetWindow(this) }.ShowDialog();
                return;
            }

            if (!(item.Tag is int stageId)) return;

            IArcadeGame game = ArcadeGames.Default;
            if (game == null) return;

            // 드롭다운은 '메뉴'처럼 동작한다: 고르는 즉시 placeholder로 되돌리고 그 단계의 게임 창을 연다.
            suppressGameComboEvent = true;
            GameStageCombo.SelectedIndex = 0;
            suppressGameComboEvent = false;

            if (!StageRecords.IsGameStageUnlocked(stageId)) { ShowLockedWarning(); return; }   // <260812_3>

            Window gameWindow = game.CreateWindow(StageSets.Current, stageId);
            MainWindow.ShowDialogDimmed(gameWindow);
            BuildGameDropdown(); // 방어적 새로고침
        }

        /// <summary><260812_3> 잠긴 자리연습 단계·오락 단계를 눌렀을 때의 경고 문구.</summary>
        internal const string LockedWarning = "아직 전 단계를 통과하지 못했습니다.";

        private static void ShowLockedWarning() =>
            MessageBox.Show(LockedWarning, "열린타자+", MessageBoxButton.OK, MessageBoxImage.Warning);

        private void BuildStageTiles()
        {
            StageTilePanel.Children.Clear();

            IStageSet set = StageSets.Current;
            IReadOnlyList<PracticeStage> stages = set.Stages;
            int tilesPerRow = set.TilesPerRow;
            IReadOnlyList<Brush> colors = TileBrushes(set);

            // <260927_5>(2), <260927_6>(2)(2.1): 모든 타일은 같은 크기의 직사각형이고, 행마다 tilesPerRow 개씩
            // 왼쪽부터 채운다(마지막 행이 덜 차면 그 타일들은 왼쪽 정렬).
            // 단계 개수를 가정하지 않으므로 단계 정의 JSON을 편집해 개수가 바뀌어도 죽지 않는다.
            for (int start = 0; start < stages.Count; start += tilesPerRow)
            {
                var rowPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, TileGapY)
                };

                int end = System.Math.Min(start + tilesPerRow, stages.Count);
                for (int index = start; index < end; index++)
                {
                    int col = index - start;
                    rowPanel.Children.Add(
                        CreateStageTile(stages[index], index + 1, colors[col % colors.Count],
                                        TileWidth, TileHeight, col < tilesPerRow - 1 ? TileGapX : 0));
                }

                StageTilePanel.Children.Add(rowPanel);
            }
        }

        private Tile CreateStageTile(PracticeStage stage, int stageNumber, Brush background,
                                     double width, double height, double rightGap, double leftGap = 0)
        {
            FontFamily font = (FontFamily)FindResource("NanumBarunGothic");

            // 단계성 강화 (<260724_2-1>(2)): 1단계는 항상, 그 외는 앞 단계 최고 기록이 목표 타수 이상이어야 활성.
            // 비활성 타일은 글자 색을 회색으로 하고 클릭(연습 시작)을 막는다.
            bool active = StageRecords.IsPracticeStageActive(stageNumber);
            Brush textColor = active ? Brushes.White : InactiveTileTextBrush;
            // 비활성 타일은 굵지 않게 해 활성 타일과 더 뚜렷이 갈라 놓는다 (<260812_11-1>).
            FontWeight textWeight = active ? FontWeights.Bold : FontWeights.Normal;

            // "x단계(...)" → 윗줄 "x단계", 아랫줄 "(...)" (<260717_28-1>, <260927_5>(1.1)).
            UIElement content;
            int parenIndex = stage.Name.IndexOf('(');
            bool twoLine = width < TileWidth * 2 && parenIndex > 0;

            if (twoLine)
            {
                var lines = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                lines.Children.Add(new TextBlock
                {
                    Text = stage.Name.Substring(0, parenIndex),
                    Foreground = textColor,
                    FontWeight = textWeight,
                    FontFamily = font,
                    FontSize = 14 * 96.0 / 72.0, // 14pt
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                lines.Children.Add(new TextBlock
                {
                    Text = stage.Name.Substring(parenIndex),
                    Foreground = textColor,
                    FontWeight = textWeight,
                    FontFamily = font,
                    FontSize = 11 * 96.0 / 72.0, // 11pt
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 1, 0, 0)
                });
                content = lines;
            }
            else
            {
                content = new TextBlock
                {
                    Text = stage.Name,
                    Foreground = textColor,
                    FontWeight = textWeight,
                    FontFamily = font,
                    FontSize = 14 * 96.0 / 72.0
                };
            }

            var tile = new Tile
            {
                Width = width,
                Height = height,
                Background = background,
                Margin = new Thickness(leftGap, 0, rightGap, 0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = new Viewbox
                {
                    Stretch = Stretch.Uniform,
                    StretchDirection = StretchDirection.DownOnly, // 넘칠 때만 축소
                    // Tile 템플릿이 내용 폭을 제약하지 않으므로 여기서 명시해야 축소가 작동한다
                    Width = width - 14,
                    MaxHeight = height - 10,
                    Child = content
                }
            };

            // 최고 타 기록을 툴팁으로 보여 준다(마우스를 올리면 확인) (<260724_2-1>).
            tile.ToolTip = StageRecords.HasRecord(stageNumber)
                ? "이 단계 최고 기록: " + StageRecords.BestTa(stageNumber) + "타"
                : (active ? "아직 완주 기록이 없습니다"
                          : "앞 단계를 " + StageRecords.TargetTa(stageNumber - 1) + "타 이상으로 통과하면 열립니다");

            // 활성 타일만 클릭(연습 시작) 가능 (<260724_2-1>(2)).
            // 잠긴 타일을 눌렀을 때는 왜 안 되는지 알려 준다 (<260812_3>).
            if (!active)
            {
                tile.Click += (sender, e) => ShowLockedWarning();
            }
            else
            {
                tile.Click += (sender, e) => RunStage(stage);
            }

            return tile;
        }

        /// <summary>
        /// 단계 자리연습을 연다. 연습이 끝난 뒤 뜬 창(<260812_5>)에서 '다음 단계'나 '오락'을 골랐으면
        /// 그것을 이어서 연다 — 연습 창 안에서 창을 겹쳐 여는 대신 여기서 차례로 열어, 화면에 창이
        /// 하나만 있게 한 <260811_34>를 지킨다.
        /// </summary>
        private void RunStage(PracticeStage stage)
        {
            IStageSet set = StageSets.Current;
            while (stage != null)
            {
                var keyPracticeWindow = new KeyPracticeWindow(set, stage);
                MainWindow.ShowDialogDimmed(keyPracticeWindow);

                // 방금 연습에서 목표 타수로 통과했으면 다음 타일이 활성화되고 오락이 해금됐을 수 있으니
                // 타일과 드롭다운을 모두 갱신한다 (<260724_2>).
                BuildStageTiles();
                BuildGameDropdown();

                int stageNumber = set.Stages.ToList().IndexOf(stage) + 1;
                switch (keyPracticeWindow.FinishChoice)
                {
                    case StageFinishWindow.Choice.NextStage:
                        stage = stageNumber < set.Stages.Count
                            ? set.Stages[stageNumber]   // 0-기반이라 이것이 n+1단계
                            : null;
                        continue;

                    case StageFinishWindow.Choice.Game:
                        IArcadeGame game = ArcadeGames.Default;
                        if (game != null)
                        {
                            MainWindow.ShowDialogDimmed(game.CreateWindow(set, stage.GameStageId));
                            BuildGameDropdown();
                        }
                        return;

                    case StageFinishWindow.Choice.Exit:
                        // <260812_5-1> '나가기': 창을 모두 닫고 '자리연습' 탭이 켜진 메인 창으로
                        // 돌아간다. 연습 창은 이미 닫혔고 메인 창은 ShowDialogDimmed 가 되살렸으므로,
                        // 여기서는 탭만 이 화면으로 맞춰 주면 된다 (<260811_34>).
                        MainWindow.ShowKeyPracticeTab();
                        return;

                    default:
                        return;
                }
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            List<KeyPos> pressedKeys = KeyLayoutBox.PressedKeys();
            
            if (pressedKeys.Count <= 1)
            {
                MessageBox.Show("연습할 키를 2개 이상 선택해주세요.",
                                "열린타자+",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                return;
            }

            var keyPracticeWindow = new KeyPracticeWindow(pressedKeys, NoShiftMode);
            MainWindow.ShowDialogDimmed(keyPracticeWindow);
        }
    }
}
