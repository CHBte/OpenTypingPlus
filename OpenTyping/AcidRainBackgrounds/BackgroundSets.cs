using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTyping
{
    /// <summary>
    /// 산성비 오락의 `배경 세트`(<261025_3>) 목록과 자판별 배정, 오락 단계 → 배경 단계 대응 규칙.
    /// - 추가: BackgroundSet 을 이어받은 클래스 하나 + 그림 폴더 Resources\AcidRain\&lt;Id&gt;\ 를 만들고 All 에 한 줄 더한다.
    /// - 삭제: All 에서 빼고(ByKeyboard 에 그 Id 가 있으면 바꾸고) 클래스와 그림 폴더를 지운다.
    /// - 자판별 기본 배경 교체: ByKeyboard 의 Id 만 바꾼다. 같은 배경을 여러 자판에 써도 된다.
    /// - 사용자는 '자리연습' 화면의 '오락 열기' → '배경 선택'(BackgroundSelectWindow, <261025_4>)으로 자판마다 배경을 고른다(설정 파일에
    ///   저장 — UserSettingsStore.AcidRainBackgrounds). 고르지 않았으면 ByKeyboard 의 기본 배경.
    /// - '배경 선택'에서만 숨기기: 그 클래스의 ShowInMenu 를 false 로.
    /// </summary>
    internal static class BackgroundSets
    {
        /// <summary>프로그램에 들어 있는 배경 세트(이 차례로 메뉴에 나온다).</summary>
        internal static readonly IReadOnlyList<BackgroundSet> All = new BackgroundSet[]
        {
            new GrowingFlowerGarden(),
            new EcoEnergyVillage(),
        };

        /// <summary>'배경 선택' 창에 보일 배경 세트(ShowInMenu 인 것).</summary>
        internal static IEnumerable<BackgroundSet> Menu => All.Where(s => s.ShowInMenu);

        internal static BackgroundSet Find(string id) => All.FirstOrDefault(s => s.Id == id);

        /// <summary>
        /// 자판(단계 묶음의 key — 두벌식 표준 한글 "ko", QWERTY 영문 "en")마다의 기본 배경 세트 Id(<261025_4>: 두벌식은 '자라는 꽃밭',
        /// 영문은 '친환경 에너지 마을로'). 사용자가 '배경 선택'으로 고르기 전에 쓴다. 여기에 없는 자판은 DefaultId.
        /// </summary>
        internal static readonly IReadOnlyDictionary<string, string> ByKeyboard = new Dictionary<string, string>
        {
            ["ko"] = GrowingFlowerGarden.SetId,
            ["en"] = EcoEnergyVillage.SetId,
        };

        internal const string DefaultId = GrowingFlowerGarden.SetId;

        /// <summary>이 자판(key)의 기본 배경 세트.</summary>
        internal static BackgroundSet DefaultFor(string keyboardKey)
        {
            string id = keyboardKey != null && ByKeyboard.TryGetValue(keyboardKey, out string kid) ? kid : DefaultId;
            return Find(id) ?? Find(DefaultId) ?? All[0];
        }

        /// <summary>
        /// 자판 keyboardKey 에서 사용자가 고른 배경 세트 Id(chosenId)를 따라 쓸 배경 세트. 고르지 않았거나, 그 배경 세트가 없어졌거나
        /// '배경 선택'에서 숨긴 것이면 자판의 기본 배경.
        /// </summary>
        internal static BackgroundSet Resolve(string keyboardKey, string chosenId)
        {
            BackgroundSet chosen = chosenId != null ? Find(chosenId) : null;
            return chosen != null && chosen.ShowInMenu ? chosen : DefaultFor(keyboardKey);
        }

        /// <summary>이 자판의 오락에 쓰는 배경 세트(사용자가 고른 것 또는 기본 배경).</summary>
        internal static BackgroundSet For(IStageSet keyboard)
        {
            string key = keyboard?.Key;
            return Resolve(key, UserSettingsStore.GetAcidRainBackground(key));
        }

        /// <summary>배경 세트 bg 를 자판 keyboardKey 의 오락에 쓸 때의 배경 단계 차례(그 자판에 따로 맞춘 것이 있으면 그것).</summary>
        internal static IReadOnlyList<int> StageSequence(BackgroundSet bg, string keyboardKey) =>
            (keyboardKey != null ? bg.StagesFor(keyboardKey) : null) ?? bg.Stages;

        /// <summary>
        /// 오락의 ordinal 번째(0부터) 단계(오락 단계 수 gameStageCount)에 쓸 배경 단계. 단계 수가 다르면 썰렁이가 정한 규칙(<261025_3>(2.2))을
        /// 따른다 — 배경 단계가 적으면 마지막 배경 단계를 여러 번 쓰고(앞에서부터 맞춘다), 많으면 끝에서부터 맞춘다(마지막 오락 단계 =
        /// 마지막 배경 단계라, 앞쪽 배경 단계 몇 개는 쓰이지 않는다).
        /// </summary>
        internal static int SceneFor(IReadOnlyList<int> stages, int ordinal, int gameStageCount)
        {
            if (stages == null || stages.Count == 0) return 10;   // 배경 단계를 적지 않은 배경 세트(잘못 만든 것)는 완성된 장면 번호로
            int n = stages.Count, m = Math.Max(1, gameStageCount);
            int o = Math.Max(0, Math.Min(ordinal, m - 1));
            int index = n >= m ? o + (n - m) : Math.Min(o, n - 1);
            return stages[index];
        }

        /// <summary>
        /// 레벨 level 에서 이 종류의 지면 클립 아트가 얼마나 올라왔나(0 = 땅속에 숨음, 1 = 다 올라옴 — acidrain-bg3 <261024_1>(3) 규칙).
        /// 처음 레벨에서 0, 다 올라오는 레벨에서 1이고 그 사이는 같은 간격이다. 땅의 것은 5단계까지 레벨 8, 6단계 레벨 4, 10단계 레벨 3에서
        /// 다 올라온다. 날벌레·새는 6단계에서 레벨 5부터 나타나 레벨 8, 10단계에서 레벨 3부터 나타나 레벨 5에 다 올라오고, 마지막 장면의
        /// 주인공은 10단계에서 레벨 6부터 나타나 레벨 8에 다 올라온다. scene 은 배경 단계 번호(1~6, 10).
        /// </summary>
        internal static double RiseFraction(RiseKind kind, int scene, int level)
        {
            int from, full;
            switch (kind)
            {
                case RiseKind.Ground:
                    from = 1;
                    full = scene >= 10 ? 3 : scene == 6 ? 4 : 8;
                    break;
                case RiseKind.Flyer:
                    if (scene < 6) return 0;
                    if (scene >= 10) { from = 2; full = 5; }
                    else { from = 4; full = 8; }
                    break;
                default:
                    if (scene < 10) return 0;
                    from = 5; full = 8;
                    break;
            }
            return Math.Max(0, Math.Min(1, (double)(level - from) / (full - from)));
        }

        /// <summary>
        /// 썰렁이가 단계 수가 다른 채로 쓰기로 받아들인 짝(배경 세트 Id, 자판 key). 단계 수가 다른 짝을 빌드하기 전에는 썰렁이에게 알려야
        /// 하고(CLAUDE.md), 썰렁이가 "그냥 빌드해"라고 하면 여기 적는다 — 그러면 불일치 검사(dotnet test)가 그 짝은 넘어간다.
        /// </summary>
        internal static readonly IReadOnlyList<(string Background, string Keyboard)> AcceptedStageMismatches =
            new (string Background, string Keyboard)[0];

        /// <summary>모든 배경 세트 × 오락이 있는 모든 자판에서, 배경 단계 수와 오락 단계 수가 다른 짝.</summary>
        internal static List<(BackgroundSet Set, IStageSet Keyboard, int BackgroundStages, int GameStages)> StageCountMismatches()
        {
            var list = new List<(BackgroundSet, IStageSet, int, int)>();
            foreach (IStageSet keyboard in StageSets.All)
            {
                int games = GameStages.For(keyboard).Count;
                if (games == 0) continue;
                foreach (BackgroundSet bg in All)
                {
                    int n = StageSequence(bg, keyboard.Key).Count;
                    if (n != games) list.Add((bg, keyboard, n, games));
                }
            }
            return list;
        }
    }
}
