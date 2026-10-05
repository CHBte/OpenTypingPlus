using System;
using System.IO;
using System.Text;

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
            // 저장 폴더가 중간에 사라졌어도(정리 프로그램·수동 삭제 등) 다시 만든다 — 안 그러면 그 뒤 모든 저장이
            // DirectoryNotFoundException 으로 실패한다(호출부 대부분이 그 실패를 조용히 삼킨다).
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            // 임시 파일명이 인스턴스끼리 겹치지 않도록 프로세스 id를 붙인다(동시 저장 시 서로의
            // .tmp 파일을 밟아 교체가 실패하는 것을 막는다).
            string tmpPath = path + "." + Environment.ProcessId + ".tmp";
            bool tmpComplete = false;
            try
            {
                // 디스크까지 확실히 내려쓴(Flush(true)) 뒤에 교체한다 — 교체 직후 전원이 나가도 길이 0 인 파일이
                // 원본 자리에 남지 않게. (File.WriteAllText 와 같은 UTF-8, BOM 없음.)
                using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(content);
                    fs.Write(bytes, 0, bytes.Length);
                    fs.Flush(true);
                }
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

        /// <summary>
        /// 손상돼 읽을 수 없는 파일을 <c>.bad</c> 로 복사해 둔다. 읽지 못한 파일은 '빈 기록으로 시작'해 곧이어 정상 내용으로
        /// 덮어쓰는 방침이라, 그 전에 사본을 남겨 두면 사용자가 손으로 살릴 수 있다. 복사에 실패해도 조용히 넘어간다.
        /// </summary>
        public static void BackUpCorrupt(string path)
        {
            try { File.Copy(path, path + ".bad", true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
