using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MahApps.Metro.Controls;
using SkiaSharp;

namespace OpenTyping
{
    /// <summary>
    /// SentencePracticeWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SentencePracticeWindow : MetroWindow, INotifyPropertyChanged
    {
        private string currentText;
        public string CurrentText
        {
            get => currentText;
            set => SetField(ref currentText, value);
        }

        private PracticeData practiceData;

        private readonly TypingMeasurer typingMeasurer = new TypingMeasurer();

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

        public ObservableCollection<int> TypingSpeedList { get; } = new ObservableCollection<int>();
        public ObservableCollection<int> AccuracyList { get; } = new ObservableCollection<int>();

        private int? currentSentenceIndex;


        private static readonly Differ Differ = new Differ();

        public SentencePracticeWindow(PracticeData practiceData, bool shuffle)
        {
            InitializeComponent();
            Title = VersionInfo.WindowTitle; // <2600912_5-1>

            this.practiceData = practiceData;

            if (shuffle)
            {
                // 메뉴가 들고 있는 연습 데이터(설정 창을 닫을 때까지 같은 개체)를 직접 바꾸지 않고 사본을 섞는다.
                // 직접 바꾸면 '무작위로 섞기'를 끄고 같은 글을 다시 열어도 이미 섞이고 중복이 빠진 채로 남는다.
                var sentenceIndexRandom = new Random();
                this.practiceData = new PracticeData
                {
                    Name = practiceData.Name,
                    Author = practiceData.Author,
                    Character = practiceData.Character,
                    Location = practiceData.Location,
                    TextData = practiceData.TextData.Distinct().OrderBy(s => sentenceIndexRandom.Next()).ToList(),
                };
                // 학습 데이터 무작위로 섞기
            }

            var speedColor = SKColor.Parse("#1c7ed6");
            var accuracyColor = SKColor.Parse("#f03e3e");

            SpeedChart.Series = new ISeries[]
            {
                new LineSeries<int>
                {
                    Values = TypingSpeedList,
                    Name = "타속",
                    Stroke = new SolidColorPaint(speedColor, 2),
                    Fill = new SolidColorPaint(speedColor.WithAlpha(0x26)),
                    GeometryStroke = new SolidColorPaint(speedColor, 2),
                    GeometryFill = new SolidColorPaint(SKColors.White),
                    GeometrySize = 10
                },
                new LineSeries<int>
                {
                    Values = AccuracyList,
                    Name = "정확도",
                    Stroke = new SolidColorPaint(accuracyColor, 2),
                    Fill = null,
                    GeometryStroke = new SolidColorPaint(accuracyColor, 2),
                    GeometryFill = new SolidColorPaint(SKColors.White),
                    GeometrySize = 10,
                    ScalesYAt = 1
                }
            };

            SpeedChart.YAxes = new[]
            {
                new Axis
                {
                    MinLimit = 0,
                    LabelsPaint = new SolidColorPaint(speedColor)
                },
                new Axis
                {
                    MinLimit = 0,
                    MaxLimit = 100,
                    Position = AxisPosition.End,
                    LabelsPaint = new SolidColorPaint(accuracyColor),
                    SeparatorsPaint = null
                }
            };

            SpeedChart.XAxes = new[]
            {
                new Axis { LabelsPaint = null, SeparatorsPaint = null, MinStep = 1 }
            };

            this.Loaded += SentencePracticeWindow_Loaded;
        }

        private void SentencePracticeWindow_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            practiceData = PracticeData.FitPracticeData(practiceData, CurrentTextBlock);
            NextSentence();
        }

        private void SentencePracticeWindow_Closed(object sender, EventArgs e)
        {
            if (TypingSpeedList.Count > 0)
            {
                MainWindow.CurrentKeyLayout.Stats.AddStats(new KeyLayoutStats()
                {
                    SentencePracticeCount = TypingSpeedList.Count,
                    AverageTypingSpeed = TypingMeasurer.RoundToInt(TypingSpeedList.Average()),
                    AverageAccuracy = TypingMeasurer.RoundToInt(AccuracyList.Average())
                });
            }
        }

        private void NextSentence()
        {
            if (!string.IsNullOrEmpty(CurrentTextBox.Text)) // Diff 구하고 하이라이트, 타속, 정확도 계산 : 첫 호출인 경우 수행하지 않음
            {
                PreviousTextBlock.Inlines.Clear();
                var diffs = new List<Differ.DiffData>(
                    Differ.Diff(CurrentTextBox.Text, CurrentText, CurrentTextBox.Text));

                for (int i = 0; i < diffs.Count(); i++)
                {
                    if (diffs[i].State == Differ.DiffData.DiffState.Intermediate)
                    {
                        diffs[i].State = Differ.DiffData.DiffState.Unequal;
                    }
                }

                foreach (var diff in diffs)
                {
                    var run = new Run(diff.Text)
                    {
                        Background = Differ.MapDiffState(diff.State)
                    };
                    PreviousTextBlock.Inlines.Add(run);
                }

                if (typingMeasurer.IsRunning) // 측정이 시작된 입력만 통계에 기록 (시작 안 된 0 표본이 평균을 왜곡하지 않도록)
                {
                    double accuracy = Differ.CalculateAccuracy(diffs);
                    TypingAccuracy = TypingMeasurer.RoundToInt(accuracy * 100);
                    AccuracyList.Add(TypingAccuracy);

                    // <260812_14>(2) 지금 걸린 계산 방식을 따른다(기본은 통일 공식 '정타 수 ÷ 분',
                    // 치트 창에서 '원래 방식'을 고르면 옛 공식). 간단 방식은 맞게 친 부분만 세므로
                    // 정확도를 따로 곱하지 않는다.
                    TypingSpeed = typingMeasurer.FinishSpeed(CurrentTextBox.Text, diffs, accuracy);
                    TypingSpeedList.Add(TypingSpeed);
                    AverageTypingSpeed = TypingMeasurer.RoundToInt(TypingSpeedList.Average());
                }
            }

            if (!string.IsNullOrEmpty(CurrentTextBox.Text) || currentSentenceIndex is null) // 입력이 비어있지 않거나 첫 번째 호출인 경우
            {
                if (currentSentenceIndex is null) currentSentenceIndex = 0; // 첫 호출인 경우
                else if (currentSentenceIndex == practiceData.TextData.Count - 1) currentSentenceIndex = 0; // 마지막 인덱스인 경우, 순환
                else currentSentenceIndex++;

                CurrentText = practiceData.TextData[currentSentenceIndex.Value];
            }
        }

        private void CurrentTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                NextSentence();
                CurrentTextBox.Text = "";
                e.Handled = true;

                return;
            }
            if (CurrentTextBox.Text == "")
            {
                typingMeasurer.Start();
            }
        }

        private void CurrentTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string input = CurrentTextBox.Text;
            var diffs
                = new List<Differ.DiffData>(Differ.Diff(CurrentText.Substring(0, Math.Min(input.Length, CurrentText.Length)),
                                                        CurrentTextBox.Text,
                                                        CurrentText));

            for (int i = 0; i < diffs.Count() - 1; i++)
            {
                if (diffs[i].State == Differ.DiffData.DiffState.Intermediate)
                {
                    diffs[i].State = Differ.DiffData.DiffState.Unequal;
                }
            }
            
            CurrentTextBlock.Inlines.Clear();
            foreach (Differ.DiffData diff in diffs)
            {
                var run = new Run(diff.Text)
                {
                    Background = Differ.MapDiffState(diff.State)
                };
                CurrentTextBlock.Inlines.Add(run);
            }

            if (input.Length < CurrentText.Length)
            {
                CurrentTextBlock.Inlines.Add(new Run(CurrentText.Substring(input.Length)));
            }
        }

        private void CurrentTextBox_PreviewExecuted(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            if (e.Command == ApplicationCommands.Copy ||
                e.Command == ApplicationCommands.Cut ||
                e.Command == ApplicationCommands.Paste)
            {
                e.Handled = true;
            }
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
