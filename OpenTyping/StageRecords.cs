using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace OpenTyping
{
    /// <summary>
    /// '자리연습' 단계별 최고 타(분당 타수) 기록의 단일 진실원 (<260724_2-1>). 각 단계를 마칠 때마다
    /// 그때의 분당 타수를 최고 기록으로 갱신한다. 여기서 두 가지를 파생한다:
    ///  - 자리연습 타일 활성: 1단계는 항상, 그 외는 '앞 단계 최고 기록이 목표 타수 이상'이어야 활성 (<260724_2-1>(2)).
    ///  - 오락(게임) 단계 해금: 그 단계(번호=오락 stage id)의 최고 기록이 목표 타수 이상이면 해금 (<260724_2>(1)).
    /// 저장 파일 이름을 새로 써서(stage_records.json) 이전 진행도 파일의 상태를 물려받지 않는다.
    /// </summary>
    public static class StageRecords
    {
        /// <summary>단계 활성·오락 해금에 필요한 분당 타수 기준.</summary>
        public const int PassThreshold = 200;

        private class Store
        {
            /// <summary>옛 형식 1(모두 열기 참/거짓). 읽을 때만 쓰고 새로 쓰지는 않는다.</summary>
            [JsonProperty("cheat", NullValueHandling = NullValueHandling.Ignore)]
            public bool? Cheat { get; set; }

            /// <summary>옛 형식 2(0 = 꺼짐). 읽을 때만 쓴다.</summary>
            [JsonProperty("cheatStages", NullValueHandling = NullValueHandling.Ignore)]
            public int? CheatStagesLegacy { get; set; }

            /// <summary>
            /// 치트로 '통과한 것으로 치는' 마지막 단계 번호 (<260812_10>, <260812_10-2>).
            /// null = 치트 꺼짐(실제 기록을 따름), 0 = 켜짐이되 한 단계도 통과하지 않은 것으로.
            /// </summary>
            [JsonProperty("cheatUpTo", NullValueHandling = NullValueHandling.Ignore)]
            public int? CheatUpTo { get; set; }

            [JsonProperty("records")] public Dictionary<int, int> Records { get; set; } = new Dictionary<int, int>();
        }

        private static readonly string FilePath = BuildPath();
        private static readonly Store Data = Load();

        private static string BuildPath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OTP", "OpenTypingPlus");
            try { Directory.CreateDirectory(dir); } catch { /* 실패해도 메모리 상 진행도는 유지 */ }
            return Path.Combine(dir, "stage_records.json");
        }

        /// <summary>이 단계의 최고 분당 타수(기록 없으면 0).</summary>
        public static int BestTa(int stageNumber) =>
            Data.Records.TryGetValue(stageNumber, out int ta) ? ta : 0;

        /// <summary>이 단계에 완주 기록이 있는지.</summary>
        public static bool HasRecord(int stageNumber) => Data.Records.ContainsKey(stageNumber);

        /// <summary>
        /// 단계를 통과(최고 기록 ≥ 목표 타수)한 적이 있는지.
        ///
        /// 치트가 걸려 있으면 실제 기록을 보지 않고 **치트가 정한 단계까지만** 통과로 친다
        /// (<260812_10->). 실제 기록과 OR 로 합치면 이미 통과한 단계를 낮춰 지정해도 그대로 열려
        /// 있어(예: 2단계를 통과한 상태에서 '1단계까지'로 낮춰도 3단계 타일이 활성) 치트가 '상태를
        /// 정하는' 도구 노릇을 못 한다. 기록 자체는 건드리지 않으므로 치트를 끄면 그대로 돌아온다.
        /// </summary>
        public static bool IsPassed(int stageNumber) =>
            Data.CheatUpTo.HasValue ? stageNumber <= Data.CheatUpTo.Value
                                    : BestTa(stageNumber) >= PassThreshold;

        /// <summary>자리연습 타일 활성: 1단계는 항상, 그 외는 앞 단계를 통과했을 때 (<260724_2-1>(2)).</summary>
        public static bool IsPracticeStageActive(int stageNumber) =>
            stageNumber <= 1 || IsPassed(stageNumber - 1);

        /// <summary>오락 단계(stage id) 해금: 같은 번호의 자리연습 단계를 통과했으면 (<260724_2>(1)).</summary>
        public static bool IsGameStageUnlocked(int stageId) => IsPassed(stageId);

        /// <summary>이 단계 완주 시의 분당 타수를 최고 기록으로 반영한다(더 높을 때만 갱신).</summary>
        public static void Record(int stageNumber, int ta)
        {
            if (ta <= BestTa(stageNumber)) return;
            Data.Records[stageNumber] = ta;
            Save();
        }

        /// <summary>지금 치트가 걸려 있는지(실제 기록 대신 치트 값을 따르는 상태인지).</summary>
        public static bool CheatOn => Data.CheatUpTo.HasValue;

        /// <summary>치트로 통과 처리한 마지막 단계 번호. 꺼져 있으면 null (<260812_10-2>).</summary>
        public static int? CheatUpTo => Data.CheatUpTo;

        /// <summary>
        /// 치트코드 (<260724_2>(3), <260812_10>, <260812_10-2>): 몇 단계까지 통과한 것으로 칠지 정한다.
        /// null 을 주면 치트를 끄고 실제 기록으로 돌아간다. 0 은 '한 단계도 통과하지 않은 것으로'다.
        /// 이 값은 프로그램 폴더가 아니라 %APPDATA%\OpenTypingPlus\stage_records.json 에 남아 다시
        /// 빌드·배포해도 유지되므로, 끄는 길을 앱 안에 반드시 두어야 한다(같은 코드를 다시 입력).
        /// 어느 쪽이든 실제 최고 기록(records)은 건드리지 않는다.
        /// </summary>
        public static void SetCheatUpTo(int? stages)
        {
            Data.CheatUpTo = stages.HasValue && stages.Value < 0 ? 0 : stages;
            cheatUpToDirty = true;   // 지금 이 값이 방금 사용자가 고른 새 값임을 표시한다
            Save();
        }

        /// <summary>
        /// true면 Data.CheatUpTo 가 이 프로세스에서 방금 SetCheatUpTo 로 바꾼 새 값이라, 저장할 때
        /// 디스크 값을 따르지 않고 이 값을 그대로 쓴다. Save() 가 끝나면 다시 false로 돌아간다.
        /// </summary>
        private static bool cheatUpToDirty;

        private static Store Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    Store s = JsonConvert.DeserializeObject<Store>(File.ReadAllText(FilePath));
                    if (s != null)
                    {
                        if (s.Records == null) s.Records = new Dictionary<int, int>();
                        // 옛 형식을 새 형식으로 옮긴다 (<260812_10>, <260812_10-2>).
                        //  - "cheat": true      → 모두 열기
                        //  - "cheatStages": n>0 → n단계까지 (0 은 그 시절 '꺼짐'이었으므로 꺼짐 그대로)
                        if (!s.CheatUpTo.HasValue)
                        {
                            if (s.CheatStagesLegacy > 0) s.CheatUpTo = s.CheatStagesLegacy;
                            else if (s.Cheat == true) s.CheatUpTo = int.MaxValue;
                        }
                        s.Cheat = null;
                        s.CheatStagesLegacy = null;
                        return s;
                    }
                }
            }
            catch { /* 손상 시 빈 기록으로 시작 */ }
            return new Store();
        }

        private static void Save()
        {
            try
            {
                // 창을 여러 개(또는 앱을 두 번) 띄운 상태에서 서로 다른 단계를 통과시키면, 나중에
                // 저장하는 쪽이 메모리 스냅샷 전체로 파일을 덮어써 먼저 저장된 기록이 사라질 수 있다.
                // 저장 직전 디스크의 최신 기록과 병합한다(더 높은 타만 반영 — Record()와 같은 정책).
                MergeRecordsFromDisk();
                MergeCheatUpToFromDisk();

                AtomicFile.WriteText(FilePath, JsonConvert.SerializeObject(Data));

                cheatUpToDirty = false;
            }
            catch { /* 저장 실패는 조용히 무시 */ }
        }

        private static void MergeRecordsFromDisk()
        {
            if (!File.Exists(FilePath)) return;
            try
            {
                Store onDisk = JsonConvert.DeserializeObject<Store>(File.ReadAllText(FilePath));
                if (onDisk?.Records == null) return;
                foreach (KeyValuePair<int, int> kv in onDisk.Records)
                {
                    if (!Data.Records.TryGetValue(kv.Key, out int mine) || kv.Value > mine)
                        Data.Records[kv.Key] = kv.Value;
                }
            }
            catch { /* 병합 실패 시 메모리 값 그대로 저장 */ }
        }

        /// <summary>
        /// Records 와 달리 CheatUpTo 는 '더 큰 값이 이긴다'는 규칙이 없는 사용자 선택값이라, 지금
        /// 이 프로세스가 SetCheatUpTo 로 방금 바꾼 게 아니라면(cheatUpToDirty == false) 디스크에
        /// 있는 값을 그대로 따른다 — 안 그러면 다른 창·프로세스가 방금 바꾼 치트 값을, 단순히 기록만
        /// 저장하려던 이 프로세스의 오래된 메모리 값이 조용히 덮어쓸 수 있다.
        /// </summary>
        private static void MergeCheatUpToFromDisk()
        {
            if (cheatUpToDirty) return;
            if (!File.Exists(FilePath)) return;
            try
            {
                Store onDisk = JsonConvert.DeserializeObject<Store>(File.ReadAllText(FilePath));
                if (onDisk == null) return;

                int? onDiskCheatUpTo = onDisk.CheatUpTo;
                if (!onDiskCheatUpTo.HasValue) // 옛 형식만 남아 있는 경우도 같은 우선순위로 계산
                {
                    if (onDisk.CheatStagesLegacy > 0) onDiskCheatUpTo = onDisk.CheatStagesLegacy;
                    else if (onDisk.Cheat == true) onDiskCheatUpTo = int.MaxValue;
                }
                Data.CheatUpTo = onDiskCheatUpTo;
            }
            catch { /* 병합 실패 시 메모리 값 그대로 저장 */ }
        }
    }
}
