using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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
            InitializeComponent();

            void FocusCurrentTextBox(object sender, System.Windows.RoutedEventArgs e) { CurrentTextBox.Focus(); }
            this.Loaded += FocusCurrentTextBox;
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

            switch (input.Length)
            {
                case 0:
                    CurrentTextBoxBorder.Background = Brushes.White;
                    return;
                case 1:
                    List<char> decomposedCurrentSyllable = new List<char>(Differ.DecomposeHangul(CurrentSyllable)),
                               decomposedInput = new List<char>(Differ.DecomposeHangul(input[0]));

                    if (decomposedInput.SequenceEqual(decomposedCurrentSyllable))
                    {
                        CorrectCount++;
                        confirmedTextLength = text.Length;
                        MoveSyllable();
                        return;
                    }
                    if (decomposedInput.Any() &&
                        decomposedInput.SequenceEqual(decomposedCurrentSyllable.Take(decomposedInput.Count))) // 부분 일치
                    {
                        CurrentTextBoxBorder.Background = Brushes.White;
                        return;
                    }

                    break;
            }

            CurrentTextBoxBorder.Background = incorrectBackground;
        }
    }
}
