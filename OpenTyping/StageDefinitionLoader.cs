using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace OpenTyping
{
    /// <summary>
    /// 단계 정의 파일(stages\*.json) 하나의 맨 위 설정(자판 묶음 정보·타일·오락 규칙 등).
    /// 형식 설명은 각 파일의 "_comment"에 있다.
    /// </summary>
    public sealed class StageSetMeta
    {
        public string FileName;
        public string LayoutName;
        public string Key;
        public string WordSection;
        public string ScriptName;
        public int Order;
        public int TilesPerRow = 5;
        public IReadOnlyList<string> TileColors = new[] { "#F03E3E", "#1C7ED6", "#37B24D", "#F76707", "#F59F00" };
        public string IndexGuide;
        public int RareEvery = 10;
        public int UniqueUnit = 5;
    }

    /// <summary>
    /// 자판별 '연습 단계 정의' JSON을 읽어 <see cref="PracticeStage"/> 목록으로 만든다 (<260724_1>(3), <260927_3>).
    /// 실행 파일 옆의 파일(개발자 편집 가능)을 먼저 보고, 없거나 깨졌으면 어셈블리 내장 리소스로 폴백한다.
    /// 키는 글쇠 문자열로 적는다(각 문자 1키). 수준별 알파벳은 각 단계의 "words"가 정하고, 단어는
    /// 제시어 목록(<see cref="WordCatalog"/>)에서 가져온다. 형식 설명은 각 파일의 "_comment"에 있다.
    /// </summary>
    public static class StageDefinitionLoader
    {
        /// <summary>target_ta 가 없거나 잘못 적힌 단계의 목표 타수.</summary>
        public const int DefaultTargetTa = 200;

        internal const string ResourceFolderMarker = ".stages.";

        /// <summary>단계 정의 파일의 원문. 실행 파일 옆 stages\ → 내장 리소스 순. <paramref name="preferFile"/>가
        /// false면 내장 리소스만 본다(파일이 깨졌을 때의 폴백용).</summary>
        internal static string ReadText(string fileName, bool preferFile = true)
        {
            if (preferFile)
            {
                try
                {
                    string path = Path.Combine(AppContext.BaseDirectory, "stages", fileName);
                    if (File.Exists(path)) return File.ReadAllText(path);
                }
                catch { /* 파일 문제 → 내장 리소스로 */ }
            }
            try { return EmbeddedResource.ReadText(typeof(StageDefinitionLoader).Assembly, ResourceFolderMarker + fileName); }
            catch { return null; }
        }

        /// <summary>실행 파일 옆과 내장 리소스에 있는 단계 정의 파일 이름들(중복 없음).</summary>
        internal static IReadOnlyList<string> DiscoverFileNames()
        {
            var names = new List<string>();
            try
            {
                string dir = Path.Combine(AppContext.BaseDirectory, "stages");
                if (Directory.Exists(dir))
                    names.AddRange(Directory.GetFiles(dir, "*.json").Select(Path.GetFileName));
            }
            catch { /* 폴더를 못 읽으면 내장 리소스만 */ }
            foreach (string res in typeof(StageDefinitionLoader).Assembly.GetManifestResourceNames())
            {
                int i = res.IndexOf(ResourceFolderMarker, StringComparison.OrdinalIgnoreCase);
                if (i >= 0 && res.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    names.Add(res.Substring(i + ResourceFolderMarker.Length));
            }
            return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>이 이름의 단계 정의 파일이 프로그램에 내장돼 있는가(기본 제공 파일인가).</summary>
        internal static bool IsBundled(string fileName) =>
            typeof(StageDefinitionLoader).Assembly.GetManifestResourceNames()
                .Any(r => r.EndsWith(ResourceFolderMarker + fileName, StringComparison.OrdinalIgnoreCase));

        /// <summary>맨 위 설정만 읽는다(단계는 읽지 않음). layout·word_section이 없으면 null.</summary>
        internal static StageSetMeta ReadMeta(string fileName)
        {
            foreach (bool preferFile in new[] { true, false })
            {
                try
                {
                    string text = ReadText(fileName, preferFile);
                    if (text == null) continue;
                    StageSetMeta m = ParseMeta(JObject.Parse(text), fileName);
                    if (m != null) return m;
                }
                catch { /* 다음 후보로 */ }
            }
            return null;
        }

        private static StageSetMeta ParseMeta(JObject root, string fileName)
        {
            string layout = (string)root["layout"];
            string section = (string)root["word_section"];
            if (string.IsNullOrWhiteSpace(layout) || string.IsNullOrWhiteSpace(section)) return null;

            var m = new StageSetMeta
            {
                FileName = fileName,
                LayoutName = layout,
                WordSection = section,
                Key = SafeKey((string)root["key"], fileName),
                ScriptName = (string)root["script_name"] ?? layout,
                Order = (int?)root["order"] ?? 100,
                IndexGuide = (string)root["index_guide"],
            };
            if (root["tiles"] is JObject tiles)
            {
                int perRow = (int?)tiles["per_row"] ?? m.TilesPerRow;
                if (perRow > 0) m.TilesPerRow = perRow;
                if (tiles["colors"] is JArray colors && colors.Count > 0)
                    m.TileColors = colors.Select(c => c.ToString()).ToList();
            }
            if (root["game_rules"] is JObject rules)
            {
                m.RareEvery = Math.Max(1, (int?)rules["rare_every"] ?? m.RareEvery);
                m.UniqueUnit = Math.Max(1, (int?)rules["unique_unit"] ?? m.UniqueUnit);
            }
            return m;
        }

        // "key"는 기록 파일 이름(stage_records_<key>.json 등)에 그대로 들어가므로, 경로 구분자·".."
        // 같은 것이 섞여 기록 폴더 밖을 가리키지 못하게 영문·숫자·_·-만 허용한다. 어긋나면 단계
        // 정의 파일 이름(폴더 목록에서 얻은 순수한 이름)을 쓴다.
        // 끝을 \z로 잡는다: $는 맨 끝의 줄바꿈 앞에서도 맞아 "ko\n"을 통과시킨다.
        private static readonly Regex SafeKeyPattern = new Regex(@"^[A-Za-z0-9_-]{1,32}\z");

        private static string SafeKey(string key, string fileName) =>
            key != null && SafeKeyPattern.IsMatch(key) ? key : Path.GetFileNameWithoutExtension(fileName);

        // 단계 정의 파일의 개수 값이 비정상적으로 크면 연습 창을 여는 순간 멈출 수 있으므로 상한을 둔다
        // (실제 파일에 쓰인 값은 수십 이하).
        private const int MaxRoundSize = 1000;

        /// <summary>파일(실행 파일 옆) → 내장 리소스 순으로 시도해 단계 목록을 만든다. 실패 시 빈 목록.</summary>
        public static List<PracticeStage> Load(string fileName, string wordSection, string layoutName)
        {
            KeyboardMap map = KeyboardMaps.For(layoutName);
            foreach (bool preferFile in new[] { true, false })
            {
                try
                {
                    string text = ReadText(fileName, preferFile);
                    if (text == null) continue;
                    List<PracticeStage> stages = Parse(text, wordSection, map);
                    // 문법은 맞지만 구조가 잘못돼 단계가 하나도 안 나오면 내장 리소스로 폴백한다 —
                    // 안 그러면 자리연습 화면이 통째로 빈다.
                    if (stages.Count > 0) return stages;
                }
                catch { /* 다음 후보로 */ }
            }
            return new List<PracticeStage>();
        }

        public static List<PracticeStage> Parse(string json, string wordSection, KeyboardMap map)
        {
            var result = new List<PracticeStage>();
            var targetTokens = new List<IReadOnlyList<string>>();   // 단계별 targets 키 이름(숫자·기호 판정용)
            var wordsByStage = new Dictionary<int, StageWords>();
            JObject root = JObject.Parse(json);
            if (!(root["stages"] is JArray stagesArr)) return result;

            int index = 0;
            foreach (JToken st in stagesArr)
            {
                index++;
                int stageNumber = index; // 제시어 목록은 1-based 단계 번호로 조회
                string name = (string)st["name"] ?? (stageNumber + "단계");
                int game = (int?)st["game"] ?? 0;
                int targetTa = (int?)st["target_ta"] ?? 0;
                if (targetTa <= 0) targetTa = DefaultTargetTa;
                IReadOnlyList<string> targets = Tokens(st["targets"]);

                StageWords words = BuildWords(st["words"], wordSection, map, stageNumber, wordsByStage);
                wordsByStage[stageNumber] = words;

                int mainLevel = (int?)st["game_words"]?["main_level"] ?? 0;
                int rareLevel = (int?)st["game_words"]?["rare_level"] ?? 0;

                var rounds = new List<PracticeRound>();
                if (st["rounds"] is JArray roundsArr)
                    foreach (JToken r in roundsArr)
                    {
                        PracticeRound round = BuildRound(r, wordSection, map, words, targetTokens, result);
                        if (round != null) rounds.Add(round);
                    }

                result.Add(new PracticeStage(name, rounds, game, targetTa, KeysOf(map, targets), words, mainLevel, rareLevel));
                targetTokens.Add(targets);
            }
            return result;
        }

        private static StageWords BuildWords(JToken spec, string wordSection, KeyboardMap map, int stageNumber,
                                             IReadOnlyDictionary<int, StageWords> earlier)
        {
            if (!(spec is JObject w)) return StageWords.None;

            // levels: 수준마다 "parts"=[[용어, 알파벳], ...] — 그 수준에서 더해지는 알파벳(키 이름)들.
            // 알파벳은 문자열(한 글자 = 키 하나)이나 키 이름 배열(["ㄱ", "ㄱ (받침)"])로 적는다.
            var levelParts = new List<IReadOnlyList<string>>();
            if (w["levels"] is JArray levels)
                foreach (JToken lv in levels)
                {
                    var add = new List<string>();
                    if (lv["parts"] is JArray parts)
                        foreach (JToken p in parts)
                            if (p is JArray pair && pair.Count >= 2) add.AddRange(Tokens(pair[1]));
                    levelParts.Add(add);
                }

            List<int> reuse = (w["reuse"] as JArray)?
                .Select(x => int.TryParse(x?.ToString(), out int n) ? n : 0).Where(n => n > 0).ToList();
            bool fromAll = string.Equals((string)w["from"], "all", StringComparison.OrdinalIgnoreCase);

            return StageWords.Build(wordSection, map, stageNumber, Tokens(w["required"]), levelParts, reuse, fromAll, earlier);
        }

        /// <summary>
        /// 키 목록 표기 → 키 이름 목록. 문자열이면 한 글자가 키 하나("ㅁㄴㅇ"), 배열이면 원소 하나가 키 하나
        /// (["ㄱ", "ㄱ (받침)"] — 이름이 두 글자 이상인 키를 적을 때). 중복은 그대로 둔다(그룹 주머니용).
        /// </summary>
        internal static List<string> Tokens(JToken t)
        {
            var list = new List<string>();
            if (t is JArray arr)
            {
                foreach (JToken x in arr)
                {
                    string label = KeyboardMap.Normalize(x?.ToString());
                    if (!string.IsNullOrEmpty(label)) list.Add(label);
                }
            }
            else if (t != null && t.Type == JTokenType.String)
            {
                foreach (char ch in (string)t)
                    if (!char.IsWhiteSpace(ch)) list.Add(ch.ToString());
            }
            return list;
        }

        private static PracticeRound BuildRound(JToken r, string wordSection, KeyboardMap map, StageWords words,
                                                List<IReadOnlyList<string>> earlierTargets, List<PracticeStage> earlierStages)
        {
            string kind = ((string)r["kind"] ?? "").Trim().ToLowerInvariant();
            bool noRepeat = (bool?)r["no_repeat"] ?? false;
            int count = Math.Clamp((int?)r["count"] ?? 0, 0, MaxRoundSize);
            int times = Math.Clamp((int?)r["times"] ?? 1, 1, MaxRoundSize);

            switch (kind)
            {
                case "sequential":
                    return PracticeRound.Sequential(KeysOf(map, Tokens(r["keys"])));

                case "grouped":
                {
                    var groups = new List<IReadOnlyList<PracticeStage.StageItem>>();
                    if (r["groups"] is JArray ga)
                        foreach (JToken g in ga) groups.Add(KeysOf(map, Tokens(g)));
                    return PracticeRound.Grouped(groups, (string)r["pattern"], times, noRepeat);
                }

                case "practiced_keys":
                {
                    var keys = new List<PracticeStage.StageItem>();
                    foreach (PracticeStage s in earlierStages)
                        foreach (PracticeStage.StageItem it in s.Targets)
                            if (!keys.Any(k => PracticeStage.SameKey(k, it))) keys.Add(it);
                    return PracticeRound.PracticedKeys(keys, times, noRepeat);
                }

                case "syllables":
                    return PracticeRound.Texts(words.Singles, count, noRepeat);

                case "words":
                {
                    int level = (int?)r["level"] ?? 1;
                    IEnumerable<string> pool = words.UpTo(level);
                    // <260927_3>(2) 대소문자가 있는 자판의 자리연습에서는 대문자가 든 단어(DVD 등)를 뺀다.
                    if (map.HasCaseLetters) pool = pool.Where(w => !w.Any(char.IsUpper));
                    return PracticeRound.Texts(pool.ToList(), count, noRepeat);
                }

                case "mixed_syllables_symbols":
                {
                    var symbols = new List<PracticeStage.StageItem>();
                    foreach (IReadOnlyList<string> targets in earlierTargets)
                        foreach (string label in targets)
                            if (IsSymbolOrNumber(label) && map.TryKey(label, out KeyPos pos, out bool isShift))
                            {
                                var it = new PracticeStage.StageItem(pos.Row, pos.Column, isShift);
                                if (!symbols.Any(k => PracticeStage.SameKey(k, it))) symbols.Add(it);
                            }
                    return PracticeRound.Mixed(words.Singles, symbols, count);
                }

                default:
                    return null; // 알 수 없는 kind는 건너뛴다
            }
        }

        // 키 이름 목록 → StageItem 목록(이 자판에서 그 키 자리). 이 자판에 없는 키는 건너뛴다.
        // 중복은 그대로 여러 개가 된다(그룹 주머니에서 '두 번씩' 등을 적을 때 쓴다).
        private static List<PracticeStage.StageItem> KeysOf(KeyboardMap map, IEnumerable<string> labels)
        {
            var list = new List<PracticeStage.StageItem>();
            foreach (string label in labels ?? new string[0])
                if (map.TryKey(label, out KeyPos pos, out bool isShift))
                    list.Add(new PracticeStage.StageItem(pos.Row, pos.Column, isShift));
            return list;
        }

        // 숫자·특수 기호 키인가(글자 — 한글 낱자·영문 등 — 가 아닌 한 글자 키).
        private static bool IsSymbolOrNumber(string label) =>
            label != null && label.Length == 1 && !char.IsLetter(label[0]) && !char.IsWhiteSpace(label[0]);
    }
}
