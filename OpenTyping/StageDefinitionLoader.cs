using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace OpenTyping
{
    /// <summary>
    /// 자판별 '연습 단계 정의' JSON을 읽어 <see cref="PracticeStage"/> 목록으로 만든다 (<260724_1>(3)).
    /// 실행 파일 옆의 파일(사용자 편집 가능)을 먼저 보고, 없으면 어셈블리 내장 리소스로 폴백한다.
    /// 키는 자모/기호 문자열로 적고(각 문자 1키), 순3·순4의 allowed·required는 명시(선택적)한다.
    /// 13단계 같은 "연습한 키 전체" 라운드는 앞 단계들을 계산해 합집합을 낸다.
    /// </summary>
    public static class StageDefinitionLoader
    {
        /// <summary>파일(실행 파일 옆) → 내장 리소스 순으로 시도해 단계 목록을 만든다. 실패 시 빈 목록.</summary>
        public static List<PracticeStage> Load(string relativeFilePath, string resourceSuffix)
        {
            // 1) 실행 파일 옆 파일 우선(재빌드 없이 편집 가능)
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, relativeFilePath);
                if (File.Exists(path))
                {
                    List<PracticeStage> fromFile = Parse(File.ReadAllText(path));
                    // 문법은 맞지만 구조가 잘못돼(예: "stages" 키 오타·빈 배열) 단계가 하나도 안
                    // 나오면 그대로 쓰지 않고 아래 내장 리소스로 폴백한다 — 안 그러면 자리연습
                    // 화면이 통째로 빈다.
                    if (fromFile.Count > 0) return fromFile;
                }
            }
            catch { /* 파일 문제 → 내장 리소스로 폴백 */ }

            // 2) 내장 리소스 폴백(항상 존재)
            try
            {
                string text = EmbeddedResource.ReadText(typeof(StageDefinitionLoader).Assembly, resourceSuffix);
                if (text != null) return Parse(text);
            }
            catch { /* 최후엔 빈 목록 */ }

            return new List<PracticeStage>();
        }

        public static List<PracticeStage> Parse(string json)
        {
            var result = new List<PracticeStage>();
            JObject root = JObject.Parse(json);
            if (!(root["stages"] is JArray stagesArr)) return result;

            int index = 0;
            foreach (JToken st in stagesArr)
            {
                index++;
                int stageNumber = index; // 제시어 목록(PracticeWordList)은 1-based 단계 번호로 조회
                string name = (string)st["name"] ?? (stageNumber + "단계");
                int game = st["game"] != null ? (int)st["game"] : 0;

                var rounds = new List<PracticeRound>();
                if (st["rounds"] is JArray roundsArr)
                    foreach (JToken r in roundsArr)
                    {
                        PracticeRound round = BuildRound(r, stageNumber, result);
                        if (round != null) rounds.Add(round);
                    }

                result.Add(new PracticeStage(name, rounds, game));
            }
            return result;
        }

        // earlierStages: 지금까지 만들어진 앞 단계들(특수 kind가 "연습한 키 전체"를 계산할 때 참조).
        private static PracticeRound BuildRound(JToken r, int stageNumber, List<PracticeStage> earlierStages)
        {
            string kind = ((string)r["kind"] ?? "").Trim().ToLowerInvariant();
            switch (kind)
            {
                case "sequential":
                    return PracticeRound.Sequential(KeysOf((string)r["keys"]),
                        (int?)r["repeat"] ?? 1, (bool?)r["shuffle"] ?? false);

                case "weighted":
                    return PracticeRound.Weighted(KeysOf((string)r["low"]), KeysOf((string)r["high"]),
                        (int?)r["count"] ?? 0);

                case "syllables":
                    return PracticeRound.Syllables(
                        Filter(PracticeWordList.Singles(stageNumber), JamoSet((string)r["allowed"]), JamoSet((string)r["required"])),
                        (int?)r["count"] ?? 0);

                case "words":
                    return PracticeRound.Words(
                        Filter(PracticeWordList.Words(stageNumber), JamoSet((string)r["allowed"]), JamoSet((string)r["required"])),
                        (int?)r["count"] ?? 0);

                case "sequential_all_practiced":
                {
                    var practiced = earlierStages
                        .SelectMany(s => s.Rounds.SelectMany(rd => rd.AllKeyItems()))
                        .Distinct().ToList();
                    return PracticeRound.Sequential(practiced, (int?)r["repeat"] ?? 1, (bool?)r["shuffle"] ?? true);
                }

                case "mixed_all_practiced":
                {
                    var symbols = earlierStages
                        .SelectMany(s => s.Rounds.SelectMany(rd => rd.AllKeyItems()))
                        .Where(IsSymbolOrNumber).Distinct().ToList();
                    var text = new List<string>();
                    text.AddRange(PracticeWordList.AllSingles());
                    text.AddRange(PracticeWordList.Words(stageNumber));
                    text = text.Where(w => w.All(HangulJamo.CanType)).Distinct().ToList();
                    return PracticeRound.Mixed(text, symbols, (int?)r["count"] ?? 0);
                }

                default:
                    return null; // 알 수 없는 kind는 건너뛴다
            }
        }

        // 자모/기호 문자열 → StageItem 목록(각 문자 1키). 키 하나로 입력되지 않는 문자는 건너뛴다.
        private static List<PracticeStage.StageItem> KeysOf(string keys)
        {
            var list = new List<PracticeStage.StageItem>();
            if (string.IsNullOrEmpty(keys)) return list;
            foreach (char ch in keys)
                if (HangulJamo.TryKey(ch, out KeyPos pos, out bool isShift))
                    list.Add(new PracticeStage.StageItem(pos.Row, pos.Column, isShift));
            return list;
        }

        private static HashSet<char> JamoSet(string s) =>
            string.IsNullOrEmpty(s) ? null : new HashSet<char>(s);

        // 순3·순4 제시어 필터: 모든 기본 자모가 allowed에 들고(allowed==null이면 제약 없음),
        // required가 비어 있지 않으면 하나 이상 포함.
        private static List<string> Filter(IEnumerable<string> pool, HashSet<char> allowed, HashSet<char> required)
        {
            var list = new List<string>();
            foreach (string w in pool)
            {
                HashSet<char> jamo = HangulJamo.AllBaseJamo(w);
                if (allowed != null && !jamo.All(allowed.Contains)) continue;
                if (required != null && required.Count > 0 && !jamo.Any(required.Contains)) continue;
                list.Add(w);
            }
            return list;
        }

        // 13단계 '특수 기호' 풀에 넣을 키인지: 숫자 행 전체 + 자모가 아닌 기호 키들.
        // 쌍자음(윗줄 0~4열 ㅃㅉㄸㄲㅆ)·ㅒㅖ는 자모이므로 제외(위치로만 판정, 윗글쇠 무관).
        private static bool IsSymbolOrNumber(PracticeStage.StageItem it)
        {
            KeyPos p = it.Pos;
            if (p.Row == 0) return true;                                        // 숫자 행 전체
            if (p.Row == 2 && (p.Column == 9 || p.Column == 10)) return true;   // ; :  /  ' "
            if (p.Row == 1 && (p.Column == 10 || p.Column == 11)) return true;  // [ {  /  ] }
            if (p.Row == 3 && (p.Column == 7 || p.Column == 8 || p.Column == 9)) return true; // , < . > / ?
            return false;
        }
    }
}
