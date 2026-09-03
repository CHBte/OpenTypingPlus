using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OpenTyping
{
    /// <summary>
    /// 오락(게임) 하나를 추상화한다 (<260724_1>(1)). 새 오락을 추가할 때는 이 인터페이스를 구현하고
    /// <see cref="ArcadeGames"/>에 등록하기만 하면, 자리연습 드롭다운·해금(StageRecords)·치트코드가
    /// 자동으로 그 게임을 함께 다룬다. 게임별 단계는 정수 id로 식별한다(= 자리연습 단계 번호).
    /// </summary>
    public interface IArcadeGame
    {
        /// <summary>저장·식별용 고유 키(예: "acidrain").</summary>
        string Id { get; }

        /// <summary>사람이 읽는 이름(예: "산성비 타자 오락").</summary>
        string Name { get; }

        /// <summary>이 게임이 제공하는 단계 id 목록.</summary>
        IReadOnlyList<int> StageIds { get; }

        /// <summary>단계 id의 표시 이름(드롭다운 항목 등).</summary>
        string StageName(int stageId);

        /// <summary>그 단계로 바로 진입하는 게임 창을 만든다.</summary>
        Window CreateWindow(int stageId);
    }

    /// <summary>등록된 모든 오락의 레지스트리. 새 게임은 여기 배열에만 추가하면 된다 (<260724_1>(1)).</summary>
    public static class ArcadeGames
    {
        public static readonly IReadOnlyList<IArcadeGame> All = new IArcadeGame[]
        {
            new AcidRainGame(),
            // 새 오락을 여기에 추가:  new SomeOtherGame(),
        };

        public static IArcadeGame Get(string id) => All.FirstOrDefault(g => g.Id == id);

        /// <summary>기본(대표) 오락. 자리연습 드롭다운이 아직 단일 게임을 가정할 때 쓴다.</summary>
        public static IArcadeGame Default => All.Count > 0 ? All[0] : null;
    }

    /// <summary>산성비 타자 오락을 <see cref="IArcadeGame"/>으로 감싼 것. 단계 메타는 game\words.json에서 읽는다.</summary>
    public sealed class AcidRainGame : IArcadeGame
    {
        public const string GameId = "acidrain";

        // words.json에 새 한글 키가 없는 단계(9~12)는 게임 단어 목록이 없어 제외돼 있다(id 1~8, 13).
        private static readonly int[] KnownStageIds = { 1, 2, 3, 4, 5, 6, 7, 8, 13 };

        private readonly List<AcidRainWindow.WordStage> stages = AcidRainWindow.LoadStageList();

        public string Id => GameId;
        public string Name => "산성비 타자 오락";

        public IReadOnlyList<int> StageIds =>
            stages.Count > 0 ? stages.Select(s => s.Id).ToList() : KnownStageIds;

        public string StageName(int stageId)
        {
            AcidRainWindow.WordStage s = stages.FirstOrDefault(w => w.Id == stageId);
            return s?.Name ?? (stageId + "단계");
        }

        public Window CreateWindow(int stageId) => new AcidRainWindow(stageId);
    }
}
