using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace OpenTyping
{
    public class TypingMeasurer
    {
        private readonly Stopwatch stopwatch = new Stopwatch();

        public bool IsRunning => stopwatch.IsRunning; // 측정이 시작된 상태인지 (시작 안 된 입력은 통계에서 제외하는 용도)

        private static bool IsHangulSyllable(char ch)
            => Hangul.IsSyllable(ch);

        /// <summary>
        /// 이 파일이 통일해 쓰는 반올림 규칙(AwayFromZero, 은행원 반올림 금지)을 딱 한 곳에 모은다.
        /// 반드시 Convert.ToInt32로 감싼다 — bare (int) 캐스트는 unchecked라 값이 Int32 범위를
        /// 벗어나거나 NaN이면 예외 없이 조용히 쓰레기 값(예: int.MinValue)을 반환하기 때문이다.
        /// Convert.ToInt32(double)는 checked라 그런 입력에서 여전히 OverflowException으로
        /// 크게 실패한다 — 이미 정수로 반올림된 값을 넘기므로 Convert.ToInt32 자신의 반올림
        /// 방식(은행원 반올림)은 개입하지 않는다.
        /// </summary>
        public static int RoundToInt(double value) => Convert.ToInt32(Math.Round(value, MidpointRounding.AwayFromZero));

        /// <summary>
        /// '원래 방식'의 글자수 환산: 한글 음절 하나를 2.5타로, 그 밖의 글자를 1타로 센다.
        /// 자리연습도 이 방식을 고를 수 있으므로(<260812_12>) 한 곳에 두고 함께 쓴다.
        /// </summary>
        public static int CountLetter(string text)
        {
            double count = 0;

            foreach (char ch in text)
            {
                if (IsHangulSyllable(ch)) count += 2.5;
                else count++;
            }

            // 한글 음절 수가 홀수일 때 정확히 .5(2.5, 7.5, 12.5, ...)가 나올 수 있어
            // RoundToInt(AwayFromZero)로 반올림한다(은행원 반올림 금지).
            return RoundToInt(count);
        }

        /// <summary>
        /// <260812_14>(2) 통일된 계산: 분당 타 = 올바르게 누른 키 수 ÷ 경과 시간(분), 반올림.
        /// '올바르게 누른 키 수'는 맞게 입력된 부분을 두벌식으로 완성하는 데 필요한 타건 수다
        /// (한글은 자소 단위 — '닭'=4, 그 밖의 글자·공백은 1). 자리연습의 타 정의(<260723_3-1>)와 같다.
        /// </summary>
        public static int CountStrokes(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            int strokes = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                // 서로게이트 쌍(이모지 등, BMP 밖 문자)은 char 두 개가 한 글자다 — 따로 세면 실제로
                // 입력할 수 없는 문자 하나가 2타로 부풀려진다. 문자 하나로 보고 1타로 센다.
                if (char.IsHighSurrogate(ch) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    i++;
                    strokes += 1;
                    continue;
                }
                int n = HangulJamo.DecomposeChar(ch).Count;
                strokes += n > 0 ? n : 1;   // 자판에 매핑이 없는 글자(영문 등)는 1타로 본다
            }
            return strokes;
        }

        // ===== 계산 방식 (<260812_12>, <260812_14>) =====
        // 프로그램 전체가 같은 방식을 쓴다. 기본은 통일 공식인 '간단 방식'이고, 치트 창에서 '원래 방식'을
        // 고르면 자리연습·문장연습·긴글연습이 **모두** 옛 공식으로 계산한다.

        public const string MethodSimple = "simple";     // 정타 수 ÷ 분
        public const string MethodOriginal = "original"; // (글자수 환산 ÷ 분) × 정확도

        public static string CurrentMethod =>
            UserSettingsStore.TpmMethod == MethodOriginal ? MethodOriginal : MethodSimple;

        public static bool IsOriginalMethod => CurrentMethod == MethodOriginal;

        /// <summary>
        /// 문장·긴글연습의 한 문장 결과. 지금 걸린 계산 방식을 따른다.
        ///  - 간단 방식: 맞게 친 부분의 타건 수 ÷ 분
        ///  - 원래 방식: (글자수 환산 ÷ 분) × 정확도
        /// </summary>
        /// <summary>정확도(%): 맞은 만큼 ÷ 전체 시도. 아직 아무것도 시도하지 않았으면 0.
        /// 자리연습·음절연습이 같은 식을 따로 들고 있던 것을 한 곳으로 모았다.</summary>
        public static int Accuracy(int correct, int wrong)
        {
            int tried = correct + wrong;
            return tried == 0 ? 0 : RoundToInt(correct * 100.0 / tried);
        }

        /// <summary>경과 시간(분)을 읽고 시계를 리셋한다. 0 이하(측정 시작 전 입력 등 비정상 상황)면 null.</summary>
        private double? TakeElapsedMinutes()
        {
            double elapsed = stopwatch.Elapsed.TotalMinutes;
            stopwatch.Reset();
            return elapsed > 0 ? elapsed : (double?)null;
        }

        internal int FinishSpeed(string text, IEnumerable<Differ.DiffData> diffs, double accuracy)
        {
            // CountLetter와 같은 이유로 RoundToInt(AwayFromZero)로 맞춘다(은행원 반올림 금지).
            if (IsOriginalMethod) return RoundToInt(Finish(text) * accuracy);

            double? elapsed = TakeElapsedMinutes();
            if (elapsed == null) return 0;

            int strokes = 0;
            foreach (Differ.DiffData d in diffs)
                if (d.State == Differ.DiffData.DiffState.Equal) strokes += CountStrokes(d.Text);

            return RoundToInt(strokes / elapsed.Value);
        }

        /// <summary>
        /// 음절연습처럼 '맞게 친 글자'가 곧 결과인 경우의 분당 타 (<260812_20>).
        /// 지금 걸린 계산 방식을 따른다(간단 = 타건 수, 원래 = 글자수 환산).
        /// </summary>
        public int FinishTpmForText(string text)
        {
            double? elapsed = TakeElapsedMinutes();
            if (elapsed == null) return 0;

            double count = IsOriginalMethod ? CountLetter(text) : CountStrokes(text);
            return RoundToInt(count / elapsed.Value);
        }

        public void Start() => stopwatch.Restart();
        public double Finish(string text)
        {
            int count = CountLetter(text);
            double? elapsed = TakeElapsedMinutes();
            return elapsed == null ? 0 : count / elapsed.Value;
        }
    }
}
