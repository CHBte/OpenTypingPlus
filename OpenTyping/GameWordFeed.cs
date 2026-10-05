using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTyping
{
    /// <summary>
    /// <260927_4> 오락 한 단계가 쓰는 단어 묶음. 어느 오락이든(지금은 산성비뿐) 같은 정의를 쓰도록
    /// 게임 창과 떼어 둔다. 주 목록(Main)과, RareEvery 개마다 1개 섞는 드문 목록(Rare, 없으면 빈 목록)으로
    /// 이뤄지고, UniqueUnit 개 단위로 같은 단어를 두 번 내지 않는다.
    /// </summary>
    public sealed class GameStage
    {
        public GameStage(int id, string name, IReadOnlyList<string> main, IReadOnlyList<string> rare,
                         int rareEvery = 10, int uniqueUnit = 5)
        {
            Id = id;
            Name = name;
            Main = main ?? new List<string>();
            Rare = rare ?? new List<string>();
            RareEvery = Math.Max(1, rareEvery);
            UniqueUnit = Math.Max(1, uniqueUnit);
        }

        public int Id { get; }
        public string Name { get; }
        public IReadOnlyList<string> Main { get; }
        public IReadOnlyList<string> Rare { get; }
        public int RareEvery { get; }
        public int UniqueUnit { get; }

        /// <summary>이 단계에 쓸 단어가 하나라도 있는가.</summary>
        public bool HasWords => Main.Count > 0 || Rare.Count > 0;
    }

    /// <summary>
    /// <260927_4> 자판별 오락 단계 구성. 어느 단계에 오락이 있는지(game), 어느 수준의 단어를 주 목록·드문
    /// 목록으로 쓸지(game_words), 섞는 비율·중복 금지 단위(game_rules)는 모두 단계 정의 파일이 정한다.
    /// 오락에서는 숫자·기호 없이 기존 단어만 쓴다.
    /// </summary>
    public static class GameStages
    {
        // 제시어 목록은 실행 중에 바뀌지 않으므로 자판 묶음마다 한 번만 만든다.
        private static readonly Dictionary<string, IReadOnlyList<GameStage>> Cache = new Dictionary<string, IReadOnlyList<GameStage>>();

        /// <summary>자판 데이터가 바뀌었을 때(<see cref="StageSets.ResetCaches"/>) 다시 만들게 한다.</summary>
        internal static void ClearCache() => Cache.Clear();

        public static IReadOnlyList<GameStage> For(IStageSet set)
        {
            if (set == null) return new List<GameStage>();
            if (!Cache.TryGetValue(set.Key, out IReadOnlyList<GameStage> stages))
            {
                stages = Build(set);
                Cache[set.Key] = stages;
            }
            return stages;
        }

        private static IReadOnlyList<GameStage> Build(IStageSet set)
        {
            var result = new List<GameStage>();
            foreach (PracticeStage ps in set.Stages)
            {
                int id = ps.GameStageId;
                if (id <= 0 || result.Any(g => g.Id == id)) continue;   // 오락이 없는 단계(game 0)

                // 주 목록: main_level 수준까지(0이면 마지막 수준까지 = 그 단계 단어 전부).
                // 드문 목록: rare_level 고유 수준(0이면 없음).
                int mainLevel = ps.GameMainLevel > 0 ? ps.GameMainLevel : int.MaxValue;
                IReadOnlyList<string> main = ps.Words.UpTo(mainLevel);
                var mainSet = new HashSet<string>(main);
                IReadOnlyList<string> rare = ps.GameRareLevel > 0
                    ? ps.Words.Unique(ps.GameRareLevel).Where(w => !mainSet.Contains(w)).ToList()
                    : null;

                // 영문 오락에서는 대문자가 든 단어(DJ·DVD 등)도 그대로 내려온다. 자리연습과 달리 빼지 않는다 —
                // 대신 판정은 대소문자를 구분해(AcidRainWindow.MatchesTyped) [Shift]로 대문자를 정확히 쳐야 정답이고,
                // 소문자로 치면 오답이다(썰렁이 지시, 2026-10-02). [Shift]를 아직 안 가르친 낮은 단계에 이런 단어가
                // 나와도 잘못이 아니다 — 영문 자판에서 [Shift]·[Caps Lock]으로 대문자를 입력하는 법은 사용자가 따로
                // 배울 수 있다고 가정한다(썰렁이 설명, 2026-10-02). 그러니 단계의 글쇠 범위로 거르지 않는다.
                // 이 가정은 **영문 자판에만** 해당한다: 한글 레벨2(ㅃ 등)는 대소문자 변환이 아니라 다른 자소라
                // 별개의 알파벳이고, StageWords 가 그 자소를 가르치는 단계의 단어만 남긴다(KeyboardMap.IsCasePair 참고).

                var gs = new GameStage(id, ps.Name, main, rare, set.GameRareEvery, set.GameUniqueUnit);
                if (gs.HasWords) result.Add(gs);
            }
            return result;
        }
    }

    /// <summary>
    /// <260927_4>(0.2)(0.2.1)·(1.4) 산성비 단어를 차례로 내준다. 개수는 단계 정의의 game_rules 를 따른다.
    ///  - RareEvery(지금 10)개마다 드문 목록에서 1개(자리는 무작위), 나머지는 주 목록에서.
    ///  - UniqueUnit(지금 5)개를 한 단위로, 한 단위 안에서는 같은 단어가 두 번 나오지 않는다.
    ///  - 후보가 UniqueUnit 개 미만이면 위 중복 금지 규칙은 무시한다.
    /// 어느 경우에도 예외 없이 단어를 돌려주며, 쓸 단어가 전혀 없으면 null.
    /// </summary>
    public sealed class GameWordFeed
    {
        private readonly int unitSize;
        private readonly int blockSize;

        private readonly IReadOnlyList<string> main;
        private readonly IReadOnlyList<string> rare;
        private readonly Random rng;
        private readonly HashSet<string> usedInUnit = new HashSet<string>();
        private int drawn;
        private int rareSlot;   // 이번 묶음에서 드문 목록을 쓸 자리(0 ~ blockSize-1)

        public GameWordFeed(GameStage stage, Random rng)
        {
            main = stage?.Main ?? new List<string>();
            rare = stage?.Rare ?? new List<string>();
            unitSize = stage?.UniqueUnit ?? 5;
            blockSize = stage?.RareEvery ?? 10;
            this.rng = rng ?? new Random();
            rareSlot = this.rng.Next(blockSize);
        }

        /// <summary>다음 단어. <paramref name="onScreen"/>에 든 단어(이미 떠 있는 것)는 되도록 피한다.</summary>
        public string Next(Func<string, bool> onScreen = null)
        {
            int inBlock = drawn % blockSize;
            if (inBlock == 0 && drawn > 0) rareSlot = rng.Next(blockSize);
            if (drawn % unitSize == 0) usedInUnit.Clear();

            bool useRare = rare.Count > 0 && inBlock == rareSlot;
            IReadOnlyList<string> pool = useRare ? rare : (main.Count > 0 ? main : rare);
            if (pool.Count == 0) return null;

            // (0.2.1) 후보(두 목록 합)가 한 단위보다 적으면 단위 안 중복 금지를 적용하지 않는다.
            bool enforceUnique = main.Count + rare.Count >= unitSize;

            string pick = Pick(pool, w => (!enforceUnique || !usedInUnit.Contains(w)) && (onScreen == null || !onScreen(w)))
                          ?? Pick(pool, w => !enforceUnique || !usedInUnit.Contains(w))
                          ?? pool[rng.Next(pool.Count)];

            usedInUnit.Add(pick);
            drawn++;
            return pick;
        }

        private string Pick(IReadOnlyList<string> pool, Func<string, bool> ok)
        {
            // 대부분은 몇 번 안에 맞는 단어가 나오므로 먼저 무작위로 몇 번 시도하고, 그래도 없으면 전체에서 고른다.
            for (int t = 0; t < 8; t++)
            {
                string w = pool[rng.Next(pool.Count)];
                if (ok(w)) return w;
            }
            List<string> candidates = pool.Where(ok).ToList();
            return candidates.Count > 0 ? candidates[rng.Next(candidates.Count)] : null;
        }
    }
}
