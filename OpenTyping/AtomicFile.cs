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
            File.WriteAllText(tmpPath, content);
            if (File.Exists(path)) File.Replace(tmpPath, path, null);
            else File.Move(tmpPath, path);
        }
    }
}
