using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace OpenTyping
{
    /// <summary>
    /// '자리연습' 순3('한 글자 조합')·순4('2음절 이상 단어')의 제시어 목록.
    /// 어셈블리에 내장된 <c>Resources\jariyeonseup_words.json</c>에서 단계별로 읽는다
    /// (<260723_4> (0-1)). 원본 목록은 이미 부정·성적·화장 단어를 배제해 정선돼 있고(<260723_4> (0-2)),
    /// 여기서는 두벌식 표준으로 입력 가능한 것만 한 번 더 거른다.
    /// </summary>
    public static class PracticeWordList
    {
        public sealed class StageWords
        {
            public List<string> Singles { get; } = new List<string>(); // 한 글자 조합(음절 1개)
            public List<string> Words { get; } = new List<string>();   // 2음절 이상 단어
        }

        private static readonly Dictionary<int, StageWords> ByStage = Load();

        private static readonly IReadOnlyList<string> EmptyList = new List<string>();

        /// <summary>해당 단계의 '한 글자 조합' 목록(각 항목은 완성형 음절 1자).</summary>
        public static IReadOnlyList<string> Singles(int stage) =>
            ByStage.TryGetValue(stage, out StageWords w) ? w.Singles : EmptyList;

        /// <summary>해당 단계의 '2음절 이상 단어' 목록.</summary>
        public static IReadOnlyList<string> Words(int stage) =>
            ByStage.TryGetValue(stage, out StageWords w) ? w.Words : EmptyList;

        /// <summary>1~8단계 '한 글자 조합' 전체(중복 제거) — 13단계 '중간'용.</summary>
        public static IReadOnlyList<string> AllSingles() =>
            Enumerable.Range(1, 8).SelectMany(Singles).Distinct().ToList();

        private static Dictionary<int, StageWords> Load()
        {
            var result = new Dictionary<int, StageWords>();
            try
            {
                string text = EmbeddedResource.ReadText(typeof(PracticeWordList).Assembly, "jariyeonseup_words.json");
                if (text == null) return result;

                JObject root = JObject.Parse(text);
                var stagesArr = root["stages"] as JArray;
                if (stagesArr == null) return result;

                foreach (JToken st in stagesArr)
                {
                    // "stage"가 정수인 단계만 취한다("9-12" 같은 안내용 항목은 건너뜀).
                    if (!int.TryParse(st["stage"]?.ToString(), out int stageNo)) continue;

                    var sw = new StageWords();

                    string singles = st["singles"]?.ToString() ?? "";
                    foreach (char ch in singles)
                    {
                        if (char.IsWhiteSpace(ch)) continue;
                        string one = ch.ToString();
                        if (HangulJamo.CanType(ch)) sw.Singles.Add(one);
                    }

                    var wordsArr = st["words"] as JArray;
                    if (wordsArr != null)
                    {
                        foreach (JToken wt in wordsArr)
                        {
                            string word = wt?.ToString()?.Trim();
                            if (string.IsNullOrEmpty(word)) continue;
                            if (word.All(HangulJamo.CanType)) sw.Words.Add(word);
                        }
                    }

                    result[stageNo] = sw;
                }
            }
            catch
            {
                // 목록을 못 읽으면 순3·순4 풀이 비게 되고, 그 단계 생성 시 해당 순은 건너뛴다(폴백).
            }
            return result;
        }
    }
}
