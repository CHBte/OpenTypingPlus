using OpenTyping;

namespace OpenTyping.Tests;

// 타수 세기·정확도·받아쓰기 칸 (예전 -edgetest 1~4번)
public class MeasureTests
{
    [Fact]
    public void 한글_한_음절은_반올림해_3타()
    {
        // 한 음절=2.5타 → 은행원 반올림이면 2, AwayFromZero 면 3.
        Assert.Equal(3, TypingMeasurer.CountLetter("가"));
    }

    [Fact]
    public void 서로게이트_쌍은_1타()
    {
        Assert.Equal(1, TypingMeasurer.CountStrokes("\U0001F600"));
    }

    [Fact]
    public void 빈_diffs의_정확도는_NaN이_아닌_0()
    {
        Assert.Equal(0, Differ.CalculateAccuracy(new List<Differ.DiffData>()));
    }

    [Fact]
    public void 받아쓰기_오답은_칸_7글자를_넘지_않는다()
    {
        var dictation = new DictationState();
        dictation.Start("가나다");
        for (int i = 0; i < 15; i++) dictation.Wrong("x");
        Assert.True(dictation.Segments().Sum(s => s.Text.Length) <= 7);
    }
}
