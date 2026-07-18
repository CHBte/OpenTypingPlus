using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MahApps.Metro.Controls;

namespace OpenTyping
{
    /// <summary>
    /// KeyPracticeMenu.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class KeyPracticeMenu : UserControl
    {
        public bool NoShiftMode { get; set; }

        private const string StageLayoutName = "두벌식 표준"; // 연습 단계(타일 UI)를 제공하는 자판

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

        public KeyPracticeMenu()
        {
            InitializeComponent();
            BuildStageTiles();
            RefreshLayout();
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

            if (!useStages)
            {
                KeyLayoutBox.LoadKeyLayout();
            }
        }

        private void BuildStageTiles()
        {
            StageTilePanel.Children.Clear();

            IList<PracticeStage> stages = DubeolsikStages.Stages;

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
                    rowPanel.Children.Add(
                        CreateStageTile(stages[row * 4 + col], TileColumnColors[col],
                                        TileWidth, TileHeight, col < 3 ? TileGapX : 0));
                }

                StageTilePanel.Children.Add(rowPanel);
            }

            // 13단계: 4행에 두되, 가로는 9~12단계 전체 폭의 S비율(키보드 아이콘 [Space] 비율)만큼을
            // 가운데 정렬하고 (<260718_2-1>), 세로는 다른 타일의 2/3 정도로 줄인다 (<260717_28-2>).
            Tile stage13Tile = CreateStageTile(stages[12], Stage13Color,
                                              Stage13Width, Stage13Height, 0, Stage13LeftOffset);

            var lastRowPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            lastRowPanel.Children.Add(stage13Tile);
            StageTilePanel.Children.Add(lastRowPanel);
        }

        private Tile CreateStageTile(PracticeStage stage, Brush background,
                                     double width, double height, double rightGap, double leftGap = 0)
        {
            FontFamily font = (FontFamily)FindResource("NanumBarunGothic");

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
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontFamily = font,
                    FontSize = 14 * 96.0 / 72.0, // 14pt
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                lines.Children.Add(new TextBlock
                {
                    Text = stage.Name.Substring(parenIndex),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
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
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
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

            tile.Click += (sender, e) =>
            {
                var keyPracticeWindow = new KeyPracticeWindow(stage);
                MainWindow.ShowDialogDimmed(keyPracticeWindow);
            };

            return tile;
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
