using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OpenTyping
{
    /// <summary>
    /// 오락(게임) 하나를 추상화한다 (<260724_1>(1), <260927_4>). 새 오락을 추가할 때는 이 인터페이스를
    /// 구현하고 <see cref="ArcadeGames"/>에 등록하기만 하면, 자리연습 드롭다운·해금(StageRecords)·치트코드가
    /// 자동으로 그 게임을 함께 다룬다. 게임별 단계는 정수 id로 식별한다(= 자리연습 단계 번호).
    /// 단계 구성과 단어는 자판 묶음(<see cref="IStageSet"/>)마다 다르며, 게임과 무관한 공통 정의
    /// (<see cref="GameStages"/>)에서 가져올 수 있다.
    /// </summary>
    public interface IArcadeGame
    {
        /// <summary>저장·식별용 고유 키(예: "acidrain").</summary>
        string Id { get; }

        /// <summary>사람이 읽는 이름(예: "산성비 타자 오락").</summary>
        string Name { get; }

        /// <summary>이 자판에서 이 게임이 제공하는 단계 id 목록.</summary>
        IReadOnlyList<int> StageIdsFor(IStageSet set);

        /// <summary>단계 id의 표시 이름(드롭다운 항목 등).</summary>
        string StageName(IStageSet set, int stageId);

        /// <summary>그 자판·단계로 바로 진입하는 게임 창을 만든다.</summary>
        Window CreateWindow(IStageSet set, int stageId);
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

    /// <summary>산성비 타자 오락을 <see cref="IArcadeGame"/>으로 감싼 것. 단계·단어는 <see cref="GameStages"/>에서.</summary>
    public sealed class AcidRainGame : IArcadeGame
    {
        public const string GameId = "acidrain";

        public string Id => GameId;
        public string Name => "산성비 타자 오락";

        public IReadOnlyList<int> StageIdsFor(IStageSet set) => GameStages.For(set).Select(s => s.Id).ToList();

        public string StageName(IStageSet set, int stageId) =>
            GameStages.For(set).FirstOrDefault(s => s.Id == stageId)?.Name ?? (stageId + "단계");

        public Window CreateWindow(IStageSet set, int stageId) => new AcidRainWindow(set, stageId);
    }
}
