using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTyping
{
    /// <summary>연습 프롬프트(제시 항목) 하나. 키 하나(낱자·숫자·기호)이거나 음절/단어 문자열이다.</summary>
    public sealed class PracticePrompt
    {
        private PracticePrompt(bool isText, string text, KeyPos key, bool isShift, int taCount)
        {
            IsText = isText;
            Text = text;
            Key = key;
            IsShift = isShift;
            TaCount = taCount;
        }

        public bool IsText { get; }
        public string Text { get; }       // IsText일 때: 제시 영역에 통째로 보여줄 음절/단어
        public KeyPos Key { get; }        // !IsText일 때: 제시할 키 위치
        public bool IsShift { get; }      // !IsText일 때: 윗글쇠 여부
        public int TaCount { get; }       // 이 프롬프트를 다 맞혔을 때의 '타'(완성 글자/항목 개수)

        public static PracticePrompt ForKey(PracticeStage.StageItem item) =>
            new PracticePrompt(false, null, item.Pos, item.IsShift, 1);

        public static PracticePrompt ForText(string text) =>
            new PracticePrompt(true, text, default, false, HangulJamo.SyllableCount(text));
    }

    public enum RoundKind { Sequential, Grouped, PracticedKeys, Texts, Mixed }

    /// <summary>
    /// 연습 단계의 한 '순'(라운드) (<260927_3>). 종류:
    ///  - Sequential: 적힌 키를 차례로.
    ///  - Grouped: 그룹마다 키 주머니를 두고, 패턴(예: "001" = 그룹1 두 번 후 그룹2 한 번)대로 주머니에서
    ///    비복원추출한다. Times 번 되풀이하며 되풀이마다 주머니를 새로 채운다.
    ///  - PracticedKeys: 앞 단계들의 학습 목표 키 전체를 Times 번씩 섞어서(10단계 前반 등).
    ///  - Texts: 음절이나 단어 목록에서 Count 개를 비복원추출.
    ///  - Mixed: 음절과 기호를 1:1로 Count 개(10단계 中반).
    /// NoRepeat이면 '직전값 제외'(같은 제시어가 바로 이어 나오지 않음)를 지킨다.
    /// </summary>
    public sealed class PracticeRound
    {
        private PracticeRound(RoundKind kind) { Kind = kind; }

        public RoundKind Kind { get; }

        private IReadOnlyList<PracticeStage.StageItem> items;               // Sequential / PracticedKeys
        private IReadOnlyList<IReadOnlyList<PracticeStage.StageItem>> groups; // Grouped
        private string pattern;                                              // Grouped
        private int times = 1;
        private int count;
        private bool noRepeat;
        private IReadOnlyList<string> textPool;                              // Texts / Mixed
        private IReadOnlyList<PracticeStage.StageItem> symbolPool;           // Mixed

        public static PracticeRound Sequential(IReadOnlyList<PracticeStage.StageItem> keys) =>
            new PracticeRound(RoundKind.Sequential) { items = keys };

        public static PracticeRound Grouped(IReadOnlyList<IReadOnlyList<PracticeStage.StageItem>> groups,
                                            string pattern, int times, bool noRepeat) =>
            new PracticeRound(RoundKind.Grouped) { groups = groups, pattern = pattern ?? "", times = Math.Max(1, times), noRepeat = noRepeat };

        public static PracticeRound PracticedKeys(IReadOnlyList<PracticeStage.StageItem> keys, int times, bool noRepeat) =>
            new PracticeRound(RoundKind.PracticedKeys) { items = keys, times = Math.Max(1, times), noRepeat = noRepeat };

        public static PracticeRound Texts(IReadOnlyList<string> pool, int count, bool noRepeat) =>
            new PracticeRound(RoundKind.Texts) { textPool = pool, count = count, noRepeat = noRepeat };

        public static PracticeRound Mixed(IReadOnlyList<string> textPool,
                                          IReadOnlyList<PracticeStage.StageItem> symbolPool, int count) =>
            new PracticeRound(RoundKind.Mixed) { textPool = textPool, symbolPool = symbolPool, count = count, noRepeat = true };

        /// <summary>'직전값 제외' 조건을 몇 번까지 다시 뽑아 맞춰 볼지 (<260927_3>(0.6.2)).</summary>
        private const int MaxAttempts = 400;

        /// <summary>이 라운드의 제시 프롬프트를 순서대로 생성한다. 어떤 경우에도 예외를 내지 않는다 (<260927_3>(0.6)).</summary>
        public List<PracticePrompt> Generate(Random rng)
        {
            try
            {
                switch (Kind)
                {
                    case RoundKind.Sequential:
                        return (items ?? new PracticeStage.StageItem[0]).Select(PracticePrompt.ForKey).ToList();

                    case RoundKind.Grouped:
                        return BuildChecked(() => GroupedOnce(rng), PracticeStage.SameKey)
                               .Select(PracticePrompt.ForKey).ToList();

                    case RoundKind.PracticedKeys:
                    {
                        if (items == null || items.Count == 0) return new List<PracticePrompt>();
                        return BuildChecked(() =>
                        {
                            var bag = new List<PracticeStage.StageItem>();
                            for (int t = 0; t < times; t++) bag.AddRange(items);
                            Shuffle(bag, rng);
                            return bag;
                        }, PracticeStage.SameKey).Select(PracticePrompt.ForKey).ToList();
                    }

                    case RoundKind.Texts:
                    {
                        if (textPool == null || textPool.Count == 0 || count <= 0) return new List<PracticePrompt>();
                        return BuildChecked(() => Draw(textPool, count, rng), string.Equals)
                               .Select(PracticePrompt.ForText).ToList();
                    }

                    case RoundKind.Mixed:
                        return BuildChecked(() => MixedOnce(rng), SamePrompt);
                }
            }
            catch
            {
                // 뽑기 도중 예상 못 한 오류가 나도 연습 창이 죽지 않게, 이 라운드만 건너뛴다.
            }
            return new List<PracticePrompt>();
        }

        /// <summary>
        /// 순서 전체를 미리 만든 뒤 '직전값 제외'를 검사하고, 어긋나면 처음부터 다시 만든다
        /// (<260927_3>(0.6.2)). 끝내 맞출 수 없으면(후보가 모자라 불가능한 경우) 마지막으로 만든
        /// 순서를 그대로 쓴다 — 이때는 직전값 제외를 지키지 않아도 된다 (<260927_3>(0.6.1.1)).
        /// </summary>
        private List<T> BuildChecked<T>(Func<List<T>> build, Func<T, T, bool> same)
        {
            List<T> seq = build();
            if (!noRepeat) return seq;
            for (int attempt = 1; attempt < MaxAttempts && HasImmediateRepeat(seq, same); attempt++)
                seq = build();
            return seq;
        }

        private static bool SamePrompt(PracticePrompt a, PracticePrompt b) =>
            a.IsText ? b.IsText && a.Text == b.Text
                     : !b.IsText && a.IsShift == b.IsShift && Equals(a.Key, b.Key);

        private static bool HasImmediateRepeat<T>(IList<T> seq, Func<T, T, bool> same)
        {
            for (int i = 1; i < seq.Count; i++)
                if (same(seq[i - 1], seq[i])) return true;
            return false;
        }

        private List<PracticeStage.StageItem> GroupedOnce(Random rng)
        {
            var result = new List<PracticeStage.StageItem>();
            if (groups == null || groups.Count == 0 || pattern.Length == 0) return result;

            for (int t = 0; t < times; t++)
            {
                // 되풀이마다 주머니를 새로 채운다(각 글쇠를 정해진 횟수만큼 한 번씩 쓰게).
                var bags = groups.Select(g => Shuffled(g, rng)).ToList();
                foreach (char c in pattern)
                {
                    int gi = c - '0';
                    if (gi < 0 || gi >= groups.Count || groups[gi].Count == 0) continue;
                    // 패턴이 주머니보다 많이 요구하면 비복원추출을 복원추출로 바꾼다 (<260927_3>(0.6.1)).
                    if (bags[gi].Count == 0) bags[gi] = Shuffled(groups[gi], rng);
                    result.Add(bags[gi][bags[gi].Count - 1]);
                    bags[gi].RemoveAt(bags[gi].Count - 1);
                }
            }
            return result;
        }

        private List<PracticePrompt> MixedOnce(Random rng)
        {
            var result = new List<PracticePrompt>();
            if (count <= 0) return result;
            bool hasText = textPool != null && textPool.Count > 0;
            bool hasSym = symbolPool != null && symbolPool.Count > 0;
            if (!hasText && !hasSym) return result;

            // 1:1 비율. 한쪽 후보가 없으면 다른 쪽으로 모두 채운다.
            int textN = !hasSym ? count : !hasText ? 0 : (count + 1) / 2;
            int symN = count - textN;
            result.AddRange(hasText ? Draw(textPool, textN, rng).Select(PracticePrompt.ForText) : Enumerable.Empty<PracticePrompt>());
            result.AddRange(hasSym ? Draw(symbolPool, symN, rng).Select(PracticePrompt.ForKey) : Enumerable.Empty<PracticePrompt>());
            Shuffle(result, rng);
            return result;
        }

        /// <summary>
        /// 비복원추출로 n개. 후보가 n개보다 적으면 복원추출로 바꿔(후보를 다시 섞어 이어 붙여) 채운다
        /// (<260927_3>(0.6.1)).
        /// </summary>
        private static List<T> Draw<T>(IReadOnlyList<T> pool, int n, Random rng)
        {
            var result = new List<T>();
            if (pool == null || pool.Count == 0) return result;
            while (result.Count < n)
            {
                List<T> round = Shuffled(pool, rng);
                result.AddRange(round.Take(n - result.Count));
            }
            return result;
        }

        private static List<T> Shuffled<T>(IReadOnlyList<T> source, Random rng)
        {
            var list = source.ToList();
            Shuffle(list, rng);
            return list;
        }

        private static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>진단용: 이 순이 '직전값 제외'를 지켜야 하는가.</summary>
        internal bool NoRepeat => noRepeat;

        /// <summary>진단용: 순 설명(종류와 개수).</summary>
        public override string ToString() => Kind.ToString();
    }

    /// <summary>
    /// '자리연습'의 연습 단계 하나. 여러 '순'(<see cref="PracticeRound"/>)을 차례로 진행한다 (<260927_3>).
    /// </summary>
    public class PracticeStage
    {
        public struct StageItem
        {
            public StageItem(int row, int column, bool isShift = false)
            {
                Pos = new KeyPos(row, column);
                IsShift = isShift;
            }

            public KeyPos Pos { get; }
            public bool IsShift { get; }
        }

        internal static bool SameKey(StageItem a, StageItem b) =>
            a.Pos.Row == b.Pos.Row && a.Pos.Column == b.Pos.Column && a.IsShift == b.IsShift;

        public PracticeStage(string name, IReadOnlyList<PracticeRound> rounds, int gameStageId,
                             int targetTa, IReadOnlyList<StageItem> targets,
                             StageWords words = null, int gameMainLevel = 0, int gameRareLevel = 0)
        {
            Name = name;
            Rounds = rounds;
            GameStageId = gameStageId;
            TargetTa = targetTa;
            Targets = targets ?? new StageItem[0];
            Words = words ?? StageWords.None;
            GameMainLevel = gameMainLevel;
            GameRareLevel = gameRareLevel;
        }

        /// <summary>이 단계의 제시어(수준별 단어·한 음절). 단계 정의의 "words"에서 만든다.</summary>
        public StageWords Words { get; }

        /// <summary>오락의 주 목록: 이 수준까지의 단어(0이면 마지막 수준까지) (<260927_4>).</summary>
        public int GameMainLevel { get; }

        /// <summary>오락의 드문 목록: 이 고유 수준의 단어(0이면 없음) (<260927_4>).</summary>
        public int GameRareLevel { get; }

        public string Name { get; }
        public IReadOnlyList<PracticeRound> Rounds { get; }

        /// <summary>이 단계에 대응하는 오락 단계 id. 0이면 오락 없음(한글 7~9단계).</summary>
        public int GameStageId { get; }

        /// <summary>이 단계의 '목표 타수' (<260927_3>(0.4)). 판정은 <see cref="StageRecords"/>를 거친다.</summary>
        public int TargetTa { get; }

        /// <summary>이 단계의 학습 목표 키(<260927_1>). '연습한 키 전체' 단계가 앞 단계들의 것을 모은다.</summary>
        public IReadOnlyList<StageItem> Targets { get; }

        /// <summary>이 단계의 전체 연습 프롬프트를 순서대로 생성한다(순1→순2→…).</summary>
        public List<PracticePrompt> Generate(Random rng)
        {
            var all = new List<PracticePrompt>();
            foreach (PracticeRound r in Rounds) all.AddRange(r.Generate(rng));
            return all;
        }
    }
}
