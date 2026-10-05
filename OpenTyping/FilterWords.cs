using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenTyping
{
    /// <summary>
    /// <261003_1> 필터링 파일(Resources\필터링_단어.txt)을 읽는 규칙. 필터링 파일에는 자리연습·오락의 제시어로
    /// 내지 않아야 하는 한 음절과 기존 단어를 썰렁이가 적는다.
    ///  - 쉼표(,)·전각 쉼표(，)·강제 개행(CR+LF·LF·CR)으로 항목을 구분한다.
    ///  - 빈칸(공백·탭 등)은 모두 무시하고, 빈 항목도 무시한다. '#'부터 그 줄 끝까지는 주석이다.
    ///  - 자모를 풀어 쓴 형식(NFD)이어도 완성형(NFC)으로 맞춰 읽는다.
    ///  - 한 글자 항목은 '한 음절 필터링 단어', 두 글자 이상 항목은 '기존 단어 필터링 단어'다.
    ///  - 한글 완성 음절·영문 글자 이외의 글자(깨진 글자 U+FFFD, 숫자, 기호, 낱자모 등)가 든 항목은 문제 항목이다.
    ///
    /// 실행 중인 프로그램(<see cref="WordCatalog.SyllableBlacklist"/>)과 빌드 도구(tools\apply-word-filter.ps1,
    /// tools\make-filter-syllables.ps1)가 이 파일 하나를 함께 쓴다 — 도구는 PowerShell 5.1의 Add-Type 으로 이 파일을
    /// 그대로 컴파일하므로, 옛 C# 5 컴파일러가 읽을 수 있는 문법만 쓰고 다른 파일의 형식에 기대지 않는다.
    /// </summary>
    public static class FilterWords
    {
        /// <summary>
        /// 실행 파일에 포함된 '한 음절 포함 파일'의 리소스 이름(csproj 의 LogicalName 과 같아야 한다). <261003_1.1>(5.1.1):
        /// 필터링 파일 전체가 아니라, 빌드할 때 tools\make-filter-syllables.ps1 이 한 음절 필터링 단어만 뽑아 만든 파일
        /// (한 줄에 한 음절)이 들어 있다 — 기존 단어 필터링 단어와 주석은 실행 파일에 없다. 같은 <see cref="Parse"/>로 읽는다.
        /// </summary>
        public const string ResourceName = "OpenTyping.FilterSyllables.txt";

        public sealed class Entry
        {
            /// <summary>빈칸을 지우고 완성형으로 맞춘 항목.</summary>
            public string Text;
            /// <summary>필터링 파일에서의 줄 번호(1부터).</summary>
            public int Line;
            /// <summary>문제 설명. 정상이면 null.</summary>
            public string Problem;

            public bool IsSyllable { get { return Text.Length == 1; } }
        }

        /// <summary>필터링 파일의 내용(이미 글자로 읽은 것)을 항목 목록으로 나눈다. 문제 항목도 Problem 을 채워 함께 돌려준다.</summary>
        public static List<Entry> Parse(string text)
        {
            var result = new List<Entry>();
            if (string.IsNullOrEmpty(text)) return result;
            if (text[0] == '﻿') text = text.Substring(1);   // UTF-8 BOM

            int lineNo = 1;
            var line = new StringBuilder();
            for (int i = 0; i <= text.Length; i++)
            {
                bool end = i == text.Length;
                char c = end ? '\n' : text[i];
                if (c == '\r' || c == '\n')
                {
                    AddLine(line.ToString(), lineNo, result);
                    line.Length = 0;
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;   // CR+LF 는 한 번의 줄바꿈
                    lineNo++;
                    continue;
                }
                line.Append(c);
            }
            return result;
        }

        private static void AddLine(string line, int lineNo, List<Entry> result)
        {
            int hash = line.IndexOf('#');
            if (hash >= 0) line = line.Substring(0, hash);
            foreach (string piece in line.Split(new[] { ',', '，' }))
            {
                var sb = new StringBuilder();
                foreach (char ch in piece)
                    if (!char.IsWhiteSpace(ch)) sb.Append(ch);
                if (sb.Length == 0) continue;   // 빈 항목

                string raw = sb.ToString();
                string text;
                try { text = raw.Normalize(NormalizationForm.FormC); }
                catch (ArgumentException) { text = raw; }   // 짝 없는 서로게이트 등 — 아래 검사에서 문제 항목이 된다

                var entry = new Entry();
                entry.Text = text;
                entry.Line = lineNo;
                entry.Problem = Check(text);
                result.Add(entry);
            }
        }

        private static string Check(string text)
        {
            foreach (char ch in text)
            {
                if (IsHangulSyllable(ch) || IsLatinLetter(ch)) continue;
                if (ch == '�') return "깨진 글자(U+FFFD)가 있습니다 — 필터링 파일을 UTF-8로 저장했는지 확인하세요";
                if ((ch >= 'ㄱ' && ch <= 'ㆎ') || (ch >= 'ᄀ' && ch <= 'ᇿ'))
                    return "완성되지 않은 한글 낱자 '" + ch + "'가 있습니다(완성된 음절만 쓸 수 있습니다)";
                return "한글·영문 이외의 글자 '" + ch + "'(U+" + ((int)ch).ToString("X4", CultureInfo.InvariantCulture) + ")가 있습니다";
            }
            return null;
        }

        private static bool IsHangulSyllable(char ch) { return ch >= '가' && ch <= '힣'; }

        private static bool IsLatinLetter(char ch) { return (ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z'); }

        /// <summary>
        /// 실행 중 적용하는 한 음절 필터링 단어(한 글자 항목)만 모아 문자열로 돌려준다. 문제 항목과 두 글자 이상
        /// 항목(기존 단어 필터링 단어 — 실행 중에는 적용하지 않는다)은 무시한다.
        /// </summary>
        public static string SyllablesOf(List<Entry> entries)
        {
            var sb = new StringBuilder();
            if (entries == null) return "";
            foreach (Entry e in entries)
                if (e.Problem == null && e.IsSyllable && IsHangulSyllable(e.Text[0])) sb.Append(e.Text);
            return sb.ToString();
        }

        /// <summary>
        /// 기존 단어 비교용 열쇠: 완성형으로 맞추고 영문은 대소문자를 무시한다("abc"는 "ABC"와 같다). 한글은
        /// 대소문자가 없어 그대로다.
        /// </summary>
        public static string WordKey(string word)
        {
            if (word == null) return null;
            string w = word;
            try { w = w.Normalize(NormalizationForm.FormC); }
            catch (ArgumentException) { }
            var sb = new StringBuilder(w.Length);
            foreach (char ch in w) sb.Append(ch >= 'A' && ch <= 'Z' ? (char)(ch + 32) : ch);
            return sb.ToString();
        }
    }
}
