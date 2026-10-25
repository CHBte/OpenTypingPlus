using OpenTyping;

namespace OpenTyping.Tests;

// 산성비 오락의 `배경 세트`(<261025_3>): 목록·자판 배정, 오락 단계 → 배경 단계 대응(단계 수 불일치 규칙 포함), 지면 클립 아트가 레벨마다
// 얼마나 올라오는가(acidrain-bg3 의 <261024_1> 규칙), 단계별 구름.
public class AcidRainSceneTests
{
    private static double Rise(RiseKind kind, int scene, int level) => BackgroundSets.RiseFraction(kind, scene, level);

    private static readonly int[] Seven = { 1, 2, 3, 4, 5, 6, 10 };
    private static readonly int[] Six = { 1, 2, 3, 4, 5, 10 };

    [Fact]
    public void 배경_세트는_정해진_이름이고_Id가_서로_다르며_자판_배정은_있는_배경_세트를_가리킨다()
    {
        Assert.Equal("자라는 꽃밭", BackgroundSets.Find(GrowingFlowerGarden.SetId)?.Name);
        Assert.Equal("친환경 에너지 마을로", BackgroundSets.Find(EcoEnergyVillage.SetId)?.Name);
        var ids = BackgroundSets.All.Select(s => s.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(BackgroundSets.ByKeyboard.Values, id => Assert.NotNull(BackgroundSets.Find(id)));
        Assert.NotNull(BackgroundSets.Find(BackgroundSets.DefaultId));
        Assert.All(BackgroundSets.All, s => Assert.NotEmpty(s.Stages));
    }

    [Fact]
    public void 기본_배경은_두벌식이_자라는_꽃밭_영문이_친환경_에너지_마을로다()
    {
        // <261025_4>
        Assert.Equal(GrowingFlowerGarden.SetId, BackgroundSets.DefaultFor("ko").Id);
        Assert.Equal(EcoEnergyVillage.SetId, BackgroundSets.DefaultFor("en").Id);
        Assert.Equal(BackgroundSets.DefaultId, BackgroundSets.DefaultFor("다른 자판").Id);
        Assert.Equal(BackgroundSets.DefaultId, BackgroundSets.DefaultFor(null).Id);
    }

    [Fact]
    public void 사용자가_고른_배경을_쓰고_없거나_모르는_배경이면_자판의_기본_배경을_쓴다()
    {
        // <261025_4> '배경 선택'으로 고른 것(설정 파일의 Id)
        Assert.Equal(EcoEnergyVillage.SetId, BackgroundSets.Resolve("ko", EcoEnergyVillage.SetId).Id);
        Assert.Equal(GrowingFlowerGarden.SetId, BackgroundSets.Resolve("en", GrowingFlowerGarden.SetId).Id);
        Assert.Equal(GrowingFlowerGarden.SetId, BackgroundSets.Resolve("ko", null).Id);
        Assert.Equal(EcoEnergyVillage.SetId, BackgroundSets.Resolve("en", "없어진 배경").Id);
    }

    [Fact]
    public void 두_배경_세트는_두벌식_7단계_영문_6단계로_맞춰져_있다()
    {
        foreach (string id in new[] { GrowingFlowerGarden.SetId, EcoEnergyVillage.SetId })
        {
            BackgroundSet bg = BackgroundSets.Find(id)!;
            Assert.Equal(Seven, BackgroundSets.StageSequence(bg, "ko"));
            Assert.Equal(Six, BackgroundSets.StageSequence(bg, "en"));
            Assert.Equal(Seven, BackgroundSets.StageSequence(bg, "다른 자판"));   // 따로 맞춘 것이 없으면 기본 차례
        }
    }

    [Fact]
    public void 단계_수가_같으면_차례대로_쓴다()
    {
        for (int i = 0; i < 7; i++) Assert.Equal(Seven[i], BackgroundSets.SceneFor(Seven, i, 7));
        for (int i = 0; i < 6; i++) Assert.Equal(Six[i], BackgroundSets.SceneFor(Six, i, 6));
    }

    [Fact]
    public void 배경_단계가_적으면_마지막_배경_단계를_여러_번_쓴다()
    {
        // 6단계짜리 배경을 7단계 오락에: 1~6번째는 차례대로, 7번째는 마지막(10단계) 배경을 한 번 더.
        int[] got = Enumerable.Range(0, 7).Select(i => BackgroundSets.SceneFor(Six, i, 7)).ToArray();
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 10, 10 }, got);
        // 3단계짜리 배경을 7단계 오락에.
        int[] three = { 1, 5, 10 };
        Assert.Equal(new[] { 1, 5, 10, 10, 10, 10, 10 }, Enumerable.Range(0, 7).Select(i => BackgroundSets.SceneFor(three, i, 7)).ToArray());
    }

    [Fact]
    public void 배경_단계가_많으면_끝에서부터_맞춰_앞쪽_배경_단계를_쓰지_않는다()
    {
        // 썰렁이 예(<261025_3>(2.2)): 두벌식 7단계에만 맞춘 배경을 영문 6단계 오락에 쓰면 첫 단계 배경은 쓰지 않고, 두 번째 배경을 1단계에.
        int[] got = Enumerable.Range(0, 6).Select(i => BackgroundSets.SceneFor(Seven, i, 6)).ToArray();
        Assert.Equal(new[] { 2, 3, 4, 5, 6, 10 }, got);
        // 오락 단계가 하나뿐이면(내장 기본 단어) 마지막(완성된) 장면.
        Assert.Equal(10, BackgroundSets.SceneFor(Seven, 0, 1));
        Assert.Equal(10, BackgroundSets.SceneFor(Six, 0, 1));
        // 배경 단계를 적지 않은 배경 세트라도 멈추지 않는다.
        Assert.Equal(10, BackgroundSets.SceneFor(new int[0], 3, 7));
    }

    [Fact]
    public void 배경_세트와_자판의_단계_수가_다르면_알린다()
    {
        // <261025_3>(2.2): 단계 수가 다른 채로 빌드하기 전에는 썰렁이에게 알려야 한다. 썰렁이가 "그냥 빌드해"라고 한 짝은
        // BackgroundSets.AcceptedStageMismatches 에 적어 이 검사를 넘긴다.
        var mismatches = BackgroundSets.StageCountMismatches()
            .Where(m => !BackgroundSets.AcceptedStageMismatches.Contains((m.Set.Id, m.Keyboard.Key)))
            .Select(m => $"{m.Set.Name}({m.Set.Id}) × {m.Keyboard.Key}: 배경 {m.BackgroundStages}단계, 오락 {m.GameStages}단계")
            .ToList();
        Assert.True(mismatches.Count == 0, "단계 수 불일치 — 썰렁이에게 알릴 것: " + string.Join("; ", mismatches));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void 땅의_것은_5단계까지_레벨_1에서_8까지_같은_간격으로_올라온다(int scene)
    {
        Assert.Equal(0, Rise(RiseKind.Ground, scene, 1));
        for (int lv = 2; lv <= 7; lv++) Assert.Equal((lv - 1) / 7.0, Rise(RiseKind.Ground, scene, lv), 9);
        Assert.Equal(1, Rise(RiseKind.Ground, scene, 8));
        Assert.Equal(1, Rise(RiseKind.Ground, scene, 9));    // 레벨 9라고 더 올라오지 않는다
        Assert.Equal(1, Rise(RiseKind.Ground, scene, 30));
    }

    [Fact]
    public void 땅의_것은_6단계에서_레벨_4_10단계에서_레벨_3에_다_올라온다()
    {
        Assert.Equal(0, Rise(RiseKind.Ground, 6, 1));
        Assert.Equal(1 / 3.0, Rise(RiseKind.Ground, 6, 2), 9);
        Assert.Equal(1, Rise(RiseKind.Ground, 6, 4));
        Assert.Equal(0, Rise(RiseKind.Ground, 10, 1));
        Assert.Equal(0.5, Rise(RiseKind.Ground, 10, 2), 9);
        Assert.Equal(1, Rise(RiseKind.Ground, 10, 3));
    }

    [Fact]
    public void 날벌레와_새는_6단계에서_레벨_5부터_10단계에서_레벨_3부터_나타난다()
    {
        for (int lv = 1; lv <= 20; lv++) Assert.Equal(0, Rise(RiseKind.Flyer, 5, lv));   // 5단계까지는 없다
        Assert.Equal(0, Rise(RiseKind.Flyer, 6, 4));
        Assert.Equal(0.25, Rise(RiseKind.Flyer, 6, 5), 9);
        Assert.Equal(1, Rise(RiseKind.Flyer, 6, 8));
        Assert.Equal(0, Rise(RiseKind.Flyer, 10, 2));
        Assert.Equal(1 / 3.0, Rise(RiseKind.Flyer, 10, 3), 9);
        Assert.Equal(1, Rise(RiseKind.Flyer, 10, 5));
    }

    [Fact]
    public void 마지막_장면의_주인공은_10단계에서만_레벨_6부터_나타나_레벨_8에_다_올라온다()
    {
        for (int lv = 1; lv <= 20; lv++) Assert.Equal(0, Rise(RiseKind.Party, 6, lv));
        Assert.Equal(0, Rise(RiseKind.Party, 10, 5));
        Assert.Equal(1 / 3.0, Rise(RiseKind.Party, 10, 6), 9);
        Assert.Equal(1, Rise(RiseKind.Party, 10, 8));
        Assert.Equal(1, Rise(RiseKind.Party, 10, 9));
    }

    [Fact]
    public void 친환경_에너지_마을로의_구름은_1단계에_크고_짙은_매연_구름이다가_5단계부터_원래_흰_구름이다()
    {
        BackgroundSet bg = BackgroundSets.Find(EcoEnergyVillage.SetId)!;
        Assert.Equal(113 / 255.0, bg.CloudLook(1).Shade, 9);   // 기준 장면의 매연 색(113,113,114)
        for (int scene = 1; scene < 5; scene++)
        {
            Assert.True(bg.CloudLook(scene).Scale > bg.CloudLook(scene + 1).Scale);   // 단계마다 작아지고
            Assert.True(bg.CloudLook(scene).Shade < bg.CloudLook(scene + 1).Shade);   // 밝아진다
        }
        foreach (int scene in new[] { 5, 6, 10 }) Assert.Equal((1.0, 1.0), bg.CloudLook(scene));
    }

    [Fact]
    public void 자라는_꽃밭의_구름은_늘_흰_구름이다()
    {
        BackgroundSet bg = BackgroundSets.Find(GrowingFlowerGarden.SetId)!;
        foreach (int scene in Seven) Assert.Equal((1.0, 1.0), bg.CloudLook(scene));
    }

    [Fact]
    public void 키운_구름은_가장자리_반투명_띠만_배율만큼_좁아진다()
    {
        for (int a = 0; a <= 255; a++) Assert.Equal(a, SceneKit.SharpenAlpha((byte)a, 1));   // 흰 구름(배율 1)은 그대로
        Assert.Equal(0, SceneKit.SharpenAlpha(0, 2.6));
        Assert.Equal(255, SceneKit.SharpenAlpha(255, 2.6));
        Assert.InRange(SceneKit.SharpenAlpha(128, 2.6), 127, 130);   // 50% 지점(윤곽선 자리)은 그대로
        // 반투명(1~254)으로 남는 알파 값의 수가 약 1/2.6 로 준다 = 번진 띠가 그만큼 좁아진다.
        int band = Enumerable.Range(0, 256).Count(a => SceneKit.SharpenAlpha((byte)a, 2.6) is > 0 and < 255);
        Assert.InRange(band, 254 / 2.6 - 2, 254 / 2.6 + 2);
    }
}
