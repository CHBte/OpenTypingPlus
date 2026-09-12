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

        // 한 개의 제시 프롬프트를 실제 연습에 필요한 형태로 펼친 것.
        // 순1·순2(키 1개)든 순3·순4(음절/단어)든 모두 '스트로크(키 입력) 시퀀스'로 다룬다.
        private sealed class LivePrompt
        {
            public string Display;                       // 제시 영역에 통째로 보여줄 문자열
            public List<HangulJamo.Stroke> Strokes;      // 순서대로 눌러야 하는 키들
            public int Index;                            // program 내 위치(클래식은 -1) — <260812_7>
        }

        private readonly IList<KeyPos> keyList;
        private readonly bool noShiftMode;
        private readonly PracticeStage stage;      // 단계 모드일 때만
        private readonly int stageNumber;          // 단계 번호(1~13), 단계 모드일 때만 의미
        private readonly Dictionary<KeyPos, int> incorrectStats = new Dictionary<KeyPos, int>();
        private readonly HashSet<KeyPos> physicallyDownKeys = new HashSet<KeyPos>();

        // ── 프로그램(프롬프트 재생) 상태 ──
        private List<PracticePrompt> program;   // 단계 모드: 생성된 프롬프트 목록
        private int cursor;                      // 다음에 뽑을 program 인덱스
        private LivePrompt prevLive, curLive, nextLive;
        private int strokeIndex;

        // 초록 제시 표시 추적(스트로크 넘어갈 때 지우기)
        private bool greenActive, greenLShift, greenRShift;
        private KeyPos greenPos;

        // ── 분당 타 (<260723_3-1>): 타 = 올바르게 누른 키보드 키의 개수(타수) ──
        private readonly System.Diagnostics.Stopwatch practiceClock = new System.Diagnostics.Stopwatch();
        private DispatcherTimer tpmTimer;
        private int keystrokeCount;

        // ── 결합된 글자 구간에서 타수 다시 재기 (<260812_7>) ──
        // 제시어 목록의 후반부가 '자음과 모음이 결합된 글자'(완성형 음절)로만 이어지다 끝나면,
        // 그 구간에 들어가 사용자가 첫 타건을 하는 순간부터 타수를 처음부터 다시 잰다. 앞의 낱자·
        // 숫자·기호 구간은 속도 성격이 달라 함께 재면 결과가 왜곡되기 때문. 이렇게 다시 잰 값이
        // 곧 목표 타수와 견줄 그 단계의 타수가 된다.
        private int combinedTailStart = -1;   // 그 구간의 첫 프롬프트 위치(없으면 -1)
        private bool tpmRestarted;

        // ── 10개 끊어 오타 감지 (<260723_4> (0-4)) ──
        private int groupInputCount, groupWrongCount, consecutiveBadGroups;
        private DispatcherTimer noticeTimer;

        private bool practiceFinished;

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

        // <260812_2> 받아쓰기 칸: 제시어를 어디까지 맞게 눌렀는지 색으로 보여 준다 (<260812_2-1>).
        private readonly DictationState dictation = new DictationState();

        /// <summary>받아쓰기 칸을 지금 상태대로 다시 그린다.</summary>
        private void RefreshDictation()
        {
            DictationText.Inlines.Clear();
            foreach ((string text, System.Windows.Media.Brush color) in dictation.Segments())
                DictationText.Inlines.Add(new System.Windows.Documents.Run(text) { Foreground = color });

            // 직전에 통과한 제시어는 1~7번째 자리에 초록으로 (<260812_2-4>(2))
            DictationPassedText.Text = dictation.LastPassed;
        }

        /// <summary>눌린 키가 그 자판에서 내는 글자(받아쓰기 칸에 빨강으로 붙일 글자).</summary>
        private static string KeyChar(KeyPos pos, bool isShift)
        {
            Key key = MainWindow.CurrentKeyLayout[pos];
            if (key == null) return null;
            return isShift ? key.ShiftKeyData : key.KeyData;
        }

        private int correctCount = 0;
        public int CorrectCount
        {
            get => correctCount;
            private set { if (SetField(ref correctCount, value)) OnPropertyChanged(nameof(TypingAccuracy)); }
        }

        private int incorrectCount = 0;
        public int IncorrectCount
        {
            get => incorrectCount;
            private set { if (SetField(ref incorrectCount, value)) OnPropertyChanged(nameof(TypingAccuracy)); }
        }

        /// <summary>
        /// <260812_17> 카운터 영역의 정확도 타일. 맞게 누른 타 ÷ 전체 타건(%).
        /// 아직 아무것도 누르지 않았으면 0%로 시작한다.
        /// </summary>
        public int TypingAccuracy => TypingMeasurer.Accuracy(CorrectCount, IncorrectCount);

        private static readonly Random Randomizer = new Random();
        private static readonly ThicknessAnimationUsingKeyFrames ShakeAnimation = new ThicknessAnimationUsingKeyFrames();

        private static readonly Brush NoticeWarnBrush = new SolidColorBrush(Color.FromRgb(0xf0, 0x3e, 0x3e)); // 빨강(경고)
        private static readonly Brush NoticeCongratsBrush = new SolidColorBrush(Color.FromRgb(0x1c, 0x7e, 0xd6)); // 파랑(축하)

        /// <summary>기존 방식: 선택한 키 목록에서 조건부 2단계 복원추출 균등난수 방식으로 연습 ('두벌식 표준' 외 자판).</summary>
        public KeyPracticeWindow(IList<KeyPos> keyList, bool noShiftMode)
        {
            InitializeComponent();
            Title = VersionInfo.WindowTitle; // <2600912_5-1>

            this.keyList = keyList;
            this.noShiftMode = noShiftMode;

            InitPractice();
        }

        /// <summary>단계 모드: 단계 프로그램(순1~순4)을 차례로 진행 ('두벌식 표준' 자판) (<260723_4>).</summary>
        public KeyPracticeWindow(PracticeStage stage)
        {
            InitializeComponent();
            Title = VersionInfo.WindowTitle; // <2600912_5-1>

            this.stage = stage;
            isStageMode = true;
            stageNumber = DubeolsikStages.Stages.IndexOf(stage) + 1;
            isStage1 = ReferenceEquals(stage, DubeolsikStages.Stages[0]);

            InitPractice();
        }

        private readonly bool isStageMode;
        private readonly bool isStage1;

        private void InitPractice()
        {
            // <260812_2-2>(2-2) 받아쓰기 칸이 제시 영역 위에 한 줄을 새로 차지하므로, 그 높이만큼
            // 창 세로도 늘려 아래 요소들(카운터·안내 문구·키보드)의 간격이 그대로 유지되게 한다.
            double dictationRowHeight = DictationRow.Height.Value;   // XAML 고정값(56)
            RootGrid.Height += dictationRowHeight;
            MinHeight += dictationRowHeight;
            Height += dictationRowHeight;

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

            // 카운트 행에 "손 모양" 버튼이 있어 그 행이 커지는 만큼 키보드가 아래로 밀려, 렌더링 키보드
            // 바닥 간격이 표준 바닥 간격 15px보다 8px 작다. 그래서 세로를 8px 늘려 맞춘다 (<260718_6>).
            // <260811_33> 클래식 창(두벌식 표준 외 자판)도 단계 창과 같은 구성(손 모양 버튼·손가락
            // 레이어)을 쓰게 되었으므로, 이 보정을 두 경우 모두에 적용한다.
            {
                const double bottomGapAdjust = 8;
                RootGrid.Height += bottomGapAdjust;
                MinHeight += bottomGapAdjust;
                Height += bottomGapAdjust;
            }

            // <260718_3-2>: 빨간 ⓧ 바닥→키보드 꼭대기 '안내 문구 영역'을 모든 창에서 표준 36 logical로 통일.
            // 현재 값: 1단계 45, 2~13단계 29, 클래식 28.5 → 목표 36이 되도록 창/그리드 세로를 델타만큼 조정.
            double topGapAdjust;
            if (isStage1)
            {
                topGapAdjust = -9; // 45 → 36 (키보드가 위로 9)
            }
            else
            {
                // 2~13단계와 클래식 창: 1단계와 같은 구조(안내문 행 21px 빈 스페이서 + 창 세로 동일)로
                // 맞춰 안내 문구 영역을 36으로 한다 (<260811_33> 로 두 경우의 구성이 같아졌다).
                topGapAdjust = 21;
            }
            GuideRow.Height = new GridLength(21);

            // 안내 문구 영역의 글자 자리는 **모든 창에서 같다** (<260812_15> 뒤 위치 통일).
            // 1단계 안내문을 손모양 버튼 쪽으로 끌어올리던 보정(<260718_3-2>, 손모양↔안내문 간격 ≈ 4/11)을
            // 경고·축하 문구에도, 그리고 1단계가 아닌 창에도 똑같이 준다. 그러지 않으면 어떤 문구가
            // 뜨느냐·어떤 창이냐에 따라 글자가 5.5px 뛰어 보인다.
            GuideTextPanel.VerticalAlignment = VerticalAlignment.Top;
            GuideTextPanel.Margin = new Thickness(0, -5, 0, 0);
            NoticeText.VerticalAlignment = VerticalAlignment.Top;
            NoticeText.Margin = new Thickness(0, -5, 0, 0);

            RootGrid.Height += topGapAdjust;
            MinHeight += topGapAdjust;
            Height += topGapAdjust;

            // 타속 타일은 클래식 창(두벌식 표준 외 자판)에서도 보여 준다 (<260812_21-1>).
            // 단계 모드에서만 보이던 <260723_3>의 제한을 이것이 대체한다.

            PreviewKeyDown += KeyPracticeWindow_PreviewKeyDown;
            PreviewKeyUp += KeyPracticeWindow_PreviewKeyUp;
            Deactivated += KeyPracticeWindow_Deactivated;

            // 손가락 레이어 인트로가 끝난 뒤에야 연습 값이 제시된다 (<260717_29-2>).
            // <260811_33> 클래식 창(두벌식 표준 외 자판)도 단계 창과 똑같이 손가락 레이어·"손 모양"
            // 버튼·안내 문구를 쓴다. 손 모양 벡터는 물리 키 위치(행·열) 기준이라 자판이 달라도 그대로
            // 재활용된다. 배정이 없는 키(`⧵` 등)는 SetPose 가 기본자세로 되돌리므로 손이 가만히 있는다.
            Loaded += (sender, e) => RunFingerLayerIntro();

            BuildShakeAnimation();
        }

        private void BuildShakeAnimation()
        {
            double shakiness = 30;
            const double shakeDiff = 3;
            var keyFrames = new ThicknessKeyFrameCollection();

            for (int timeSpan = 5; shakiness > 0;)
            {
                keyFrames.Add(new EasingThicknessKeyFrame(new Thickness(0, 10, 0, 0))
                { KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, timeSpan)) });
                timeSpan += 5;
                keyFrames.Add(new EasingThicknessKeyFrame(new Thickness(shakiness, 10, 0, 0))
                { KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, timeSpan)) });
                timeSpan += 5;
                keyFrames.Add(new EasingThicknessKeyFrame(new Thickness(0, 10, 0, 0))
                { KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, timeSpan)) });
                timeSpan += 5;
                keyFrames.Add(new EasingThicknessKeyFrame(new Thickness(-shakiness, 10, 0, 0))
                { KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, timeSpan)) });
                timeSpan += 5;
                keyFrames.Add(new EasingThicknessKeyFrame(new Thickness(0, 10, 0, 0))
                { KeyTime = KeyTime.FromTimeSpan(new TimeSpan(0, 0, 0, 0, timeSpan)) });
                timeSpan += 5;
                shakiness -= shakeDiff;
            }
            ShakeAnimation.KeyFrames = keyFrames;
        }

        // ===== 프롬프트 소싱 =====

        /// <summary>다음 프롬프트를 뽑는다. 단계 모드는 프로그램이 끝나면 null(연습 종료), 클래식은 무한.</summary>
        private PracticePrompt DrawNext()
        {
            if (isStageMode)
            {
                if (program == null || cursor >= program.Count) { lastDrawnIndex = -1; return null; }
                lastDrawnIndex = cursor;
                return program[cursor++];
            }
            lastDrawnIndex = -1;
            return DrawClassic();
        }

        private int lastDrawnIndex = -1;   // 방금 뽑은 프롬프트의 program 내 위치(클래식은 -1)

        // 조건부 2단계 복원추출 균등난수 방식 (기존 방식): 키 목록에서 뽑고 윗글쇠는 동전 던지기.
        private PracticePrompt DrawClassic()
        {
            KeyPos keyPos = keyList[Randomizer.Next(0, keyList.Count)];
            Key key = MainWindow.CurrentKeyLayout[keyPos];

            bool isShift;
            if (noShiftMode || string.IsNullOrEmpty(key.ShiftKeyData)) isShift = false;
            else isShift = Randomizer.Next(0, 2) == 0;

            return PracticePrompt.ForKey(new PracticeStage.StageItem(keyPos.Row, keyPos.Column, isShift));
        }

        private LivePrompt Build(PracticePrompt p)
        {
            if (p == null) return null;
            var lp = new LivePrompt { Index = lastDrawnIndex };
            if (p.IsText)
            {
                lp.Display = p.Text;
                lp.Strokes = HangulJamo.Decompose(p.Text);
            }
            else
            {
                lp.Display = PromptDisplay(p);
                lp.Strokes = new List<HangulJamo.Stroke> { new HangulJamo.Stroke(p.Key, p.IsShift) };
            }
            return lp;
        }

        private static string PromptDisplay(PracticePrompt p)
        {
            if (p.IsText) return p.Text;
            Key k = MainWindow.CurrentKeyLayout[p.Key];
            return p.IsShift ? k.ShiftKeyData : k.KeyData;
        }

        // ===== 재생 =====

        private HangulJamo.Stroke CurrentStroke => curLive.Strokes[strokeIndex];

        /// <summary>검사용: 지금 눌러야 하는 키 자리(연습이 시작되지 않았으면 null).</summary>
        internal KeyPos ExpectedPos =>
            curLive != null && strokeIndex < curLive.Strokes.Count ? curLive.Strokes[strokeIndex].Pos : null;

        private void StartPractice()
        {
            if (isStageMode) program = stage.Generate(Randomizer);
            combinedTailStart = FindCombinedTailStart(program);   // <260812_7>
            tpmRestarted = false;
            cursor = 0;
            keystrokeCount = 0;
            measuredLetters = 0;
            measuredCorrect = measuredWrong = 0;
            CorrectCount = 0;
            IncorrectCount = 0;
            groupInputCount = groupWrongCount = consecutiveBadGroups = 0;
            // 다시 시작(재도전·사보타지 재시작)한 시도의 오타는 버려진 시도의 것이므로, 자판에
            // 영구히 누적되는 '많이 틀리는 키' 통계(incorrectStats)에도 남지 않아야 한다.
            incorrectStats.Clear();
            practiceFinished = false;
            dictation.Clear();   // <260812_2-1> 처음부터 다시 시작할 때는 받아쓰기 칸도 비운다

            prevLive = null;
            curLive = SkipEmpty(Build(DrawNext()));
            nextLive = SkipEmpty(Build(DrawNext()));
            strokeIndex = 0;

            if (curLive == null) { FinishStage(); return; }

            RefreshDisplay();
            HighlightStroke();

            // <260812_14>(1) 시계는 첫 제시어가 뜰 때가 아니라 사용자가 첫 타건을 했을 때 시작한다.
            // (제시어를 보고 머뭇거린 시간이 타수를 깎지 않도록.)
            practiceClock.Reset();
            StartTpmTimer();
            UpdateTpm();
        }

        // 스트로크가 0개인(매핑 실패) 프롬프트는 건너뛴다.
        private LivePrompt SkipEmpty(LivePrompt lp)
        {
            while (lp != null && lp.Strokes.Count == 0)
                lp = Build(DrawNext());
            return lp;
        }

        private void RefreshDisplay()
        {
            PreviousKey = prevLive == null ? null : new KeyInfo(prevLive.Display, default, false);
            CurrentKey = curLive == null ? null : new KeyInfo(curLive.Display, default, false);
            NextKey = nextLive == null ? null : new KeyInfo(nextLive.Display, default, false);
        }

        private void ClearGreen()
        {
            if (greenActive) { KeyLayoutBox.SetTargetKey(greenPos, false); greenActive = false; }
            if (greenLShift) { KeyLayoutBox.LShiftKey.SetTarget(false); greenLShift = false; }
            if (greenRShift) { KeyLayoutBox.RShiftKey.SetTarget(false); greenRShift = false; }
        }

        // 현재 스트로크의 키를 초록으로 제시하고 손가락 포즈를 맞춘다.
        private void HighlightStroke()
        {
            ClearGreen();
            if (curLive != null && strokeIndex == 0) dictation.Start(curLive.Display);
            RefreshDictation();
            if (curLive == null || strokeIndex >= curLive.Strokes.Count) return;

            HangulJamo.Stroke s = CurrentStroke;
            KeyLayoutBox.SetTargetKey(s.Pos, true);
            greenPos = s.Pos;
            greenActive = true;

            if (s.IsShift)
            {
                if (IsRightShiftCorrect(s.Pos)) { KeyLayoutBox.RShiftKey.SetTarget(true); greenRShift = true; }
                else { KeyLayoutBox.LShiftKey.SetTarget(true); greenLShift = true; }
            }

            FingerLayer.SetPose(s.Pos, s.IsShift);   // <260811_33> 클래식 창에서도 적용
        }

        // 어느 쪽 [Shift]가 정답인지: 왼손 담당 키는 오른쪽 Shift가, 오른손 담당 키는 왼쪽 Shift가 정답
        // (0행은 열 0~5가 왼손 담당) (<260718_4>, <260718_5>, <260718_10>).
        private static bool IsRightShiftCorrect(KeyPos pos) =>
            pos.Row == 0 ? pos.Column <= 5 : pos.Column <= 4;

        // 현재 프롬프트의 스트로크 하나를 맞혔을 때: 타를 더하고 다음 스트로크/프롬프트로 넘어간다.
        private void AdvanceStroke()
        {
            keystrokeCount++; // 올바르게 누른 키보드 키 하나 = 1타 (<260723_3-1>)
            measuredCorrect++;
            strokeIndex++;

            if (strokeIndex < curLive.Strokes.Count)
            {
                HighlightStroke();
                UpdateTpm();
                return;
            }

            // 제시어 하나를 끝냈으니 '원래 방식'의 글자수 환산에 더한다 (<260812_12>).
            measuredLetters += TypingMeasurer.CountLetter(curLive.Display);

            // 프롬프트 완료 → 받아쓰기 칸을 곧바로 비우고 다음 프롬프트로 (<260812_2-3>(2))
            dictation.Pass();
            RefreshDictation();

            prevLive = curLive;
            curLive = SkipEmpty(nextLive);
            nextLive = SkipEmpty(Build(DrawNext()));
            strokeIndex = 0;

            if (curLive == null) { FinishStage(); return; }

            RefreshDisplay();
            HighlightStroke();
            UpdateTpm();
        }

        // ===== 입력 =====

        private void KeyPracticeWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            System.Windows.Input.Key actualKey =
                e.Key == System.Windows.Input.Key.ImeProcessed ? e.ImeProcessedKey : e.Key;

            if (e.IsRepeat) return;

            // 인트로/종료 상태에서는 트리거 작동 안 함.
            if (curLive == null || practiceFinished || strokeIndex >= curLive.Strokes.Count) return;

            EndStage1Guide();

            HangulJamo.Stroke expected = CurrentStroke;

            if (actualKey == System.Windows.Input.Key.LeftShift) { HandlePhysicalShiftPress(false, expected); return; }
            if (actualKey == System.Windows.Input.Key.RightShift) { HandlePhysicalShiftPress(true, expected); return; }

            KeyPos pos = KeyPos.FromKeyCode(actualKey);
            if (pos == null) return;

            bool isShift = Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift)
                        || Keyboard.IsKeyDown(System.Windows.Input.Key.RightShift);

            bool correct = expected.Pos == pos && expected.IsShift == isShift;

            // <260812_14>(1) 첫 타건에 시계를 켠다.
            if (!practiceClock.IsRunning) practiceClock.Restart();

            RestartTpmIfCombinedTailBegan();   // <260812_7> 이 타건부터 타수를 다시 잰다

            if (correct)
            {
                KeyLayoutBox.PressPhysicalKey(pos);
                physicallyDownKeys.Add(pos);
                CorrectCount++;
                dictation.Correct();   // <260812_2-1>(3) 빨강 글자를 지우고 한 타 나아간다
                HideTypoWarning();     // <260812_15> 맞게 눌렀으니 원래 문구로
                bool restarted = RegisterInput(true);
                if (!restarted) AdvanceStroke();
            }
            else
            {
                KeyLayoutBox.GetKeyBox(pos).PressPhysicalIncorrect();
                physicallyDownKeys.Add(pos);
                measuredWrong++;                          // <260812_12> '원래 방식'의 정확도용
                dictation.Wrong(KeyChar(pos, isShift));   // <260812_2-1>(3) 틀린 글자는 빨강
                RefreshDictation();
                ShowTypoWarning();                        // <260812_15>
                RecordIncorrect();
                RegisterInput(false);
            }
        }

        /// <summary>제시된 키가 윗글쇠 값인데 반대쪽 Shift를 누르면 오답 처리. 그 외는 눌림 표시만.</summary>
        private void HandlePhysicalShiftPress(bool isRight, HangulJamo.Stroke expected)
        {
            KeyBox shiftBox = isRight ? KeyLayoutBox.RShiftKey : KeyLayoutBox.LShiftKey;

            if (expected.IsShift && IsRightShiftCorrect(expected.Pos) != isRight)
            {
                shiftBox.PressPhysicalIncorrect();
                RecordIncorrect();
                ShowTypoWarning();   // <260812_15> 반대쪽 Shift 도 오타다
                RegisterInput(false);
            }
            else
            {
                shiftBox.PressPhysical();
            }
        }

        private void RecordIncorrect()
        {
            IncorrectCount++;
            KeyPos p = CurrentStroke.Pos;
            if (!incorrectStats.ContainsKey(p)) incorrectStats[p] = 1;
            else incorrectStats[p]++;
            KeyGrid.BeginAnimation(MarginProperty, ShakeAnimation);
        }

        // 10개 끊어서 8개 이상 틀리기를 연속 두 번 하면 경고 + 초기화 + 단계 재시작 (<260723_4> (0-4)).
        // 단계 모드에서만 동작. 재시작을 트리거했으면 true.
        // <260811_33> 안내 문구도 단계 창과 같은 방식으로 나와야 하므로 클래식 창에서도 동작한다
        // (클래식은 끝이 없어 축하 문구는 나올 일이 없고, 경고 문구만 해당된다).
        private bool RegisterInput(bool correct)
        {
            groupInputCount++;
            if (!correct) groupWrongCount++;

            if (groupInputCount >= 10)
            {
                bool bad = groupWrongCount >= 8;
                consecutiveBadGroups = bad ? consecutiveBadGroups + 1 : 0;
                groupInputCount = 0;
                groupWrongCount = 0;

                if (consecutiveBadGroups >= 2)
                {
                    TriggerSabotageWarning();
                    return true;
                }
            }
            return false;
        }

        private void TriggerSabotageWarning()
        {
            // 경고 문구를 10초간 단독 표시(다른 문구가 있어도 이 문구만).
            ShowNotice("경고: 의도적으로 오타를 내고 있는 것 같습니다.", NoticeWarnBrush, hideOthers: true);
            sabotageNoticeActive = true;   // 이 10초 동안은 오타 경고(<260812_15>)가 끼어들지 않는다
            noticeTimer?.Stop();
            noticeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            noticeTimer.Tick += (s, e) => { noticeTimer.Stop(); sabotageNoticeActive = false; HideNotice(); };
            noticeTimer.Start();

            // 맞은/틀린 카운트 0으로 초기화하고 단계 연습을 처음부터 다시 시작.
            RestartStage();
        }

        private void RestartStage()
        {
            ClearGreen();
            EndStage1Guide();
            StartPractice(); // 카운트·타·타이머·프로그램 전부 초기화 후 재시작
        }

        /// <summary>
        /// <260812_7> 제시어 목록의 '후반부가 결합된 글자로만 이어지다 끝나는' 구간의 시작 위치.
        /// 그런 구간이 없거나(마지막이 낱자·기호), 목록 전체가 결합된 글자라 다시 잴 필요가 없으면 -1.
        /// </summary>
        internal static int FindCombinedTailStart(IList<PracticePrompt> program)
        {
            if (program == null || program.Count == 0) return -1;

            int i = program.Count - 1;
            while (i >= 0 && IsCombined(program[i])) i--;

            int start = i + 1;
            // start == 0 이면 처음부터 전부 결합된 글자 → 원래 시작 시점과 같아 다시 잴 것이 없다.
            // start == Count 이면 마지막이 낱자·기호 → 해당 없음.
            return start > 0 && start < program.Count ? start : -1;
        }

        /// <summary>자음과 모음이 결합된 글자(완성형 한글 음절)를 담고 있는 제시어인가.</summary>
        private static bool IsCombined(PracticePrompt p)
        {
            // 키 하나로 내는 글자(낱자·숫자·기호)는 정의상 결합된 글자가 아니므로 자판을 볼 것도 없다.
            if (p == null || !p.IsText) return false;
            string text = p.Text;
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char ch in text)
                if (ch >= 0xAC00 && ch <= 0xD7A3) return true;
            return false;
        }

        /// <summary>
        /// <260812_7> 결합된 글자 구간의 첫 타건이면 타수를 처음부터 다시 재기 시작한다.
        /// 맞든 틀리든 '타건을 한 순간'이 기준이며, 이 타건부터가 새 측정의 1타째다.
        /// </summary>
        private void RestartTpmIfCombinedTailBegan()
        {
            if (tpmRestarted || combinedTailStart < 0 || curLive == null) return;
            if (curLive.Index < combinedTailStart) return;

            tpmRestarted = true;
            keystrokeCount = 0;
            measuredLetters = 0;
            measuredCorrect = measuredWrong = 0;
            practiceClock.Restart();
            UpdateTpm();
        }

        // ===== 분당 타 =====

        private void StartTpmTimer()
        {
            tpmTimer?.Stop();
            tpmTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            tpmTimer.Tick += (s, e) => UpdateTpm();
            tpmTimer.Start();
        }

        /// <summary>
        /// <260812_12> 타속 계산 방법. 치트 창에서 고른 값(<see cref="UserSettingsStore.TpmMethod"/>)을 따른다.
        ///  - 간단 방식: 분당 타 = 올바르게 누른 키 수 ÷ 경과 시간(분)
        ///  - 원래 방식: 타속 = (글자수 환산 ÷ 걸린 시간(분)) × 정확도
        ///    (글자수 환산은 문장연습과 같은 규칙 — 한글 음절 2.5, 그 외 1. 정확도는 이번 측정 구간의
        ///     정타 ÷ 전체 타건.)
        /// 둘 다 반올림한 정수다.
        /// </summary>
        private int CurrentTpm()
        {
            double minutes = practiceClock.Elapsed.TotalMinutes;
            if (minutes <= 0) return 0;

            // TypingMeasurer의 다른 타속 계산과 반올림 규칙을 맞춘다(AwayFromZero) — 기본 Math.Round는
            // 은행원 반올림(가장 가까운 짝수)이라 x.5 값에서 TypingMeasurer 쪽과 결과가 갈릴 수 있다.
            if (TypingMeasurer.IsOriginalMethod)
            {
                if (measuredLetters <= 0) return 0;
                int typed = measuredCorrect + measuredWrong;
                double accuracy = typed > 0 ? measuredCorrect / (double)typed : 1.0;
                return (int)Math.Round(measuredLetters / minutes * accuracy, MidpointRounding.AwayFromZero);
            }

            if (keystrokeCount <= 0) return 0;
            return (int)Math.Round(keystrokeCount / minutes, MidpointRounding.AwayFromZero);
        }

        // '원래 방식'용 누적값 — 측정 구간(<260812_7>로 다시 잴 수 있다) 안에서만 센다.
        private double measuredLetters;   // 통과한 제시어의 글자수 환산 합
        private int measuredCorrect, measuredWrong;

        private void UpdateTpm()
        {
            TpmText.Text = CurrentTpm().ToString();   // <260812_21> '타속' 타일 안의 숫자
        }

        // ===== 종료 / 오락 해금 (<260723_4> (1)) =====

        private void FinishStage()
        {
            practiceFinished = true;
            practiceClock.Stop();
            tpmTimer?.Stop();
            noticeTimer?.Stop(); // 진행 중이던 경고 10초 타이머가 아래 축하 문구를 지우지 않도록
            ClearGreen();
            dictation.Clear();          // <260812_2-3>(2) 통과와 동시에 칸을 비운다
            RefreshDictation();
            UpdateTpm();

            if (!isStageMode) return; // 클래식은 종료 개념 없음(무한)

            int tpm = CurrentTpm();
            // 이번 완주로 '처음' 목표 타수를 넘겨 오락이 새로 열리는지(이전 최고가 목표 타수 미만) 기록 전에 판정한다.
            bool newlyUnlocked = tpm >= StageRecords.PassThreshold
                                 && StageRecords.BestTa(stageNumber) < StageRecords.PassThreshold;

            // 매 완주마다 이 단계의 최고 타 기록을 갱신한다 (<260724_2-1>).
            StageRecords.Record(stageNumber, tpm);

            // 처음 목표 타수를 넘긴 순간에만, 오락이 있는 단계(9~12 제외)면 축하 문구를 띄운다 (<260724_2>(1)).
            // (다음 자리연습 단계 타일 활성화·오락 해금은 최고 기록에서 파생되므로 문구와 무관하게 이뤄진다.)
            // <260812_4-2>: 완주할 때마다 결과 문구를 띄우던 <260812_4>·<260812_4-1>은 폐기되어,
            // 여기 표시는 원래의 이 축하 문구로 되돌아왔다.
            if (newlyUnlocked && stage.GameStageId != 0)
                ShowNotice("축하합니다! " + stageNumber + "단계의 오락이 열렸습니다",
                           NoticeCongratsBrush, hideOthers: true);

            // <260812_5> 이어서 무엇을 할지 고르는 창을 띄운다. 마지막 키 입력 처리가 끝난 뒤에
            // 열어야 입력 이벤트와 모달 창이 얽히지 않는다.
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(ShowFinishWindow));
        }

        /// <summary>
        /// <260812_5> 연습을 마친 뒤 고른 다음 행동. 이 창을 연 '자리연습' 화면이 읽어 실행한다
        /// (창 안에서 창을 겹쳐 열지 않기 위함 — <260811_34>).
        /// </summary>
        public StageFinishWindow.Choice FinishChoice { get; private set; } = StageFinishWindow.Choice.Close;

        private void ShowFinishWindow()
        {
            // 마지막 키 입력 직후 곧바로 이 창을 닫으면(Alt+F4 등) 이 콜백은 Background 우선순위라
            // 그 뒤에 실행될 수 있다 — 이미 닫힌 창을 Owner로 새 창을 띄우면 WPF가 예외를 던지므로
            // 먼저 걸러낸다.
            if (!IsLoaded) return;

            var win = new StageFinishWindow(stageNumber, DubeolsikStages.Stages.Count,
                                            CurrentTpm(), stage.GameStageId)
            {
                Owner = this
            };
            win.ShowDialog();

            if (win.Action == StageFinishWindow.Choice.Retry)
            {
                RestartStage();   // 같은 창에서 이 단계를 처음부터 다시
                return;
            }

            FinishChoice = win.Action;
            // 버튼을 눌러 닫았든[Alt+F4] 등으로 그냥 닫혔든, 이 연습 창은 이미 끝난 상태이므로 항상
            // 닫는다 — 안 그러면 [Alt+F4]로 완료 창만 닫혔을 때 입력을 더 받지 않는 이 창이 화면에
            // 남아 호출부(RunStage)의 대화상자 대기가 풀리지 않는다.
            Close();
        }

        // ===== 안내 문구 영역 =====

        /// <summary>
        /// <260812_15> 오타를 내면 안내 문구 영역에 빨간 경고를 띄우고, 다음에 맞게 누르면 원래 문구로
        /// 되돌린다. 단, 10초짜리 '의도적 오타' 경고(<260723_4>(0-4))가 떠 있는 동안에는 건드리지
        /// 않는다 — 그 경고가 한 타 만에 지워지면 알릴 이유가 없어진다.
        /// </summary>
        internal const string TypoWarning = "오타를 내면 타속이 낮아집니다.";

        private bool sabotageNoticeActive;

        private void ShowTypoWarning()
        {
            if (sabotageNoticeActive) return;
            ShowNotice(TypoWarning, NoticeWarnBrush, hideOthers: true);
        }

        private void HideTypoWarning()
        {
            if (sabotageNoticeActive) return;
            if (NoticeText.Visibility == Visibility.Visible && NoticeText.Text == TypoWarning) HideNotice();
        }

        private void ShowNotice(string text, Brush brush, bool hideOthers)
        {
            if (hideOthers)
            {
                GuideTextPanel.Visibility = Visibility.Collapsed;
                IndexGuideOverlay.Visibility = Visibility.Collapsed;
                ThumbGuideOverlay.Visibility = Visibility.Collapsed;
            }
            NoticeText.Foreground = brush;
            NoticeText.Text = text;
            NoticeText.Visibility = Visibility.Visible;
        }

        private void HideNotice()
        {
            NoticeText.Visibility = Visibility.Collapsed;
            // 1단계의 상시 안내문("10개의 손가락을...")은 다시 보여 준다(연습이 계속되는 경우).
            if (isStage1 && !practiceFinished) GuideTextPanel.Visibility = Visibility.Visible;
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

        private void RunFingerLayerIntro()
        {
            FingerLayer.ApplySettings();
            FingerLayer.Visibility = Visibility.Visible;

            var slide = new TranslateTransform(0, -340);
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
                // 인트로 애니메이션(약 1.1초) 도중에 창을 닫으면, 애니메이션 클록은 창 생명주기와
                // 무관하게 계속 돌다가 뒤늦게 이 콜백을 실행한다 — 이미 닫힌 창에서 StartPractice()가
                // 새 타이머를 만들어 아무도 멈추지 않는 채로 계속 도는 것을 막는다.
                if (!IsLoaded) return;

                if (!UserSettingsStore.FingerLayerEnabled)
                {
                    FingerLayer.Visibility = Visibility.Collapsed;
                }

                if (isStage1) RunStage1Guide();

                // 인트로가 끝난 뒤에야 연습 값이 제시된다 (<260717_29-2>)
                StartPractice();
            };

            slide.BeginAnimation(TranslateTransform.YProperty, drop);
        }

        private static readonly KeyPos[] Stage1IndexKeys = { new KeyPos(2, 3), new KeyPos(2, 6) }; // ㄹ, ㅓ

        private bool stage1GuideActive;
        private DispatcherTimer stage1GuideTimer;

        private void RunStage1Guide()
        {
            stage1GuideActive = true;

            GuideTextPanel.Visibility = Visibility.Visible;
            IndexGuideOverlay.Visibility = Visibility.Visible;
            ThumbGuideOverlay.Visibility = Visibility.Visible;

            foreach (KeyPos pos in Stage1IndexKeys)
            {
                KeyBox keyBox = KeyLayoutBox.GetKeyBox(pos);
                keyBox.SetGuideHighlight(true);
                keyBox.BlinkHomeUnderline();
            }

            stage1GuideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            stage1GuideTimer.Tick += (sender, e) => EndStage1Guide();
            stage1GuideTimer.Start();
        }

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
            // 설정 창을 보는 동안의 시간이 타수 측정에 끼지 않도록 잠시 멈춘다(AcidRainWindow의
            // 치트 창 열기와 같은 이유). Stop()은 지난 경과 시간을 그대로 두고 멈추므로,
            // 다시 Start()하면 이어서 잰다(Restart()처럼 0으로 되돌아가지 않는다).
            bool wasRunning = practiceClock.IsRunning;
            if (wasRunning) practiceClock.Stop();

            var handSettingsWindow = new HandSettingsWindow(FingerLayer) { Owner = this };
            handSettingsWindow.ShowDialog();

            if (wasRunning) practiceClock.Start();
        }

        private void KeyPracticeWindow_Closed(object sender, EventArgs e)
        {
            tpmTimer?.Stop();
            noticeTimer?.Stop();
            stage1GuideTimer?.Stop(); // 1단계 안내 15초 타이머가 닫힌 창에서 뒤늦게 발화하지 않도록
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
