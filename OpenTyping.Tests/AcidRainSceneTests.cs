using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        // Id(그림 폴더 이름)와 그림 파일 이름은 그림 주소(pack URI — SceneKit.SheetUri)에 그대로 이어 붙으므로, 주소에서 특별한 뜻을
        // 갖는 문자('#'·'?'·'%'·'/'·빈칸 등)와 겹치지 않게 영문·숫자·밑줄만 쓴다.
        Assert.All(BackgroundSets.All, s =>
        {
            Assert.Matches("^[A-Za-z0-9_]+$", s.Id);
            Assert.Matches("^[A-Za-z0-9_]+$", s.CloudFile);
            Assert.All(s.SheetFiles, f => Assert.Matches("^[A-Za-z0-9_]+$", f));
        });
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
    public void 배경_선택을_가린_동안은_예전에_고른_것과_관계없이_자판별_기본_배경을_쓴다()
    {
        // <261026_1>·<261026_2>: 설정 파일에 남은 선택은 읽지 않는다.
        Assert.False(BackgroundSets.UserChoiceEnabled);
        Assert.Equal(GrowingFlowerGarden.SetId, BackgroundSets.For(StageSets.DubeolsikStandard).Id);
        Assert.Equal(EcoEnergyVillage.SetId, BackgroundSets.For(StageSets.QwertyEnglish).Id);
    }

    [Fact]
    public void 생명을_잃을수록_하늘이_밤으로_어두워지고_아래로_갈수록_밝은_그라데이션은_그대로다()
    {
        // <261026_4>
        static double Lum(Color c) => 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
        var day = AcidRainWindow.SkyColors(AcidRainWindow.StartLives);
        Assert.Equal(Color.FromRgb(0xd0, 0xeb, 0xff), day.Top);      // 처음은 예전과 같은 맑은 하늘
        Assert.Equal(Color.FromRgb(0xf8, 0xfb, 0xff), day.Bottom);
        for (int lives = AcidRainWindow.StartLives; lives >= 0; lives--)
        {
            var sky = AcidRainWindow.SkyColors(lives);
            Assert.True(Lum(sky.Bottom) > Lum(sky.Top));                              // 아래가 더 밝다
            Assert.True(sky.Top.B >= sky.Top.R && sky.Bottom.B >= sky.Bottom.R);       // 주황으로 물들지 않고 푸른 기운 그대로
            if (lives < AcidRainWindow.StartLives)
            {
                var prev = AcidRainWindow.SkyColors(lives + 1);
                Assert.True(Lum(sky.Top) < Lum(prev.Top) && Lum(sky.Bottom) < Lum(prev.Bottom));   // 생명 하나마다 더 어둡다
            }
        }
        var night = AcidRainWindow.SkyColors(0);
        Assert.True(Lum(night.Top) < 40 && Lum(night.Bottom) < 90);   // 생명 0이면 밤하늘
    }

    [Fact]
    public void 구름과_지면_클립_아트는_생명_하나에_10퍼센트씩_투명해져_생명_0이면_보이지_않는다()
    {
        // <261026_4>
        Assert.Equal(10, AcidRainWindow.StartLives);
        for (int lives = 0; lives <= 10; lives++) Assert.Equal(lives / 10.0, AcidRainWindow.ClipArtOpacity(lives), 9);
        Assert.Equal(0, AcidRainWindow.ClipArtOpacity(-1));
        Assert.Equal(1, AcidRainWindow.ClipArtOpacity(11));
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

    [Fact]
    public void 흰_꽃잎에만_색을_입히고_음영은_살리며_색이_있는_곳과_윤곽선은_그대로다()
    {
        // 썰렁이 지시 2026-10-26(태그 없는 지시): 흰 꽃이 흰 하늘에서 잘 안 보여 꽃잎에 주황·보라를 입힌다(SceneKit.TintWhite).
        Color t = SceneKit.FlowerOrange;
        (byte B, byte G, byte R) white = SceneKit.TintWhite(255, 255, 255, t);
        Assert.InRange(white.R, t.R - 15, 255);
        Assert.InRange(white.G, t.G - 15, t.G + 15);
        Assert.InRange(white.B, t.B - 15, t.B + 15);
        // 꽃잎의 회색 그림자는 같은 색의 더 어두운 쪽이 된다.
        (byte B, byte G, byte R) shadow = SceneKit.TintWhite(190, 190, 190, t);
        Assert.True(shadow.R < white.R && shadow.G < white.G && shadow.R > shadow.G && shadow.G > shadow.B);
        // 노란 꽃술·녹색 밑동·분홍 꽃잎·어두운 윤곽선은 그대로다(B, G, R 차례).
        Assert.Equal(((byte)60, (byte)210, (byte)250), SceneKit.TintWhite(60, 210, 250, t));
        Assert.Equal(((byte)60, (byte)160, (byte)90), SceneKit.TintWhite(60, 160, 90, t));
        Assert.Equal(((byte)180, (byte)150, (byte)240), SceneKit.TintWhite(180, 150, 240, t));
        Assert.Equal(((byte)60, (byte)60, (byte)60), SceneKit.TintWhite(60, 60, 60, t));
        // 흰 정도가 애매한 곳(채도 0.10~0.25)은 원래 색과 섞인다 — 가장자리가 끊겨 보이지 않게.
        (byte B, byte G, byte R) half = SceneKit.TintWhite(200, 240, 245, t);
        Assert.True(half.B < 200 && half.B > SceneKit.TintWhite(255, 255, 255, t).B);
    }

    // 꽃 그림에서 거의 흰 픽셀(가장 작은 값 205 초과, 가장 큰 값과 가장 작은 값의 차 30 미만)이 불투명 픽셀 가운데 얼마인가.
    private static double WhiteShare(byte[] px, bool tinted, Color? tint)
    {
        int opaque = 0, white = 0;
        for (int i = 0; i < px.Length; i += 4)
        {
            if (px[i + 3] < 200) continue;
            byte b = px[i], g = px[i + 1], r = px[i + 2];
            if (tinted && tint != null) (b, g, r) = SceneKit.TintWhite(b, g, r, tint.Value);
            opaque++;
            int mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
            if (mn > 205 && mx - mn < 30) white++;
        }
        return opaque == 0 ? 0 : (double)white / opaque;
    }

    private static string AcidRainFolder(string setId)
    {
        for (DirectoryInfo d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            string p = Path.Combine(d.FullName, "OpenTyping", "Resources", "AcidRain", setId);
            if (Directory.Exists(p)) return p;
        }
        throw new DirectoryNotFoundException("Resources\\AcidRain\\" + setId);
    }

    // 저장소의 그림 파일을 읽기만 한다(다시 저장하지 않는다 — 캔바 출처 정보).
    private static FormatConvertedBitmap LoadSheet(string setId, string file) =>
        new FormatConvertedBitmap(BitmapFrame.Create(new Uri(Path.Combine(AcidRainFolder(setId), file + ".png")),
                                                     BitmapCreateOptions.None, BitmapCacheOption.OnLoad), PixelFormats.Bgra32, null, 0);

    [Theory]
    [InlineData("GrowingFlowerGarden", "flowers")]
    [InlineData("GrowingFlowerGarden", "buds")]
    [InlineData("EcoEnergyVillage", "flowers")]
    public void 흰_꽃과_봉오리는_빠짐없이_색을_입혀_거의_흰_곳이_남지_않는다(string setId, string file)
    {
        (Int32Rect Piece, double StubX, int StubLen)[] heads = setId == "EcoEnergyVillage" ? EcoEnergyVillage.PlantFlowers
            : file == "buds" ? GrowingFlowerGarden.PlantBuds : GrowingFlowerGarden.PlantFlowers;
        Color?[] tints = setId == "EcoEnergyVillage" ? EcoEnergyVillage.HeadTints : GrowingFlowerGarden.HeadTints;
        Assert.Equal(heads.Length, tints.Length);

        FormatConvertedBitmap sheet = LoadSheet(setId, file);
        var report = new List<string>();
        for (int i = 0; i < heads.Length; i++)
        {
            Int32Rect r = heads[i].Piece;
            var px = new byte[r.Width * r.Height * 4];
            sheet.CopyPixels(r, px, r.Width * 4, 0);
            double before = WhiteShare(px, false, null), after = WhiteShare(px, true, tints[i]);
            // 흰 부분이 20% 이상인 꽃·봉오리에는 색이 정해져 있어야 하고, 색을 입힌 뒤에는 거의 흰 곳이 3% 미만이어야 한다.
            if ((before >= 0.2 && tints[i] == null) || after >= 0.03) report.Add($"{i + 1}번째: 흰 곳 {before:P0} → {after:P0}");
        }
        Assert.True(report.Count == 0, $"{setId}\\{file}.png: " + string.Join(", ", report));
    }

    [Theory]
    [InlineData("GrowingFlowerGarden")]
    [InlineData("EcoEnergyVillage")]
    public void 꽃마다_보이는_크기가_다른_꽃들의_가운데_값보다_많이_작지_않다(string setId)
    {
        // 썰렁이 지시 2026-10-26(태그 없는 지시): 파란 들꽃이 줄기의 잎에 비해 작아 보여 키웠다(FlowerSizes). 꽃 부분(밑동 토막을 뺀 불투명
        // 영역의 너비×높이)의 제곱근에 크기 배율을 곱한 값이 꽃들의 가운데 값의 75% 이상이어야 한다(키우기 전 파란 들꽃은 약 66%).
        (Int32Rect Piece, double StubX, int StubLen)[] heads = setId == "EcoEnergyVillage" ? EcoEnergyVillage.PlantFlowers : GrowingFlowerGarden.PlantFlowers;
        double[] sizes = setId == "EcoEnergyVillage" ? EcoEnergyVillage.FlowerSizes : GrowingFlowerGarden.FlowerSizes;
        Assert.Equal(heads.Length, sizes.Length);

        FormatConvertedBitmap sheet = LoadSheet(setId, "flowers");
        var shown = new double[heads.Length];
        for (int i = 0; i < heads.Length; i++)
        {
            Int32Rect r = heads[i].Piece;
            int h = r.Height - heads[i].StubLen;
            var px = new byte[r.Width * h * 4];
            sheet.CopyPixels(new Int32Rect(r.X, r.Y, r.Width, h), px, r.Width * 4, 0);
            int x0 = int.MaxValue, x1 = -1, y0 = int.MaxValue, y1 = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < r.Width; x++)
                    if (px[(y * r.Width + x) * 4 + 3] >= 128) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); }
            shown[i] = Math.Sqrt((x1 - x0 + 1.0) * (y1 - y0 + 1.0)) * sizes[i];
        }
        double median = shown.OrderBy(v => v).ElementAt(shown.Length / 2);
        var small = shown.Select((v, i) => (v, i)).Where(t => t.v < median * 0.75).Select(t => $"{t.i + 1}번째 {t.v / median:P0}").ToList();
        Assert.True(small.Count == 0, setId + " — 가운데 값보다 많이 작은 꽃: " + string.Join(", ", small));
    }
}
