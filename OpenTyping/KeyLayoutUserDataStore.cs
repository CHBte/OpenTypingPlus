using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenTyping
{
    /// <summary>
    /// 자판(KeyLayout) 하나의 사용자 데이터 — 오타 통계(Stats)와 사용자가 고른 연습 키(DefaultKeys).
    /// </summary>
    public class KeyLayoutUserData
    {
        public KeyLayoutStats Stats { get; set; } = new KeyLayoutStats();
        public List<KeyPos> DefaultKeys { get; set; }
    }

    /// <summary>
    /// 자판별 사용자 데이터를 자판 이름(Name)을 키로 삼아 한 파일에 모아 담는다 (<260828_1>).
    ///
    /// 예전에는 이 데이터를 자판 정의 파일(layouts\*.json) 자체에 얹어 저장했는데, 그 파일은 실행
    /// 파일 옆(향후 Program Files처럼 쓰기 금지된 곳일 수 있음)에 있어 매번 앱을 닫을 때마다 저장이
    /// 실패할 수 있었다. 항상 쓰기 가능한 %APPDATA%\OpenTypingPlus\ 밑에 따로 저장해 분리한다.
    /// StageRecords와 같은 패턴(병합 후 원자적 쓰기)을 따른다.
    /// </summary>
    public static class KeyLayoutUserDataStore
    {
        private static readonly string FilePath = BuildPath();

        private static string BuildPath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OTP", "OpenTypingPlus");
            try { Directory.CreateDirectory(dir); } catch { /* 실패해도 저장 시점에 다시 시도됨 */ }
            return Path.Combine(dir, "key_layout_data.json");
        }

        private static Dictionary<string, KeyLayoutUserData> ReadFromDisk()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var data = JsonConvert.DeserializeObject<Dictionary<string, KeyLayoutUserData>>(
                        File.ReadAllText(FilePath, Encoding.UTF8));
                    if (data != null) return data;
                }
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException)
            {
                /* 손상 시 빈 상태로 시작 — 다음 저장에서 정상 내용으로 복구된다 */
            }
            return new Dictionary<string, KeyLayoutUserData>();
        }

        /// <summary>
        /// 이 저장소에 keyLayout.Name의 데이터가 있으면 그것을 keyLayout에 반영한다.
        ///
        /// 저장소에 아직 없으면(첫 실행이거나, 이 자판을 아직 한 번도 저장하지 않음) 아무것도 하지
        /// 않는다 — keyLayout은 KeyLayout.Parse가 자판 파일에서 읽어 채워 둔 값(예전 방식으로 파일에
        /// 남아 있던 통계, 또는 새 파일의 기본값)을 그대로 유지한다. 그 값은 다음 저장 시점
        /// (KeyLayout.TrySaveKeyLayout)에 자연스럽게 이 저장소로 옮겨지므로 별도의 이전(migration)
        /// 코드가 필요 없다 — 기존 사용자가 쌓아 둔 통계를 잃지 않는다.
        /// </summary>
        public static void ApplyTo(KeyLayout keyLayout)
        {
            if (!ReadFromDisk().TryGetValue(keyLayout.Name, out KeyLayoutUserData data)) return;

            keyLayout.Stats = data.Stats ?? new KeyLayoutStats();
            if (data.DefaultKeys != null)
            {
                keyLayout.DefaultKeys = data.DefaultKeys;
            }
        }

        public static void Save(string layoutName, KeyLayoutStats stats, List<KeyPos> defaultKeys)
        {
            // 다른 자판의 데이터를 잃지 않도록 디스크의 최신 내용과 병합한 뒤 이 자판 항목만 갱신한다
            // (여러 창·프로세스가 서로 다른 자판을 각각 저장할 때를 대비 — StageRecords와 같은 이유).
            Dictionary<string, KeyLayoutUserData> all = ReadFromDisk();
            all[layoutName] = new KeyLayoutUserData { Stats = stats, DefaultKeys = defaultKeys };
            AtomicFile.WriteText(FilePath, JsonConvert.SerializeObject(all, Formatting.Indented));
        }
    }
}
