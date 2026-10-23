using System.IO;
using OpenTyping;

namespace OpenTyping.Tests;

// 원자적 쓰기 (예전 -edgetest 14·18번)
public class AtomicFileTests
{
    [Fact]
    public void 교체에_실패해도_tmp_파일을_남기지_않는다()
    {
        string dir = Path.Combine(Path.GetTempPath(), "otp_test_atomic_" + Guid.NewGuid().ToString("N"));
        string blocked = Path.Combine(dir, "target.json");
        Directory.CreateDirectory(blocked);   // 같은 이름의 폴더가 있어 파일로 옮길 수 없다
        try
        {
            Assert.ThrowsAny<Exception>(() => AtomicFile.WriteText(blocked, "{}"));
            Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void 저장_폴더가_사라졌으면_다시_만들어_쓴다()
    {
        string root = Path.Combine(Path.GetTempPath(), "otp_test_gone_" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(root, "sub", "f.json");
        try
        {
            AtomicFile.WriteText(path, "{\"a\":1}");
            Assert.Equal("{\"a\":1}", File.ReadAllText(path));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
