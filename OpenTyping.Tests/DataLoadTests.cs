using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using OpenTyping;

namespace OpenTyping.Tests;

// 자판·연습 데이터·단어 목록·단계 정의·설정 파일 읽기 (예전 -edgetest 6·7·12·13·16·17·19·20번)
public class DataLoadTests
{
    private static string LayoutJson() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "layouts", "DubeolsikStandard.json"));

    private static string NewTempDir(string tag)
    {
        string dir = Path.Combine(Path.GetTempPath(), "otp_test_" + tag + "_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void 빈_자판_폴더_오류_메시지에_넘긴_경로가_들어간다()
    {
        string dir = NewTempDir("empty_layouts");
        try
        {
            var ex = Assert.Throws<KeyLayoutLoadFail>(() => KeyLayout.LoadFromDirectory(dir));
            Assert.Contains(dir, ex.Message);
        }
        finally { Directory.Delete(dir); }
    }

    [Fact]
    public void 자판_데이터_첫_행이_null이면_원인을_알_수_있는_예외()
    {
        var jobj = JObject.Parse(LayoutJson());
        ((JArray)jobj["KeyLayoutData"])[0] = JValue.CreateNull();
        Assert.Throws<InvalidKeyLayoutDataException>(() => KeyLayout.Parse(jobj.ToString()));
    }

    [Fact]
    public void 빈_연습_데이터_폴더_오류_메시지에_넘긴_경로가_들어간다()
    {
        string dir = NewTempDir("empty_practice");
        try
        {
            var ex = Assert.Throws<PracticeDataLoadFail>(() => PracticeData.LoadFromDirectory(dir));
            Assert.Contains(dir, ex.Message);
        }
        finally { Directory.Delete(dir); }
    }

    [Fact]
    public void NFD로_저장된_단어도_완성형으로_읽는다()
    {
        string nfd = "나라".Normalize(NormalizationForm.FormD);
        try
        {
            WordCatalog.UseJsonForTest("{\"hangul\":{\"stages\":[{\"stage\":1,\"levels\":[{\"level\":1,\"words\":[\"" + nfd + "\"]}]}]}}");
            Assert.Equal(new[] { "나라" }, WordCatalog.RawLevelWords(WordCatalog.Hangul, 1, 1));
        }
        finally { WordCatalog.UseJsonForTest(null); }
    }

    [Fact]
    public void 연습_데이터는_NFC로_맞추고_null과_빈_줄을_버린다()
    {
        string nfdLine = "나라".Normalize(NormalizationForm.FormD);
        PracticeData parsed = PracticeData.Parse(
            "{\"Name\":\"t\",\"Character\":\"한글\",\"TextData\":[\"" + nfdLine + "\",null,\"  \"]}");
        Assert.Equal(new[] { "나라" }, parsed.TextData);
    }

    [Fact]
    public void 쓸_줄이_없는_연습_데이터는_원인을_알_수_있는_예외()
    {
        Assert.Throws<InvalidPracticeDataException>(() =>
            PracticeData.Parse("{\"Name\":\"t\",\"Character\":\"한글\",\"TextData\":[null]}"));
    }

    [Fact]
    public void 자판의_null_키_칸은_빈_키로_음수_통계는_0으로()
    {
        var jl = JObject.Parse(LayoutJson());
        ((JArray)((JArray)jl["KeyLayoutData"])[1])[3] = JValue.CreateNull();
        jl["Stats"] = JObject.Parse("{\"SentencePracticeCount\":-5,\"AverageTypingSpeed\":-3}");
        KeyLayout parsed = KeyLayout.Parse(jl.ToString());
        Assert.NotNull(parsed.KeyLayoutData[1][3]);
        Assert.Equal(0, parsed.Stats.SentencePracticeCount);
        Assert.Equal(0, parsed.Stats.AverageTypingSpeed);
    }

    [Fact]
    public void 통계_평균_합산이_넘쳐도_예외_없이_범위_안()
    {
        var big = new KeyLayoutStats { SentencePracticeCount = int.MaxValue, AverageTypingSpeed = 2 };
        big.AddStats(new KeyLayoutStats { SentencePracticeCount = 1, AverageTypingSpeed = 3 });
        Assert.InRange(big.AverageTypingSpeed, 2, 3);
    }

    [Fact]
    public void 단계_정의의_키_목록_길이에_상한이_있다()
    {
        int count = StageDefinitionLoader.Tokens(new JValue(new string('a', 100000))).Count;
        Assert.InRange(count, 1, 1000);
    }

    [Fact]
    public void 설정_파일의_NaN과_범위_밖_값을_기본값으로_되돌린다()
    {
        // Store 와 Sanitize 는 비공개라 리플렉션으로 부른다.
        var flags = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        Type storeType = typeof(UserSettingsStore).GetNestedType("Store", BindingFlags.NonPublic);
        object store = Activator.CreateInstance(storeType);
        storeType.GetProperty("FingerLayerThickness").SetValue(store, double.NaN);
        storeType.GetProperty("FingerLayerOpacity").SetValue(store, 99.0);
        storeType.GetProperty("TpmMethod").SetValue(store, null);
        typeof(UserSettingsStore).GetMethod("Sanitize", flags).Invoke(null, new[] { store });
        Assert.Equal(3.5, (double)storeType.GetProperty("FingerLayerThickness").GetValue(store));
        Assert.Equal(1.0, (double)storeType.GetProperty("FingerLayerOpacity").GetValue(store));
        Assert.Equal("simple", (string)storeType.GetProperty("TpmMethod").GetValue(store));
    }
}
