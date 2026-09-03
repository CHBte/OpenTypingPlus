using System.Collections.Generic;
using System.Windows.Media;

namespace OpenTyping
{
    /// <summary>
    /// '받아쓰기 칸'(<260812_2>, 명칭 <260812_2-1>(1))에 무엇을 어떤 색으로 보여 줄지 계산한다.
    /// 창(UI)과 떼어 놓아 헤드리스로 검사할 수 있게 했다(-dictationtest).
    ///
    /// 규칙 (<260812_2-1>(3),(4)):
    ///  - 올바로 입력한 만큼은 파랑(28,126,214)으로, 조합되는 대로 보여 준다(ㅌ → 타 → 탈).
    ///  - 틀리게 누른 글자는 그 뒤에 빨강(240,62,62)으로 붙고, 다음에 올바로 누르면 사라진다.
    ///  - 제시어를 통과하면 입력 자리는 곧바로 비고 (<260812_2-3>(2)), 그 제시어는 칸의 1~7번째
    ///    자리로 옮겨 가 초록(55,178,77)으로 남는다 (<260812_2-4>(2)).
    /// </summary>
    internal sealed class DictationState
    {
        public static readonly Brush CorrectBrush = Frozen(0x1C, 0x7E, 0xD6); // (28,126,214) 파랑
        public static readonly Brush WrongBrush = Frozen(0xF0, 0x3E, 0x3E);   // (240,62,62) 빨강
        public static readonly Brush PassedBrush = Frozen(0x37, 0xB2, 0x4D);  // (55,178,77) 초록

        /// <summary>직전에 통과한 제시어(칸의 1~7번째 자리에 초록으로 보인다). 없으면 빈 문자열.</summary>
        public string LastPassed { get; private set; } = "";

        private static Brush Frozen(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        /// <summary>'지금 입력 중' 칸 하나가 담을 수 있는 글자 수(<260812_2> 받아쓰기 칸 설계).</summary>
        private const int CellChars = 7;

        private string prompt = "";
        private int strokes;
        private string wrong = "";

        /// <summary>새 제시어를 받는다(칸을 비운 상태에서 시작).</summary>
        public void Start(string text)
        {
            prompt = text ?? "";
            strokes = 0;
            wrong = "";
        }

        /// <summary>올바로 눌렀다: 틀린 글자를 지우고 한 타 나아간다 (<260812_2-1>(1-3)).</summary>
        public void Correct()
        {
            wrong = "";
            strokes++;
        }

        /// <summary>
        /// 틀리게 눌렀다: 그 글자를 빨강으로 덧붙인다(글자가 없는 키는 무시).
        /// 이 칸은 7글자만 담을 수 있으므로, 이미 맞게 입력한 글자 수를 뺀 나머지만큼만 쌓는다
        /// (넘치면 그 뒤로는 더 쌓지 않는다 — 옆 칸을 침범하거나 칸 밖으로 흘러넘치는 것을 막는다).
        /// </summary>
        public void Wrong(string ch)
        {
            if (string.IsNullOrEmpty(ch)) return;
            int room = CellChars - HangulJamo.TypedPrefix(prompt, strokes).Length - wrong.Length;
            if (room <= 0) return;
            wrong += ch.Length <= room ? ch : ch.Substring(0, room);
        }

        /// <summary>
        /// 제시어를 통과했다: 입력 자리는 곧바로 비우고 (<260812_2-3>(2)), 그 제시어는 1~7번째
        /// 자리로 옮겨 초록으로 남긴다 (<260812_2-4>(2)).
        /// </summary>
        public void Pass()
        {
            string done = prompt;
            Clear();
            LastPassed = done;
        }

        /// <summary>연습이 끝나거나 다시 시작할 때: 칸을 완전히 비운다.</summary>
        public void Clear()
        {
            prompt = "";
            strokes = 0;
            wrong = "";
            LastPassed = "";
        }

        /// <summary>지금 칸에 그릴 (글자, 색) 조각들. 비어 있으면 빈 목록.</summary>
        public List<(string Text, Brush Color)> Segments()
        {
            var list = new List<(string, Brush)>();
            string done = HangulJamo.TypedPrefix(prompt, strokes);
            if (done.Length > 0) list.Add((done, CorrectBrush));
            if (wrong.Length > 0) list.Add((wrong, WrongBrush));
            return list;
        }

        /// <summary>검사·진단용 요약: "초록글자/파랑글자|빨강글자".</summary>
        public string Describe() =>
            LastPassed + "/" + HangulJamo.TypedPrefix(prompt, strokes) + "|" + wrong;
    }
}
