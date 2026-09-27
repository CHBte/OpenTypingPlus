using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OpenTyping
{
    /// <summary>
    /// 어셈블리 내장 리소스를 파일명 접미사로 찾아 텍스트로 읽는다(리소스 이름 찾기 → 스트림 열기 →
    /// 끝까지 읽기). StageDefinitionLoader(내장 단계 정의)와 WordCatalog(내장 예비 단어 목록)가 쓴다.
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
