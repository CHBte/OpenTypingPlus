using Newtonsoft.Json;
using OpenTyping.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTyping
{
    public class KeyLayout
    {
        public KeyLayout(string name, string character, IList<IList<Key>> keyLayoutData, List<KeyPos> defaultKeys)
        {  
            Name = name;
            Character = character;
            KeyLayoutData = keyLayoutData;
            DefaultKeys = defaultKeys;
        }

        public string Name { get; }
        public string Character { get; }
        public IList<IList<Key>> KeyLayoutData { get; }
        public List<KeyPos> DefaultKeys { get; set; }

        /// <summary>
        /// '설정' 창 '현재 자판' 목록에서의 차례 (<260812_8>). 작은 값이 위에 온다.
        /// 적혀 있지 않은 자판(사용자가 나중에 추가한 것)은 맨 뒤로 가고, 그들끼리는 이름순이다.
        /// </summary>
        [JsonProperty]
        public int Order { get; set; } = int.MaxValue;

        [JsonProperty]
        public KeyLayoutStats Stats { get; set; } = new KeyLayoutStats();

        [JsonIgnore]
        public string Location { get; set; } = "";

        public Key this[KeyPos pos] => KeyLayoutData[pos.Row][pos.Column];

        // 통계(Stats)·사용자가 고른 연습 키(DefaultKeys)는 자판 정의 파일(keyLayout.Location, 실행
        // 파일 옆이라 향후 Program Files처럼 쓰기 금지된 곳일 수 있음)이 아니라 항상 쓰기 가능한
        // AppData(KeyLayoutUserDataStore)에 저장한다 (<260828_1>). 그래도 디스크 쓰기가 실패할
        // 가능성(디스크 꽉 참 등)은 남으므로 앱이 죽지 않도록 여전히 감싼다 — 실패하면 false를
        // 반환하고 errorMessage에 원인을 담는다.
        public static bool TrySaveKeyLayout(KeyLayout keyLayout, out string errorMessage)
        {
            try
            {
                KeyLayoutUserDataStore.Save(keyLayout.Name, keyLayout.Stats, keyLayout.DefaultKeys);
                errorMessage = null;
                return true;
            }
            catch (Exception ex) when (ex is IOException ||
                                       ex is UnauthorizedAccessException ||
                                       ex is System.Security.SecurityException)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static KeyLayout Parse(string data)
        {
            KeyLayout keyLayout = JsonConvert.DeserializeObject<KeyLayout>(data);

            if (keyLayout is null) // 파일 내용이 "null" 등일 때 역직렬화 결과가 null이 될 수 있음
            {
                const string message = "자판 데이터가 비어 있습니다.";
                throw new InvalidKeyLayoutDataException(message);
            }

            if (string.IsNullOrEmpty(keyLayout.Name))
            {
                const string message = "자판 데이터의 이름(Name 필드)이 주어지지 않았습니다.";
                throw new InvalidKeyLayoutDataException(message);
            }

            if (keyLayout.KeyLayoutData is null)
            {
                const string message = "자판 데이터(KeyLayoutData 필드)가 주어지지 않았습니다.";
                throw new InvalidKeyLayoutDataException(message);
            }

            if (string.IsNullOrEmpty(keyLayout.Character))
            {
                const string message = "자판 데이터의 문자 종류(Character 필드)가 주어지지 않았습니다.";
                throw new InvalidKeyLayoutDataException(message);
            }

            if (keyLayout.DefaultKeys is null)
            {
                keyLayout.DefaultKeys = new List<KeyPos>();
            }

            var rowNumberData = new List<Tuple<string, int>>
            {
                Tuple.Create("숫자열", 13),
                Tuple.Create("첫째 열", 13),
                Tuple.Create("둘째 열", 11),
                Tuple.Create("셋째 열", 10)
            };

            if (keyLayout.KeyLayoutData.Count != rowNumberData.Count)
            {
                string message = "자판 데이터의 열 개수는 " + rowNumberData.Count + " 이어야 하는데 "
                               + keyLayout.KeyLayoutData.Count + "개가 주어졌습니다.";
                throw new InvalidKeyLayoutDataException(message);
            }

            for (int i = 0; i < keyLayout.KeyLayoutData.Count; i++)
            {
                if (keyLayout.KeyLayoutData[i] == null)
                {
                    throw new InvalidKeyLayoutDataException(rowNumberData[i].Item1 + " 데이터가 비어 있습니다.");
                }
                if (keyLayout.KeyLayoutData[i].Count != rowNumberData[i].Item2)
                {
                    string message = rowNumberData[i].Item1 + "의 키 개수는 " + rowNumberData[i].Item2 + " 이어야 하는데 "
                                   + keyLayout.KeyLayoutData[i].Count + "개가 주어졌습니다.";
                    throw new InvalidKeyLayoutDataException(message);
                }

                // 키 칸이 null 로 적혀 있으면 클래식 연습 창이 그 칸을 고를 때 NullReferenceException 을 낸다 — 빈 키로 대신한다.
                for (int j = 0; j < keyLayout.KeyLayoutData[i].Count; j++)
                {
                    if (keyLayout.KeyLayoutData[i][j] == null) keyLayout.KeyLayoutData[i][j] = new Key();
                }
            }

            // 손상되었거나 조작된 파일에 대비해 파싱 단계에서 정리한다.
            SanitizeAgainstGrid(keyLayout);

            return keyLayout;
        }

        // 자판 격자 범위를 벗어난 키 위치(DefaultKeys, Stats.KeyIncorrectCount)를 걸러낸다. 남아
        // 있으면 이후 인덱싱(대문 화면 통계 표시 등)에서 앱이 죽는다. 손상·조작된 자판 파일뿐 아니라
        // (<260828_1>) AppData 저장소(KeyLayoutUserDataStore)에서 불러온 값에도 같은 기준을 적용한다
        // — 자판 격자가 바뀐 뒤에도 예전에 저장해 둔 범위 밖 위치가 살아남지 않게 한다.
        private static void SanitizeAgainstGrid(KeyLayout keyLayout)
        {
            bool InRange(KeyPos pos) => pos != null &&
                                        pos.Row >= 0 && pos.Row < keyLayout.KeyLayoutData.Count &&
                                        pos.Column >= 0 && pos.Column < keyLayout.KeyLayoutData[pos.Row].Count;

            keyLayout.DefaultKeys = (keyLayout.DefaultKeys ?? new List<KeyPos>()).Where(InRange).ToList();

            keyLayout.Stats ??= new KeyLayoutStats(); // "Stats": null 로 저장된 파일
            keyLayout.Stats.KeyIncorrectCount =
                (keyLayout.Stats.KeyIncorrectCount ?? new Dictionary<KeyPos, int>())
                    .Where(kv => InRange(kv.Key))
                    .ToDictionary(kv => kv.Key, kv => kv.Value);

            // 횟수·평균은 음수일 수 없다 — 손상된 파일의 음수가 평균 계산(합이 0 이 되어 0 으로 나눔)을 깨뜨리지 않게.
            keyLayout.Stats.SentencePracticeCount = Math.Max(0, keyLayout.Stats.SentencePracticeCount);
            keyLayout.Stats.AverageTypingSpeed = Math.Max(0, keyLayout.Stats.AverageTypingSpeed);
            keyLayout.Stats.AverageAccuracy = Math.Max(0, Math.Min(100, keyLayout.Stats.AverageAccuracy));
            keyLayout.Stats.KeyIncorrectCount =
                keyLayout.Stats.KeyIncorrectCount.ToDictionary(kv => kv.Key, kv => Math.Max(0, kv.Value));

            // MostIncorrect도 저장된 값을 신뢰하지 않고 정리된 딕셔너리에서 다시 계산한다.
            keyLayout.Stats.RecomputeMostIncorrect();
        }

        public static KeyLayout Load(string dataFileLocation)
        {
            string keyLayoutLines = File.ReadAllText(dataFileLocation, Encoding.UTF8);

            try
            {
                KeyLayout keyLayout = Parse(keyLayoutLines);
                keyLayout.Location = dataFileLocation;

                // <260828_1>: 자판 파일에 남아 있던 값(방금 Parse가 채움) 위에, AppData에 저장된
                // 최신 통계·연습 키가 있으면 그것으로 덮어쓴다. 그 값도 같은 격자 기준으로 다시 거른다.
                KeyLayoutUserDataStore.ApplyTo(keyLayout);
                SanitizeAgainstGrid(keyLayout);

                return keyLayout;
            }
            catch (InvalidKeyLayoutDataException ex)
            {
                throw new InvalidKeyLayoutDataException(dataFileLocation + " : " + ex.Message, ex);
            }
            catch (Exception ex) when (ex is JsonException || ex is FormatException)
            {
                // JSON 형식 오류, 또는 키 위치 값 등 개별 값의 형식 오류로 파일을 읽지 못한 경우
                // (Newtonsoft가 변환기 예외를 JsonSerializationException으로 감싸므로 내부 메시지를 우선 사용)
                string reason = ex.InnerException?.Message ?? ex.Message;
                throw new InvalidKeyLayoutDataException(dataFileLocation + " : 자판 데이터 파일을 읽을 수 없습니다. (" + reason + ")", ex);
            }
        }

        public static IList<KeyLayout> LoadFromDirectory(string layoutsDirectory)
        {
            var keyLayouts = new List<KeyLayout>();

            Directory.CreateDirectory(layoutsDirectory);
            string[] keyLayoutFiles = Directory.GetFiles(layoutsDirectory, "*.json");

            if (!keyLayoutFiles.Any())
            {
                // 저장된 설정값이 아니라 실제로 검사한 경로(layoutsDirectory)를 알려야 한다 — 설정
                // 창에서 방금 고른 새 경로가 문제일 때, 아직 저장 전이라 다른(예전) 경로가 뜨면 안 된다.
                string message = "경로 " + layoutsDirectory +
                                 "에서 자판 데이터 파일을 찾을 수 없습니다. 해당 경로에 자판 데이터를 생성하고 다시 시도하세요.";
                throw new KeyLayoutLoadFail(message);
            }

            foreach (string keyLayoutFile in keyLayoutFiles)
            {
                KeyLayout keyLayout = Load(keyLayoutFile);
                KeyLayout duplicate = keyLayouts.Find(kl => kl.Name == keyLayout.Name);

                if (duplicate != null)
                {
                    string message = "자판 이름 \"" + keyLayout.Name + "\" 이 중복되게 존재합니다.\n" +
                                     keyLayout.Location + "\n" + duplicate.Location;
                    throw new KeyLayoutLoadFail(message);
                }
                keyLayouts.Add(keyLayout);
            }

            // 파일 이름순(DubeolsikStandard, Dvorak, Qwerty, Sebeolsik390)이 아니라 자판 데이터에 적힌
            // 차례대로 보여 준다 (<260812_8>). 차례가 없는 자판은 뒤에 이름순으로 붙는다.
            return keyLayouts
                .OrderBy(kl => kl.Order)
                .ThenBy(kl => kl.Name, StringComparer.CurrentCulture)
                .ToList();
        }
    }
}