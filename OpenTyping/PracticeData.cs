using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;

namespace OpenTyping
{
    public class PracticeData
    {
        public PracticeData() {}

        public string Name { get; set; }
        public string Author { get; set; }
        public IList<string> TextData { get; set; }
        public string Character { get; set; }

        [JsonIgnore]
        public string Location { get; set; }

        public static PracticeData Parse(string data)
        {
            PracticeData practiceData = JsonConvert.DeserializeObject<PracticeData>(data);

            if (practiceData is null) // 파일 내용이 "null" 등일 때 역직렬화 결과가 null이 될 수 있음
            {
                const string message = "연습 데이터가 비어 있습니다.";
                throw new InvalidPracticeDataException(message);
            }

            if (string.IsNullOrEmpty(practiceData.Name))
            {
                const string message = "연습 데이터의 이름(Name 필드)이 주어지지 않았습니다.";
                throw new InvalidPracticeDataException(message);
            }

            if (practiceData.TextData is null)
            {
                const string message = "연습 데이터의 글자 데이터(TextData 필드)가 주어지지 않았습니다.";
                throw new InvalidPracticeDataException(message);
            }

            if (practiceData.TextData.Count == 0)
            {
                const string message = "연습 데이터의 글자 데이터(TextData 필드) 크기가 0 입니다.";
                throw new InvalidPracticeDataException(message);
            }

            if (string.IsNullOrEmpty(practiceData.Character))
            {
                const string message = "연습 데이터의 문자 종류(Character 필드)가 주어지지 않았습니다.";
                throw new InvalidPracticeDataException(message);
            }

            // 줄마다 완성형(NFC)으로 맞추고 null·빈 줄은 버린다. 자모를 풀어 쓴(NFD) 줄은 입력기가 만드는 완성형과
            // 영영 안 맞아 그 문장의 정확도·타속이 0이 되고(WordCatalog 와 같은 문제), null 줄은 연습 도중
            // NullReferenceException 을 키를 칠 때마다 되풀이한다.
            practiceData.TextData = practiceData.TextData
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(ToComposed)
                .ToList();

            if (practiceData.TextData.Count == 0)
            {
                const string message = "연습 데이터의 글자 데이터(TextData 필드)에 쓸 수 있는 문장이 없습니다.";
                throw new InvalidPracticeDataException(message);
            }

            return practiceData;
        }

        private static string ToComposed(string line)
        {
            try { return line.Normalize(NormalizationForm.FormC); }
            catch (ArgumentException) { return line; } // 짝 없는 서로게이트 등: 원문 그대로
        }

        public static PracticeData Load(string dataFileLocation)
        {
            string dataLines = File.ReadAllText(dataFileLocation, Encoding.UTF8);

            try
            {
                PracticeData practiceData = Parse(dataLines);
                practiceData.Location = dataFileLocation;

                return practiceData;
            }
            catch (InvalidPracticeDataException ex)
            {
                throw new InvalidPracticeDataException(dataFileLocation + " : " + ex.Message, ex);
            }
            catch (JsonException ex) // JSON 형식 오류 등으로 파일을 읽지 못한 경우
            {
                throw new InvalidPracticeDataException(dataFileLocation + " : 연습 데이터 파일을 읽을 수 없습니다. (" + ex.Message + ")", ex);
            }
        }

        public static IList<PracticeData> LoadFromDirectory(string dataDirectory, string character = null)
        {
            var practiceDataList = new List<PracticeData>();

            Directory.CreateDirectory(dataDirectory);
            string[] practiceDataFiles = Directory.GetFiles(dataDirectory, "*.json");

            if (!practiceDataFiles.Any())
            {
                // 저장된 설정값이 아니라 실제로 검사한 경로(dataDirectory)를 알려야 한다 — 설정
                // 창에서 방금 고른 새 경로가 문제일 때, 아직 저장 전이라 다른(예전) 경로가 뜨면 안 된다.
                string message = "경로 " + dataDirectory +
                                 "에서 연습 데이터 파일을 찾을 수 없습니다. 해당 경로에 연습 데이터를 생성하고 다시 시도하세요.";
                throw new PracticeDataLoadFail(message);
            }

            foreach (string practiceDataFile in practiceDataFiles)
            {
                PracticeData practiceData = Load(practiceDataFile);
                if (character != null && practiceData.Character != character)
                {
                    continue; 
                }

                PracticeData duplicate = practiceDataList.Find(data => data.Name == practiceData.Name);

                if (duplicate != null)
                {
                    string message = "연습 데이터 이름 \"" + practiceData.Name + "\" 이 중복되게 존재합니다.\n" +
                                     practiceData.Location + "\n" + duplicate.Location;
                    throw new PracticeDataLoadFail(message);
                }
                practiceDataList.Add(practiceData);
            }

            return practiceDataList;
        }

        public static PracticeData FitPracticeData(PracticeData oldData, TextBlock textBlock) // 넘치지 않게 연습 데이터의 문장들을 적절히 자른다.
        {
            PracticeData newData = new PracticeData()
            {
                Name = oldData.Name,
                Author = oldData.Author,
                Character = oldData.Character,
            };

            var newTextData = new List<string>();
            double pixelsPerDip = VisualTreeHelper.GetDpi(textBlock).PixelsPerDip;

            IList<string> FitLine(string line)
            {
                IList<string> splited = line.Split(' ').ToList();
                if (splited.Count == 1) return splited;

                for (int i = 2; i <= splited.Count; i++) // 첫 단어 하나는 더 쪼갤 수 없으므로(넘쳐도 한 줄로 확정) i = 2 부터 측정
                {
                    var formattedText = new FormattedText(
                        string.Join(" ", splited.Take(i)),
                        CultureInfo.CurrentCulture,
                        System.Windows.FlowDirection.LeftToRight,
                        new Typeface(textBlock.FontFamily,
                                     textBlock.FontStyle,
                                     textBlock.FontWeight,
                                     textBlock.FontStretch),
                        textBlock.FontSize,
                        Brushes.Black,
                        new NumberSubstitution(),
                        TextFormattingMode.Display,
                        pixelsPerDip);

                    if (formattedText.Width > textBlock.ActualWidth - 10)
                    {
                        var result = new List<string>();

                        result.Add(string.Join(" ", splited.Take(i - 1)));
                        result.AddRange(FitLine(string.Join(" ", splited.Skip(i - 1))));

                        return result;
                    }
                }

                return new List<string> { line };
            }

            foreach (string line in oldData.TextData)
            {
                newTextData.AddRange(FitLine(line));
            }

            newData.TextData = newTextData;
            return newData;
        }

    }
}
