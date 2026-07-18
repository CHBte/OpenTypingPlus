using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace OpenTyping
{
    /// <summary>
    /// KeyPracticeWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class KeyPracticeWindow : MetroWindow, INotifyPropertyChanged
    {
        public class KeyInfo
        {
            public KeyInfo(string keyData, KeyPos pos, bool isShift)
            {
                KeyData = keyData;
                Pos = pos;
                IsShift = isShift;
            }

            public string KeyData { get; set; }
            public KeyPos Pos { get; set; }
            public bool IsShift { get; set; }
        }

        private readonly IList<KeyPos> keyList;
        private readonly bool noShiftMode;
        private readonly List<PracticeStage.StageItem> stagePool; // 단계 모드일 때만 사용 (가중 복원추출 균등난수 방식)
        private readonly Dictionary<KeyPos, int> incorrectStats = new Dictionary<KeyPos, int>();
        private readonly HashSet<KeyPos> physicallyDownKeys = new HashSet<KeyPos>(); // 지금 물리적으로 눌려 있는 키들

        private KeyInfo previousKey;
        public KeyInfo PreviousKey
        {
            get => previousKey;
            private set => SetField(ref previousKey, value);
        }

        private KeyInfo currentKey;
        public KeyInfo CurrentKey
        {
            get => currentKey;
            private set => SetField(ref currentKey, value);
        }

        private KeyInfo nextKey;
        public KeyInfo NextKey
        {
            get => nextKey;
            private set => SetField(ref nextKey, value);
        }

        private int correctCount = 0;
        public int CorrectCount
        {
            get => correctCount;
            private set => SetField(ref correctCount, value);
        }

        private int incorrectCount = 0;
        public int IncorrectCount
        {
            get => incorrectCount;
            private set => SetField(ref incorrectCount, value);
        }

        private static readonly Random Randomizer = new Random();
        private static readonly ThicknessAnimationUsingKeyFrames ShakeAnimation = new ThicknessAnimationUsingKeyFrames();

        /// <summary>기존 방식: 선택한 키 목록에서 조건부 2단계 복원추출 균등난수 방식으로 연습 ('두벌식 표준' 외 자판).</summary>
        public KeyPracticeWindow(IList<KeyPos> keyList, bool noShiftMode)
        {
            InitializeComponent();

            this.keyList = keyList;
            this.noShiftMode = noShiftMode;

            InitPractice();
        }

        /// <summary>단계 모드: 단계에 정의된 (키, 윗글쇠) 항목들에서 가중 복원추출 균등난수 방식으로 연습 ('두벌식 표준' 자판).</summary>
        public KeyPracticeWindow(PracticeStage stage)
        {
            InitializeComponent();

            stagePool = stage.BuildPool();
            isStageMode = true;
            isStage1 = ReferenceEquals(stage, DubeolsikStages.Stages[0]);

            InitPractice();
        }

        private readonly bool isStageMode;
        private readonly bool isStage1;

        private void InitPractice()
        {
            // 안내문("10개의 손가락을...")은 1단계 창에서만 쓰므로, 그 외 창(2~13단계·기존 방식)에서는
            // 한 줄짜리 안내문 행을 없애고 창 세로도 그만큼 줄인다 (<260718_3-1>).
            if (!isStage1)
            {
                double guideRowHeight = GuideRow.Height.Value; // XAML에 고정된 한 줄 높이(30)
                GuideRow.Height = new GridLength(0);
                RootGrid.Height -= guideRowHeight;
                MinHeight -= guideRowHeight;
                Height -= guideRowHeight;
            }

            // 단계 창에는 카운트 행에 "손 모양" 버튼이 있어 그 행이 커지는 만큼 키보드가 아래로 밀려,
            // 렌더링 키보드 바닥 간격이 클래식 창(손 모양 버튼 없음, 표준 바닥 간격 15px)보다 8px 작다.
            // 그래서 단계 창은 세로를 8px 늘려 바닥 간격을 표준 바닥 간격으로 맞춘다 (<260718_6>).
            if (isStageMode)
            {
                const double bottomGapAdjust = 8;
                RootGrid.Height += bottomGapAdjust;
                MinHeight += bottomGapAdjust;
                Height += bottomGapAdjust;
            }

            // <260718_3-2>: 빨간 ⓧ 바닥→키보드 꼭대기 '키보드위 간격'을 모든 창에서 표준 36 logical로 통일.
            // 현재 값: 1단계 45, 2~13단계 29, 클래식 28.5 → 목표 36이 되도록 창/그리드 세로를 델타만큼 조정.
            double topGapAdjust;
            if (isStage1)
            {
                topGapAdjust = -9; // 45 → 36 (키보드가 위로 9)
                // 1단계 안내문을 손모양 버튼 쪽으로 끌어올려(손모양↔안내문 간격 ≈ 4/11) 위로 온 키보드와 겹치지 않게 한다.
                GuideRow.Height = new GridLength(21);
                GuideTextPanel.VerticalAlignment = VerticalAlignment.Top;
                GuideTextPanel.Margin = new Thickness(0, -5, 0, 0);
            }
            else if (isStageMode)
            {
                // 2~13단계: 1단계와 같은 구조(안내문 행 21px 빈 스페이서 + 창 세로 동일)로 맞춰 키보드위 간격을 36으로.
                // (내용[안내문 행]+RootGrid를 함께 키워야 키보드가 1:1로 내려간다; 창만 키우면 Viewbox letterbox로 효과가 준다)
                topGapAdjust = 21;
                GuideRow.Height = new GridLength(21);
            }
            else
            {
                // 클래식(비두벌식): 손모양 버튼이 없어 단계 창보다 top 간격이 4 작으므로 4 더 준다 (25).
                topGapAdjust = 25;
                GuideRow.Height = new GridLength(25);
            }
            RootGrid.Height += topGapAdjust;
            MinHeight += topGapAdjust;
            Height += topGapAdjust;

            PreviewKeyDown += KeyPracticeWindow_PreviewKeyDown;
            PreviewKeyUp += KeyPracticeWindow_PreviewKeyUp;
            Deactivated += KeyPracticeWindow_Deactivated;

            if (isStageMode)
            {
                // 단계 모드: 손가락 레이어 인트로가 끝난 뒤에야 연습 키값이 제시된다 (<260717_29-2>).
                // 그때까지 상단 표시(다음 키 미리보기 포함)와 녹색 키는 나타나지 않는다.
                Loaded += (sender, e) => RunFingerLayerIntro();
            }
            else
            {
                // 기존 방식 창: 즉시 연습 시작. 손가락 레이어/손 모양 설정은 제공하지 않는다.
                HandButton.Visibility = Visibility.Collapsed;
                NextKey = RandomKey();
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
                                       new Action(MoveKey));
            }

            double shakiness = 30;
            const double shakeDiff = 3;
            var keyFrames = new ThicknessKeyFrameCollection();

            for (int timeSpan = 5; shakiness > 0;)
            {
                keyFrames.Add(new EasingThicknessKeyFrame(new Thickness(0, 10, 0, 0))
                {
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, timeSpan))
                });
                timeSpan += 5;

                keyFrames.Add(new EasingThicknessKeyFrame(new Thickness(shakiness, 10, 0, 0))
                {
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, timeSpan))
                });
                timeSpan += 5;

                keyFrames.Add(new EasingThicknessKeyFrame(new Thickness(0, 10, 0, 0))
                {
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, timeSpan))
                });
                timeSpan += 5;

                keyFrames.Add(new EasingThicknessKeyFrame(new Thickness(-shakiness, 10, 0, 0))
                {
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, timeSpan))
                });
                timeSpan += 5;

                keyFrames.Add(new EasingThicknessKeyFrame(new Thickness(0, 10, 0, 0))
                {
                    KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, timeSpan))
                });
                timeSpan += 5;

                shakiness -= shakeDiff;
            }

            ShakeAnimation.KeyFrames = keyFrames;
        }


        private KeyInfo RandomKey()
        {
            if (stagePool != null)
            {
                // 가중 복원추출 균등난수 방식: 가중 다중집합에서 균등하게 하나를 뽑는다.
                // 윗글쇠 여부는 뽑힌 항목에 고정되어 있다 (동전 던지기 없음).
                PracticeStage.StageItem item = stagePool[Randomizer.Next(0, stagePool.Count)];
                Key stageKey = MainWindow.CurrentKeyLayout[item.Pos];
                return new KeyInfo(item.IsShift ? stageKey.ShiftKeyData : stageKey.KeyData,
                                   item.Pos, item.IsShift);
            }

            // 조건부 2단계 복원추출 균등난수 방식 (기존 방식)
            KeyPos keyPos = keyList[Randomizer.Next(0, keyList.Count)];
            Key key = MainWindow.CurrentKeyLayout[keyPos];

            if (noShiftMode || string.IsNullOrEmpty(key.ShiftKeyData))
            {
                return new KeyInfo(key.KeyData, keyPos, false);
            }

            bool isShift = Randomizer.Next(0, 2) == 0;
            return new KeyInfo(isShift ? key.ShiftKeyData : key.KeyData, keyPos, isShift);
        }

        // 어느 쪽 [Shift]가 정답인지: 열(칼럼) 0~4는 왼손이 담당하므로 오른쪽 Shift가,
        // 열 5 이상은 오른손이 담당하므로 왼쪽 Shift가 정답이다(ㅒ·ㅖ 등 오른손 윗글쇠 포함). 격자 열
        // 위치만으로 판정하므로 자판 종류(두벌식 표준이 아니어도)와 무관하게 동일하다 (<260718_4>, <260718_5>).
        private static bool IsRightShiftCorrect(KeyPos pos) => pos.Column <= 4;

        private void MoveKey()
        {
            // 제시 키는 녹색으로만 표시하고 눌린 모양으로 만들지 않는다.
            // 눌린 모양+주황색은 사용자가 물리적으로 누르고 있는 키에만 쓴다. (<260717_26>)
            PreviousKey = CurrentKey;
            if (PreviousKey != null)
            {
                KeyLayoutBox.SetTargetKey(PreviousKey.Pos, false);
                if (PreviousKey.IsShift)
                {
                    KeyLayoutBox.LShiftKey.SetTarget(false);
                    KeyLayoutBox.RShiftKey.SetTarget(false);
                }
            }

            CurrentKey = NextKey;
            KeyLayoutBox.SetTargetKey(CurrentKey.Pos, true);
            if (CurrentKey.IsShift)
            {
                // 둘 다가 아니라, 정답인 쪽 Shift 하나만 녹색으로 제시한다 (<260718_4>)
                if (IsRightShiftCorrect(CurrentKey.Pos))
                {
                    KeyLayoutBox.RShiftKey.SetTarget(true);
                }
                else
                {
                    KeyLayoutBox.LShiftKey.SetTarget(true);
                }
            }

            NextKey = RandomKey();
        }

        private void KeyPracticeWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            System.Windows.Input.Key actualKey =
                e.Key == System.Windows.Input.Key.ImeProcessed ? e.ImeProcessedKey : e.Key;

            if (e.IsRepeat) return;

            // 인트로 애니메이션 중(첫 연습 키값이 제시되기 전)에는 렌더링된 키보드 트리거가
            // 전혀 작동하지 않는다 (<260717_29-4>) — Shift 포함, 어떤 키를 눌러도 색이 바뀌지 않는다.
            if (CurrentKey == null) return;

            // 1단계 안내(문구 2개·[ㄹ]/[ㅓ] 강조·밑줄 깜빡임) 진행 중에 아무 키나 누르면 즉시 끝낸다 (<260717_29-3>)
            EndStage1Guide();

            // Shift 키의 물리적 눌림 표시: 제시된 키가 윗글쇠이고 반대쪽 Shift를 누르면
            // 즉시 오답으로 간주해 빨간 키로 바꾸고 흔들며 다음 키값으로 넘어가지 않는다 (<260718_4>, <260718_5>).
            if (actualKey == System.Windows.Input.Key.LeftShift)
            {
                HandlePhysicalShiftPress(isRight: false);
                return;
            }
            if (actualKey == System.Windows.Input.Key.RightShift)
            {
                HandlePhysicalShiftPress(isRight: true);
                return;
            }

            KeyPos pos = KeyPos.FromKeyCode(actualKey);
            if (pos == null) return;

            bool isLShift = Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift);
            bool isRShift = Keyboard.IsKeyDown(System.Windows.Input.Key.RightShift);
            bool isShift = isLShift || isRShift;

            bool correct = CurrentKey.Pos == pos && CurrentKey.IsShift == isShift;

            // 물리적 눌림 표시: 제시된 키를 맞게 누르면 짙은 노란색, 아니면 빨강 (<260717_26-1-2>)
            if (correct)
            {
                KeyLayoutBox.PressPhysicalKey(pos);
            }
            else
            {
                KeyLayoutBox.GetKeyBox(pos).PressPhysicalIncorrect();
            }
            physicallyDownKeys.Add(pos);

            if (correct)
            {
                CorrectCount++;
                MoveKey();
            }
            else
            {
                IncorrectCount++;

                if (!incorrectStats.ContainsKey(CurrentKey.Pos)) incorrectStats[CurrentKey.Pos] = 1;
                else incorrectStats[CurrentKey.Pos]++;

                KeyGrid.BeginAnimation(MarginProperty, ShakeAnimation);
            }
        }

        /// <summary>Shift 키 하나가 눌렸을 때의 처리. 제시된 키가 윗글쇠 값이고 반대쪽 Shift가
        /// 눌리면 오답으로 간주해 빨간 키로 바꾸고 흔들며 다음 키값으로 넘어가지 않는다.
        /// 그 외(정답 쪽이거나 제시된 키가 윗글쇠가 아닐 때)에는 짙은 노란색으로만 표시한다 (<260718_4>, <260718_5>).</summary>
        private void HandlePhysicalShiftPress(bool isRight)
        {
            KeyBox shiftBox = isRight ? KeyLayoutBox.RShiftKey : KeyLayoutBox.LShiftKey;

            if (CurrentKey.IsShift && IsRightShiftCorrect(CurrentKey.Pos) != isRight)
            {
                shiftBox.PressPhysicalIncorrect();

                IncorrectCount++;
                if (!incorrectStats.ContainsKey(CurrentKey.Pos)) incorrectStats[CurrentKey.Pos] = 1;
                else incorrectStats[CurrentKey.Pos]++;

                KeyGrid.BeginAnimation(MarginProperty, ShakeAnimation);
            }
            else
            {
                shiftBox.PressPhysical();
            }
        }

        private void KeyPracticeWindow_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            System.Windows.Input.Key actualKey =
                e.Key == System.Windows.Input.Key.ImeProcessed ? e.ImeProcessedKey : e.Key;

            if (actualKey == System.Windows.Input.Key.LeftShift)
            {
                KeyLayoutBox.LShiftKey.ReleasePhysical();
                return;
            }
            if (actualKey == System.Windows.Input.Key.RightShift)
            {
                KeyLayoutBox.RShiftKey.ReleasePhysical();
                return;
            }

            KeyPos pos = KeyPos.FromKeyCode(actualKey);
            if (pos != null)
            {
                KeyLayoutBox.ReleasePhysicalKey(pos);
                physicallyDownKeys.Remove(pos);
            }
        }

        // 창이 비활성화되면 KeyUp을 놓칠 수 있으므로, 눌림 상태로 남은 키들을 모두 원래 색으로 되돌린다.
        private void KeyPracticeWindow_Deactivated(object sender, EventArgs e)
        {
            foreach (KeyPos pos in physicallyDownKeys)
            {
                KeyLayoutBox.ReleasePhysicalKey(pos);
            }
            physicallyDownKeys.Clear();

            KeyLayoutBox.LShiftKey.ReleasePhysical();
            KeyLayoutBox.RShiftKey.ReleasePhysical();
        }

        // ===== 손가락 레이어 (<260717_29>) =====

        /// <summary>
        /// 창이 열리면 0.1초 뒤에 손가락 레이어가 위에서 내려와 기본 자리에 놓이는 인트로.
        /// 애니메이션 동안에는 사용자의 켬/끔 설정과 무관하게 항상 보여 주고,
        /// 끝난 뒤에 사용자가 꺼 둔 상태라면 레이어를 숨긴다.
        /// </summary>
        private void RunFingerLayerIntro()
        {
            FingerLayer.ApplySettings();
            FingerLayer.Visibility = Visibility.Visible;

            var slide = new TranslateTransform(0, -340); // 키보드 위쪽 화면 밖에서 시작
            FingerLayer.RenderTransform = slide;

            var drop = new DoubleAnimation
            {
                From = -340,
                To = 0,
                BeginTime = TimeSpan.FromSeconds(0.1),
                Duration = new Duration(TimeSpan.FromSeconds(1.0)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            drop.Completed += (sender, e) =>
            {
                if (!Properties.Settings.Default.FingerLayerEnabled)
                {
                    FingerLayer.Visibility = Visibility.Collapsed;
                }

                if (isStage1)
                {
                    RunStage1Guide();
                }

                // 인트로가 끝난 뒤에야 연습 키값이 제시된다 (<260717_29-2>)
                StartPractice();
            };

            slide.BeginAnimation(TranslateTransform.YProperty, drop);
        }

        /// <summary>첫 연습 키값을 제시하며 연습을 시작한다.</summary>
        private void StartPractice()
        {
            NextKey = RandomKey();
            MoveKey();
        }

        private static readonly KeyPos[] Stage1IndexKeys = { new KeyPos(2, 3), new KeyPos(2, 6) }; // ㄹ, ㅓ (물리적 F/J)

        private bool stage1GuideActive;
        private DispatcherTimer stage1GuideTimer;

        /// <summary>
        /// 1단계 창 전용 후속 안내 (<260717_29-1>, <260717_29-3>): [ㄹ]·[ㅓ]를 #1c7ed6으로 강조하고 홈 포지션
        /// 밑줄을 깜빡이며, 키보드 바로 위·[ㅅ]·[ㅛ] 위·[ㅠ] 위에 안내 문구를 띄운다. 키보드 위 두 문구와
        /// 강조·밑줄 깜빡임은 15초가 지나거나 사용자가 키를 누르면(둘 중 먼저 오는 시점에) 사라지고,
        /// 키보드 바로 위 문구("10개의 손가락을...")는 계속 남는다.
        /// </summary>
        private void RunStage1Guide()
        {
            stage1GuideActive = true;

            GuideTextPanel.Visibility = Visibility.Visible;    // "10개의 손가락을..." (계속 유지)
            IndexGuideOverlay.Visibility = Visibility.Visible;  // "검지를 [ㄹ]과 [ㅓ] 위에 댑니다." ([ㅅ]·[ㅛ] 위)
            ThumbGuideOverlay.Visibility = Visibility.Visible;  // "두 엄지는 [Space] 위에 놓습니다." ([ㅠ] 위)

            foreach (KeyPos pos in Stage1IndexKeys)
            {
                KeyBox keyBox = KeyLayoutBox.GetKeyBox(pos);
                keyBox.SetGuideHighlight(true); // #1c7ed6 강조
                keyBox.BlinkHomeUnderline();    // EndStage1Guide()가 호출될 때까지 계속 깜빡임
            }

            stage1GuideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            stage1GuideTimer.Tick += (sender, e) => EndStage1Guide();
            stage1GuideTimer.Start();
        }

        /// <summary>1단계 안내(문구 2개·[ㄹ]/[ㅓ] 강조·밑줄 깜빡임)를 끝낸다. 15초 타이머와 첫 키 입력
        /// 양쪽에서 호출되므로, 이미 끝난 뒤라면 아무 일도 하지 않는다 (<260717_29-3>).</summary>
        private void EndStage1Guide()
        {
            if (!stage1GuideActive) return;
            stage1GuideActive = false;

            stage1GuideTimer?.Stop();

            IndexGuideOverlay.Visibility = Visibility.Collapsed;
            ThumbGuideOverlay.Visibility = Visibility.Collapsed;
            foreach (KeyPos pos in Stage1IndexKeys)
            {
                KeyBox keyBox = KeyLayoutBox.GetKeyBox(pos);
                keyBox.SetGuideHighlight(false);
                keyBox.StopBlink();
            }
        }

        private void HandButton_Click(object sender, RoutedEventArgs e)
        {
            var handSettingsWindow = new HandSettingsWindow(FingerLayer) { Owner = this };
            handSettingsWindow.ShowDialog();
        }

        private void KeyPracticeWindow_Closed(object sender, EventArgs e)
        {
            MainWindow.CurrentKeyLayout.Stats.AddStats(new KeyLayoutStats()
            {
                KeyIncorrectCount = incorrectStats
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
