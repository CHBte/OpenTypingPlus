using System;
using System.IO;

namespace OpenTyping
{
    /// <summary>
    /// 텍스트를 파일에 원자적으로 쓴다(임시 파일에 먼저 쓴 뒤 교체) — 쓰다가 중단돼도(강제 종료 등)
    /// 원본 파일은 안전하다. StageRecords와 AcidRainWindow의 진행도 저장이 같은 방식을 각자 따로
    /// 구현하던 것을 모았다.
    /// </summary>
    internal static class AtomicFile
    {
        public static void WriteText(string path, string content)
        {
            // 임시 파일명이 인스턴스끼리 겹치지 않도록 프로세스 id를 붙인다(동시 저장 시 서로의
            // .tmp 파일을 밟아 교체가 실패하는 것을 막는다).
            string tmpPath = path + "." + Environment.ProcessId + ".tmp";
            bool tmpComplete = false;
            try
            {
                File.WriteAllText(tmpPath, content);
                tmpComplete = true;
                if (File.Exists(path)) File.Replace(tmpPath, path, null);
                // 확인과 이동 사이에 다른 인스턴스가 같은 파일을 먼저 만들어도 실패하지 않게 덮어쓴다.
                else File.Move(tmpPath, path, overwrite: true);
            }
            finally
            {
                // 성공했으면 .tmp는 이미 없다. 실패로 남았으면:
                //  - .tmp를 다 쓴 뒤 원본이 없어진 상태라면(백업 없이 부른 File.Replace가 원본을 지운 뒤
                //    이름 바꾸기에 실패하는 경우가 문서화돼 있다) .tmp가 유일한 사본이므로 지우지 말고
                //    원본 자리로 되살린다.
                //  - 그 밖에는(원본이 그대로이거나, .tmp를 쓰다가 실패해 내용이 불완전함) .tmp만 지운다.
                try
                {
                    if (File.Exists(tmpPath))
                    {
                        if (tmpComplete && !File.Exists(path) && !Directory.Exists(path)) File.Move(tmpPath, path);
                        else File.Delete(tmpPath);
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
