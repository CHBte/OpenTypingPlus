using System;
using System.Diagnostics;

namespace OpenTyping
{
    public class TypingMeasurer
    {
        private readonly Stopwatch stopwatch = new Stopwatch();

        public bool IsRunning => stopwatch.IsRunning; // 측정이 시작된 상태인지 (시작 안 된 입력은 통계에서 제외하는 용도)

        private static bool IsHangulSyllable(char ch)
            => Hangul.IsSyllable(ch);

        private static int CountLetter(string text)
        {
            double count = 0;

            foreach (char ch in text)
            {
                if (IsHangulSyllable(ch)) count += 2.5;
                else count++;
            }

            return Convert.ToInt32(count);
        }

        public void Start() => stopwatch.Restart();
        public double Finish(string text)
        {
            double elapsed = stopwatch.Elapsed.TotalMinutes;
            int count = CountLetter(text);

            stopwatch.Reset();

            if (elapsed <= 0) return 0; // 측정 시작 전 입력 등 비정상 상황에서 0으로 나누기 방지

            return count / elapsed;
        }
    }
}
