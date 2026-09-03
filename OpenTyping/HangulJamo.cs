using System.Collections.Generic;

namespace OpenTyping
{
    /// <summary>
    /// 완성형 한글 음절(및 낱자·특수기호)을 '두벌식 표준' 자판에서 실제로 눌러야 하는
    /// 키 입력 시퀀스((KeyPos, 윗글쇠여부))로 분해한다. 순3('한 글자 조합')·순4('2음절 이상 단어')
    /// 를 렌더링 키보드에 자모 단위로 순차 하이라이트할 때 쓴다 (<260723_4>, <260723_2>).
    /// 겹자모(ㅘ·ㄳ 등)는 두 번의 입력으로 분해한다.
    /// </summary>
    public static class HangulJamo
    {
        public struct Stroke
        {
            public Stroke(KeyPos pos, bool isShift) { Pos = pos; IsShift = isShift; }
            public KeyPos Pos { get; }
            public bool IsShift { get; }
        }

        private const int SBase = 0xAC00;   // '가'
        private const int LCount = 19, VCount = 21, TCount = 28;

        // 초성 19 (ㄱ..ㅎ)
        private static readonly char[] LeadJamo =
            { 'ㄱ', 'ㄲ', 'ㄴ', 'ㄷ', 'ㄸ', 'ㄹ', 'ㅁ', 'ㅂ', 'ㅃ', 'ㅅ', 'ㅆ', 'ㅇ', 'ㅈ', 'ㅉ', 'ㅊ', 'ㅋ', 'ㅌ', 'ㅍ', 'ㅎ' };
        // 중성 21 (ㅏ..ㅣ)
        private static readonly char[] VowelJamo =
            { 'ㅏ', 'ㅐ', 'ㅑ', 'ㅒ', 'ㅓ', 'ㅔ', 'ㅕ', 'ㅖ', 'ㅗ', 'ㅘ', 'ㅙ', 'ㅚ', 'ㅛ', 'ㅜ', 'ㅝ', 'ㅞ', 'ㅟ', 'ㅠ', 'ㅡ', 'ㅢ', 'ㅣ' };
        // 종성 28 (첫 항목은 받침 없음)
        private static readonly char[] TailJamo =
            { '\0', 'ㄱ', 'ㄲ', 'ㄳ', 'ㄴ', 'ㄵ', 'ㄶ', 'ㄷ', 'ㄹ', 'ㄺ', 'ㄻ', 'ㄼ', 'ㄽ', 'ㄾ', 'ㄿ', 'ㅀ', 'ㅁ', 'ㅂ', 'ㅄ', 'ㅅ', 'ㅆ', 'ㅇ', 'ㅈ', 'ㅊ', 'ㅋ', 'ㅌ', 'ㅍ', 'ㅎ' };

        // 낱자(자모/겹자모) → 두벌식 키 입력들. 겹자모는 2타.
        // KeyPos(row, col), 윗글쇠 여부.
        private static readonly Dictionary<char, Stroke[]> JamoToStrokes = BuildJamoMap();

        private static Dictionary<char, Stroke[]> BuildJamoMap()
        {
            Stroke S(int r, int c, bool sh = false) => new Stroke(new KeyPos(r, c), sh);
            var m = new Dictionary<char, Stroke[]>();

            // 자음(윗줄/홈/아랫줄) 기본
            m['ㅂ'] = new[] { S(1, 0) };  m['ㅈ'] = new[] { S(1, 1) };  m['ㄷ'] = new[] { S(1, 2) };
            m['ㄱ'] = new[] { S(1, 3) };  m['ㅅ'] = new[] { S(1, 4) };
            m['ㅁ'] = new[] { S(2, 0) };  m['ㄴ'] = new[] { S(2, 1) };  m['ㅇ'] = new[] { S(2, 2) };
            m['ㄹ'] = new[] { S(2, 3) };  m['ㅎ'] = new[] { S(2, 4) };
            m['ㅋ'] = new[] { S(3, 0) };  m['ㅌ'] = new[] { S(3, 1) };  m['ㅊ'] = new[] { S(3, 2) };
            m['ㅍ'] = new[] { S(3, 3) };
            // 쌍자음(윗글쇠)
            m['ㅃ'] = new[] { S(1, 0, true) }; m['ㅉ'] = new[] { S(1, 1, true) }; m['ㄸ'] = new[] { S(1, 2, true) };
            m['ㄲ'] = new[] { S(1, 3, true) }; m['ㅆ'] = new[] { S(1, 4, true) };

            // 모음 기본
            m['ㅛ'] = new[] { S(1, 5) };  m['ㅕ'] = new[] { S(1, 6) };  m['ㅑ'] = new[] { S(1, 7) };
            m['ㅐ'] = new[] { S(1, 8) };  m['ㅔ'] = new[] { S(1, 9) };
            m['ㅗ'] = new[] { S(2, 5) };  m['ㅓ'] = new[] { S(2, 6) };  m['ㅏ'] = new[] { S(2, 7) };
            m['ㅣ'] = new[] { S(2, 8) };
            m['ㅠ'] = new[] { S(3, 4) };  m['ㅜ'] = new[] { S(3, 5) };  m['ㅡ'] = new[] { S(3, 6) };
            // 모음 윗글쇠
            m['ㅒ'] = new[] { S(1, 8, true) }; m['ㅖ'] = new[] { S(1, 9, true) };
            // 겹모음(2타)
            m['ㅘ'] = new[] { S(2, 5), S(2, 7) };            // ㅗ+ㅏ
            m['ㅙ'] = new[] { S(2, 5), S(1, 8) };            // ㅗ+ㅐ
            m['ㅚ'] = new[] { S(2, 5), S(2, 8) };            // ㅗ+ㅣ
            m['ㅝ'] = new[] { S(3, 5), S(2, 6) };            // ㅜ+ㅓ
            m['ㅞ'] = new[] { S(3, 5), S(1, 9) };            // ㅜ+ㅔ
            m['ㅟ'] = new[] { S(3, 5), S(2, 8) };            // ㅜ+ㅣ
            m['ㅢ'] = new[] { S(3, 6), S(2, 8) };            // ㅡ+ㅣ
            // 겹받침(2타) — 앞 자모 + 뒤 자모
            m['ㄳ'] = new[] { S(1, 3), S(1, 4) };            // ㄱ+ㅅ
            m['ㄵ'] = new[] { S(2, 1), S(1, 1) };            // ㄴ+ㅈ
            m['ㄶ'] = new[] { S(2, 1), S(2, 4) };            // ㄴ+ㅎ
            m['ㄺ'] = new[] { S(2, 3), S(1, 3) };            // ㄹ+ㄱ
            m['ㄻ'] = new[] { S(2, 3), S(2, 0) };            // ㄹ+ㅁ
            m['ㄼ'] = new[] { S(2, 3), S(1, 0) };            // ㄹ+ㅂ
            m['ㄽ'] = new[] { S(2, 3), S(1, 4) };            // ㄹ+ㅅ
            m['ㄾ'] = new[] { S(2, 3), S(3, 1) };            // ㄹ+ㅌ
            m['ㄿ'] = new[] { S(2, 3), S(3, 3) };            // ㄹ+ㅍ
            m['ㅀ'] = new[] { S(2, 3), S(2, 4) };            // ㄹ+ㅎ
            m['ㅄ'] = new[] { S(1, 0), S(1, 4) };            // ㅂ+ㅅ

            // 특수기호(두벌식 표준 격자). 윗글쇠 기호 포함.
            m[';'] = new[] { S(2, 9) };  m[':'] = new[] { S(2, 9, true) };
            m[','] = new[] { S(3, 7) };  m['<'] = new[] { S(3, 7, true) };
            m['.'] = new[] { S(3, 8) };  m['>'] = new[] { S(3, 8, true) };
            m['/'] = new[] { S(3, 9) };  m['?'] = new[] { S(3, 9, true) };
            m['\''] = new[] { S(2, 10) }; m['"'] = new[] { S(2, 10, true) };
            m['['] = new[] { S(1, 10) };  m['{'] = new[] { S(1, 10, true) };
            m[']'] = new[] { S(1, 11) };  m['}'] = new[] { S(1, 11, true) };
            m['`'] = new[] { S(0, 0) };   m['~'] = new[] { S(0, 0, true) };
            m['1'] = new[] { S(0, 1) };   m['!'] = new[] { S(0, 1, true) };
            m['2'] = new[] { S(0, 2) };   m['@'] = new[] { S(0, 2, true) };
            m['3'] = new[] { S(0, 3) };   m['#'] = new[] { S(0, 3, true) };
            m['4'] = new[] { S(0, 4) };   m['$'] = new[] { S(0, 4, true) };
            m['5'] = new[] { S(0, 5) };   m['%'] = new[] { S(0, 5, true) };
            m['6'] = new[] { S(0, 6) };   m['^'] = new[] { S(0, 6, true) };
            m['7'] = new[] { S(0, 7) };   m['&'] = new[] { S(0, 7, true) };
            m['8'] = new[] { S(0, 8) };   m['*'] = new[] { S(0, 8, true) };
            m['9'] = new[] { S(0, 9) };   m['('] = new[] { S(0, 9, true) };
            m['0'] = new[] { S(0, 10) };  m[')'] = new[] { S(0, 10, true) };
            m['-'] = new[] { S(0, 11) };  m['_'] = new[] { S(0, 11, true) };
            m['='] = new[] { S(0, 12) };  m['+'] = new[] { S(0, 12, true) };

            return m;
        }

        /// <summary>한 문자(완성형 음절/낱자/기호)를 키 입력 시퀀스로 분해한다. 매핑 없는 문자는 빈 목록.</summary>
        public static List<Stroke> DecomposeChar(char ch)
        {
            var result = new List<Stroke>();

            if (ch >= SBase && ch < SBase + LCount * VCount * TCount)
            {
                int sIndex = ch - SBase;
                int lead = sIndex / (VCount * TCount);
                int vowel = (sIndex % (VCount * TCount)) / TCount;
                int tail = sIndex % TCount;

                AppendJamo(result, LeadJamo[lead]);
                AppendJamo(result, VowelJamo[vowel]);
                if (tail != 0) AppendJamo(result, TailJamo[tail]);
                return result;
            }

            AppendJamo(result, ch);
            return result;
        }

        private static void AppendJamo(List<Stroke> list, char jamo)
        {
            if (JamoToStrokes.TryGetValue(jamo, out Stroke[] strokes))
                list.AddRange(strokes);
        }

        /// <summary>문자열(음절 조합/단어) 전체를 키 입력 시퀀스로 분해한다.</summary>
        public static List<Stroke> Decompose(string text)
        {
            var result = new List<Stroke>();
            if (string.IsNullOrEmpty(text)) return result;
            foreach (char ch in text)
                result.AddRange(DecomposeChar(ch));
            return result;
        }

        /// <summary>이 문자가 두벌식 표준으로 입력 가능한지(모든 자모가 매핑되는지).</summary>
        public static bool CanType(char ch) => DecomposeChar(ch).Count > 0;

        /// <summary>한 문자가 '키 하나'로 입력되는 낱자/기호이면 그 키 위치·윗글쇠를 돌려준다
        /// (자모→KeyPos 역해석; 단계 정의 JSON에서 키를 문자로 적을 때 쓴다, <260724_1>(3)).
        /// 겹자모(ㅘ 등)나 완성형 음절처럼 2타 이상이면 false.</summary>
        public static bool TryKey(char ch, out KeyPos pos, out bool isShift)
        {
            List<Stroke> strokes = DecomposeChar(ch);
            if (strokes.Count == 1)
            {
                pos = strokes[0].Pos;
                isShift = strokes[0].IsShift;
                return true;
            }
            pos = null;
            isShift = false;
            return false;
        }

        // 겹모음·겹받침을 기본 자모로 쪼갠다(자리연습 순3·순4의 '알파벳 구성' 판정용).
        // 쌍자음(ㄲㄸㅃㅆㅉ)은 그 자체가 한 개의 알파벳이므로 쪼개지 않는다.
        private static readonly Dictionary<char, char[]> CompoundJamo = new Dictionary<char, char[]>
        {
            ['ㅘ'] = new[] { 'ㅗ', 'ㅏ' }, ['ㅙ'] = new[] { 'ㅗ', 'ㅐ' }, ['ㅚ'] = new[] { 'ㅗ', 'ㅣ' },
            ['ㅝ'] = new[] { 'ㅜ', 'ㅓ' }, ['ㅞ'] = new[] { 'ㅜ', 'ㅔ' }, ['ㅟ'] = new[] { 'ㅜ', 'ㅣ' },
            ['ㅢ'] = new[] { 'ㅡ', 'ㅣ' },
            ['ㄳ'] = new[] { 'ㄱ', 'ㅅ' }, ['ㄵ'] = new[] { 'ㄴ', 'ㅈ' }, ['ㄶ'] = new[] { 'ㄴ', 'ㅎ' },
            ['ㄺ'] = new[] { 'ㄹ', 'ㄱ' }, ['ㄻ'] = new[] { 'ㄹ', 'ㅁ' }, ['ㄼ'] = new[] { 'ㄹ', 'ㅂ' },
            ['ㄽ'] = new[] { 'ㄹ', 'ㅅ' }, ['ㄾ'] = new[] { 'ㄹ', 'ㅌ' }, ['ㄿ'] = new[] { 'ㄹ', 'ㅍ' },
            ['ㅀ'] = new[] { 'ㄹ', 'ㅎ' }, ['ㅄ'] = new[] { 'ㅂ', 'ㅅ' },
        };

        private static void AddBaseJamo(HashSet<char> set, char jamo)
        {
            if (CompoundJamo.TryGetValue(jamo, out char[] parts))
                foreach (char p in parts) set.Add(p);
            else
                set.Add(jamo);
        }

        /// <summary>한 문자(완성형 음절/낱자)를 이루는 기본 자모(겹자모는 분해)의 집합.</summary>
        public static HashSet<char> BaseJamoOf(char ch)
        {
            var set = new HashSet<char>();
            if (ch >= SBase && ch < SBase + LCount * VCount * TCount)
            {
                int sIndex = ch - SBase;
                AddBaseJamo(set, LeadJamo[sIndex / (VCount * TCount)]);
                AddBaseJamo(set, VowelJamo[(sIndex % (VCount * TCount)) / TCount]);
                int tail = sIndex % TCount;
                if (tail != 0) AddBaseJamo(set, TailJamo[tail]);
            }
            else
            {
                AddBaseJamo(set, ch);
            }
            return set;
        }

        /// <summary>문자열(음절 조합/단어)을 이루는 기본 자모 전체의 집합.</summary>
        public static HashSet<char> AllBaseJamo(string text)
        {
            var set = new HashSet<char>();
            if (string.IsNullOrEmpty(text)) return set;
            foreach (char ch in text)
                foreach (char j in BaseJamoOf(ch)) set.Add(j);
            return set;
        }

        // ===== 입력 진행 표시 (<260812_2>) =====

        // 한 타로 입력되는 낱자 → 그 낱자. 겹자모의 첫 타가 어떤 낱자인지 되짚는 데 쓴다.
        private static readonly Dictionary<(int Row, int Col, bool Shift), char> SingleStrokeToJamo = BuildReverseMap();

        private static Dictionary<(int, int, bool), char> BuildReverseMap()
        {
            var m = new Dictionary<(int, int, bool), char>();
            foreach (KeyValuePair<char, Stroke[]> kv in JamoToStrokes)
                if (kv.Value.Length == 1)
                {
                    var k = (kv.Value[0].Pos.Row, kv.Value[0].Pos.Column, kv.Value[0].IsShift);
                    if (!m.ContainsKey(k)) m[k] = kv.Key;
                }
            return m;
        }

        private static int StrokeCount(char jamo) =>
            JamoToStrokes.TryGetValue(jamo, out Stroke[] s) ? s.Length : 0;

        /// <summary>겹자모의 첫 타에 해당하는 낱자(ㅘ→ㅗ, ㄳ→ㄱ). 없으면 '\0'.</summary>
        private static char FirstComponent(char jamo) =>
            JamoToStrokes.TryGetValue(jamo, out Stroke[] s) && s.Length > 0
            && SingleStrokeToJamo.TryGetValue((s[0].Pos.Row, s[0].Pos.Column, s[0].IsShift), out char c)
                ? c : '\0';

        private static char Compose(int lead, int vowel, int tail) =>
            (char)(SBase + (lead * VCount + vowel) * TCount + tail);

        /// <summary>
        /// <paramref name="text"/>를 처음부터 <paramref name="strokes"/>타 만큼 정확히 쳤을 때
        /// 화면에 보일 문자열 (<260812_2> 자리연습 입력 진행 표시). 다 친 글자는 완성형으로, 치는
        /// 중인 글자는 조합되는 대로 보여 준다 — 예를 들어 "탈"은 ㅌ → 타 → 탈 순으로 나타난다.
        /// 오타는 타로 세지 않으므로 이 함수에 들어오지 않는다.
        /// </summary>
        public static string TypedPrefix(string text, int strokes)
        {
            if (string.IsNullOrEmpty(text) || strokes <= 0) return "";

            var sb = new System.Text.StringBuilder();
            foreach (char ch in text)
            {
                int need = DecomposeChar(ch).Count;
                if (need == 0) continue;                 // 입력할 수 없는 글자(매핑 없음)
                if (strokes >= need) { sb.Append(ch); strokes -= need; continue; }
                sb.Append(PartialChar(ch, strokes));
                break;
            }
            return sb.ToString();
        }

        /// <summary>글자 하나를 <paramref name="done"/>타 만큼만 친 중간 모양(0 &lt; done &lt; 전체 타수).</summary>
        private static string PartialChar(char ch, int done)
        {
            if (done <= 0) return "";

            if (ch < SBase || ch >= SBase + LCount * VCount * TCount)
            {
                // 완성형 음절이 아닌 낱자·기호: 겹자모라면 첫 낱자만 친 상태.
                char first = FirstComponent(ch);
                return first == '\0' ? "" : first.ToString();
            }

            int sIndex = ch - SBase;
            int lead = sIndex / (VCount * TCount);
            int vowel = (sIndex % (VCount * TCount)) / TCount;
            int tail = sIndex % TCount;

            int rest = done - StrokeCount(LeadJamo[lead]);   // 초성은 항상 1타
            if (rest < 0) return "";
            if (rest == 0) return LeadJamo[lead].ToString(); // 초성만: ㅌ

            int vowelStrokes = StrokeCount(VowelJamo[vowel]);
            if (rest < vowelStrokes)                          // 겹모음의 앞 모음만: 화 → 호
            {
                int half = System.Array.IndexOf(VowelJamo, FirstComponent(VowelJamo[vowel]));
                return half < 0 ? LeadJamo[lead].ToString() : Compose(lead, half, 0).ToString();
            }

            rest -= vowelStrokes;
            if (rest == 0 || tail == 0) return Compose(lead, vowel, 0).ToString();   // 받침 전: 타

            int half2 = System.Array.IndexOf(TailJamo, FirstComponent(TailJamo[tail]));  // 겹받침 앞 자음: 닭 → 달
            return Compose(lead, vowel, half2 < 0 ? 0 : half2).ToString();
        }

        /// <summary>완성형 한글 음절 개수(자리연습 '타' 계산용: '나라'=2). 음절이 하나도 없으면 1(단독 자모·기호).</summary>
        public static int SyllableCount(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int n = 0;
            foreach (char ch in text)
                if (ch >= SBase && ch < SBase + LCount * VCount * TCount) n++;
            return n == 0 ? 1 : n;
        }
    }
}
