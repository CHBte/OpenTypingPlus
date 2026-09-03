using System;
using System.IO;
using Newtonsoft.Json;

namespace OpenTyping
{
    /// <summary>
    /// 사용자 설정값(자판 선택, 자판/연습 데이터 폴더, 타속 계산 방법, 손가락 레이어 설정) 저장소
    /// (<260830_2-4-3-1>).
    ///
    /// 원래 .NET의 Properties.Settings(user.config)를 썼는데, user.config의 실제 저장 위치는
    /// LocalFileSettingsProvider가 어셈블리 이름 기반 해시 폴더로 자체 결정해(예:
    /// AppData\Local\열린타자+\열린타자+_Path_&lt;해시&gt;\...) AppData\OTP\OpenTypingPlus\ 밑으로
    /// 옮길 수 없었다. KeyLayoutUserDataStore/StageRecords와 같은 패턴(직접 JSON 파일)으로 교체해
    /// 원하는 경로에 둘 수 있게 했다. user.config 시절 저장된 값은 자동 이전하지 않는다(새 설치처럼
    /// 기본값에서 시작 — 자판 선택·손가락 레이어 취향 정도라 재설정 부담이 적다고 판단).
    /// </summary>
    public static class UserSettingsStore
    {
        /// <summary>App.xaml.cs의 -edgetest 진단이 "설정 기본값"을 확인하는 데 쓴다(예전엔 Settings.
        /// Properties[...].DefaultValue 리플렉션으로 읽었으나 그 메커니즘 자체가 없어져 상수로 노출).</summary>
        public const string DefaultFingerLayerColor = "#F76707";

        private class Store
        {
            public string KeyLayout { get; set; } = "";
            public string KeyLayoutDataDir { get; set; } = "";
            public string PracticeDataDir { get; set; } = "";
            public string TpmMethod { get; set; } = "simple";
            public bool FingerLayerEnabled { get; set; } = true;
            public double FingerLayerThickness { get; set; } = 3.5;
            public string FingerLayerColor { get; set; } = DefaultFingerLayerColor;
            public double FingerLayerOpacity { get; set; } = 0.65;
        }

        private static readonly string FilePath = BuildPath();
        private static readonly Store Data = Load();

        private static string BuildPath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OTP", "OpenTypingPlus");
            try { Directory.CreateDirectory(dir); } catch { /* 실패해도 저장 시점에 다시 시도됨 */ }
            return Path.Combine(dir, "user_settings.json");
        }

        private static Store Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    Store s = JsonConvert.DeserializeObject<Store>(File.ReadAllText(FilePath));
                    if (s != null) return s;
                }
            }
            catch { /* 손상 시 기본값으로 시작 */ }
            return new Store();
        }

        public static string KeyLayout
        {
            get => Data.KeyLayout;
            set => Data.KeyLayout = value;
        }

        public static string KeyLayoutDataDir
        {
            get => Data.KeyLayoutDataDir;
            set => Data.KeyLayoutDataDir = value;
        }

        public static string PracticeDataDir
        {
            get => Data.PracticeDataDir;
            set => Data.PracticeDataDir = value;
        }

        public static string TpmMethod
        {
            get => Data.TpmMethod;
            set => Data.TpmMethod = value;
        }

        public static bool FingerLayerEnabled
        {
            get => Data.FingerLayerEnabled;
            set => Data.FingerLayerEnabled = value;
        }

        public static double FingerLayerThickness
        {
            get => Data.FingerLayerThickness;
            set => Data.FingerLayerThickness = value;
        }

        public static string FingerLayerColor
        {
            get => Data.FingerLayerColor;
            set => Data.FingerLayerColor = value;
        }

        public static double FingerLayerOpacity
        {
            get => Data.FingerLayerOpacity;
            set => Data.FingerLayerOpacity = value;
        }

        /// <summary>
        /// 예전 Settings.Default[문자열 상수] 인덱서 호출부(MainWindow.KeyLayoutDataDirStr 등)를
        /// 옮기는 용도. 정적 클래스는 인덱서를 선언할 수 없어(컴파일해서 확인함 — CS0720) 이름을
        /// 문자열로 받는 메서드 쌍으로 대신한다.
        /// </summary>
        public static object Get(string name)
        {
            switch (name)
            {
                case nameof(KeyLayout): return KeyLayout;
                case nameof(KeyLayoutDataDir): return KeyLayoutDataDir;
                case nameof(PracticeDataDir): return PracticeDataDir;
                case nameof(TpmMethod): return TpmMethod;
                case nameof(FingerLayerEnabled): return FingerLayerEnabled;
                case nameof(FingerLayerThickness): return FingerLayerThickness;
                case nameof(FingerLayerColor): return FingerLayerColor;
                case nameof(FingerLayerOpacity): return FingerLayerOpacity;
                default: throw new ArgumentException("알 수 없는 설정 이름: " + name, nameof(name));
            }
        }

        public static void Set(string name, object value)
        {
            switch (name)
            {
                case nameof(KeyLayout): KeyLayout = (string)value; break;
                case nameof(KeyLayoutDataDir): KeyLayoutDataDir = (string)value; break;
                case nameof(PracticeDataDir): PracticeDataDir = (string)value; break;
                case nameof(TpmMethod): TpmMethod = (string)value; break;
                case nameof(FingerLayerEnabled): FingerLayerEnabled = (bool)value; break;
                case nameof(FingerLayerThickness): FingerLayerThickness = (double)value; break;
                case nameof(FingerLayerColor): FingerLayerColor = (string)value; break;
                case nameof(FingerLayerOpacity): FingerLayerOpacity = (double)value; break;
                default: throw new ArgumentException("알 수 없는 설정 이름: " + name, nameof(name));
            }
        }

        /// <summary>디스크에 씀. 옛 Settings.Default.Save()와 마찬가지로 실패 시 예외를 그대로 던진다
        /// (KeyLayoutUserDataStore.Save()와 같은 방침 — 실패를 조용히 감추지 않음).</summary>
        public static void Save()
        {
            AtomicFile.WriteText(FilePath, JsonConvert.SerializeObject(Data, Formatting.Indented));
        }
    }
}
