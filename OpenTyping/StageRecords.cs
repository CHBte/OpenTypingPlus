using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace OpenTyping
{
    /// <summary>
    /// '자리연습' 단계별 최고 타(분당 타수) 기록의 단일 진실원 (<260724_2-1>). 각 단계를 마칠 때마다
    /// 그때의 분당 타수를 최고 기록으로 갱신한다. 여기서 두 가지를 파생한다:
    ///  - 자리연습 타일 활성: 1단계는 항상, 그 외는 '앞 단계 최고 기록이 그 단계의 목표 타수 이상'이어야 활성.
    ///  - 오락(게임) 단계 해금: 그 단계(번호=오락 stage id)의 최고 기록이 목표 타수 이상이면 해금.
    ///
    /// <260927_3>(0.4)(0.5)(0.5.1): 목표 타수는 단계마다 다르며(<see cref="PracticeStage.TargetTa"/>),
    /// 판정은 반드시 <see cref="TargetTa"/>를 거친다. 기록은 자판 묶음(<see cref="IStageSet.Key"/>)마다
    /// 따로 저장한다(stage_records_ko.json / stage_records_en.json). 옛 단계 체계의 stage_records.json 은
    /// 새 이름의 파일을 쓰므로 읽지 않는다 — 새 체계를 처음 실행할 때 기록이 한 번 초기화되고, 그 뒤로는
    /// 새 파일의 기록이 그대로 유지된다.
    ///
    /// 아래의 정적 메서드는 모두 **지금 '설정'에 지정된 자판**(<see cref="StageSets.Current"/>)의 기록을
    /// 다룬다(<260927_5>(0), <260927_6>(0.1): 치트도 그 자판의 기록에만 적용).
    ///
    /// <2600919_3-1>: 치트는 디스크에 전혀 쓰지 않는 순수 메모리 상태다. 치트가 켜져 있는 동안의
    /// 모든 변경(Record 호출)은 메모리에만 반영되고 저장되지 않는다. 치트를 끄면("원래대로") 디스크에서
    /// 기록을 다시 읽어 그 사이의 변경을 전부 버린다. 프로그램만 종료했다가 다시 켜도 같은 결과가 된다.
    /// </summary>
    public static class StageRecords
    {
        private sealed class Store
        {
            [JsonProperty("records")] public Dictionary<int, int> Records { get; set; } = new Dictionary<int, int>();
        }

        /// <summary>한 자판 묶음의 기록과 치트 상태.</summary>
        private sealed class Book
        {
            public string FilePath;
            public Store Data;
            public int? CheatUpTo;   // null = 치트 꺼짐, 0 = 켜짐이되 한 단계도 통과하지 않은 것으로
        }

        private static readonly Dictionary<string, Book> Books = new Dictionary<string, Book>();

        private static Book Current => BookFor(StageSets.Current.Key);

        private static Book BookFor(string key)
        {
            if (!Books.TryGetValue(key, out Book b))
            {
                string path = BuildPath("stage_records_" + key + ".json");
                b = new Book { FilePath = path, Data = Load(path) };
                Books[key] = b;
            }
            return b;
        }

        internal static string RecordsDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OTP", "OpenTypingPlus");

        private static string BuildPath(string fileName)
        {
            string dir = RecordsDirectory;
            try { Directory.CreateDirectory(dir); } catch { /* 실패해도 메모리 상 진행도는 유지 */ }
            return Path.Combine(dir, fileName);
        }

        /// <summary>이 단계의 목표 타수(지금 자판의 단계 정의에서). 범위 밖이면 기본값.</summary>
        public static int TargetTa(int stageNumber)
        {
            IReadOnlyList<PracticeStage> stages = StageSets.Current.Stages;
            return stageNumber >= 1 && stageNumber <= stages.Count
                ? stages[stageNumber - 1].TargetTa
                : StageDefinitionLoader.DefaultTargetTa;
        }

        /// <summary>이 단계의 최고 분당 타수(기록 없으면 0).</summary>
        public static int BestTa(int stageNumber) =>
            Current.Data.Records.TryGetValue(stageNumber, out int ta) ? ta : 0;

        /// <summary>이 단계에 완주 기록이 있는지.</summary>
        public static bool HasRecord(int stageNumber) => Current.Data.Records.ContainsKey(stageNumber);

        /// <summary>
        /// 단계를 통과(최고 기록 ≥ 목표 타수)한 적이 있는지. 치트가 걸려 있으면 실제 기록을 보지 않고
        /// **치트가 정한 단계까지만** 통과로 친다 (<260812_10->).
        /// </summary>
        public static bool IsPassed(int stageNumber)
        {
            Book b = Current;
            return b.CheatUpTo.HasValue ? stageNumber <= b.CheatUpTo.Value
                                        : BestTa(stageNumber) >= TargetTa(stageNumber);
        }

        /// <summary>자리연습 타일 활성: 1단계는 항상, 그 외는 앞 단계를 통과했을 때 (<260724_2-1>(2)).</summary>
        public static bool IsPracticeStageActive(int stageNumber) =>
            stageNumber <= 1 || IsPassed(stageNumber - 1);

        /// <summary>오락 단계(stage id) 해금: 같은 번호의 자리연습 단계를 통과했으면 (<260724_2>(1)).</summary>
        public static bool IsGameStageUnlocked(int stageId) => IsPassed(stageId);

        /// <summary>
        /// 이 단계 완주 시의 분당 타수를 최고 기록으로 반영한다(더 높을 때만 갱신).
        /// 치트가 켜진 동안은 메모리에만 반영하고 디스크에 저장하지 않는다 (<2600919_3-1>).
        /// </summary>
        public static void Record(int stageNumber, int ta)
        {
            if (ta <= BestTa(stageNumber)) return;
            Book b = Current;
            b.Data.Records[stageNumber] = ta;
            if (!b.CheatUpTo.HasValue) Save(b);
        }

        /// <summary>지금 자판에 치트가 걸려 있는지.</summary>
        public static bool CheatOn => Current.CheatUpTo.HasValue;

        /// <summary>지금 자판의 치트로 통과 처리한 마지막 단계 번호. 꺼져 있으면 null (<260812_10-2>).</summary>
        public static int? CheatUpTo => Current.CheatUpTo;

        /// <summary>
        /// 치트코드 (<260724_2>(3), <260812_10>, <260812_10-2>, <2600919_3-1>): 지금 자판에서 몇 단계까지
        /// 통과한 것으로 칠지 정한다. null을 주면 치트를 끄고 디스크의 진짜 기록으로 되돌린다("원래대로").
        /// 0은 '한 단계도 통과하지 않은 것으로'다.
        /// </summary>
        public static void SetCheatUpTo(int? stages)
        {
            Book b = Current;
            bool turningOff = !stages.HasValue && b.CheatUpTo.HasValue;

            b.CheatUpTo = stages.HasValue && stages.Value < 0 ? 0 : stages;

            if (turningOff) b.Data = Load(b.FilePath);

            CheatChanged?.Invoke();
        }

        /// <summary>치트 켜짐/꺼짐·설정값이 바뀌거나, 자판이 바뀌어 '지금 자판의 치트 상태'가 달라질 수
        /// 있을 때 알린다(<2600919_4> 창 테두리·제목표시줄 색). 구독자는 CheatOn 을 다시 읽는다.</summary>
        public static event Action CheatChanged;

        /// <summary>설정에서 자판이 바뀌었을 때 부른다 — 치트 표시(테마)를 새 자판 기준으로 맞춘다.</summary>
        public static void NotifyLayoutChanged() => CheatChanged?.Invoke();

        private static Store Load(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    Store s = JsonConvert.DeserializeObject<Store>(File.ReadAllText(path));
                    if (s != null)
                    {
                        if (s.Records == null) s.Records = new Dictionary<int, int>();
                        return s;
                    }
                }
            }
            catch { /* 손상 시 빈 기록으로 시작 */ }
            return new Store();
        }

        private static void Save(Book b)
        {
            try
            {
                // 창을 여러 개(또는 앱을 두 번) 띄운 상태에서 서로 다른 단계를 통과시키면, 나중에
                // 저장하는 쪽이 먼저 저장된 기록을 지울 수 있다. 저장 직전 디스크의 최신 기록과
                // 병합한다(더 높은 타만 반영). 치트 중엔 Record()가 Save()를 부르지 않으므로 이 병합은
                // 항상 진짜 진행도끼리만 일어난다.
                Store onDisk = Load(b.FilePath);
                foreach (KeyValuePair<int, int> kv in onDisk.Records)
                {
                    if (!b.Data.Records.TryGetValue(kv.Key, out int mine) || kv.Value > mine)
                        b.Data.Records[kv.Key] = kv.Value;
                }
                AtomicFile.WriteText(b.FilePath, JsonConvert.SerializeObject(b.Data));
            }
            catch { /* 저장 실패는 조용히 무시 */ }
        }
    }
}
