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

        // 가장 긴 문구인 7단계 "(가운데 오른쪽 자리)"가 꼭 맞는 크기를 1~12단계 전체에 적용 (<260717_28-1>)
        private const double TileWidth = 130;                      // 1~12단계 타일 가로 (<260718_2>)
        private const double TileHeight = 64;
        private const double TileGapX = 22;                        // 타일 좌우 간격 (<260717_28-2>)
        private const double TileGapY = 22;                        // 행 사이(상하) 간격 (<260718_2>)
        private const double Stage13Height = TileHeight * 2 / 3.0; // 13단계 타일 세로 (<260717_28-2>)

        // 13단계 타일 가로: 헤더의 키보드 아이콘(FontAwesome KeyboardRegular) 벡터 데이터를 실측해
        // 그 안의 [Space] 막대가 5키 나열 폭에 대해 갖는 비율(S비율)을 9~12단계 전체 폭에 적용한다 (<260718_2-1>).
        //   길이1 = 한 줄 5개 키(각 48px, 80px 피치) 전체 폭 = (424+48) - 104 = 368
        //   길이2 = [Space] 막대 폭 = 256
        //   S비율 = 256 / 368 ≈ 0.6957  (아이콘에서 Space가 5키 약 70% 폭)
        // 아이콘에서 Space 막대가 5키 아래 가운데 정렬(중심 288)돼 있으므로 13단계도 가운데 정렬한다.
        private const double Stage13FullSpan = TileWidth * 4 + TileGapX * 3; // 9~12단계 전체 폭 (586)
        private const double SRatio = 256.0 / 368.0;                         // 아이콘 [Space]폭 / 5키 나열폭
        private const double Stage13Width = Stage13FullSpan * SRatio;        // ≈ 407.65
        private const double Stage13LeftOffset = (Stage13FullSpan - Stage13Width) / 2.0;

        // 1~3행 타일 색: 행마다 왼쪽부터 4개 타일에 순서대로 적용 (<260717_28>)
        private static readonly Brush[] TileColumnColors =
        {
            new SolidColorBrush(Color.FromRgb(240, 62, 62)),
            new SolidColorBrush(Color.FromRgb(28, 126, 214)),
            new SolidColorBrush(Color.FromRgb(55, 178, 77)),
            new SolidColorBrush(Color.FromRgb(247, 103, 7)),
        };
        private static readonly Brush Stage13Color = new SolidColorBrush(Color.FromRgb(245, 159, 0));
        // 비활성(앞 단계 미통과) 타일의 글자 색: 회색 (<260724_2>(2), 값은 <260812_11>)
        private static readonly Brush InactiveTileTextBrush = new SolidColorBrush(Color.FromRgb(190, 190, 190));

        public KeyPracticeMenu()
        {
            InitializeComponent();
            BuildStageTiles();
            RefreshLayout();

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
        /// 치트코드 처리 (<260724_2>(3), <260812_10>). 꺼져 있으면 몇 단계까지 통과한 것으로 칠지
        /// 물어보고, 이미 걸려 있으면 그대로 꺼서 원래 상태로 되돌린다. 실제 기록은 건드리지 않는다.
        /// </summary>
        private void ApplyCheat()
        {
            // <260812_10-2> 되돌리기('원래대로')도 목록의 한 항목이 되었으므로, 치트가 이미 걸려
            // 있든 아니든 늘 이 창을 띄운다. 그래야 걸려 있는 값을 다른 값으로 바꿀 수도 있다.
            // 치트 창은 다른 2단계 창과 달리 메인 창을 숨기지 않는다(사용자 지정).
            var dialog = new CheatStageWindow(DubeolsikStages.Stages) { Owner = Window.GetWindow(this) };
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
        /// 현재 자판에 맞는 UI를 보여준다: '두벌식 표준'이면 13개 연습 단계 타일,
        /// 그 외 자판이면 기존 키 선택 UI. 설정에서 자판이 바뀔 때마다 다시 호출된다.
        /// </summary>
        public void RefreshLayout()
        {
            bool useStages = MainWindow.CurrentKeyLayout != null &&
                             MainWindow.CurrentKeyLayout.Name == StageLayoutName;

            StageTilePanel.Visibility = useStages ? Visibility.Visible : Visibility.Collapsed;
            ClassicBody.Visibility = useStages ? Visibility.Collapsed : Visibility.Visible;
            ClassicBottomBar.Visibility = ClassicBody.Visibility;

            // 오락 드롭다운은 '두벌식 표준'(연습 단계·게임 대응)에서만 보인다 (<260723_2>).
            GameStageCombo.Visibility = useStages ? Visibility.Visible : Visibility.Collapsed;
            if (useStages) BuildGameDropdown();

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
                foreach (int stageId in game.StageIds)
                {
                    bool unlocked = StageRecords.IsGameStageUnlocked(stageId);
                    string label = game.StageName(stageId);
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

            Window gameWindow = game.CreateWindow(stageId);
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

            IList<PracticeStage> stages = DubeolsikStages.Stages;

            // 타일 배치는 13개 단계(3행×4 + 13단계)를 전제로 한다. 단계 정의 JSON을 편집·손상해 13개
            // 미만이 되면 인덱스 접근이 크래시하므로, 그 경우엔 타일을 그리지 않아 앱이 죽지 않게 한다
            // (단계 개수를 바꾸면 타일 UI는 별도로 재설계 — <260724_1>(3) 범위 밖).
            if (stages.Count < 13) return;

            for (int row = 0; row < 3; row++)
            {
                var rowPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, TileGapY)
                };

                for (int col = 0; col < 4; col++)
                {
                    int index = row * 4 + col;
                    rowPanel.Children.Add(
                        CreateStageTile(stages[index], index + 1, TileColumnColors[col],
                                        TileWidth, TileHeight, col < 3 ? TileGapX : 0));
                }

                StageTilePanel.Children.Add(rowPanel);
            }

            // 13단계: 4행에 두되, 가로는 9~12단계 전체 폭의 S비율(키보드 아이콘 [Space] 비율)만큼을
            // 가운데 정렬하고 (<260718_2-1>), 세로는 다른 타일의 2/3 정도로 줄인다 (<260717_28-2>).
            Tile stage13Tile = CreateStageTile(stages[12], 13, Stage13Color,
                                              Stage13Width, Stage13Height, 0, Stage13LeftOffset);

            var lastRowPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            lastRowPanel.Children.Add(stage13Tile);
            StageTilePanel.Children.Add(lastRowPanel);
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

            // "x단계(...)" → 윗줄 "x단계", 아랫줄 "(...)" (<260717_28-1>).
            // 13단계(전체 폭)는 한 줄 그대로 둔다.
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
                          : "앞 단계를 " + StageRecords.PassThreshold + "타 이상으로 통과하면 열립니다");

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
            while (stage != null)
            {
                var keyPracticeWindow = new KeyPracticeWindow(stage);
                MainWindow.ShowDialogDimmed(keyPracticeWindow);

                // 방금 연습에서 목표 타수로 통과했으면 다음 타일이 활성화되고 오락이 해금됐을 수 있으니
                // 타일과 드롭다운을 모두 갱신한다 (<260724_2>).
                BuildStageTiles();
                BuildGameDropdown();

                int stageNumber = DubeolsikStages.Stages.IndexOf(stage) + 1;
                switch (keyPracticeWindow.FinishChoice)
                {
                    case StageFinishWindow.Choice.NextStage:
                        stage = stageNumber < DubeolsikStages.Stages.Count
                            ? DubeolsikStages.Stages[stageNumber]   // 0-기반이라 이것이 n+1단계
                            : null;
                        continue;

                    case StageFinishWindow.Choice.Game:
                        IArcadeGame game = ArcadeGames.Default;
                        if (game != null)
                        {
                            MainWindow.ShowDialogDimmed(game.CreateWindow(stage.GameStageId));
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
