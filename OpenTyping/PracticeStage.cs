using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTyping
{
    /// <summary>연습 프롬프트(제시 항목) 하나. 순1·순2는 키 1개, 순3·순4는 음절/단어 문자열.</summary>
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

    public enum RoundKind { Sequential, Weighted, Syllables, Words, Mixed }

    /// <summary>
    /// 연습 단계의 한 '순'(라운드). 종류에 따라 제시 방식이 다르다:
    /// Sequential(순1) 나열 순서대로 Repeat번 반복(선택적 셔플), Weighted(순2) 3:1 가중 복원추출 Count번,
    /// Syllables(순3) 한 글자 조합 Count번, Words(순4) 2음절 이상 단어 Count번,
    /// Mixed(13단계 중간) 한 글자·단어·특수기호 섞어 Count번.
    /// </summary>
    public sealed class PracticeRound
    {
        private PracticeRound(RoundKind kind) { Kind = kind; }

        public RoundKind Kind { get; }

        // Sequential
        private IReadOnlyList<PracticeStage.StageItem> seqItems;
        private int repeat;
        private bool shuffle;

        // Weighted
        private IReadOnlyList<PracticeStage.StageItem> lowItems;
        private IReadOnlyList<PracticeStage.StageItem> highItems;
        private int count;

        // Syllables / Words / Mixed
        private IReadOnlyList<string> textPool;
        private IReadOnlyList<PracticeStage.StageItem> symbolPool;

        public static PracticeRound Sequential(IReadOnlyList<PracticeStage.StageItem> items, int repeat, bool shuffle = false) =>
            new PracticeRound(RoundKind.Sequential) { seqItems = items, repeat = repeat, shuffle = shuffle };

        public static PracticeRound Weighted(IReadOnlyList<PracticeStage.StageItem> low,
                                             IReadOnlyList<PracticeStage.StageItem> high, int count) =>
            new PracticeRound(RoundKind.Weighted) { lowItems = low, highItems = high, count = count };

        public static PracticeRound Syllables(IReadOnlyList<string> pool, int count) =>
            new PracticeRound(RoundKind.Syllables) { textPool = pool, count = count };

        public static PracticeRound Words(IReadOnlyList<string> pool, int count) =>
            new PracticeRound(RoundKind.Words) { textPool = pool, count = count };

        public static PracticeRound Mixed(IReadOnlyList<string> textPool,
                                          IReadOnlyList<PracticeStage.StageItem> symbolPool, int count) =>
            new PracticeRound(RoundKind.Mixed) { textPool = textPool, symbolPool = symbolPool, count = count };

        /// <summary>이 라운드가 만들어 내는 모든 (키 위치) 항목 — 13단계의 "연습한 키 전체" 계산에 쓴다.</summary>
        public IEnumerable<PracticeStage.StageItem> AllKeyItems()
        {
            switch (Kind)
            {
                case RoundKind.Sequential: return seqItems;
                case RoundKind.Weighted: return lowItems.Concat(highItems);
                case RoundKind.Mixed: return symbolPool ?? Enumerable.Empty<PracticeStage.StageItem>();
                default: return Enumerable.Empty<PracticeStage.StageItem>();
            }
        }

        /// <summary>이 라운드의 제시 프롬프트를 순서대로 생성한다.</summary>
        public List<PracticePrompt> Generate(Random rng)
        {
            var result = new List<PracticePrompt>();
            switch (Kind)
            {
                case RoundKind.Sequential:
                {
                    var items = new List<PracticeStage.StageItem>();
                    for (int r = 0; r < repeat; r++) items.AddRange(seqItems);
                    if (shuffle) Shuffle(items, rng);
                    foreach (var it in items) result.Add(PracticePrompt.ForKey(it));
                    break;
                }
                case RoundKind.Weighted:
                {
                    var pool = new List<PracticeStage.StageItem>(lowItems);
                    for (int i = 0; i < PracticeStage.HighWeight; i++) pool.AddRange(highItems);
                    if (pool.Count == 0) break;
                    for (int i = 0; i < count; i++)
                        result.Add(PracticePrompt.ForKey(pool[rng.Next(pool.Count)]));
                    break;
                }
                case RoundKind.Syllables:
                case RoundKind.Words:
                {
                    if (textPool == null || textPool.Count == 0) break;
                    for (int i = 0; i < count; i++)
                        result.Add(PracticePrompt.ForText(textPool[rng.Next(textPool.Count)]));
                    break;
                }
                case RoundKind.Mixed:
                {
                    int textN = textPool?.Count ?? 0;
                    int symN = symbolPool?.Count ?? 0;
                    int total = textN + symN;
                    if (total == 0) break;
                    for (int i = 0; i < count; i++)
                    {
                        int pick = rng.Next(total);
                        result.Add(pick < textN
                            ? PracticePrompt.ForText(textPool[pick])
                            : PracticePrompt.ForKey(symbolPool[pick - textN]));
                    }
                    break;
                }
            }
            return result;
        }

        private static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

    /// <summary>
    /// '자리연습'의 연습 단계 하나. 여러 '순'(<see cref="PracticeRound"/>)을 차례로 진행한다 (<260723_4>).
    /// 출력 원리는 순마다 다르다(순1 나열, 순2 가중 복원추출 3:1, 순3 한 글자 조합, 순4 단어).
    /// </summary>
    public class PracticeStage
    {
        public const int HighWeight = 3; // 많은 빈도 : 적은 빈도 = 3 : 1

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

        public PracticeStage(string name, IReadOnlyList<PracticeRound> rounds, int gameStageId)
        {
            Name = name;
            Rounds = rounds;
            GameStageId = gameStageId;
        }

        public string Name { get; }
        public IReadOnlyList<PracticeRound> Rounds { get; }

        /// <summary>이 단계에 대응하는 산성비 오락 단계 id. 0이면 오락 없음(9~12단계) (<260723_4> (1)).</summary>
        public int GameStageId { get; }

        /// <summary>이 단계의 전체 연습 프롬프트를 순서대로 생성한다(순1→순2→…).</summary>
        public List<PracticePrompt> Generate(Random rng)
        {
            var all = new List<PracticePrompt>();
            foreach (PracticeRound r in Rounds) all.AddRange(r.Generate(rng));
            return all;
        }

    }

    /// <summary>
    /// '두벌식 표준' 자판 연습 단계(1~13). 정의는 stages\dubeolsik_standard.json에서 로드한다
    /// (<260724_1>(3)로 BuildStages() 하드코딩을 옮김). 기존 소비 코드가 IList로 쓰므로 List로 노출한다.
    /// </summary>
    public static class DubeolsikStages
    {
        public static readonly IList<PracticeStage> Stages = LoadStages();

        private static IList<PracticeStage> LoadStages()
        {
            IStageSet set = StageSets.ForLayout(KeyPracticeMenu.StageLayoutName);
            return set != null ? set.Stages.ToList() : new List<PracticeStage>();
        }
    }
}
