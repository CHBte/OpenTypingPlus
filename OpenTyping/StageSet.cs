using System.Collections.Generic;
using System.Linq;

namespace OpenTyping
{
    /// <summary>
    /// 한 자판(예: '두벌식 표준')의 연습 단계 묶음 (<260724_1>(3)). 영어 등 다른 자판을 추가할 때는
    /// 이 인터페이스를 구현(보통 JSON 하나)해 <see cref="StageSets"/>에 등록하면 된다.
    /// </summary>
    public interface IStageSet
    {
        string LayoutName { get; }
        IReadOnlyList<PracticeStage> Stages { get; }
    }

    /// <summary>JSON 파일/내장 리소스로 정의된 단계 묶음.</summary>
    public sealed class JsonStageSet : IStageSet
    {
        public string LayoutName { get; }
        public IReadOnlyList<PracticeStage> Stages { get; }

        public JsonStageSet(string layoutName, string relativeFilePath, string resourceSuffix)
        {
            LayoutName = layoutName;
            Stages = StageDefinitionLoader.Load(relativeFilePath, resourceSuffix);
        }
    }

    /// <summary>자판 이름 → 단계 묶음 레지스트리. 새 자판은 여기에 추가한다.</summary>
    public static class StageSets
    {
        // 자판 이름은 layouts\*.json 의 "Name" 과 같아야 한다 (<260812_8>로 바뀐 이름).
        public static readonly IStageSet DubeolsikStandard =
            new JsonStageSet(KeyPracticeMenu.StageLayoutName, "stages/dubeolsik_standard.json", "dubeolsik_standard.json");

        private static readonly IStageSet[] All = { DubeolsikStandard };

        public static IStageSet ForLayout(string layoutName) =>
            All.FirstOrDefault(s => s.LayoutName == layoutName);
    }
}
