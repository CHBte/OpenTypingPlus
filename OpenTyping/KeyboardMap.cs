using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTyping
{
    /// <summary>
    /// 자판 하나의 '글자 → 키 입력' 규칙. 코드에 자판별 표를 적어 두지 않고 자판 파일(layouts\*.json)의
    /// 키 글자(KeyData / ShiftKeyData)에서 자동으로 만든다 — 그래서 두벌식·세벌식·QWERTY·Dvorak 등 어느
    /// 자판이든 단계 정의 파일만 더하면 자리연습 단계와 오락을 붙일 수 있다.
    ///
    /// 한글 음절은 자판 파일의 표시로 규칙을 정한다:
    ///  - 초성: 표시 없는 자음 키. 쌍초성(ㄲ 등) 키가 없으면 같은 자음 키를 두 번(세벌식).
    ///  - 중성: 그 모음 키. 없으면 겹모음을 쪼개어 앞 모음은 "(이중 모음)" 키(있으면), 뒤 모음은 보통 키.
    ///  - 종성: "(받침)" 표시 키가 있는 자판(세벌식)은 "X (받침)" 키, 없으면 겹받침·쌍받침을 받침 키 둘로.
    ///    표시가 없는 자판(두벌식)은 보통 자음 키, 없으면 겹받침을 쪼갠 두 자음 키.
    /// 그 밖의 글자(영문·숫자·기호, 낱자)는 그 글자를 내는 키를 찾는다(대문자는 윗글쇠 쪽).
    ///
    /// '알파벳'(단계 정의의 수준별 알파벳·학습 목표 알파벳이 가리키는 단위)은 키 이름이다: 그 키가 내는
    /// 글자 그대로("ㅃ", "ㄱ (받침)")이되, 대소문자 짝인 영문 글자는 소문자("D"도 "d")로 친다.
    /// </summary>
    public sealed class KeyboardMap
    {
        public const string FinalTag = "(받침)";
        public const string CombiningTag = "(이중 모음)";

        private readonly Dictionary<string, HangulJamo.Stroke> byLabel = new Dictionary<string, HangulJamo.Stroke>();
        private readonly Dictionary<(int, int, bool), string> alphabetOf = new Dictionary<(int, int, bool), string>();
        private readonly HashSet<(int, int)> caseLetterKeys = new HashSet<(int, int)>();
        private readonly bool hasFinalKeys;

        /// <summary>이 규칙을 만든 자판 이름.</summary>
        public string LayoutName { get; }

        /// <summary>대소문자가 있는 글자 키가 있는가(Caps Lock 처리 대상인가).</summary>
        public bool HasCaseLetters => caseLetterKeys.Count > 0;

        /// <summary>한글 음절을 칠 수 있는 자판인가.</summary>
        public bool CanTypeHangul { get; }

        private KeyboardMap(string layoutName, IEnumerable<(KeyPos Pos, string Normal, string Shift)> keys)
        {
            LayoutName = layoutName;
            foreach ((KeyPos pos, string normal, string shift) in keys)
            {
                AddKey(pos, false, normal, normal);
                bool casePair = IsCasePair(normal, shift);
                AddKey(pos, true, shift, casePair ? normal : shift);
                if (casePair) caseLetterKeys.Add((pos.Row, pos.Column));
            }
            hasFinalKeys = byLabel.Keys.Any(k => k.EndsWith(FinalTag, StringComparison.Ordinal));
            CanTypeHangul = DecomposeChar('가').Count > 0;
        }

        private void AddKey(KeyPos pos, bool shift, string label, string alphabet)
        {
            label = Normalize(label);
            if (string.IsNullOrEmpty(label)) return;
            var stroke = new HangulJamo.Stroke(pos, shift);
            if (!byLabel.ContainsKey(label)) byLabel[label] = stroke;   // 같은 글자를 내는 키가 둘이면 앞의 것
            alphabetOf[(pos.Row, pos.Column, shift)] = Normalize(alphabet);
        }

        // 대소문자 짝(영문 'd'/'D')만 같은 알파벳으로 친다 — 영문에서는 [Shift]·[Caps Lock]으로 대문자를 내는 법을
        // 사용자가 따로 배울 수 있다고 가정하기 때문이다(썰렁이 설명, 2026-10-02). 한글 자판의 윗글쇠(ㅂ→ㅃ 등)는
        // 소/대문자 변환이 아니라 아예 다른 자소라 짝이 아니다 — 별개의 알파벳으로 남아, 그 자소를 가르치는
        // 단계의 알파벳에 들어 있을 때만 제시어에 쓰인다.
        private static bool IsCasePair(string normal, string shift) =>
            normal != null && shift != null && normal.Length == 1 && shift.Length == 1
            && char.IsLetter(normal[0]) && normal != shift
            && string.Equals(normal, shift, StringComparison.OrdinalIgnoreCase);

        /// <summary>키 이름 정리: 앞뒤 공백을 없애고 "ㄱ(받침)"·"ㄱ  (받침)"을 "ㄱ (받침)"으로.</summary>
        public static string Normalize(string label)
        {
            if (label == null) return null;
            string s = label.Trim();
            int i = s.IndexOf('(');
            return i > 0 ? s.Substring(0, i).TrimEnd() + " " + s.Substring(i) : s;
        }

        // ===== 만들기 =====

        public static KeyboardMap FromLayout(KeyLayout layout)
        {
            var keys = new List<(KeyPos, string, string)>();
            if (layout?.KeyLayoutData != null)
                for (int r = 0; r < layout.KeyLayoutData.Count; r++)
                {
                    IList<Key> row = layout.KeyLayoutData[r];
                    if (row == null) continue;
                    for (int c = 0; c < row.Count; c++)
                        if (row[c] != null) keys.Add((new KeyPos(r, c), row[c].KeyData, row[c].ShiftKeyData));
                }
            return new KeyboardMap(layout?.Name, keys);
        }

        /// <summary>자판 파일을 못 찾을 때의 예비: 코드에 있는 두벌식 표준 + QWERTY 글자 자리(<see cref="HangulJamo"/>).</summary>
        internal static KeyboardMap BuiltInFallback(string layoutName)
        {
            bool hangul = layoutName != null && layoutName.Contains("한글");
            // 한 자리에 한글 낱자와 영문 글자가 함께 적혀 있으므로, 자판 종류에 맞는 쪽을 고른다.
            string Pick(IEnumerable<string> labels) =>
                labels.FirstOrDefault(l => hangul ? !IsLatinLetter(l) : !IsJamo(l));
            var keys = HangulJamo.SingleStrokeKeys()
                .GroupBy(k => (k.Pos.Row, k.Pos.Column))
                .Select(g => (new KeyPos(g.Key.Item1, g.Key.Item2),
                              Pick(g.Where(x => !x.IsShift).Select(x => x.Label)),
                              Pick(g.Where(x => x.IsShift).Select(x => x.Label))));
            return new KeyboardMap(layoutName, keys);
        }

        private static bool IsLatinLetter(string l) => l != null && l.Length == 1 && l[0] < 128 && char.IsLetter(l[0]);
        private static bool IsJamo(string l) => l != null && l.Length == 1 && l[0] >= 0x3131 && l[0] <= 0x318E;

        // ===== 조회 =====

        /// <summary>키 이름(자판 파일에 적힌 글자, 예: "ㄱ", "ㄱ (받침)", "A") → 키 자리.</summary>
        public bool TryKey(string label, out KeyPos pos, out bool isShift)
        {
            if (label != null && byLabel.TryGetValue(Normalize(label), out HangulJamo.Stroke s))
            {
                pos = s.Pos;
                isShift = s.IsShift;
                return true;
            }
            pos = null;
            isShift = false;
            return false;
        }

        /// <summary>이 키 입력의 '알파벳'(키 이름. 대소문자 짝인 영문은 소문자).</summary>
        public string AlphabetOf(HangulJamo.Stroke s) =>
            alphabetOf.TryGetValue((s.Pos.Row, s.Pos.Column, s.IsShift), out string a) ? a : null;

        /// <summary>Caps Lock이 대문자로 바꾸는 글자 키인가.</summary>
        public bool IsCaseLetterKey(KeyPos pos) => pos != null && caseLetterKeys.Contains((pos.Row, pos.Column));

        // ===== 분해 =====

        /// <summary>한 글자를 이 자판의 키 입력 순서로 분해한다. 칠 수 없는 글자는 빈 목록.</summary>
        public List<HangulJamo.Stroke> DecomposeChar(char ch) => DecomposeParts(ch).SelectMany(p => p.Strokes).ToList();

        public List<HangulJamo.Stroke> Decompose(string text)
        {
            var result = new List<HangulJamo.Stroke>();
            if (string.IsNullOrEmpty(text)) return result;
            foreach (char ch in text)
            {
                List<HangulJamo.Stroke> one = DecomposeChar(ch);
                if (one.Count == 0) return new List<HangulJamo.Stroke>();   // 칠 수 없는 글자가 있으면 통째로 불가
                result.AddRange(one);
            }
            return result;
        }

        /// <summary>문자열을 이루는 알파벳(키 이름)들. 칠 수 없는 글자가 있으면 null.</summary>
        public HashSet<string> AlphabetsOf(string text)
        {
            var set = new HashSet<string>();
            if (string.IsNullOrEmpty(text)) return set;
            foreach (char ch in text)
            {
                List<HangulJamo.Stroke> strokes = DecomposeChar(ch);
                if (strokes.Count == 0) return null;
                foreach (HangulJamo.Stroke s in strokes)
                {
                    string a = AlphabetOf(s);
                    if (a != null) set.Add(a);
                }
            }
            return set;
        }

        /// <summary>한 음절의 구성 부분(초성·중성·종성)과 각 부분을 이루는 낱자·키 입력.</summary>
        private sealed class Part
        {
            public readonly List<char> Components = new List<char>();          // 키마다 더해지는 낱자(ㄱ, ㅗ …)
            public readonly List<HangulJamo.Stroke> Strokes = new List<HangulJamo.Stroke>();
            public bool Ok = true;
            public void Add(char component, HangulJamo.Stroke s) { Components.Add(component); Strokes.Add(s); }
        }

        private List<Part> DecomposeParts(char ch)
        {
            var parts = new List<Part>();
            if (HangulJamo.TrySplitSyllable(ch, out char lead, out char vowel, out char tail))
            {
                parts.Add(Lead(lead));
                parts.Add(Vowel(vowel));
                if (tail != '\0') parts.Add(Tail(tail));
            }
            else
            {
                var p = new Part();
                if (TryStroke(ch.ToString(), out HangulJamo.Stroke s)) p.Add(ch, s);
                else if (HangulJamo.TryCompoundParts(ch, out char[] comps)) AddEach(p, comps, c => c.ToString());
                else p.Ok = false;
                parts.Add(p);
            }
            return parts.Any(p => !p.Ok || p.Strokes.Count == 0) ? new List<Part>() : parts;
        }

        private bool TryStroke(string label, out HangulJamo.Stroke s) => byLabel.TryGetValue(label, out s);

        private void AddEach(Part p, IEnumerable<char> comps, Func<char, string> labelFor)
        {
            foreach (char c in comps)
            {
                if (TryStroke(labelFor(c), out HangulJamo.Stroke s)) p.Add(c, s);
                else { p.Ok = false; return; }
            }
        }

        private Part Lead(char jamo)
        {
            var p = new Part();
            if (TryStroke(jamo.ToString(), out HangulJamo.Stroke s)) p.Add(jamo, s);
            else if (HangulJamo.TryDoubleBase(jamo, out char single)) AddEach(p, new[] { single, single }, c => c.ToString());
            else p.Ok = false;
            return p;
        }

        private Part Vowel(char jamo)
        {
            var p = new Part();
            if (TryStroke(jamo.ToString(), out HangulJamo.Stroke s)) { p.Add(jamo, s); return p; }
            if (!HangulJamo.TryCompoundParts(jamo, out char[] comps)) { p.Ok = false; return p; }
            for (int i = 0; i < comps.Length; i++)
            {
                string label = comps[i].ToString();
                // 겹모음의 앞 모음은 "(이중 모음)" 키가 있으면 그것으로(세벌식).
                if (i == 0 && TryStroke(label + " " + CombiningTag, out HangulJamo.Stroke c)) { p.Add(comps[i], c); continue; }
                if (TryStroke(label, out HangulJamo.Stroke n)) p.Add(comps[i], n);
                else { p.Ok = false; return p; }
            }
            return p;
        }

        private Part Tail(char jamo)
        {
            var p = new Part();
            Func<char, string> label = hasFinalKeys ? (c => c + " " + FinalTag) : (Func<char, string>)(c => c.ToString());
            if (TryStroke(label(jamo), out HangulJamo.Stroke s)) { p.Add(jamo, s); return p; }
            if (HangulJamo.TryCompoundParts(jamo, out char[] comps)) AddEach(p, comps, label);
            else if (HangulJamo.TryDoubleBase(jamo, out char single)) AddEach(p, new[] { single, single }, label);
            else p.Ok = false;
            return p;
        }

        // ===== 입력 진행 표시 (<260812_2>) =====

        /// <summary>
        /// <paramref name="text"/>를 처음부터 <paramref name="strokes"/>타 만큼 정확히 쳤을 때 화면에 보일 문자열.
        /// 다 친 글자는 완성형으로, 치는 중인 글자는 조합되는 대로 보여 준다(ㅌ → 타 → 탈).
        /// </summary>
        public string TypedPrefix(string text, int strokes)
        {
            if (string.IsNullOrEmpty(text) || strokes <= 0) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char ch in text)
            {
                if (strokes <= 0) break;   // 다 친 글자 다음 글자는 아직 한 타도 안 쳤다
                List<Part> parts = DecomposeParts(ch);
                int need = parts.Sum(p => p.Strokes.Count);
                if (need == 0) continue;
                if (strokes >= need) { sb.Append(ch); strokes -= need; continue; }
                sb.Append(PartialChar(ch, parts, strokes));
                break;
            }
            return sb.ToString();
        }

        private static string PartialChar(char ch, List<Part> parts, int done)
        {
            if (!HangulJamo.TrySplitSyllable(ch, out char lead, out char vowel, out _))
                return parts.Count > 0 && parts[0].Components.Count > 0 ? parts[0].Components[0].ToString() : "";

            Part pl = parts[0], pv = parts[1];
            if (done < pl.Strokes.Count) return pl.Components[0].ToString();         // 쌍초성의 첫 타: ㄱ
            done -= pl.Strokes.Count;
            if (done == 0) return lead.ToString();                                     // 초성만: ㅌ
            if (done < pv.Strokes.Count) return HangulJamo.Compose(lead, pv.Components[0], '\0'); // 겹모음 앞: 호
            done -= pv.Strokes.Count;
            if (done == 0 || parts.Count < 3) return HangulJamo.Compose(lead, vowel, '\0');       // 받침 전: 타
            return HangulJamo.Compose(lead, vowel, parts[2].Components[0]);                        // 겹받침 앞: 달
        }
    }

    /// <summary>
    /// 자판 이름 → <see cref="KeyboardMap"/>. 지금 쓰는 자판이면 그것을, 아니면 자판 파일(설정의 자판 데이터
    /// 폴더 → 실행 파일 옆 layouts\)을 읽어 만든다. 파일을 못 찾으면 코드에 있는 두벌식·QWERTY 자리로 대신한다.
    /// </summary>
    public static class KeyboardMaps
    {
        private static readonly Dictionary<string, KeyboardMap> Cache = new Dictionary<string, KeyboardMap>();

        public static KeyboardMap For(string layoutName)
        {
            if (layoutName == null) layoutName = "";
            lock (Cache)
            {
                if (Cache.TryGetValue(layoutName, out KeyboardMap m)) return m;
                KeyLayout layout = MainWindow.CurrentKeyLayout?.Name == layoutName ? MainWindow.CurrentKeyLayout : FindLayout(layoutName);
                m = layout != null ? KeyboardMap.FromLayout(layout) : KeyboardMap.BuiltInFallback(layoutName);
                Cache[layoutName] = m;
                return m;
            }
        }

        /// <summary>자판 데이터가 바뀌었을 때(<see cref="StageSets.ResetCaches"/>) 만들어 둔 규칙을 버린다.</summary>
        internal static void ClearCache() { lock (Cache) Cache.Clear(); }

        private static KeyLayout FindLayout(string layoutName)
        {
            var dirs = new List<string>();
            try
            {
                if (UserSettingsStore.Get(MainWindow.KeyLayoutDataDirStr) is string configured && configured.Length > 0)
                    dirs.Add(configured);
            }
            catch { /* 설정을 못 읽으면 실행 파일 옆만 */ }
            dirs.Add(Path.Combine(AppContext.BaseDirectory, "layouts"));

            foreach (string dir in dirs)
            {
                try
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (string file in Directory.GetFiles(dir, "*.json"))
                    {
                        try
                        {
                            // 키 배치만 필요하므로 통계·연습 키를 덮어쓰는 Load 대신 Parse 만 쓴다(부수효과 없음).
                            KeyLayout k = KeyLayout.Parse(File.ReadAllText(file));
                            if (k.Name == layoutName) return k;
                        }
                        catch { /* 다른 파일 */ }
                    }
                }
                catch { /* 다음 폴더 */ }
            }
            return null;
        }
    }
}
