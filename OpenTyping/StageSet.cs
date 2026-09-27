using System.Collections.Generic;
using System.Linq;

namespace OpenTyping
{
    /// <summary>
    /// 한 자판(예: '두벌식 표준 한글')의 연습 단계 묶음 (<260724_1>(3)). 단계 정의 파일(stages\*.json) 하나에
    /// 대응하며, 단계 수·타일·목표 타수·수준별 알파벳·오락 단어 구성이 모두 그 파일에서 온다.
    /// </summary>
    public interface IStageSet
    {
        /// <summary>layouts\*.json 의 "Name" 과 같은 자판 이름.</summary>
        string LayoutName { get; }

        /// <summary>기록 파일 이름 등에 쓰는 짧은 키("ko"/"en") (<260927_3>(0.5.1)).</summary>
        string Key { get; }

        /// <summary>제시어 목록(<see cref="WordCatalog"/>)의 묶음 이름.</summary>
        string WordSection { get; }

        /// <summary>문구에 쓰는 문자 이름("한글"/"영문").</summary>
        string ScriptName { get; }

        /// <summary>자리연습 화면 타일: 행당 개수와 왼쪽부터의 색(#RRGGBB).</summary>
        int TilesPerRow { get; }
        IReadOnlyList<string> TileColors { get; }

        /// <summary>1단계 창의 검지 자리 안내 문구(없으면 null → 창의 기본 문구).</summary>
        string IndexGuide { get; }

        /// <summary>오락 단어 규칙: 이 개수마다 드문 목록 1개, 이 개수 단위로 중복 금지 (<260927_4>).</summary>
        int GameRareEvery { get; }
        int GameUniqueUnit { get; }

        /// <summary>이 자판의 '글자 → 키 입력' 규칙(자판 파일에서 만든다).</summary>
        KeyboardMap Keyboard { get; }

        IReadOnlyList<PracticeStage> Stages { get; }
    }

    /// <summary>단계 정의 파일 하나로 정의된 단계 묶음. 맨 위 설정은 바로, 단계는 처음 쓸 때 읽는다.</summary>
    public sealed class JsonStageSet : IStageSet
    {
        private readonly StageSetMeta meta;
        private IReadOnlyList<PracticeStage> stages;

        public JsonStageSet(StageSetMeta meta) { this.meta = meta; }

        public string LayoutName => meta.LayoutName;
        public string Key => meta.Key;
        public string WordSection => meta.WordSection;
        public string ScriptName => meta.ScriptName;
        public int TilesPerRow => meta.TilesPerRow;
        public IReadOnlyList<string> TileColors => meta.TileColors;
        public string IndexGuide => meta.IndexGuide;
        public int GameRareEvery => meta.RareEvery;
        public int GameUniqueUnit => meta.UniqueUnit;
        internal int Order => meta.Order;
        internal string FileName => meta.FileName;

        public KeyboardMap Keyboard => KeyboardMaps.For(meta.LayoutName);

        public IReadOnlyList<PracticeStage> Stages =>
            stages ?? (stages = StageDefinitionLoader.Load(meta.FileName, meta.WordSection, meta.LayoutName));

        /// <summary>단계 정의를 다시 읽게 한다(<see cref="StageSets.ResetCaches"/>).</summary>
        internal void Reload() => stages = null;
    }

    /// <summary>
    /// 자판 이름 → 단계 묶음 레지스트리. 실행 파일 옆 stages\*.json 과 내장 리소스의 단계 정의 파일을
    /// 모두 찾아 등록하므로, 새 자판의 단계는 파일만 더하면 된다(코드 수정 불필요).
    /// </summary>
    public static class StageSets
    {
        public const string QwertyLayoutName = "QWERTY 영문";

        public static readonly IReadOnlyList<IStageSet> All = Discover();

        private static IReadOnlyList<IStageSet> Discover()
        {
            var metas = new List<StageSetMeta>();
            foreach (string file in StageDefinitionLoader.DiscoverFileNames())
            {
                StageSetMeta m = StageDefinitionLoader.ReadMeta(file);
                if (m != null) metas.Add(m);
            }

            // 같은 자판을 가리키는 파일이 둘이면 먼저 찾은 것(order 순)을 쓴다.
            List<StageSetMeta> chosen = metas.OrderBy(m => m.Order).ThenBy(m => m.FileName)
                                             .GroupBy(m => m.LayoutName).Select(g => g.First()).ToList();

            // "key"는 기록·오락 기록 파일 이름과 캐시의 열쇠라, 파일을 복사해 layout만 바꾸고 key를 그대로
            // 두면 두 자판이 기록·치트를 함께 쓰게 된다. 기본 제공(내장) 파일이 먼저 그 key를 갖고 — 그래야
            // 기존 기록이 복사본 쪽으로 넘어가지 않는다 — 그다음은 order·파일 이름 순이다. 뒤에 온 쪽은 파일
            // 이름으로 바꾼다(그것도 겹치면 번호를 붙인다). 파일 시스템처럼 대소문자는 구분하지 않는다.
            var usedKeys = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (StageSetMeta m in chosen.OrderByDescending(m => StageDefinitionLoader.IsBundled(m.FileName)))
            {
                if (usedKeys.Add(m.Key)) continue;
                string baseKey = System.IO.Path.GetFileNameWithoutExtension(m.FileName);
                string key = baseKey;
                for (int n = 2; !usedKeys.Add(key); n++) key = baseKey + "_" + n;
                m.Key = key;
            }
            return chosen.Select(m => (IStageSet)new JsonStageSet(m)).ToList();
        }

        /// <summary>
        /// 설정에서 자판(또는 자판 데이터 폴더)이 바뀌면 부른다: 옛 자판 파일로 만들어 둔 '글자 → 키 입력'
        /// 규칙·단계·오락 단어를 버려, 다음에 쓸 때 새 자판 파일로 다시 만들게 한다(같은 이름인데 배치가
        /// 다른 자판을 고른 경우에도 제시어와 렌더링 키보드가 어긋나지 않게).
        /// </summary>
        internal static void ResetCaches()
        {
            KeyboardMaps.ClearCache();
            GameStages.ClearCache();
            foreach (IStageSet s in All)
                if (s is JsonStageSet js) js.Reload();
        }

        public static IStageSet ForLayout(string layoutName) =>
            All.FirstOrDefault(s => s.LayoutName == layoutName);

        /// <summary>두벌식 표준 한글 묶음(기본값·검사용).</summary>
        public static IStageSet DubeolsikStandard => ForLayout(KeyPracticeMenu.StageLayoutName) ?? All.FirstOrDefault();

        /// <summary>QWERTY 영문 묶음(검사용).</summary>
        public static IStageSet QwertyEnglish => ForLayout(QwertyLayoutName);

        /// <summary>
        /// 지금 '설정'에 지정된 자판의 단계 묶음. 단계가 없는 자판(Dvorak·세벌식 등)이거나 자판을
        /// 아직 읽지 않았으면 두벌식 표준 한글로 친다 — 기록·치트가 늘 어느 한 묶음을 가리키게.
        /// </summary>
        public static IStageSet Current =>
            ForLayout(MainWindow.CurrentKeyLayout?.Name) ?? DubeolsikStandard;

        /// <summary>이 자판에 단계 타일 UI가 있는가. 단계 정의 파일은 있어도 단계가 하나도 만들어지지
        /// 않았으면(내장 예비본도 없는 사용자 추가 파일이 깨진 경우) 기존 키 선택 UI를 쓴다 — 안 그러면
        /// '자리연습' 화면이 통째로 빈다.</summary>
        public static bool HasStages(string layoutName) => ForLayout(layoutName)?.Stages.Count > 0;
    }
}
