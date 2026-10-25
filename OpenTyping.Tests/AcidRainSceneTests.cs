using OpenTyping;
using RiseKind = OpenTyping.AcidRainWindow.RiseKind;

namespace OpenTyping.Tests;

// <261024_1> 산성비 오락 배경: 오락 단계마다 몇 단계 배경인가, 지면 클립 아트가 레벨마다 얼마나 올라오는가
public class AcidRainSceneTests
{
    private static double Rise(RiseKind kind, int scene, int level) => AcidRainWindow.RiseFraction(kind, scene, level);

    [Theory]
    [InlineData(0, 7, 1)]
    [InlineData(4, 7, 5)]
    [InlineData(5, 7, 6)]    // 두벌식 6단계
    [InlineData(6, 7, 10)]   // 두벌식 마지막(10단계)
    [InlineData(4, 6, 5)]    // 영문 5단계
    [InlineData(5, 6, 10)]   // 영문 마지막(6단계) = 10단계 배경
    [InlineData(0, 1, 10)]   // 단계가 하나뿐(내장 기본 단어)이면 완성된 장면
    public void 오락_단계마다_배경_단계(int ordinal, int count, int scene)
    {
        Assert.Equal(scene, AcidRainWindow.SceneNumberFor(ordinal, count));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void 식물은_5단계까지_레벨_1에서_8까지_같은_간격으로_올라온다(int scene)
    {
        foreach (RiseKind k in new[] { RiseKind.Tree, RiseKind.Plant })
        {
            Assert.Equal(0, Rise(k, scene, 1));
            for (int lv = 2; lv <= 7; lv++) Assert.Equal((lv - 1) / 7.0, Rise(k, scene, lv), 9);
            Assert.Equal(1, Rise(k, scene, 8));
            Assert.Equal(1, Rise(k, scene, 9));    // 레벨 9라고 더 올라오지 않는다
            Assert.Equal(1, Rise(k, scene, 30));
        }
    }

    [Fact]
    public void 식물은_6단계에서_레벨_4_10단계에서_레벨_3에_다_올라온다()
    {
        Assert.Equal(0, Rise(RiseKind.Plant, 6, 1));
        Assert.Equal(1 / 3.0, Rise(RiseKind.Plant, 6, 2), 9);
        Assert.Equal(1, Rise(RiseKind.Tree, 6, 4));
        Assert.Equal(0, Rise(RiseKind.Tree, 10, 1));
        Assert.Equal(0.5, Rise(RiseKind.Plant, 10, 2), 9);
        Assert.Equal(1, Rise(RiseKind.Plant, 10, 3));
    }

    [Fact]
    public void 벌은_6단계에서_레벨_5부터_10단계에서_레벨_3부터_나타난다()
    {
        for (int lv = 1; lv <= 20; lv++) Assert.Equal(0, Rise(RiseKind.Bee, 5, lv));   // 5단계까지는 벌이 없다
        Assert.Equal(0, Rise(RiseKind.Bee, 6, 4));
        Assert.Equal(0.25, Rise(RiseKind.Bee, 6, 5), 9);
        Assert.Equal(1, Rise(RiseKind.Bee, 6, 8));
        Assert.Equal(0, Rise(RiseKind.Bee, 10, 2));
        Assert.Equal(1 / 3.0, Rise(RiseKind.Bee, 10, 3), 9);
        Assert.Equal(1, Rise(RiseKind.Bee, 10, 5));
    }

    [Fact]
    public void 나비는_10단계에서만_레벨_6부터_나타나_레벨_8에_다_올라온다()
    {
        for (int lv = 1; lv <= 20; lv++) Assert.Equal(0, Rise(RiseKind.Butterfly, 6, lv));
        Assert.Equal(0, Rise(RiseKind.Butterfly, 10, 5));
        Assert.Equal(1 / 3.0, Rise(RiseKind.Butterfly, 10, 6), 9);
        Assert.Equal(1, Rise(RiseKind.Butterfly, 10, 8));
        Assert.Equal(1, Rise(RiseKind.Butterfly, 10, 9));
    }
}
