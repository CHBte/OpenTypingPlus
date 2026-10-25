using OpenTyping;

namespace OpenTyping.Tests;

// 산성비 오락의 특수(파란 글씨) 효과 시간 (<산성비 타자 오락 260812_13>(2), <261026_7>)
public class AcidEffectTests
{
    [Fact]
    public void 화면_흔들림은_5초이고_치트_창의_이름도_같은_시간을_가리킨다()
    {
        // <261026_7> 10초에서 5초로. 안내 문구("화면 흔들림! (5초)")는 같은 상수(ShakeDuration)로 만든다.
        Assert.Equal(5.0, AcidRainWindow.ShakeDuration);
        Assert.Equal("화면 흔들림 (5초)", AcidCheatWindow.Effects.First(e => e.Type == 3).Name);
    }
}
