using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MahApps.Metro.Controls;

namespace OpenTyping
{
    /// <summary>
    /// SyllablePracticeWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SyllablePracticeWindow : MetroWindow, INotifyPropertyChanged
    {
        private static readonly Random Randomizer = new Random();
        private readonly string syllablesList;
        private readonly bool hasVariety; // 서로 다른 음절이 2개 이상인지 (무한 루프 방지용)

        private char previousSyllable = ' ';
        public char PreviousSyllable
        {
            get => previousSyllable;
            set => SetField(ref previousSyllable, value);
        }

        private char currentSyllable = ' ';
        public char CurrentSyllable
        {
            get => currentSyllable;
            set => SetField(ref currentSyllable, value);
        }

        private char nextSyllable = ' ';
        public char NextSyllable
        {
            get => nextSyllable;
            set => SetField(ref nextSyllable, value);
        }

        private int correctCount;
        public int CorrectCount
        {
            get => correctCount;
            set => SetField(ref correctCount, value);
        }

        private readonly Brush incorrectBackground = Brushes.Pink;

        public SyllablePracticeWindow(string syllablesList)
        {
            // 음절이 하나도 없으면 RandomSyllable() 이 빈 문자열을 인덱싱하다 알아보기 힘든
            // IndexOutOfRangeException 으로 죽는다 — 원인을 바로 알 수 있는 예외로 일찍 막는다.
            if (string.IsNullOrEmpty(syllablesList))
                throw new ArgumentException("연습할 음절이 하나도 없습니다.", nameof(syllablesList));

            InitializeComponent();
            Title = VersionInfo.WindowTitle; // <2600912_5-1>

            void FocusCurrentTextBox(object sender, System.Windows.RoutedEventArgs e) { CurrentTextBox.Focus(); }
            this.Loaded += FocusCurrentTextBox;
            // 배치가 끝난 뒤에 재야 하므로 Loaded 안에서 곧바로 UpdateLayout 하지 않고 한 박자 미룬다
            // (레이아웃 도중에 다시 레이아웃을 돌리면 WPF 가 죽는다).
            this.Loaded += (s, e) => Dispatcher.BeginInvoke(
                new Action(AlignSpeedTileToInputBox), System.Windows.Threading.DispatcherPriority.Loaded);
            CurrentTextBox.LostFocus += FocusCurrentTextBox;
            // 음절 입력 텍스트 박스 포커스 항상 유지

            this.syllablesList = syllablesList;
            hasVariety = syllablesList.Distinct().Count() > 1;

            NextSyllable = RandomSyllable();
            MoveSyllable();
        }

        private char RandomSyllable()
        {
            return syllablesList[Randomizer.Next(syllablesList.Length)];
        }

        public void MoveSyllable()
        {
            PreviousSyllable = CurrentSyllable;
            CurrentSyllable = NextSyllable;

            char newSyllable;
            do
            {
                newSyllable = RandomSyllable();
            } while (hasVariety && newSyllable == CurrentSyllable); // 새 음절과 전 음절 중복 확인

            NextSyllable = newSyllable;

            // 주의: 여기서 CurrentTextBox 내용을 지우면 안 된다.
            // 한글 IME 조합이 진행 중일 때 코드로 텍스트를 바꾸면(Clear 등)
            // 조합 세션이 깨져 이후 입력이 TextChanged로 전달되지 않는 버그가 생긴다
            // (제시된 글자를 입력해도 넘어가지 않고 Space+Backspace를 눌러야 하는 원본 버그).
            // 대신 입력을 그대로 누적시키고, TextChanged에서 confirmedTextLength
            // 이후의 글자만 현재 음절과 비교한다.
            CurrentTextBoxBorder.Background = Brushes.White;
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

        private void CurrentTextBox_PreviewExecuted(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            // 다른 연습 창과 동일하게 복사/잘라내기/붙여넣기를 차단한다 (타자 없이 정답 입력 방지)
            if (e.Command == System.Windows.Input.ApplicationCommands.Copy ||
                e.Command == System.Windows.Input.ApplicationCommands.Cut ||
                e.Command == System.Windows.Input.ApplicationCommands.Paste)
            {
                e.Handled = true;
            }
        }

        private int confirmedTextLength; // 이미 정답 처리되어 소비된 입력 길이

        // ── 타속·평균·정확도 (<260812_20>) ──
        // 음절 하나를 처음 누른 순간부터 맞게 칠 때까지를 재고, 그 값들을 모아 평균을 낸다.
        // 계산식은 프로그램 전체와 같다 (<260812_14>: 정타 수 ÷ 분).
        private readonly TypingMeasurer measurer = new TypingMeasurer();
        private readonly List<int> speedList = new List<int>();
        private bool wasIncorrect;   // 같은 오타로 여러 번 세지 않도록 '틀린 상태' 진입만 센다
        private int wrongCount;

        private int typingSpeed;
        public int TypingSpeed
        {
            get => typingSpeed;
            private set => SetField(ref typingSpeed, value);
        }

        private int averageTypingSpeed;
        public int AverageTypingSpeed
        {
            get => averageTypingSpeed;
            private set => SetField(ref averageTypingSpeed, value);
        }

        private int typingAccuracy;
        public int TypingAccuracy
        {
            get => typingAccuracy;
            private set => SetField(ref typingAccuracy, value);
        }

        /// <summary>
        /// <260812_20> '타속' 타일의 좌우 가운데를 입력 칸의 좌우 가운데에 맞춘다. 나머지 두 타일은
        /// 그 오른쪽으로 이어진다. 제시 글자 폭이 자판·글꼴에 따라 달라질 수 있으므로 값을 박아 두지
        /// 않고 배치가 끝난 뒤 실제 좌표를 재서 정한다.
        /// </summary>
        internal void AlignSpeedTileToInputBox()
        {
            if (SpeedTile.ActualWidth <= 0 || CurrentTextBoxBorder.ActualWidth <= 0) return;

            // 입력 칸과 Canvas 는 형제라 서로가 조상이 아니다. 둘의 공통 조상(세로 StackPanel)을
            // 기준으로 좌표를 재서 Canvas 안의 위치로 환산한다.
            var common = VisualTreeHelper.GetParent(TileCanvas) as Visual;
            if (common == null) return;

            double boxCenter = CurrentTextBoxBorder.TransformToAncestor(common)
                .Transform(new Point(CurrentTextBoxBorder.ActualWidth / 2, 0)).X;
            double canvasLeft = TileCanvas.TransformToAncestor(common)
                .Transform(new Point(0, 0)).X;

            Canvas.SetLeft(TileRow, boxCenter - canvasLeft - SpeedTile.ActualWidth / 2);
        }

        /// <summary>'틀린 상태'로 막 들어섰을 때만 한 번 센다(같은 오타를 자모마다 세지 않도록).</summary>
        private void CountWrongOnce()
        {
            if (wasIncorrect) return;
            wasIncorrect = true;
            wrongCount++;
            UpdateAccuracy();
        }

        private void UpdateAccuracy()
        {
            TypingAccuracy = TypingMeasurer.Accuracy(CorrectCount, wrongCount);
        }

        private void CurrentTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string text = CurrentTextBox.Text;

            if (text.Length < confirmedTextLength) // 사용자가 처리된 영역까지 지운 경우
            {
                confirmedTextLength = text.Length;
            }

            // 처리된 글자들이 항상 시야 왼쪽 밖에 있도록 이동량을 동기화한다.
            // 텍스트를 바꾸지 않는 순수 시각 이동이라 IME 조합이 깨지지 않으며,
            // 레이아웃 갱신 뒤 좌표가 유효하도록 지연 호출한다.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var confirmedEdge = CurrentTextBox.GetRectFromCharacterIndex(confirmedTextLength);
                if (!confirmedEdge.IsEmpty && !double.IsInfinity(confirmedEdge.X))
                {
                    HiddenTextTranslate.X = -confirmedEdge.X;
                }
            }), System.Windows.Threading.DispatcherPriority.Render);

            string input = text.Substring(confirmedTextLength); // 아직 처리되지 않은 입력

            // <260812_20> 이 음절의 첫 타건에 시계를 켠다 (<260812_14>(1)와 같은 규칙).
            if (input.Length > 0 && !measurer.IsRunning) measurer.Start();

            switch (input.Length)
            {
                case 0:
                    CurrentTextBoxBorder.Background = Brushes.White;
                    // 지우고 다시 치는 것은 새 시도다 — 이전 오타로 잠긴 wasIncorrect를 풀어 둬야
                    // 다음에 (같은 자모든 다른 자모든) 오타를 내면 그것도 오타로 셀 수 있다.
                    wasIncorrect = false;
                    return;
                case 1:
                    List<char> decomposedCurrentSyllable = new List<char>(Differ.DecomposeHangul(CurrentSyllable)),
                               decomposedInput = new List<char>(Differ.DecomposeHangul(input[0]));

                    if (decomposedInput.SequenceEqual(decomposedCurrentSyllable))
                    {
                        // <260812_20> 이 음절을 맞게 쳤다 — 타속을 재고 평균·정확도를 갱신한다.
                        int speed = measurer.FinishTpmForText(CurrentSyllable.ToString());
                        if (speed > 0)
                        {
                            TypingSpeed = speed;
                            speedList.Add(speed);
                            AverageTypingSpeed = (int)Math.Round(speedList.Average());
                        }
                        wasIncorrect = false;

                        CorrectCount++;
                        UpdateAccuracy();
                        confirmedTextLength = text.Length;
                        MoveSyllable();
                        return;
                    }
                    if (decomposedInput.Any() &&
                        decomposedInput.SequenceEqual(decomposedCurrentSyllable.Take(decomposedInput.Count))) // 부분 일치
                    {
                        CurrentTextBoxBorder.Background = Brushes.White;
                        wasIncorrect = false;
                        return;
                    }

                    break;

                default:
                    // <260812_13> 2음절 이상 오타: 입력을 전부 버리고 처음부터 다시 치게 한다.
                    // (그대로 두면 확정 글자 왼쪽 밀어내기와 어긋나 글자는 안 보이고 빨간 칸만 남았다.)
                    //
                    // CurrentTextBox.Text 를 직접 지우지는 않는다 — 한글 IME 조합 중에 코드로 텍스트를
                    // 바꾸면 조합 세션이 깨져 이후 입력이 TextChanged 로 오지 않는다(MoveSyllable 의 주석).
                    // 대신 여기까지를 '처리된 입력'으로 삼아 화면 왼쪽 밖으로 밀어낸다. 사용자에게는
                    // 칸이 비워진 것과 똑같이 보이고, 다음에 제시어를 맞게 치면 그대로 통과한다.
                    confirmedTextLength = text.Length;
                    CurrentTextBoxBorder.Background = Brushes.White;
                    CountWrongOnce();
                    return;
            }

            CurrentTextBoxBorder.Background = incorrectBackground;
            CountWrongOnce();
        }
    }
}
