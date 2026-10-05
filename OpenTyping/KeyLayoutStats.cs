using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OpenTyping
{
    public class KeyLayoutStats : INotifyPropertyChanged
    {
        public Dictionary<KeyPos, int> KeyIncorrectCount { get; set; } = new Dictionary<KeyPos, int>();

        private KeyValuePair<KeyPos, int> mostIncorrect;
        public KeyValuePair<KeyPos, int> MostIncorrect
        {
            get => mostIncorrect;
            set => SetField(ref mostIncorrect, value);
        }

        private int sentencePracticeCount;
        public int SentencePracticeCount
        {
            get => sentencePracticeCount;
            set => SetField(ref sentencePracticeCount, value);
        }

        private int averageTypingSpeed;
        public int AverageTypingSpeed
        {
            get => averageTypingSpeed;
            set => SetField(ref averageTypingSpeed, value);
        }

        private int averageAccuracy;

        public int AverageAccuracy
        {
            get => averageAccuracy;
            set => SetField(ref averageAccuracy, value);
        }

        // lhs와 rhs를 합친다. 중복된 key가 있을 경우 두 value를 mergeFunc에 넣어 나온 값을 value로 이용한다.
        private static Dictionary<TK, TV> MergeBy<TK, TV>(IReadOnlyDictionary<TK, TV> lhs, IReadOnlyDictionary<TK, TV> rhs, Func<TV, TV, TV> mergeFunc)
        { 
            Dictionary<TK, TV> result = lhs.ToDictionary(kv => kv.Key, kv => kv.Value);

            foreach (KeyValuePair<TK, TV> kv in rhs)
            {
                result[kv.Key] = result.ContainsKey(kv.Key) ? mergeFunc(lhs[kv.Key], rhs[kv.Key]) : kv.Value;
            }

            return result;
        }

        // KeyIncorrectCount로부터 가장 많이 틀린 키를 다시 계산한다.
        // 비어 있으면(오타 없이 연습을 마친 경우 등) 기본값으로 둔다 — 빈 딕셔너리에 Max()를 호출하면 예외가 발생한다.
        public void RecomputeMostIncorrect()
        {
            if (KeyIncorrectCount.Count == 0)
            {
                MostIncorrect = default;
                return;
            }

            int maxCount = KeyIncorrectCount.Values.Max();
            MostIncorrect = KeyIncorrectCount.First(kv => kv.Value == maxCount);
        }

        public void AddStats(KeyLayoutStats other)
        {
            if (other.KeyIncorrectCount != null)
            {
                // 저장 파일에서 읽은 값이 int 끝이어도 넘쳐서 음수가 되지 않게(포화 덧셈).
                int AddInt(int lhs, int rhs) => (int)Math.Min((long)lhs + rhs, int.MaxValue);

                KeyIncorrectCount = MergeBy(KeyIncorrectCount, other.KeyIncorrectCount, AddInt);
                RecomputeMostIncorrect();
            }

            if (other.SentencePracticeCount > 0)
            {
                // double 로 더한다 — 저장 파일에서 읽은 값이 크거나 음수여도 int 곱셈이 넘쳐 합이 0 이 되고
                // (0 으로 나누어 NaN → RoundToInt 예외) 종료 처리에서 예외가 나는 일이 없게.
                double newSpeedSum = ((double)AverageTypingSpeed * SentencePracticeCount) +
                                     ((double)other.AverageTypingSpeed * other.SentencePracticeCount);
                double newAccuracySum = ((double)AverageAccuracy * SentencePracticeCount) +
                                        ((double)other.AverageAccuracy * other.SentencePracticeCount);

                // 횟수도 int 끝에서 넘쳐 음수가 되지 않게 포화시킨다(그 값으로 나누므로 음수·0 이면 평균이 깨진다).
                long newCount = Math.Min((long)SentencePracticeCount + other.SentencePracticeCount, int.MaxValue);
                SentencePracticeCount = (int)newCount;

                // 정수 나눗셈(자름)이 아니라 이 프로젝트의 다른 평균 계산과 같은 AwayFromZero
                // 반올림으로 맞춘다 — 안 그러면 예: 합 21 ÷ 2 가 반올림 시 11이어야 할 값이
                // 자름으로 10이 되어, 방금 통일한 반올림 규칙과 다시 어긋난다.
                AverageTypingSpeed = TypingMeasurer.RoundToInt(newSpeedSum / newCount);
                AverageAccuracy = TypingMeasurer.RoundToInt(newAccuracySum / newCount);
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
