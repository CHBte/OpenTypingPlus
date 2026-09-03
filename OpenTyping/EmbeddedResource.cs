using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OpenTyping
{
    /// <summary>
    /// 어셈블리 내장 리소스를 파일명 접미사로 찾아 텍스트로 읽는다. StageDefinitionLoader와
    /// PracticeWordList가 각자 같은 방식(리소스 이름 찾기 → 스트림 열기 → 끝까지 읽기)을 따로
    /// 들고 있던 것을 한 곳으로 모았다.
    /// </summary>
    internal static class EmbeddedResource
    {
        /// <summary>이름이 <paramref name="suffix"/>로 끝나는 리소스를 찾아 텍스트로 읽는다. 못 찾으면 null.</summary>
        public static string ReadText(Assembly asm, string suffix)
        {
            string resName = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
            if (resName == null) return null;

            using (Stream s = asm.GetManifestResourceStream(resName))
            using (var reader = new StreamReader(s))
                return reader.ReadToEnd();
        }
    }
}
