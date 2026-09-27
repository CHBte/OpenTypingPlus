using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace OpenTyping
{
    /// <summary>
    /// <260927_2> 제시어 목록(단어 목록 전용). 자리연습과 오락이 함께 쓴다.
    ///
    /// 파일 구성: 묶음("hangul"/"english" 등, 단계 정의의 word_section) → 단계 → 수준 → words(그 수준의
    /// 고유 단어). 수준별 알파벳·학습 목표 알파벳·'조합이 가능한 한 음절'·재사용 단계는 단계 정의
    /// (stages\*.json)가 정하므로 여기에는 없다 — 검증과 계산은 <see cref="StageWords"/>가 한다.
    ///
    /// 읽는 순서: AppData(Roaming)\OTP\OpenTypingPlus\wordslist\words.json(설치본의 편집 가능한 사본)
    /// → 실행 파일 옆 wordslist\words.json(개발 빌드) → 내장 예비본(고유 수준마다 10개만). 묶음마다
    /// 앞에서부터 처음으로 단어가 있는 곳을 쓴다.
    /// </summary>
    public static class WordCatalog
    {
        public const string Hangul = "hangul";
        public const string English = "english";

        private sealed class Source
        {
            public string Origin;
            public string SyllableBlacklist;
            // 묶음 → 단계 → 수준 → 단어(원문 그대로, 공백 정리·중복 제거만)
            public Dictionary<string, Dictionary<int, Dictionary<int, List<string>>>> Sections =
                new Dictionary<string, Dictionary<int, Dictionary<int, List<string>>>>();
        }

        private static readonly IReadOnlyList<string> Empty = new List<string>();
        private static readonly object Gate = new object();
        private static List<Source> sources;
        // 파일은 있는데 읽지 못해(편집하다 JSON 문법이 깨진 경우 등) 건너뛴 후보 경로. 조용히 예비본으로
        // 넘어가면 사용자는 편집이 왜 반영되지 않는지 알 수 없으므로 한 번은 알린다.
        private static readonly List<string> unreadablePaths = new List<string>();
        private static bool unreadableWarned;

        /// <summary>words.json 후보 경로(순서가 곧 우선순위). 설치 스크립트·진단도 같은 경로를 쓴다.</summary>
        internal static string[] CandidatePaths() => new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "OTP", "OpenTypingPlus", "wordslist", "words.json"),
            Path.Combine(AppContext.BaseDirectory, "wordslist", "words.json"),
        };

        private const string FallbackResource = "words_fallback.json";

        private static List<Source> Sources
        {
            get
            {
                lock (Gate)
                {
                    if (sources != null) return sources;
                    var list = new List<Source>();
                    foreach (string path in CandidatePaths())
                    {
                        try
                        {
                            if (File.Exists(path)) list.Add(Parse(File.ReadAllText(path), path));
                        }
                        catch { unreadablePaths.Add(path); /* 손상된 후보는 건너뛴다 */ }
                    }
                    try
                    {
                        string text = EmbeddedResource.ReadText(typeof(WordCatalog).Assembly, FallbackResource);
                        if (text != null) list.Add(Parse(text, "내장 예비본"));
                    }
                    catch { /* 예비본도 없으면 빈 목록 */ }
                    sources = list;
                    return sources;
                }
            }
        }

        /// <summary>검사용: 주어진 JSON만을 제시어 목록으로 쓴다(null이면 원래대로 파일에서 다시 읽음).</summary>
        internal static void UseJsonForTest(string json)
        {
            lock (Gate) sources = json == null ? null : new List<Source> { Parse(json, "검사") };
        }

        /// <summary>
        /// 읽지 못해 건너뛴 단어 목록 파일이 있으면 알릴 문구를 한 번만 돌려준다(없거나 이미 알렸으면
        /// null). 아직 목록을 읽지 않았으면 읽지 않는다 — 단어를 쓰는 화면이 먼저 읽은 뒤에 부른다.
        /// </summary>
        internal static string TakeUnreadableWarning()
        {
            lock (Gate)
            {
                if (sources == null || unreadableWarned || unreadablePaths.Count == 0) return null;
                unreadableWarned = true;
                return "다음 단어 목록 파일을 읽지 못해 건너뛰었습니다(JSON 형식이 깨졌을 수 있습니다).\n" +
                       "자리연습과 오락은 다른 단어 목록(또는 내장 예비 단어)으로 진행됩니다.\n\n" +
                       string.Join("\n", unreadablePaths);
            }
        }

        private static Source SourceFor(string section) =>
            Sources.FirstOrDefault(s => s.Sections.TryGetValue(section, out var st) && st.Count > 0);

        /// <summary>이 묶음을 어디에서 읽었는지(진단용).</summary>
        internal static string OriginOf(string section) => SourceFor(section)?.Origin ?? "(없음)";

        /// <summary>그 단계·수준의 고유 단어(파일에 적힌 그대로. 알파벳 검증 전).</summary>
        public static IReadOnlyList<string> RawLevelWords(string section, int stage, int level)
        {
            Source s = SourceFor(section);
            if (s == null) return Empty;
            return s.Sections[section].TryGetValue(stage, out var levels) && levels.TryGetValue(level, out var words)
                ? words : Empty;
        }

        /// <summary>그 묶음의 모든 단어(단계·수준 구분 없이, 중복 없음). 단계 정의의 "from": "all" 이 쓴다.</summary>
        public static IReadOnlyList<string> AllWords(string section)
        {
            Source s = SourceFor(section);
            if (s == null) return Empty;
            return s.Sections[section].OrderBy(kv => kv.Key)
                    .SelectMany(kv => kv.Value.OrderBy(l => l.Key).SelectMany(l => l.Value))
                    .Distinct().ToList();
        }

        /// <summary>'조합이 가능한 한 음절' 목록에서 뺄 음절들. 그 묶음을 읽어 온 파일의 값(없으면 빈 문자열).</summary>
        public static string SyllableBlacklist(string section) =>
            SourceFor(section)?.SyllableBlacklist
            ?? Sources.Select(s => s.SyllableBlacklist).FirstOrDefault(b => b != null) ?? "";

        private static Source Parse(string json, string origin)
        {
            var src = new Source { Origin = origin };
            JObject root = JObject.Parse(json);
            src.SyllableBlacklist = ToComposed((string)root["syllable_blacklist"]);   // 단어와 같은 형식(완성형)으로
            foreach (JProperty prop in root.Properties())
            {
                if (!(prop.Value is JObject sec) || !(sec["stages"] is JArray arr)) continue;
                var stages = new Dictionary<int, Dictionary<int, List<string>>>();
                foreach (JToken t in arr)
                {
                    if (!int.TryParse(t["stage"]?.ToString(), out int number) || number <= 0) continue;
                    if (!(t["levels"] is JArray levels)) continue;
                    var byLevel = new Dictionary<int, List<string>>();
                    foreach (JToken lt in levels)
                    {
                        if (!int.TryParse(lt["level"]?.ToString(), out int ln) || ln <= 0) continue;
                        var words = new List<string>();
                        if (lt["words"] is JArray wa)
                            foreach (JToken w in wa)
                            {
                                string word = ToComposed(w?.ToString()?.Trim());
                                if (!string.IsNullOrEmpty(word) && !words.Contains(word)) words.Add(word);
                            }
                        byLevel[ln] = words;
                    }
                    if (byLevel.Values.Any(l => l.Count > 0) && !stages.ContainsKey(number)) stages[number] = byLevel;
                }
                if (stages.Count > 0) src.Sections[prop.Name] = stages;
            }
            return src;
        }

        // 자모를 풀어 쓴 형식(NFD)으로 저장된 파일(일부 편집기·macOS에서 흔함)도 완성형 음절로 맞춘다 —
        // 안 그러면 모든 단어가 자판으로 칠 수 없는 글자로 걸러져 한글 제시어가 통째로 빈다.
        private static string ToComposed(string word)
        {
            if (string.IsNullOrEmpty(word)) return word;
            try { return word.Normalize(NormalizationForm.FormC); }
            catch (ArgumentException) { return word; } // 짝 없는 서로게이트 등: 원문 그대로(이후 검증에서 걸러짐)
        }
    }
}
