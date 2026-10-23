using OpenTyping;

namespace OpenTyping.Tests;

// 영문 대문자 단어와 한글 레벨2 자소 (예전 -edgetest 15·22번, CLAUDE.md '영문 오락(산성비)의 대문자 단어')
public class EnglishCaseTests
{
    [Theory]
    [InlineData("DJ", "DJ", true)]
    [InlineData("DJ", "dj", false)]
    [InlineData("DVD", "DVD", true)]
    [InlineData("DVD", "dvd", false)]
    [InlineData("sea", "sea", true)]
    [InlineData("sea", "SEA", false)]
    public void 영문_오락은_대소문자를_구분해_판정한다(string word, string typed, bool expected)
    {
        Assert.Equal(expected, AcidRainWindow.MatchesTyped(word, typed));
    }

    [Fact]
    public void 영문_오락에는_대문자가_든_단어도_그대로_나온다()
    {
        IStageSet qwerty = StageSets.QwertyEnglish;
        Assert.NotNull(qwerty);
        IReadOnlyList<GameStage> games = GameStages.For(qwerty);
        Assert.NotEmpty(games);
        Assert.True(games.Sum(g => g.Main.Concat(g.Rare).Count(w => w.Any(char.IsUpper))) > 0);
    }

    [Fact]
    public void 영문_대문자는_소문자와_같은_알파벳()
    {
        KeyboardMap en = StageSets.QwertyEnglish?.Keyboard;
        Assert.NotNull(en);
        Assert.True(en.HasCaseLetters);
        Assert.True(en.AlphabetsOf("DJ").SetEquals(new[] { "d", "j" }));
    }

    [Fact]
    public void 한글_레벨2_자소는_별개의_알파벳이고_대소문자_글쇠가_없다()
    {
        KeyboardMap ko = StageSets.DubeolsikStandard?.Keyboard;
        Assert.NotNull(ko);
        Assert.False(ko.HasCaseLetters);
        HashSet<string> alpha = ko.AlphabetsOf("뿌");   // ㅃ(윗글쇠) + ㅜ
        Assert.Contains("ㅃ", alpha);
        Assert.DoesNotContain("ㅂ", alpha);
    }
}
