using System.Text;
using OpenTyping;

namespace OpenTyping.Tests;

// 필터링 파일 형식과 한 음절 필터링 (<261003_1>, 예전 -edgetest 23·24번)
public class FilterWordsTests
{
    [Fact]
    public void 필터링_파일_형식을_읽는다()
    {
        // 쉼표·전각 쉼표·CR+LF/LF/CR·빈칸·빈 항목·'#' 주석·NFD·BOM.
        string nfd = "똥".Normalize(NormalizationForm.FormD);
        List<FilterWords.Entry> parsed = FilterWords.Parse(
            "﻿# 주석, 무시\r\n삥, 나라 ，어 망\n\t다리  # 메모\r뿡,,\r\n" + nfd + ",\r\n,");
        string text = string.Join("|", parsed.Select(x => x.Text + "@" + x.Line + (x.Problem == null ? "" : "!")));
        Assert.Equal("삥@2|나라@2|어망@2|다리@3|뿡@4|똥@5", text);
    }

    [Theory]
    [InlineData("가1", true)]       // 숫자
    [InlineData("ㄱ", true)]        // 낱자모
    [InlineData("a�b", true)]  // 깨진 글자
    [InlineData("나-라", true)]     // 기호
    [InlineData("좋아", false)]
    [InlineData("Abc", false)]
    public void 문제_항목을_가려낸다(string item, bool isProblem)
    {
        FilterWords.Entry entry = Assert.Single(FilterWords.Parse(item));
        Assert.Equal(isProblem, entry.Problem != null);
    }

    [Fact]
    public void 실행_중에는_한_음절만_쓴다()
    {
        Assert.Equal("삥", FilterWords.SyllablesOf(FilterWords.Parse("삥, 삥땅, ab, x")));
    }

    // 실제 필터링 파일(Resources\필터링_단어.txt)은 저장소에 올리지 않으므로, 새로 받은 저장소에서는 빌드가 예시 파일
    // (Resources\필터링_단어.example.txt, 예전에 공개된 한 음절 18개)로 대신한다. 그래서 아래 검사는 두 파일에 모두 있는
    // 그 18개만 기대한다 — 실제 필터링 파일에만 있는 음절(예: '삥')을 기대하면 새로 받은 저장소에서 실패한다.
    private const string PublicSyllables = "년놈뒈똥뽕섹술썅씹엿젖졷좃좆좇죽총칼";

    [Fact]
    public void 한_음절_필터링_단어는_blacklist와_합쳐지고_그_음절이_든_기존_단어는_남는다()
    {
        string blacklist;
        bool wordKept;
        try
        {
            WordCatalog.UseJsonForTest("{\"syllable_blacklist\":\"갉\",\"hangul\":{\"stages\":[{\"stage\":1,\"levels\":[{\"level\":1,\"words\":[\"뽕나무\",\"나라\"]}]}]}}");
            blacklist = WordCatalog.SyllableBlacklist(WordCatalog.Hangul);
            wordKept = WordCatalog.RawLevelWords(WordCatalog.Hangul, 1, 1).Contains("뽕나무");
        }
        finally { WordCatalog.UseJsonForTest(null); }

        Assert.Contains('갉', blacklist);
        Assert.All(PublicSyllables, c => Assert.Contains(c, blacklist));
        Assert.True(wordKept);

        KeyboardMap ko = StageSets.DubeolsikStandard?.Keyboard;
        Assert.NotNull(ko);
        var keys = new HashSet<string> { "ㅃ", "ㅗ", "ㅇ" };
        Assert.Contains("뽕", StageWords.ComputeSingles(ko, keys, null, ""));          // 거르지 않으면 나온다
        Assert.DoesNotContain("뽕", StageWords.ComputeSingles(ko, keys, null, blacklist)); // 거르면 빠진다
    }

    [Fact]
    public void 실제_두벌식_단계의_한_음절에는_필터링_음절이_없다()
    {
        string blacklist = WordCatalog.SyllableBlacklist(WordCatalog.Hangul);
        IStageSet ko = StageSets.DubeolsikStandard;
        Assert.NotNull(ko);
        Assert.All(PublicSyllables, c => Assert.Contains(c, blacklist));
        Assert.True(ko.Stages.Sum(st => st.Words.Singles.Count) > 0);
        Assert.Equal(0, ko.Stages.Sum(st => st.Words.Singles.Count(s => blacklist.Contains(s[0]))));
    }
}
