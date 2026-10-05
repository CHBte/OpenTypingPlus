using System.Collections.Generic;
using System.Linq;

namespace OpenTyping
{
    /// <summary>
    /// 한 단계의 제시어(<260927_2>). 수준별 알파벳·학습 목표 알파벳·재사용 단계는 단계 정의(stages\*.json의
    /// "words")가 정하고, 단어는 제시어 목록(<see cref="WordCatalog"/>)에서 가져와 그 알파벳에 맞는지 검증한다.
    /// '알파벳'은 자판의 키 이름이다(<see cref="KeyboardMap"/>: 두벌식 "ㄱ"·"ㅃ", 세벌식 "ㄱ (받침)", 영문 "a").
    /// '조합이 가능한 한 음절'은 1수준 알파벳으로 여기서 계산한다(파일에 적어 두지 않는다).
    /// </summary>
    public sealed class StageWords
    {
        public static readonly StageWords None = new StageWords();

        private readonly List<HashSet<string>> levelKeys = new List<HashSet<string>>(); // 누적 알파벳(인덱스 0 = 1수준)
        private readonly List<List<string>> unique = new List<List<string>>();          // 수준별 고유 단어(검증 후)
        private readonly List<StageWords> reused = new List<StageWords>();
        private List<string> singles = new List<string>();

        public IReadOnlyCollection<string> Required { get; private set; } = new HashSet<string>();

        /// <summary>'조합이 가능한 한 음절' 전량(한글을 칠 수 있는 자판만, 1수준 알파벳).</summary>
        public IReadOnlyList<string> Singles => singles;

        /// <summary>n 고유 수준의 단어. 재사용 단계는 수준 구분이 없어 전체 단어를 돌려준다.</summary>
        public IReadOnlyList<string> Unique(int level)
        {
            if (reused.Count > 0) return UpTo(int.MaxValue);
            return level >= 1 && level <= unique.Count ? unique[level - 1] : new List<string>();
        }

        /// <summary>n수준 단어(= 1~n 고유 수준의 합). 재사용 단계는 재사용한 단계들의 모든 단어.</summary>
        public IReadOnlyList<string> UpTo(int level)
        {
            if (reused.Count > 0)
                return reused.SelectMany(r => r.UpTo(int.MaxValue)).Distinct().ToList();
            return unique.Take(System.Math.Max(0, level)).SelectMany(l => l).Distinct().ToList();
        }

        /// <summary>
        /// 단계 정의의 words 항목으로 만든다.
        /// <paramref name="levelParts"/>: 수준별로 '더해지는' 알파벳(키 이름) 목록 — 빈 목록이면 앞 수준과 같음.
        /// <paramref name="fromAll"/>: 제시어 목록의 단계 구분을 무시하고 그 묶음의 모든 단어를 모아, 이 단계의
        /// 알파벳에 맞는 가장 낮은 수준에 넣는다(프로그램이 다른 자판의 단계 제시어를 이미 있는 단어 목록에서 가져와 다시 나눌 때).
        /// <paramref name="earlier"/>: 이미 만든 앞 단계들(번호 → StageWords) — reuse 가 가리킨다.
        /// </summary>
        public static StageWords Build(string section, KeyboardMap map, int stageNumber,
                                       IEnumerable<string> required, IReadOnlyList<IReadOnlyList<string>> levelParts,
                                       IReadOnlyList<int> reuse, bool fromAll,
                                       IReadOnlyDictionary<int, StageWords> earlier)
        {
            var sw = new StageWords { Required = new HashSet<string>(required ?? new string[0]) };

            var acc = new HashSet<string>();
            foreach (IReadOnlyList<string> add in levelParts ?? new IReadOnlyList<string>[0])
            {
                foreach (string a in add) acc.Add(a);
                sw.levelKeys.Add(new HashSet<string>(acc));
            }

            if (reuse != null && reuse.Count > 0)
            {
                foreach (int n in reuse)
                    if (n != stageNumber && earlier != null && earlier.TryGetValue(n, out StageWords r)) sw.reused.Add(r);
            }
            else if (fromAll)
            {
                foreach (HashSet<string> _ in sw.levelKeys) sw.unique.Add(new List<string>());
                foreach (string w in WordCatalog.AllWords(section))
                {
                    HashSet<string> alpha = AlphabetsOfWord(map, w);
                    if (alpha == null || !HasRequired(alpha, sw.Required)) continue;
                    int lv = sw.levelKeys.FindIndex(k => alpha.IsSubsetOf(k));
                    if (lv >= 0) sw.unique[lv].Add(w);
                }
            }
            else
            {
                for (int lv = 1; lv <= sw.levelKeys.Count; lv++)
                {
                    HashSet<string> keys = sw.levelKeys[lv - 1];
                    sw.unique.Add(WordCatalog.RawLevelWords(section, stageNumber, lv)
                        .Where(w => IsValidWord(map, w, keys, sw.Required)).ToList());
                }
            }

            if (map.CanTypeHangul && sw.levelKeys.Count > 0)
                sw.singles = ComputeSingles(map, sw.levelKeys[0], sw.Required, WordCatalog.SyllableBlacklist(section));
            return sw;
        }

        /// <summary>1수준 알파벳만으로 칠 수 있고 학습 목표 알파벳을 하나 이상 품은 음절 전량(제외 음절 빼고).</summary>
        internal static List<string> ComputeSingles(KeyboardMap map, HashSet<string> keys,
                                                    IReadOnlyCollection<string> required, string blacklist)
        {
            var result = new List<string>();
            for (char ch = (char)0xAC00; ch <= (char)0xD7A3; ch++)
            {
                if (blacklist != null && blacklist.IndexOf(ch) >= 0) continue;
                HashSet<string> alpha = map.AlphabetsOf(ch.ToString());
                if (alpha != null && alpha.Count > 0 && alpha.IsSubsetOf(keys) && HasRequired(alpha, required))
                    result.Add(ch.ToString());
            }
            return result;
        }

        /// <summary>단어(두 글자 이상, 글자로만 이뤄짐)를 이루는 알파벳. 칠 수 없으면 null.</summary>
        private static HashSet<string> AlphabetsOfWord(KeyboardMap map, string word)
        {
            if (string.IsNullOrEmpty(word) || word.Length < 2 || !word.All(char.IsLetter)) return null;
            return map.AlphabetsOf(word);
        }

        private static bool HasRequired(HashSet<string> alpha, IReadOnlyCollection<string> required) =>
            required == null || required.Count == 0 || required.Any(alpha.Contains);

        /// <summary>제시어 하나가 그 수준 알파벳만으로 이뤄졌고 학습 목표 알파벳을 하나 이상 품는가.</summary>
        internal static bool IsValidWord(KeyboardMap map, string word, HashSet<string> keys, IReadOnlyCollection<string> required)
        {
            HashSet<string> alpha = AlphabetsOfWord(map, word);
            return alpha != null && alpha.IsSubsetOf(keys) && HasRequired(alpha, required);
        }
    }
}
