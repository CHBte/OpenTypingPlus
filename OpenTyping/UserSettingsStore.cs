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

        // 파일이 있는데 입출력 오류(다른 프로그램이 잡고 있는 경우 등)로 끝내 못 읽었으면 true — 이때 기본값으로 시작하되,
        // 그 기본값이 사용자의 진짜 설정 파일을 덮어쓰지 않도록 Save 가 건너뛴다.
        private static bool unreadable;
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
            // 바이러스 검사기 등이 잠깐 파일을 잡는 일은 짧게 기다리면 풀리므로 몇 번 다시 읽어 본다.
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    if (File.Exists(FilePath))
                    {
                        Store s = JsonConvert.DeserializeObject<Store>(File.ReadAllText(FilePath));
                        if (s != null)
                        {
                            Sanitize(s);
                            return s;
                        }
                    }
                    return new Store();
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    if (attempt == 3) unreadable = true;
                    else System.Threading.Thread.Sleep(100);
                }
                catch { return new Store(); /* 손상 시 기본값으로 시작 */ }
            }
            return new Store();
        }

        /// <summary>
        /// 손으로 고치거나 손상된 파일의 값을 쓸 수 있는 값으로 되돌린다. 특히 JSON 의 NaN(Newtonsoft 가 읽는다)은
        /// 손 모양 창의 슬라이더에 넣는 순간 예외를 내서, 그 창에서만 고칠 수 있는 설정이 그 창을 못 열게 만든다.
        /// 범위는 손가락 레이어가 실제로 받아들이는 범위(FingerLayer.ApplySettings: 두께 0.5~12, 투명도 5~100%)와 같다 —
        /// 손으로 고친 그 안쪽 값을 더 좁게 바꾸지 않는다.
        /// </summary>
        private static void Sanitize(Store s)
        {
            s.KeyLayout = s.KeyLayout ?? "";
            s.KeyLayoutDataDir = s.KeyLayoutDataDir ?? "";
            s.PracticeDataDir = s.PracticeDataDir ?? "";
            s.TpmMethod = s.TpmMethod ?? "simple";
            if (string.IsNullOrWhiteSpace(s.FingerLayerColor)) s.FingerLayerColor = DefaultFingerLayerColor;
            s.FingerLayerThickness = double.IsNaN(s.FingerLayerThickness)
                ? 3.5 : Math.Max(0.5, Math.Min(12.0, s.FingerLayerThickness));
            s.FingerLayerOpacity = double.IsNaN(s.FingerLayerOpacity)
                ? 0.65 : Math.Max(0.05, Math.Min(1.0, s.FingerLayerOpacity));
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
            if (unreadable)
                throw new IOException("설정 파일을 읽지 못한 채 기본값으로 시작해서, 원래 설정을 덮어쓰지 않으려고 저장하지 않았습니다. 프로그램을 다시 실행해 보세요.");
            AtomicFile.WriteText(FilePath, JsonConvert.SerializeObject(Data, Formatting.Indented));
        }

        /// <summary>
        /// <see cref="Save"/>와 같지만, 파일이 잠겼거나 디스크가 가득 찼거나 읽기 전용인 경우(입출력 실패)를
        /// 예외 대신 메시지로 돌려준다. 창을 닫는 도중 저장하는 곳(Closed·Closing 처리기)이 쓴다 — 거기서 예외가
        /// 나면 전역 처리기의 엉뚱한 오류창이 뜨기 때문이다(MainWindow_Closed 와 같은 방침).
        /// </summary>
        public static bool TrySave(out string error)
        {
            try
            {
                Save();
                error = null;
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                                       ex is System.Security.SecurityException)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
