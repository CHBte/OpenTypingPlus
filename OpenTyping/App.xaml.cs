using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ControlzEx.Theming;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using SkiaSharp;

namespace OpenTyping
{
    /// <summary>
    ///     App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        // 이 로그인 세션(같은 사용자) 안에서 중복 실행을 막는 데만 쓴다 — "Global\" 접두사를 안 붙였으므로
        // 다른 사용자 계정의 인스턴스는 막지 않는다. 프로세스가 끝나면 OS가 자동으로 풀어주므로 별도
        // 해제 코드가 필요 없다. 필드로 들고 있는 이유는 GC가 조기에 정리해 뮤텍스가 풀리는 것을 막기 위함.
        private const string SingleInstanceMutexName = "OpenTypingPlus-SingleInstance-{7B7B1C2E-6C1E-4B33-9B7B-4B7E2E9F3B10}";
        private Mutex singleInstanceMutex;

        /// <summary>
        /// 창이 한 번이라도 실제로 화면에 떴는지(Loaded 도달). OnStartup에서 심는 클래스 핸들러가
        /// 켠다. 전역 예외 처리기가 "시작 자체가 실패한 상태"와 "정상적으로 쓰다가 닫는 중"을
        /// 구분하는 데 쓴다 — 자세한 사정은 그 처리기의 주석 참고 (<260831 코드 검토>).
        /// </summary>
        private static bool anyWindowShown;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        // WPF의 MessageBox.Show는 소유(owner) 창을 다른 프로세스의 핸들로 지정할 방법이 없다.
        // 순서(활성화 먼저 → 메시지박스 나중)만으로 알림창을 항상 위에 오게 하려 했으나, 이미
        // 전경(foreground) 상태였던 창을 다시 활성화하면 그 전경 권한을 이 프로세스가 못 넘겨받아
        // 알림창이 오히려 뒤로 가는 경우가 실제로 있었다(<260828_2-1> 검증 중 발견). 그래서 원시
        // Win32 MessageBox를 owner 지정과 함께 직접 호출한다 — owner가 있는 창은 항상 그 owner보다
        // 위(topmost 관계)에 있도록 OS가 강제하므로, 타이밍에 좌우되지 않고 항상 보장된다.
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

        private const int SW_RESTORE = 9;
        private const uint MB_OK = 0x00000000;
        private const uint MB_ICONINFORMATION = 0x00000040;

        // <260828_2-1>: 이미 떠 있는 인스턴스의 대표 창 핸들을 찾는다. OTP의 여러 창이 전부 제목을
        // "열린타자+"로 공유해(FindWindow로는 어느 창인지 모호함) 창 제목이 아니라 프로세스 자체를
        // 찾아, 그 프로세스의 대표 창(MainWindowHandle)을 쓴다. 못 찾으면 IntPtr.Zero를 반환한다.
        private static IntPtr FindExistingInstanceWindow()
        {
            try
            {
                int currentId = Environment.ProcessId;
                Process other = Process.GetProcessesByName("열린타자+")
                                        .FirstOrDefault(p => p.Id != currentId);
                return other?.MainWindowHandle ?? IntPtr.Zero;
            }
            catch { return IntPtr.Zero; } // 실패해도 조용히 넘어간다 — 최소한 알림창은 그대로 뜬다
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // <2600919_4> 치트 상태일 때 모든 창의 테두리·제목표시줄 색(평소 파랑 계열 Cobalt)을
            // 하늘색(Cyan)으로 바꾼다. 헤드리스 진단 스위치(-cheattest 등)도 이 동작을 검증하므로,
            // 아래의 어떤 진단 분기보다도 먼저(Environment.Exit로 빠져나가기 전에) 구독해야 한다.
            StageRecords.CheatChanged += UpdateCheatTheme;
            UpdateCheatTheme();

            // 헤드리스 자체 검증: "-stagetest <출력경로>"로 실행하면 창을 띄우지 않고 단계 생성 결과를
            // 파일로 쓰고 즉시 종료한다(개발용, 배포에는 영향 없음). (<260723_4> 검증)
            if (e.Args != null && e.Args.Length > 0 && e.Args[0] == "-stagetest")
            {
                string outPath = e.Args.Length > 1 ? e.Args[1]
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                   "OpenTypingPlus", "stagetest.txt");
                try { RunStageSelfTest(outPath); } catch (Exception ex) { TryWrite(outPath, "EXCEPTION: " + ex); }
                Environment.Exit(0);
            }

            // 헤드리스 진단: "-homedump <출력png경로>"로 실행하면 실제 FingerLayer 컨트롤을 그대로
            // 생성자→ShowHomePose() 순서로 띄워(진짜 프로덕션 경로) 렌더링해 png 로 남기고 종료한다
            // (<260811_26>(2-1), hands\home-left/right.svg 이식이 실제로 화면에 반영되는지 확인하는
            // 개발용 도구 — 배포에는 영향 없음).
            // 헤드리스 진단: "-posesources <출력txt>" — 모든 물리 키(기본/윗글쇠)에 대해 SetPose 를 태워
            // **실제로 읽은 hands\ 파일 이름**을 한 줄씩 남긴다 (<260811_31-2>). 설치·배선이 어긋났는지를
            // 그림 비교 없이 값싸게 확인하는 용도 — update-hands.ps1 의 마지막 단계가 이 값을 대조한다.
            if (e.Args != null && e.Args.Length >= 2 && e.Args[0] == "-posesources")
            {
                try { RunPoseSources(e.Args[1]); }
                catch (Exception ex) { TryWrite(e.Args[1], "EXCEPTION: " + ex); }
                Environment.Exit(0);
            }

            // 헤드리스 진단: "-posedump <row> <col> <shift 0|1> <출력png>" — FingerLayer 를 실제로 만들어
            // SetPose(KeyPos, isShift) 를 그대로 태운 뒤 렌더링한다 (<260811_31>, 키별 hands\*.svg 이식이
            // 실제 화면 경로에 반영되는지 확인하는 개발용 도구 — 배포에는 영향 없음).
            if (e.Args != null && e.Args.Length >= 5 && e.Args[0] == "-posedump")
            {
                try
                {
                    RunPoseDump(int.Parse(e.Args[1]), int.Parse(e.Args[2]), e.Args[3] == "1", e.Args[4]);
                }
                catch (Exception ex) { TryWrite(e.Args[4] + ".txt", "EXCEPTION: " + ex); }
                Environment.Exit(0);
            }

            // 헤드리스 진단: "-typowarntest <출력txt>" — 오타 경고 문구 (<260812_15>)
            if (e.Args != null && e.Args.Length >= 2 && e.Args[0] == "-typowarntest")
            {
                try { RunTypoWarnTest(e.Args[1]); }
                catch (Exception ex) { TryWrite(e.Args[1], "EXCEPTION: " + ex); }
                return;   // 인트로를 기다려야 하므로 여기서 끝내지 않는다(테스트가 스스로 종료)
            }

            // 헤드리스 진단: "-englishtest <출력txt>" — 한글·영문 자리연습 화면 타일(그림) + 영문 단계 창의
            // Caps Lock 처리 (<260927_3>(2), <260927_5>, <260927_6>). 그림은 출력txt 옆에 남긴다.
            if (e.Args != null && e.Args.Length >= 2 && e.Args[0] == "-englishtest")
            {
                try { RunEnglishTest(e.Args[1]); }
                catch (Exception ex) { TryWrite(e.Args[1], "EXCEPTION: " + ex); Environment.Exit(0); }
                return;   // 인트로를 기다려야 하므로 여기서 끝내지 않는다(테스트가 스스로 종료)
            }

            // 헤드리스 진단: "-layouttest <출력txt>" — 자판 파일에서 만든 '글자 → 키 입력' 규칙(KeyboardMap) 검사.
            if (e.Args != null && e.Args.Length >= 2 && e.Args[0] == "-layouttest")
            {
                try { RunLayoutTest(e.Args[1]); }
                catch (Exception ex) { TryWrite(e.Args[1], "EXCEPTION: " + ex); }
                Environment.Exit(0);
            }

            // 헤드리스 진단: "-syllabletest <출력txt>" — 음절연습 2음절 오타 처리 (<260812_13>)
            if (e.Args != null && e.Args.Length >= 2 && e.Args[0] == "-syllabletest")
            {
                try { RunSyllableTest(e.Args[1]); }
                catch (Exception ex) { TryWrite(e.Args[1], "EXCEPTION: " + ex); }
                Environment.Exit(0);
            }

            // 헤드리스 진단: "-typedtest <출력txt>" — 입력 진행 상자의 조합 결과 (<260812_2>)
            if (e.Args != null && e.Args.Length >= 2 && e.Args[0] == "-typedtest")
            {
                try { RunTypedTest(e.Args[1]); }
                catch (Exception ex) { TryWrite(e.Args[1], "EXCEPTION: " + ex); }
                Environment.Exit(0);
            }

            // 헤드리스 진단: "-cheattest <출력txt>" — 치트 토글이 켜고 끄기 모두 되는지 확인한다.
            // 실제 기록 파일(%APPDATA%\OTP\OpenTypingPlus\stage_records_<자판 키>.json)을 쓰므로, 원래 내용을
            // 반드시 그대로 되돌려 놓는다.
            if (e.Args != null && e.Args.Length >= 2 && e.Args[0] == "-cheattest")
            {
                try { RunCheatTest(e.Args[1]); }
                catch (Exception ex) { TryWrite(e.Args[1], "EXCEPTION: " + ex); }
                Environment.Exit(0);
            }

            // 헤드리스 진단: "-edgetest <출력txt>" — qksqhr 1라운드 edge-test에서 고친 엣지케이스들이
            // 실제로 고쳐졌는지 (반올림/서로게이트/NaN 사고/받아쓰기 칸 상한/빈 음절 가드/자판 오류
            // 메시지·null 행/치트 값 보존/완료창 활성화 판정/산성비 지구효과 제외·치트 주머니 즉시반영).
            // 실제 기록 파일을 건드리므로 원래 내용을 반드시 그대로 되돌려 놓는다.
            if (e.Args != null && e.Args.Length >= 2 && e.Args[0] == "-edgetest")
            {
                try { RunEdgeTest(e.Args[1]); }
                catch (Exception ex) { TryWrite(e.Args[1], "EXCEPTION: " + ex); }
                Environment.Exit(0);
            }

            // 헤드리스 진단: "-classicdump <출력txt>" (<260811_33>)
            if (e.Args != null && e.Args.Length >= 2 && e.Args[0] == "-classicdump")
            {
                try { RunClassicDump(e.Args[1]); }
                catch (Exception ex) { TryWrite(e.Args[1], "EXCEPTION: " + ex); }
                Environment.Exit(0);
            }

            if (e.Args != null && e.Args.Length >= 1 && e.Args[0] == "-homedump")
            {
                string outPng = e.Args.Length > 1 ? e.Args[1]
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                   "OpenTypingPlus", "homedump.png");
                try { RunHomeDump(outPng); }
                catch (Exception ex) { TryWrite(outPng + ".txt", "EXCEPTION: " + ex); }
                Environment.Exit(0);
            }

            // <260828_2>: 중복 실행을 막는다. 위의 헤드리스 진단/테스트 스위치는 전부 여기까지 오기 전에
            // 끝나므로(이미 떠 있는 인스턴스와 무관하게 동작해야 함), 여기에 이르는 실행은 인자가 무엇이든
            // (파일을 끌어다 놓은 경로, 잘못 친 스위치 등) 평소 실행과 똑같이 막는다 — 안 그러면 두
            // 인스턴스가 떠서 설정·기록을 서로 덮어쓸 수 있다.
            singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out bool createdNew);
            if (!createdNew)
            {
                // <260828_2-1>: 기존 창을 활성화(+ 최소화였다면 복원)한 뒤, 그 창을 owner로 지정해
                // 알림창을 띄운다 — owner 지정 덕분에 알림창은 항상 그 창보다 위에 있다(OS가 보장).
                IntPtr hWnd = FindExistingInstanceWindow();
                if (hWnd != IntPtr.Zero)
                {
                    if (IsIconic(hWnd))
                    {
                        ShowWindow(hWnd, SW_RESTORE); // 최소화된 경우만 복원(최대화 상태를 되돌리지 않기 위해 무조건 호출하지 않음)
                    }
                    SetForegroundWindow(hWnd);
                }
                MessageBoxW(hWnd, "열린타자+가 이미 실행 중입니다.", "알림", MB_OK | MB_ICONINFORMATION);
                Environment.Exit(0);
            }

            // 개별 예외 처리가 놓친 예외로 앱 전체가 죽지 않도록 하는 마지막 안전망.
            //
            // <260831 실사용 중 발견>: MainWindow.xaml의 InitializeComponent()(예: KeyBox 생성)에서
            // 예외가 나면, 이 핸들러가 Handled=true로 잡아 "계속 진행"시키지만 정작 MainWindow는
            // 생성 도중 끊겨 창이 하나도 없다 — Dispatcher 메시지 루프만 살아 있는 채로 프로세스가
            // 남는다(작업 관리자엔 보이지만 창도 없고, 단일 실행 뮤텍스만 붙잡고 있어 다음 실행이
            // "이미 실행 중"만 보고 아무 반응 없는 것처럼 보임 — 재부팅 없이는 못 지워지는 원인도
            // 이 좀비 프로세스였을 가능성이 높음). 그리고 원래는 .Message만 보여줘 XamlParseException의
            // 진짜 원인(InnerException)이 숨겨져 있었다. 두 가지를 함께 고친다:
            //  1) InnerException 체인을 전부 이어 붙여 실제 원인이 보이게 한다.
            //  2) 창이 하나도 뜬 적 없는 상태(=시작 자체가 실패한 상태)면 "계속 진행" 대신 확실하게
            //     종료해, 좀비 프로세스로 남아 다음 실행을 막는 일이 없게 한다.
            //
            // <260831 코드 검토>: (2)의 판정을 처음엔 Current.Windows.Count == 0 으로 썼는데 그건
            // 양쪽으로 다 틀렸다. WPF는 Window 생성자에서 이미 Application.Windows에 자기를
            // 등록하므로, 정작 고치려던 경우(MainWindow의 InitializeComponent 도중 예외)엔 반쯤
            // 만들어진 창이 목록에 남아 Count==1 → 좀비가 그대로 남았다. 반대로 마지막 창을 닫는
            // 중(Closed 핸들러)에 예외가 나면 WPF가 이미 목록에서 뺀 뒤라 Count==0 → 정상 종료인데
            // "시작하지 못해 종료" 오안내와 함께 종료 코드 1이 됐다. "지금 창이 있나"가 아니라
            // "창이 한 번이라도 실제로 떴나"를 봐야 하므로, 아무 창이든 Loaded 되면 켜지는 플래그를
            // 클래스 핸들러로 심어 그 값을 쓴다.
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
                new RoutedEventHandler((s, a) => anyWindowShown = true));

            DispatcherUnhandledException += (sender, args) =>
            {
                var msg = new StringBuilder();
                Exception cur = args.Exception;
                while (cur != null)
                {
                    if (msg.Length > 0) msg.AppendLine("  →");
                    msg.AppendLine(cur.GetType().Name + ": " + cur.Message);
                    cur = cur.InnerException;
                }

                bool noWindowEverShown = !anyWindowShown;

                MessageBox.Show(
                    "예상하지 못한 오류가 발생했습니다.\n" + msg +
                    (noWindowEverShown ? "\n프로그램을 시작하지 못해 종료합니다." : ""),
                    "열린타자+",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                args.Handled = true;

                // 창이 하나도 없다는 건 시작 자체가 실패했다는 뜻 — 좀비로 남기지 않고 확실히 끝낸다.
                if (noWindowEverShown) Environment.Exit(1);
            };

            // 위 DispatcherUnhandledException은 UI(디스패처) 스레드의 예외만 잡는다. 스레드풀·백그라운드
            // Task에서 나는 예외는 이 경로를 타지 않아 아무 안내 없이 죽으므로, 나머지 두 표준 경로도
            // 마저 잡아 최소한 원인을 알려준다.
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                string msg = (args.ExceptionObject as Exception)?.Message ?? args.ExceptionObject?.ToString() ?? "(알 수 없음)";
                try
                {
                    Dispatcher.Invoke(() => MessageBox.Show(
                        "예상하지 못한 오류로 프로그램이 종료됩니다.\n" + msg,
                        "열린타자+", MessageBoxButton.OK, MessageBoxImage.Error));
                }
                catch { /* 이미 종료 절차 중이면 메시지를 못 띄울 수 있다 */ }
            };
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                args.SetObserved();   // 관찰됨으로 표시해 프로세스가 이 때문에 죽지 않게 한다
            };

            // SkiaSharp 기본 폰트는 한글 글리프가 없어 차트 레이블("타속" 등)이 깨진다.
            // 앱에 내장된 나눔고딕을 리소스 스트림에서 직접 로드해 전역 폰트로 지정한다.
            // (시스템 폰트 열거가 없어 시작이 빠르고, 나머지 UI와 글꼴도 일치한다.)
            LiveCharts.Configure(config => config
                .UseDefaults()
                .HasTextSettings(new TextSettings { DefaultTypeface = LoadChartTypeface() }));
        }

        /// <summary><2600919_4> 지금의 치트 상태에 맞춰 앱 전체 테마(모든 창의 테두리·제목표시줄
        /// 색)를 바꾼다. 평소(치트 꺼짐)는 App.xaml이 처음 정한 Light.Cobalt, 치트 중엔 하늘색
        /// 계열인 Light.Cyan.</summary>
        private void UpdateCheatTheme()
        {
            ThemeManager.Current.ChangeTheme(this, StageRecords.CheatOn ? "Light.Cyan" : "Light.Cobalt");
        }

        private static void TryWrite(string path, string text)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, text, new UTF8Encoding(false));
            }
            catch { /* 무시 */ }
        }

        // 모든 자판의 단계를 실제로 생성해 프롬프트 수·타·구성을 요약하고, 자모 분해 몇 개를 확인한다.
        private static void RunStageSelfTest(string outPath)
        {
            var sb = new StringBuilder();
            var rng = new Random(12345);

            sb.AppendLine("=== 자리연습 단계 생성 검증 (<260927_3>) ===");
            sb.AppendLine("stage | name | game | target | prompts | ta | key/text | roundkinds");
            foreach (IStageSet set in StageSets.All)
            {
                sb.AppendLine($"── {set.LayoutName} (키={set.Key}, 단계 {set.Stages.Count}개, 제시어 출처={WordCatalog.OriginOf(set.WordSection)}) ──");
                foreach (PracticeStage st in set.Stages)
                {
                    List<PracticePrompt> prog = st.Generate(rng);
                    int ta = prog.Sum(p => p.TaCount);
                    int keyN = prog.Count(p => !p.IsText);
                    int textN = prog.Count(p => p.IsText);
                    string kinds = string.Join(",", st.Rounds.Select(r => r.Kind.ToString()));
                    sb.AppendLine($"{st.Name} | game={st.GameStageId} | target={st.TargetTa} | prompts={prog.Count} | ta={ta} | key={keyN} text={textN} | {kinds}");

                    var samples = prog.Where(p => p.IsText).Select(p => p.Text).Distinct().Take(4).ToList();
                    if (samples.Count > 0) sb.AppendLine("      text샘플: " + string.Join(" / ", samples));
                }
            }

            // <260927_3>(0.6)~(0.6.2) '직전값 제외' 순을 여러 번 만들어 같은 제시어가 바로 이어지는지,
            // 개수가 늘 같은지 본다(후보가 모자라 불가능한 경우는 규칙상 허용되므로 따로 센다).
            sb.AppendLine();
            sb.AppendLine("=== 직전값 제외·개수 반복 검증 (각 순 300회) ===");
            int repeatFail = 0;
            foreach (IStageSet set in StageSets.All)
                foreach (PracticeStage st in set.Stages)
                    for (int ri = 0; ri < st.Rounds.Count; ri++)
                    {
                        PracticeRound round = st.Rounds[ri];
                        int repeats = 0;
                        var counts = new HashSet<int>();
                        for (int t = 0; t < 300; t++)
                        {
                            List<PracticePrompt> seq = round.Generate(rng);
                            counts.Add(seq.Count);
                            for (int i = 1; i < seq.Count; i++)
                            {
                                PracticePrompt a = seq[i - 1], b = seq[i];
                                bool same = a.IsText ? b.IsText && a.Text == b.Text
                                                     : !b.IsText && a.Key == b.Key && a.IsShift == b.IsShift;
                                if (same) repeats++;
                            }
                        }
                        bool bad = round.NoRepeat && repeats > 0;
                        if (bad || counts.Count != 1) repeatFail++;
                        sb.AppendLine($"{(bad || counts.Count != 1 ? "NG  " : "OK  ")}{set.Key} {st.Name} 순{ri + 1} {round.Kind} " +
                                      $"직전값제외={round.NoRepeat} 연속같음={repeats} 개수={string.Join("/", counts)}");
                    }
            sb.AppendLine("NO-REPEAT: " + (repeatFail == 0 ? "PASS" : $"FAIL ({repeatFail}건)"));

            // <260927_4>(0.2)(1.4) 오락 단어 공급: 10개마다 드문 목록 1개(드문 목록이 있을 때), 5개 단위 안에서는
            // 같은 단어 없음(후보가 5개 이상일 때). 2000개를 뽑아 확인한다.
            sb.AppendLine();
            sb.AppendLine("=== 오락 단어 공급 검증 (단계마다 2000개) ===");
            int feedFail = 0;
            foreach (IStageSet set in StageSets.All)
                foreach (GameStage gs in GameStages.For(set))
                {
                    var feed = new GameWordFeed(gs, rng);
                    var words = Enumerable.Range(0, 2000).Select(_ => feed.Next()).ToList();
                    var rareSet = new HashSet<string>(gs.Rare);
                    bool mixOk = gs.Rare.Count == 0
                        ? words.All(w => gs.Main.Contains(w))
                        : Enumerable.Range(0, 200).All(b => words.Skip(b * 10).Take(10).Count(rareSet.Contains) == 1);
                    bool uniqueOk = gs.Main.Count + gs.Rare.Count < 5
                        || Enumerable.Range(0, 400).All(u => words.Skip(u * 5).Take(5).Distinct().Count() == 5);
                    if (!mixOk || !uniqueOk) feedFail++;
                    sb.AppendLine($"{(mixOk && uniqueOk ? "OK  " : "NG  ")}{set.Key} {gs.Name}: 주 {gs.Main.Count} / 드문 {gs.Rare.Count} " +
                                  $"9:1={mixOk} 5개단위중복없음={uniqueOk}");
                }
            sb.AppendLine("GAME-FEED: " + (feedFail == 0 ? "PASS" : $"FAIL ({feedFail}건)"));

            sb.AppendLine();
            sb.AppendLine("=== 자모 분해·타 검증 ===");
            foreach (string w in new[] { "나라", "값", "왕", "끼니", "훑", "안녕" })
            {
                var strokes = HangulJamo.Decompose(w);
                string s = string.Join(" ", strokes.Select(k => $"({k.Pos.Row},{k.Pos.Column}{(k.IsShift ? "S" : "")})"));
                sb.AppendLine($"{w}: strokes={strokes.Count} ta={HangulJamo.SyllableCount(w)}  {s}");
            }

            sb.AppendLine();
            sb.AppendLine("=== 통합 창/데이터 검증 ===");
            try
            {
                var game = new AcidRainWindow(StageSets.DubeolsikStandard, 1); // XAML·Window.Resources 파싱 확인(로드 전 생성만)
                sb.AppendLine("AcidRainWindow 생성 OK");
                game.Close();
            }
            catch (Exception ex) { sb.AppendLine("AcidRainWindow 생성 실패: " + ex.Message); }

            // <260831> words.json은 후보 경로가 둘(AppData 우선, exe 옆 폴백)이라 각각 보고하고,
            // 자판마다 오락 단계가 몇 개 만들어지는지도 함께 남긴다(<260927_4>).
            foreach (string wordsPath in WordCatalog.CandidatePaths())
                sb.AppendLine($"제시어 목록 파일 존재: {File.Exists(wordsPath)}  ({wordsPath})");
            foreach (IStageSet set in StageSets.All)
                sb.AppendLine($"{set.LayoutName} 오락 단계: " + string.Join(", ",
                    GameStages.For(set).Select(g => $"{g.Id}(주{g.Main.Count}/드문{g.Rare.Count})")));

            sb.AppendLine();
            sb.AppendLine("OK");
            TryWrite(outPath, sb.ToString());
        }

        // <260811_26>(2-1) FingerLayer 컨트롤을 실제로 생성해(생성자가 ShowHomePose()·ApplySettings()
        // 를 그대로 호출) 777x260 로 레이아웃한 뒤 png 로 굽는다. FingerLayer.xaml 의 Path 요소를
        // 그대로 렌더링하므로, hands\*.svg 이식이 실제 화면과 100% 같은 경로로 반영되는지 확인할 수 있다.
        /// <summary>
        /// <260811_31-2> 모든 키(기본/윗글쇠)에 SetPose 를 적용하고, 손가락 레이어가 실제로 읽은
        /// hands\ 파일 이름을 표로 남긴다. 형식: "행-열 base|shift left=… right=…"
        /// </summary>
        private static void RunPoseSources(string outPath)
        {
            var layer = new FingerLayer();
            var sb = new StringBuilder();
            layer.ShowHomePose();
            sb.AppendLine($"home  base  left={layer.LastLeftSource} right={layer.LastRightSource}");

            int[] cols = { 13, 13, 11, 10 };   // 행별 키 개수(물리 키 마스터 표와 같다)
            for (int row = 0; row < cols.Length; row++)
                for (int c = 0; c < cols[row]; c++)
                    foreach (bool isShift in new[] { false, true })
                    {
                        layer.SetPose(new KeyPos(row, c), isShift);
                        sb.AppendLine($"{row}-{c} {(isShift ? "shift" : "base ")} left={layer.LastLeftSource} right={layer.LastRightSource}");
                    }
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// <260811_33> 클래식 '자리연습' 창(두벌식 표준 외 자판에서 "연습 시작"으로 여는 창)이 단계 창과
        /// 같은 구성으로 만들어졌는지 확인한다 — "손 모양" 버튼·손가락 레이어가 켜지는지, 창 세로 보정이
        /// 단계 창과 같은지, 그리고 배정 없는 `⧵`(1,12) 키에서 두 손이 기본자세로 가만히 있는지.
        /// 연습 자체(StartPractice)는 손가락 레이어 인트로가 끝난 뒤라 여기서는 실행되지 않는다
        /// (자판 데이터를 읽는 MainWindow 없이 돌리기 위함).
        /// </summary>
        private static void RunClassicDump(string outPath)
        {
            var sb = new StringBuilder();
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            void Flush() => File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);

            // 연습 창은 MainWindow 가 읽어 둔 자판 데이터(CurrentKeyLayout)에 기대므로 실제와 같은
            // 순서로 메인 창부터 만든다. 띄우지 않으면 <260811_34> 의 숨김/되살림도 확인할 수 없다.
            var main = new MainWindow
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = 200,
                Top = 100
            };
            Current.MainWindow = main;
            main.Show();
            main.UpdateLayout();

            // <260812_8> '설정' 창의 '현재 자판' 목록이 어떤 이름들로 채워지는지
            string dir = (string)OpenTyping.UserSettingsStore.Get(OpenTyping.MainWindow.KeyLayoutDataDirStr);
            sb.AppendLine("자판 폴더=" + dir);
            try
            {
                foreach (KeyLayout kl in KeyLayout.LoadFromDirectory(dir))
                    sb.AppendLine($"  자판: \"{kl.Name}\" ({kl.Character})");
            }
            catch (Exception ex) { sb.AppendLine("  자판 목록 읽기 실패: " + ex.Message); }
            // <260812_20>(2) '최고 기록' 항목과 그 창
            var recWin = new BestRecordWindow
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20000,
                Top = -20000,
                ShowActivated = false
            };
            recWin.Show();
            recWin.UpdateLayout();
            var lines = recWin.RecordPanel.Children.OfType<System.Windows.Controls.TextBlock>()
                .Select(t => t.Text.Trim()).ToList();
            sb.AppendLine($"최고 기록 창: {lines.Count}줄");
            foreach (string l in lines) sb.AppendLine("    " + l);
            recWin.Close();

            // <260812_9> '오락 열기' 드롭다운: 닫힌 글자와 펼친 목록의 첫 항목
            var combo = main.KeyPracticeMenu.GameStageCombo;
            var shown = combo.Items.OfType<System.Windows.Controls.ComboBoxItem>()
                .Where(it => it.Visibility == Visibility.Visible).ToList();
            sb.AppendLine($"오락 드롭다운: 닫힘글자=\"{(combo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content}\" " +
                          $"전체 {combo.Items.Count}개 중 보이는 항목 {shown.Count}개, " +
                          $"첫 항목=\"{(shown.Count > 0 ? shown[0].Content : "(없음)")}\"");

            // 닫힌 드롭다운이 실제로 글자를 그리는지 눈으로도 볼 수 있게 메인 창을 한 장 남긴다.
            // 겸사겸사 '자리연습' 탭으로 넘기는 <260812_5-1>의 '나가기' 경로도 태워 본다.
            OpenTyping.MainWindow.ShowKeyPracticeTab();
            main.UpdateLayout();
            sb.AppendLine($"선택된 탭 내용={main.MenuTabControl.SelectedContent?.GetType().Name}");
            try
            {
                var mrtb = new RenderTargetBitmap((int)main.ActualWidth, (int)main.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                mrtb.Render(main);
                var menc = new PngBitmapEncoder();
                menc.Frames.Add(BitmapFrame.Create(mrtb));
                using (var fs = File.Create(Path.ChangeExtension(outPath, ".main.png"))) menc.Save(fs);
            }
            catch { /* 그림은 참고용 */ }

            sb.AppendLine($"현재 자판=\"{OpenTyping.MainWindow.CurrentKeyLayout?.Name}\" " +
                          $"단계 자판=\"{KeyPracticeMenu.StageLayoutName}\" " +
                          $"단계 수={StageSets.DubeolsikStandard.Stages.Count}");

            var keys = new List<KeyPos> { new KeyPos(2, 3), new KeyPos(2, 6) };
            var win = new KeyPracticeWindow(keys, false);

            // ── <260811_33> 클래식 창의 구성 ──
            sb.AppendLine($"handButton={win.HandButton.Visibility}");
            sb.AppendLine($"guideRow={win.GuideRow.Height.Value}");
            sb.AppendLine($"rootHeight={win.RootGrid.Height} winHeight={win.Height} minHeight={win.MinHeight}");
            sb.AppendLine($"tpm={win.SpeedTile.Visibility}");
            win.FingerLayer.SetPose(new KeyPos(2, 3), false);
            sb.AppendLine($"pose 2-3  left={win.FingerLayer.LastLeftSource} right={win.FingerLayer.LastRightSource}");
            win.FingerLayer.SetPose(new KeyPos(1, 12), false);   // `⧵` — 담당 손가락 없음
            sb.AppendLine($"pose 1-12 left={win.FingerLayer.LastLeftSource} right={win.FingerLayer.LastRightSource}");
            Flush();

            // ── <260811_34> 메인 창 숨김 + 중심 맞춤 ──
            Point mainCenterBefore = CenterOf(main);
            sb.AppendLine($"main(before) visible={main.IsVisible} center={Fmt(mainCenterBefore)}");

            Point dialogCenter = new Point(double.NaN, double.NaN);
            win.Loaded += (s, e) => win.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                {
                    // 손가락 레이어 인트로가 도는 중이어도 위치·표시 상태는 이미 확정돼 있다.
                    dialogCenter = CenterOf(win);
                    sb.AppendLine($"dialog       visible={win.IsVisible} center={Fmt(dialogCenter)}");
                    sb.AppendLine($"main(during) visible={main.IsVisible}");
                    sb.AppendLine($"fingerLayer={win.FingerLayer.Visibility}");

                    // <260812_15> 실제 키 입력을 흘려 넣어 오타 경고가 떴다 사라지는지 본다.
                    // (연습 값이 제시되기 전이면 트리거가 작동하지 않으므로 제시 여부부터 적는다.)
                    string NoticeNow() => win.NoticeText.Visibility == Visibility.Visible
                        ? "\"" + win.NoticeText.Text + "\"" : "(없음)";
                    sb.AppendLine($"오타 경고 검사: 제시어=\"{win.CurrentKey?.KeyData}\" 시작문구={NoticeNow()}");

                    // <260812_2> 입력 진행 상자가 실제로 그려졌는지 + 창 그림을 남긴다.
                    var box = win.DictationText;
                    // 21글자가 실제로 들어가는지: 한글 21자를 재서 칸 안쪽 폭과 견준다.
                    var ft = new FormattedText(new string('가', 21), System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(box.FontFamily, box.FontStyle, box.FontWeight, box.FontStretch),
                        box.FontSize, Brushes.Black, 96);
                    // 카운터 영역 요소들의 세로 크기·위치 비교
                    string Box(FrameworkElement el, string label)
                    {
                        Rect r = el.TransformToAncestor(win)
                                   .TransformBounds(new Rect(0, 0, el.ActualWidth, el.ActualHeight));
                        return $"    {label}: 세로={r.Height:F1} 꼭대기={r.Top:F1} 바닥={r.Bottom:F1}";
                    }
                    // 창 종류별로 안내 문구 자리가 같은지 (<260812_15> 뒤 위치 통일 확인)
                    sb.AppendLine("── 창 종류별 안내 문구 위치 ──");
                    foreach ((string label, KeyPracticeWindow w) in new[]
                    {
                        ("클래식(그 외 자판)", win),
                        ("1단계", new KeyPracticeWindow(StageSets.DubeolsikStandard, StageSets.DubeolsikStandard.Stages[0])),
                        ("2단계", new KeyPracticeWindow(StageSets.DubeolsikStandard, StageSets.DubeolsikStandard.Stages[1])),
                    })
                    {
                        if (!ReferenceEquals(w, win))
                        {
                            w.WindowStartupLocation = WindowStartupLocation.Manual;
                            w.Left = -20000; w.Top = -20000; w.ShowActivated = false;
                            w.Show();
                            w.UpdateLayout();
                        }
                        w.NoticeText.Visibility = Visibility.Visible;
                        w.NoticeText.Text = "표본";
                        w.UpdateLayout();
                        Rect r = w.NoticeText.TransformToAncestor(w)
                                  .TransformBounds(new Rect(0, 0, w.NoticeText.ActualWidth, w.NoticeText.ActualHeight));
                        sb.AppendLine($"    {label}: 경고문구 꼭대기={r.Top:F1} 바닥={r.Bottom:F1} 창세로={w.Height:F0}");
                        if (label == "1단계")
                        {
                            w.NoticeText.Visibility = Visibility.Collapsed;
                            w.UpdateLayout();
                            try
                            {
                                var srtb = new RenderTargetBitmap((int)w.ActualWidth, (int)w.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                                srtb.Render(w);
                                var senc = new PngBitmapEncoder();
                                senc.Frames.Add(BitmapFrame.Create(srtb));
                                using (var fs = File.Create(Path.ChangeExtension(outPath, ".stage1.png"))) senc.Save(fs);
                            }
                            catch { /* 그림은 참고용 */ }
                        }
                        if (!ReferenceEquals(w, win)) w.Close();
                    }
                    win.NoticeText.Visibility = Visibility.Collapsed;

                    sb.AppendLine("── 카운터 영역 ──");
                    sb.AppendLine(Box(win.AccuracyTile, "정확도 타일"));
                    sb.AppendLine(Box(win.SpeedTile, "타속 타일"));
                    sb.AppendLine(Box(win.HandButton, "'손 모양' 버튼"));
                    // <260812_21-1> 타속 타일이 제시어(창 가운데)와 좌우 중심이 맞는가
                    double winCenter = win.ActualWidth / 2;
                    Rect st = win.SpeedTile.TransformToAncestor(win)
                        .TransformBounds(new Rect(0, 0, win.SpeedTile.ActualWidth, win.SpeedTile.ActualHeight));
                    sb.AppendLine($"    타속 타일 중심={st.Left + st.Width / 2:F1} 창 중심={winCenter:F1}");

                    sb.AppendLine($"받아쓰기칸 크기={win.DictationBox.ActualWidth:F0}x{win.DictationBox.ActualHeight:F0} " +
                                  $"21글자폭={ft.Width:F0} 들어감={(ft.Width <= win.DictationBox.ActualWidth - 3 ? "예" : "아니오")}");
                    try
                    {
                        var rtb = new RenderTargetBitmap((int)win.ActualWidth, (int)win.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                        rtb.Render(win);
                        var enc = new PngBitmapEncoder();
                        enc.Frames.Add(BitmapFrame.Create(rtb));
                        using (var fs = File.Create(Path.ChangeExtension(outPath, ".png"))) enc.Save(fs);
                    }
                    catch (Exception ex) { sb.AppendLine("png 실패: " + ex.Message); }

                    Flush();
                    win.Close();
                }));

            // App 안에서 MainWindow 는 Application.MainWindow 속성으로 먼저 해석되므로 타입을 밝힌다.
            OpenTyping.MainWindow.ShowDialogDimmed(win);

            sb.AppendLine($"main(after)  visible={main.IsVisible} center={Fmt(CenterOf(main))}");
            Flush();
            main.Close();
        }

        /// <summary>
        /// <260812_2> 입력 진행 상자에 쓰이는 HangulJamo.TypedPrefix 를 타 수를 0부터 끝까지 올려 가며
        /// 확인한다. 마지막 타에서 반드시 제시어 원문과 같아져야 하고, 중간 단계는 기대한 조합이어야 한다.
        /// </summary>
        private static void RunTypedTest(string outPath)
        {
            // 검사에서 띄운 창을 다 닫으면 WPF 가 '마지막 창이 닫혔다'며 종료 절차에 들어가,
            // 그 뒤의 Show() 가 조용히 무시된다(창 크기 0). 명시적 종료로 바꿔 막는다.
            Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // (제시어, 타별 기대 표시) — 겹모음·겹받침·쌍자음·여러 음절·기호를 고루 담았다.
            var cases = new (string Text, string[] Expected)[]
            {
                ("탈",         new[] { "", "ㅌ", "타", "탈" }),
                ("화",         new[] { "", "ㅎ", "호", "화" }),
                ("닭",         new[] { "", "ㄷ", "다", "달", "닭" }),
                ("꽉",         new[] { "", "ㄲ", "꼬", "꽈", "꽉" }),
                ("의",         new[] { "", "ㅇ", "으", "의" }),
                ("나라",       new[] { "", "ㄴ", "나", "나ㄹ", "나라" }),
                ("이탈리아어", new[] { "", "ㅇ", "이", "이ㅌ", "이타", "이탈", "이탈ㄹ", "이탈리",
                                       "이탈리ㅇ", "이탈리아", "이탈리아ㅇ", "이탈리아어" }),
                ("ㅘ",         new[] { "", "ㅗ", "ㅘ" }),
            };

            var sb = new StringBuilder();
            int fail = 0;
            foreach ((string text, string[] expected) in cases)
            {
                int total = HangulJamo.Decompose(text).Count;
                var got = new List<string>();
                for (int k = 0; k <= total; k++) got.Add(HangulJamo.TypedPrefix(text, k));

                bool ok = total + 1 == expected.Length && got.SequenceEqual(expected);
                if (!ok) fail++;
                sb.AppendLine($"{(ok ? "OK  " : "NG  ")}{text} ({total}타) : {string.Join(" → ", got.Select(s => s == "" ? "·" : s))}");
                if (!ok) sb.AppendLine($"      기대 : {string.Join(" → ", expected.Select(s => s == "" ? "·" : s))}");
            }
            sb.AppendLine("TYPED: " + (fail == 0 ? "PASS" : $"FAIL ({fail}건)"));

            // ── 받아쓰기 칸 상태 기계 (<260812_2-1>(1-3),(1-4)). Describe(): "파랑|빨강" 또는 "초록*" ──
            sb.AppendLine();
            var d = new DictationState();
            var log = new List<string>();
            void Step(string label, string expect)
            {
                string got = d.Describe();
                bool ok = got == expect;
                if (!ok) fail++;
                log.Add($"{(ok ? "OK  " : "NG  ")}{label,-22} → \"{got}\"" + (ok ? "" : $"  (기대 \"{expect}\")"));
            }

            d.Start("탈");            Step("제시 '탈'", "/|");
            d.Wrong("ㄱ");            Step("오타 ㄱ", "/|ㄱ");
            d.Wrong("ㄴ");            Step("오타 ㄴ 또", "/|ㄱㄴ");
            d.Correct();              Step("정타 ㅌ(빨강 사라짐)", "/ㅌ|");
            d.Correct();              Step("정타 ㅏ", "/타|");
            d.Correct(); d.Pass();    Step("통과(입력자리 빔, 1~7로 이동)", "탈/|");
            d.Start("나라");          Step("다음 제시어(초록 유지)", "탈/|");
            d.Correct();              Step("정타 ㄴ", "탈/ㄴ|");
            d.Correct(); d.Pass();    Step("'나' 다음 통과는 새 초록", "나라/|");
            d.Clear();                Step("연습 재시작", "/|");

            // 색이 지시문 값과 정확히 같은지 (<260812_2-1>(1), <260812_2-4>(2))
            string Rgb(Brush b) { var c = ((SolidColorBrush)b).Color; return $"({c.R},{c.G},{c.B})"; }
            bool colorOk = Rgb(DictationState.CorrectBrush) == "(28,126,214)"
                        && Rgb(DictationState.WrongBrush) == "(240,62,62)"
                        && Rgb(DictationState.PassedBrush) == "(55,178,77)";
            sb.AppendLine($"색: 입력중={Rgb(DictationState.CorrectBrush)} 오타={Rgb(DictationState.WrongBrush)} " +
                          $"통과={Rgb(DictationState.PassedBrush)} {(colorOk ? "PASS" : "FAIL")}");
            if (!colorOk) fail++;

            foreach (string l in log) sb.AppendLine(l);
            sb.AppendLine("DICTATION: " + (log.TrueForAll(l => l.StartsWith("OK")) ? "PASS" : "FAIL"));

            // <260812_7> 결합된 글자 구간의 시작 위치를 옳게 찾는가
            sb.AppendLine();
            PracticePrompt Key(int r, int c) => PracticePrompt.ForKey(new PracticeStage.StageItem(r, c, false));
            var tailCases = new (string Label, PracticePrompt[] Program, int Expected)[]
            {
                // ㄴ, ㄹ, ㅏ, 나, 나라 → '나'(3번째, 0부터)에서 다시 잰다
                ("ㄴ ㄹ ㅏ 나 나라", new[] { Key(2,1), Key(2,3), Key(2,7),
                                             PracticePrompt.ForText("나"), PracticePrompt.ForText("나라") }, 3),
                ("전부 낱자",        new[] { Key(2,1), Key(2,3) }, -1),
                ("전부 결합",        new[] { PracticePrompt.ForText("나"), PracticePrompt.ForText("라") }, -1),
                ("결합 뒤 낱자로 끝", new[] { PracticePrompt.ForText("나"), Key(2,1) }, -1),
                ("마지막 하나만 결합", new[] { Key(2,1), Key(2,3), PracticePrompt.ForText("가") }, 2),
                ("빈 목록",          new PracticePrompt[0], -1),
            };
            int tailFail = 0;
            foreach ((string label, PracticePrompt[] prog, int expected) in tailCases)
            {
                int got = KeyPracticeWindow.FindCombinedTailStart(prog);
                bool ok = got == expected;
                if (!ok) { tailFail++; fail++; }
                sb.AppendLine($"{(ok ? "OK  " : "NG  ")}{label,-18} 시작={got} (기대 {expected})");
            }
            sb.AppendLine("COMBINED-TAIL: " + (tailFail == 0 ? "PASS" : $"FAIL ({tailFail}건)"));

            // 실제 단계들에서 어디부터 다시 재게 되는지(구성이 바뀌면 이 표도 따라 바뀐다)
            var rng = new System.Random(1234);
            IReadOnlyList<PracticeStage> koStages = StageSets.DubeolsikStandard.Stages;
            for (int s = 1; s <= koStages.Count; s++)
            {
                List<PracticePrompt> prog = koStages[s - 1].Generate(rng);
                int start = KeyPracticeWindow.FindCombinedTailStart(prog);
                sb.AppendLine($"    {s,2}단계: 전체 {prog.Count}개, 다시 재기 시작={(start < 0 ? "없음" : start + "번째")}" +
                              (start >= 0 ? $" (뒤 {prog.Count - start}개가 결합된 글자)" : ""));
            }

            // <260812_5-1> 연습 종료 창: 네 갈래 × 오락 유무에 따라 문구와 버튼이 어떻게 달라지는지.
            // 단계 수·오락 유무·목표 타수를 코드가 스스로 읽어 정하는지 보는 것이므로, 값을 손으로
            // 적어 두지 않고 실제 레지스트리·설정에서 가져온다.
            sb.AppendLine();
            int last = koStages.Count;
            sb.AppendLine($"마지막 단계={last} 목표 타수(단계별)={string.Join(",", koStages.Select(st => st.TargetTa))} " +
                          $"오락 있는 단계={string.Join(",", ArcadeGames.Default?.StageIdsFor(StageSets.DubeolsikStandard) ?? new int[0])}");

            // 검사할 단계도 단계 정의에서 고른다(단계 구성이 바뀌어도 검사가 깨지지 않게):
            // 오락이 있는 첫 중간 단계, 오락이 없는 첫 중간 단계, 마지막 단계.
            int withGame = Enumerable.Range(1, Math.Max(0, last - 1)).FirstOrDefault(n => koStages[n - 1].GameStageId > 0);
            int noGame = Enumerable.Range(1, Math.Max(0, last - 1)).FirstOrDefault(n => koStages[n - 1].GameStageId == 0);
            var finishCases = new List<(int, string)>();
            if (withGame > 0) finishCases.Add((withGame, "(1) 중간·오락 있음"));
            if (noGame > 0) finishCases.Add((noGame, "(3) 중간·오락 없음"));
            finishCases.Add((last, "(2)(3-2) 마지막"));
            foreach ((int n, string label) in finishCases)
                foreach (int tpm in new[] { koStages[n - 1].TargetTa + 50, koStages[n - 1].TargetTa - 1 })
                {
                    int target = koStages[n - 1].TargetTa;
                    int gameId = koStages[n - 1].GameStageId;
                    var fw = new StageFinishWindow(n, last, tpm, gameId)
                    {
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Left = -20000,
                        Top = -20000,
                        ShowActivated = false
                    };
                    fw.Show();
                    fw.UpdateLayout();
                    sb.AppendLine($"{label} {n}단계 {tpm}타");
                    sb.AppendLine($"    1줄: {fw.TaText.Text}");
                    sb.AppendLine($"    2줄: {(fw.UnlockText.Visibility == Visibility.Visible ? fw.UnlockText.Text : "(없음)")}");
                    sb.AppendLine($"    버튼: {fw.DescribeButtons()}");
                    sb.AppendLine($"    제목단추: 최소화={fw.ShowMinButton} 최대화={fw.ShowMaxRestoreButton} 끄기={fw.ShowCloseButton}");
                    if (n == withGame && tpm > target)
                    {
                        try
                        {
                            var rtb = new RenderTargetBitmap((int)fw.ActualWidth, (int)fw.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                            rtb.Render(fw);
                            var enc = new PngBitmapEncoder();
                            enc.Frames.Add(BitmapFrame.Create(rtb));
                            using (var fs = File.Create(Path.ChangeExtension(outPath, ".finish.png"))) enc.Save(fs);
                        }
                        catch { /* 그림은 참고용 */ }
                    }
                    fw.Close();
                }

            // <260812_19> 산성비 특수(파란) 단어가 '처치한 단어 수'로 나오는지 — 단계별 간격 표본
            sb.AppendLine();
            // <260927_4> 단계 구성이 자판마다 달라져, 간격은 '그 자판 오락 단계 중 몇 번째인가'로 고른다
            // (마지막 '연습한 키 전체' 단계는 가장 잦은 값). 기대값도 단계 정의에서 계산한다.
            IReadOnlyList<GameStage> koGames = GameStages.For(StageSets.DubeolsikStandard);
            var arcade = new AcidRainWindow(StageSets.DubeolsikStandard, koGames.Count > 0 ? koGames[0].Id : 1);
            var ranges = AcidRainWindow.SpecialCatchRanges;
            var want = new Dictionary<int, (int Min, int Max)>();
            for (int gi = 0; gi < koGames.Count; gi++)
                want[koGames[gi].Id] = gi == koGames.Count - 1 ? ranges[ranges.Length - 1]
                                                                : ranges[Math.Min(gi, ranges.Length - 1)];
            int gapFail = 0;
            foreach (KeyValuePair<int, (int Min, int Max)> kv in want)
            {
                var gaps = new List<int>();
                for (int i = 0; i < 4000; i++) gaps.Add(arcade.SpecialCatchGap(kv.Key));
                bool ok = gaps.Min() == kv.Value.Min && gaps.Max() == kv.Value.Max;
                if (!ok) { gapFail++; fail++; }
                sb.AppendLine($"{(ok ? "OK  " : "NG  ")}산성비 {kv.Key,2}단계: 처치 {gaps.Min()}~{gaps.Max()}개마다 " +
                              $"(지시 {kv.Value.Min}~{kv.Value.Max})");
            }
            sb.AppendLine("SPECIAL-GAP: " + (gapFail == 0 ? "PASS" : $"FAIL ({gapFail}건)"));

            // <260812_23> 날갯짓 여러 각도를 한 줄로 그려 파인 틈이 생기는지 눈으로 본다.
            try
            {
                var strip = new System.Windows.Controls.Canvas { Width = 5 * 70, Height = 190, Background = Brushes.White };
                double[] angles = { -55, -35, -10, 10, 25 };
                for (int i = 0; i < angles.Length; i++)
                {
                    System.Windows.Controls.Canvas child = arcade.BuildBirdSpriteForTest(angles[i]);
                    System.Windows.Controls.Canvas.SetLeft(child, i * 70 + 15);
                    System.Windows.Controls.Canvas.SetTop(child, 22);
                    strip.Children.Add(child);
                }
                // 아래 줄에는 메뚜기 (<260812_26> 다리 셋 확인). 크기를 키워 자세히 보이게 한다.
                for (int i = 0; i < 3; i++)
                {
                    System.Windows.Controls.Canvas hopper = arcade.BuildGrasshopperSpriteForTest();
                    hopper.RenderTransformOrigin = new Point(0.5, 0.5);
                    hopper.RenderTransform = new ScaleTransform(1 + i * 0.5, 1 + i * 0.5);
                    System.Windows.Controls.Canvas.SetLeft(hopper, i * 110 + 25);
                    System.Windows.Controls.Canvas.SetTop(hopper, 80);
                    strip.Children.Add(hopper);
                }
                // 맨 아래 줄에는 나비(메뚜기 머리·눈·더듬이 틀을 참고해 새로 그린 것). 크기를 키워
                // 날개 무늬·더듬이 곤봉이 잘 보이게 한다.
                for (int i = 0; i < 3; i++)
                {
                    // 나비 자신의 RenderTransform(몸통 기울기)을 덮어쓰지 않도록, 크기 확대는
                    // 감싸는 빈 Canvas 쪽에 건다(새·메뚜기는 자체 회전이 없어 문제되지 않았다).
                    System.Windows.Controls.Canvas butterfly = arcade.BuildButterflySpriteForTest();
                    var wrapper = new System.Windows.Controls.Canvas
                    {
                        Width = butterfly.Width,
                        Height = butterfly.Height,
                        RenderTransformOrigin = new Point(0.5, 0.5),
                        RenderTransform = new ScaleTransform(1 + i * 0.5, 1 + i * 0.5),
                    };
                    wrapper.Children.Add(butterfly);
                    System.Windows.Controls.Canvas.SetLeft(wrapper, i * 110 + 25);
                    System.Windows.Controls.Canvas.SetTop(wrapper, 150);
                    strip.Children.Add(wrapper);
                }
                strip.Measure(new Size(strip.Width, strip.Height));
                strip.Arrange(new Rect(0, 0, strip.Width, strip.Height));
                strip.UpdateLayout();
                var srtb = new RenderTargetBitmap((int)(strip.Width * 3), (int)(strip.Height * 3), 288, 288, PixelFormats.Pbgra32);
                srtb.Render(strip);
                var senc = new PngBitmapEncoder();
                senc.Frames.Add(BitmapFrame.Create(srtb));
                using (var fs = File.Create(Path.ChangeExtension(outPath, ".wing.png"))) senc.Save(fs);
            }
            catch (Exception ex) { sb.AppendLine("날개 그림 실패: " + ex.Message); }

            // <260812_24-1>, <260812_28> 폭죽·큰 불꽃
            {
                arcade.WindowStartupLocation = WindowStartupLocation.Manual;
                arcade.Left = -20000; arcade.Top = -20000;
                arcade.Width = 900; arcade.Height = 620;
                arcade.ShowActivated = false;
                arcade.Show();
                arcade.UpdateLayout();

                sb.AppendLine($"    (게임 화면 크기 {arcade.GameCanvas.ActualWidth:F0}x{arcade.GameCanvas.ActualHeight:F0}, " +
                              $"창 {arcade.ActualWidth:F0}x{arcade.ActualHeight:F0}, 보임={arcade.IsVisible})");

                int levelSparks = arcade.LevelUpFireworksTest();
                sb.AppendLine($"레벨업 폭죽 알갱이 {levelSparks}개 (양쪽 14개씩 = 28)");

                (int wordsBefore, int after, int sparks, string banner) = arcade.EarthFireworkTest();
                bool earthOk = wordsBefore == 8 && after == 0 && sparks > 0 && banner == "지구 환경을 지켜라!!";
                sb.AppendLine($"큰 불꽃: 단어 {wordsBefore} → {after}, 남은 불티 {sparks}, 문구 \"{banner}\" " +
                              $"{(earthOk ? "PASS" : "FAIL")}");
                if (!earthOk) fail++;

                // <260812_28.1.1.2> 이벤트 중간(progress=0.5, 2.4초 중 1.2초)의 불꽃 그림 크기·프레임 번호가
                // 정한 공식대로인지. 크기는 커지는 속도를 높이려고 progress 의 제곱근을 쓰므로
                // 16 + sqrt(0.5)*(90-16) ≈ 68.3px. 프레임은 진행도 그대로라 기존과 같이 30.
                arcade.EarthFireworkFrameForTest(1.2);
                double midImageSize = arcade.EarthFireImageSizeForTest;
                int midFrame = arcade.EarthFireFrameIndexForTest;
                bool imageOk = Math.Abs(midImageSize - 68.3) < 0.5 && midFrame == 30 && midFrame % 2 == 0;
                sb.AppendLine($"큰 불꽃 중간 그림: 크기 {midImageSize:F1}px (목표 68.3px) 프레임 {midFrame}(목표 30) " +
                              $"{(imageOk ? "PASS" : "FAIL")}");
                if (!imageOk) fail++;

                // 한 이벤트 동안 쓰는 프레임이 모두 짝수 번째(한 장씩 건너뜀)이고 31가지인지.
                var usedFrames = new HashSet<int>();
                arcade.EarthFireworkFrameForTest(0);
                for (int f = 0; f < 80; f++) { arcade.EarthFireworkStepForTest(1.0 / 30); usedFrames.Add(arcade.EarthFireFrameIndexForTest); }
                bool framesOk = usedFrames.All(x => x % 2 == 0 && x <= 60) && usedFrames.Contains(0) && usedFrames.Contains(60);
                sb.AppendLine($"큰 불꽃 프레임: 쓴 프레임 {usedFrames.Count}가지(모두 짝수={usedFrames.All(x => x % 2 == 0)}, 0·60 포함) " +
                              $"{(framesOk ? "PASS" : "FAIL")}");
                if (!framesOk) fail++;

                // 큰 불꽃이 퍼지는 모습을 세 시점으로 남긴다(궤적·원운동을 눈으로 보기 위함).
                try
                {
                    double[] moments = { 0.6, 1.4, 2.4 };
                    var sheet = new System.Windows.Controls.Canvas
                    {
                        Width = 900 * moments.Length / 2.0,
                        Height = 585 / 2.0,
                        Background = Brushes.White,
                    };
                    for (int i = 0; i < moments.Length; i++)
                    {
                        arcade.StartOverlay.Visibility = Visibility.Collapsed;
                        arcade.EarthFireworkFrameForTest(moments[i]);
                        arcade.UpdateLayout();

                        var shot = new RenderTargetBitmap(900, 585, 96, 96, PixelFormats.Pbgra32);
                        shot.Render(arcade.GameAreaForTest);
                        var img = new System.Windows.Controls.Image
                        {
                            Source = shot,
                            Width = 900 / 2.0,
                            Height = 585 / 2.0,
                        };
                        System.Windows.Controls.Canvas.SetLeft(img, i * (900 / 2.0));
                        sheet.Children.Add(img);
                    }
                    sheet.Measure(new Size(sheet.Width, sheet.Height));
                    sheet.Arrange(new Rect(0, 0, sheet.Width, sheet.Height));
                    sheet.UpdateLayout();

                    var srtb2 = new RenderTargetBitmap((int)sheet.Width, (int)sheet.Height, 96, 96, PixelFormats.Pbgra32);
                    srtb2.Render(sheet);
                    var enc2 = new PngBitmapEncoder();
                    enc2.Frames.Add(BitmapFrame.Create(srtb2));
                    using (var fs = File.Create(Path.ChangeExtension(outPath, ".firework.png"))) enc2.Save(fs);
                }
                catch (Exception ex) { sb.AppendLine("불꽃 그림 실패: " + ex.Message); }

                arcade.Hide();
            }

            // <260812_26> 안내 배너가 산성비 글자보다 뒤에 있는지(겹침 차례)
            {
                (int normalZ, int frontZ, int rainZ) = arcade.BannerZTest();
                bool zOk = normalZ < rainZ && frontZ > rainZ;
                sb.AppendLine($"겹침 차례: 보통 문구 {normalZ} < 산성비 {rainZ} < 앞으로 낸 문구 {frontZ} " +
                              $"{(zOk ? "PASS" : "FAIL")}");
                if (!zOk) fail++;
            }

            // <260812_31> 메뚜기 세 발끝 '변의 중심점'이 같은 높이인지 — 그림에서 직접 잰다.
            {
                System.Windows.Controls.Canvas hopper = arcade.BuildGrasshopperSpriteForTest();
                var feet = new List<double>();
                foreach (object child in hopper.Children)
                {
                    if (child is System.Windows.Shapes.Rectangle r)   // 앞·가운뎃다리(회전한 직사각형)
                    {
                        var rot = r.RenderTransform as RotateTransform;
                        double th = (rot?.Angle ?? 0) * Math.PI / 180;
                        double cx = r.Width / 2, cy = r.Height;       // 발끝 변의 중심(로컬)
                        double y = System.Windows.Controls.Canvas.GetTop(r) + cx * Math.Sin(th) + cy * Math.Cos(th);
                        if (r.Width >= 2) feet.Add(y);                // 더듬이(굵기 1.5)는 다리가 아니므로 제외
                    }
                    else if (child is System.Windows.Shapes.Polyline pl && pl.Points.Count > 0)
                    {
                        feet.Add(pl.Points[pl.Points.Count - 1].Y);   // 뒷다리 발끝(평평한 끝 = 그 점)
                    }
                }
                feet.Sort();
                // <260812_32> 가운뎃·뒷다리는 같은 높이, 앞다리만 그보다 살짝 위(0.1~1.0)여야 한다.
                bool level = feet.Count == 3
                             && Math.Abs(feet[2] - feet[1]) < 0.05
                             && feet[1] - feet[0] > 0.1 && feet[1] - feet[0] <= 1.0;
                sb.AppendLine($"메뚜기 발끝 높이: {string.Join(" / ", feet.Select(v => v.ToString("F2")))} " +
                              $"(앞다리가 {(feet.Count == 3 ? feet[1] - feet[0] : 0):F2}만큼 위) " +
                              $"{(level ? "PASS" : "FAIL")}");
                if (!level) fail++;
            }

            // <260812_22> 새 날개에 회전 변환이 달렸는지(퍼덕임의 전제)
            bool wingOk = arcade.BirdHasWingTransform();
            sb.AppendLine($"새 날개 회전 변환={(wingOk ? "있음" : "없음")}");
            if (!wingOk) fail++;

            // <260812_15> 치트 창: 효과 목록과 '고루 분포' 확인
            var cheat = new AcidCheatWindow(arcade)
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20000,
                Top = -20000,
                ShowActivated = false
            };
            cheat.Show();
            cheat.UpdateLayout();
            var checks = cheat.EffectPanel.Children.OfType<System.Windows.Controls.CheckBox>().ToList();
            sb.AppendLine($"산성비 치트 창: 효과 체크상자 {checks.Count}개, 첫=\"{checks[0].Content}\" 끝=\"{checks[checks.Count - 1].Content}\"");
            cheat.Close();

            // 체크한 효과만, 그리고 고루 나오는지 (주머니 방식이라 한 바퀴에 한 번씩)
            arcade.CheatHalfSpecial = true;
            arcade.CheatEffects.Clear();
            foreach (int t in new[] { 2, 5, 7 }) arcade.CheatEffects.Add(t);
            var picked = new Dictionary<int, int>();
            for (int i = 0; i < 300; i++)
            {
                int t = arcade.NextCheatEffectForTest();
                picked[t] = picked.TryGetValue(t, out int c) ? c + 1 : 1;
            }
            bool onlyChecked = picked.Keys.All(k => arcade.CheatEffects.Contains(k));
            bool evenly = picked.Values.Max() - picked.Values.Min() <= 1;   // 주머니 방식이면 최대 1 차이
            sb.AppendLine($"    고른 효과만 나옴={onlyChecked} 분포={string.Join("/", picked.OrderBy(p => p.Key).Select(p => p.Key + ":" + p.Value))} 고름={evenly}");
            arcade.CheatHalfSpecial = false;
            sb.AppendLine("ACID-CHEAT: " + (checks.Count == AcidRainWindow.EffectCount && onlyChecked && evenly ? "PASS" : "FAIL"));
            if (!(checks.Count == AcidRainWindow.EffectCount && onlyChecked && evenly)) fail++;

            arcade.Close();

            // <260812_15> 오타 경고 문구가 지시문 그대로인지
            sb.AppendLine();
            bool typoOk = KeyPracticeWindow.TypoWarning == "오타를 내면 타속이 낮아집니다.";
            sb.AppendLine($"TYPO-WARNING: \"{KeyPracticeWindow.TypoWarning}\" {(typoOk ? "PASS" : "FAIL")}");
            if (!typoOk) fail++;

            // <260812_14>(2) 통일된 공식의 '올바르게 누른 키 수' 세기
            sb.AppendLine();
            var strokeCases = new (string Text, int Expected)[]
            {
                ("가", 2), ("닭", 4), ("값", 4), ("과", 3), ("깎", 3),
                ("나라", 4), ("한글 타자", 11), ("hi", 2), ("1", 1),   // 한3+글3+공백1+타2+자2
            };
            int strokeFail = 0;
            foreach ((string text, int expected) in strokeCases)
            {
                int got = TypingMeasurer.CountStrokes(text);
                bool ok = got == expected;
                if (!ok) { strokeFail++; fail++; }
                sb.AppendLine($"{(ok ? "OK  " : "NG  ")}정타수 \"{text}\" = {got} (기대 {expected})");
            }
            sb.AppendLine("STROKE-COUNT: " + (strokeFail == 0 ? "PASS" : $"FAIL ({strokeFail}건)"));

            // <260812_10> 치트 단계 고르기 창: 목록이 단계 정의를 그대로 따라가는지
            sb.AppendLine();
            var cw = new CheatStageWindow(StageSets.Current.Stages)
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20000,
                Top = -20000,
                ShowActivated = false
            };
            cw.Show();
            cw.UpdateLayout();

            // <260812_12> 타속 계산 방법 고르기. 사용자 설정을 실제로 쓰므로 원래 값으로 되돌린다.
            string savedMethod = OpenTyping.UserSettingsStore.TpmMethod;
            try
            {
                sb.AppendLine($"타속 방법(시작)={TypingMeasurer.CurrentMethod} " +
                              $"버튼: 간단={cw.SimpleButton.IsEnabled} 원래={cw.OriginalButton.IsEnabled}");
                OpenTyping.UserSettingsStore.TpmMethod = TypingMeasurer.MethodOriginal;
                bool nowOriginal = TypingMeasurer.CurrentMethod == TypingMeasurer.MethodOriginal;
                OpenTyping.UserSettingsStore.TpmMethod = TypingMeasurer.MethodSimple;
                bool nowSimple = TypingMeasurer.CurrentMethod == TypingMeasurer.MethodSimple;
                OpenTyping.UserSettingsStore.TpmMethod = "이상한값";
                bool fallback = TypingMeasurer.CurrentMethod == TypingMeasurer.MethodSimple;
                sb.AppendLine($"    원래로 바꿈={nowOriginal} 간단으로 바꿈={nowSimple} 이상한값→간단={fallback}");
                sb.AppendLine($"    글자수 환산: '나라'={TypingMeasurer.CountLetter("나라")} " +
                              $"'ㄱ'={TypingMeasurer.CountLetter("ㄱ")} '1'={TypingMeasurer.CountLetter("1")}");
                sb.AppendLine("TPM-METHOD: " + (nowOriginal && nowSimple && fallback ? "PASS" : "FAIL"));
                if (!(nowOriginal && nowSimple && fallback)) fail++;
            }
            finally
            {
                OpenTyping.UserSettingsStore.TpmMethod = savedMethod;
                OpenTyping.UserSettingsStore.Save();
            }

            var cwItems = cw.StageCombo.Items.OfType<System.Windows.Controls.ComboBoxItem>().ToList();
            // <260812_27> '전부 체크 / 전부 해제'가 실제로 15개를 한 번에 다루는지
            int before = arcade.CheatEffects.Count;
            cheat.CheckAllForTest();
            int afterAll = arcade.CheatEffects.Count;
            cheat.UncheckAllForTest();
            int afterNone = arcade.CheatEffects.Count;
            bool bulkOk = afterAll == AcidRainWindow.EffectCount && afterNone == 0;
            sb.AppendLine($"전부 체크/해제: {before} → {afterAll} → {afterNone} {(bulkOk ? "PASS" : "FAIL")}");
            if (!bulkOk) fail++;

            bool effectsMatch = AcidCheatWindow.Effects.Length == AcidRainWindow.EffectCount;
            sb.AppendLine($"산성비 효과 가짓수: 목록 {AcidCheatWindow.Effects.Length} / 게임 {AcidRainWindow.EffectCount} " +
                          $"{(effectsMatch ? "일치" : "어긋남")}");
            if (!effectsMatch) fail++;

            bool cwOk = cwItems.Count == StageSets.Current.Stages.Count + 2
                        && (string)cwItems[0].Content == "원래대로"
                        && (string)cwItems[1].Content == "1단계도 통과 못함";
            sb.AppendLine($"치트 창: 항목 {cwItems.Count}개 (단계 {StageSets.Current.Stages.Count} + 2), " +
                          $"첫=\"{cwItems[0].Content}\" 둘=\"{cwItems[1].Content}\" 끝=\"{cwItems[cwItems.Count - 1].Content}\"");
            sb.AppendLine("CHEAT-WINDOW: " + (cwOk ? "PASS" : "FAIL"));
            if (!cwOk) fail++;
            try
            {
                var crtb = new RenderTargetBitmap((int)cw.ActualWidth, (int)cw.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                crtb.Render(cw);
                var cenc = new PngBitmapEncoder();
                cenc.Frames.Add(BitmapFrame.Create(crtb));
                using (var fs = File.Create(Path.ChangeExtension(outPath, ".cheat.png"))) cenc.Save(fs);
            }
            catch { /* 그림은 참고용 */ }
            cw.Close();

            // <260812_3> 경고 문구가 지시문 그대로인지
            sb.AppendLine();
            bool warnOk = KeyPracticeMenu.LockedWarning == "아직 전 단계를 통과하지 못했습니다.";
            sb.AppendLine($"LOCKED-WARNING: \"{KeyPracticeMenu.LockedWarning}\" {(warnOk ? "PASS" : "FAIL")}");

            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// 치트 토글(<260724_2>(3))이 켜기·끄기 양쪽으로 동작하고, 그에 따라 타일 활성·오락 해금이
        /// 함께 뒤집히는지 확인한다. 기록 파일을 실제로 쓰므로 원래 내용을 그대로 복원하고 끝낸다.
        /// </summary>
        private static void RunCheatTest(string outPath)
        {
            // <260927_3>(0.5.1) 기록 파일은 자판마다 따로다. 이 검사는 지금 자판(자판을 읽기 전이면 한글)의 것을 쓴다.
            string recPath = Path.Combine(StageRecords.RecordsDirectory,
                                          "stage_records_" + StageSets.Current.Key + ".json");
            string backup = File.Exists(recPath) ? File.ReadAllText(recPath) : null;

            var sb = new StringBuilder();
            try
            {
                // <260812_10> 치트는 이제 '몇 단계까지 통과한 것으로 칠지'를 정한다.
                int? startStages = StageRecords.CheatUpTo;
                bool noRecords = !StageRecords.HasRecord(1);
                sb.AppendLine($"start   치트={(startStages?.ToString() ?? "꺼짐")} 기록없음={noRecords}");

                bool ok = true;
                // 3단계까지 통과 처리 → 1~3 통과, 4는 아님. 타일은 4단계까지 활성(=3 통과의 결과).
                StageRecords.SetCheatUpTo(3);
                bool p3 = StageRecords.IsPassed(3), p4 = StageRecords.IsPassed(4);
                bool t4 = StageRecords.IsPracticeStageActive(4), t5 = StageRecords.IsPracticeStageActive(5);
                bool g3 = StageRecords.IsGameStageUnlocked(3), g4 = StageRecords.IsGameStageUnlocked(4);
                sb.AppendLine($"3단계까지 : 통과3={p3} 통과4={p4} 타일4={t4} 타일5={t5} 오락3={g3} 오락4={g4}");
                if (noRecords) ok &= p3 && !p4 && t4 && !t5 && g3 && !g4;

                // <2600919_4> 치트 켜짐 = 하늘색(Light.Cyan) 테마.
                string cheatThemeName = ThemeManager.Current.DetectTheme(Current)?.Name;
                bool cheatThemeOk = cheatThemeName == "Light.Cyan";
                sb.AppendLine($"치트 켜짐 테마 : {cheatThemeName} (기대 Light.Cyan) {(cheatThemeOk ? "PASS" : "FAIL")}");
                ok &= cheatThemeOk;

                // <260812_10-> 이미 통과한 단계보다 낮게 지정하면 그만큼 도로 잠겨야 한다.
                // 실제 기록으로 2단계를 통과시켜 놓고 '1단계까지'로 낮춘다.
                StageRecords.SetCheatUpTo(null);
                StageRecords.Record(2, StageRecords.TargetTa(2));
                bool byRecord = StageRecords.IsPassed(2) && StageRecords.IsPracticeStageActive(3);
                StageRecords.SetCheatUpTo(1);
                bool lowered = !StageRecords.IsPassed(2) && !StageRecords.IsPracticeStageActive(3)
                               && StageRecords.IsPracticeStageActive(2);
                StageRecords.SetCheatUpTo(null);
                bool restored = StageRecords.IsPassed(2) && StageRecords.IsPracticeStageActive(3);
                sb.AppendLine($"낮추기   : 기록으로 통과={byRecord} '1단계까지'로 낮춤={lowered} 끈 뒤 복귀={restored}");
                ok &= byRecord && lowered && restored;

                // <260812_10-2> '1단계도 통과 못함'(0): 치트는 켜진 채 아무 단계도 통과하지 않은 것으로.
                StageRecords.SetCheatUpTo(0);
                bool none = StageRecords.CheatOn && !StageRecords.IsPassed(1)
                            && !StageRecords.IsPracticeStageActive(2) && !StageRecords.IsGameStageUnlocked(1)
                            && StageRecords.IsPracticeStageActive(1);
                sb.AppendLine($"1단계도 못함: 치트켜짐={StageRecords.CheatOn} 통과1={StageRecords.IsPassed(1)} " +
                              $"타일1={StageRecords.IsPracticeStageActive(1)} 타일2={StageRecords.IsPracticeStageActive(2)} " +
                              $"오락1={StageRecords.IsGameStageUnlocked(1)}");
                ok &= none;

                // '원래대로'(null) → 치트 꺼짐, 실제 기록으로 복귀
                StageRecords.SetCheatUpTo(null);
                sb.AppendLine($"원래대로  : 치트={(StageRecords.CheatUpTo?.ToString() ?? "꺼짐")} " +
                              $"타일1={StageRecords.IsPracticeStageActive(1)} 타일3={StageRecords.IsPracticeStageActive(3)}");
                ok &= StageRecords.IsPracticeStageActive(1) && !StageRecords.CheatOn;

                // <2600919_4> 치트 꺼짐 = 원래 테마(Light.Cobalt)로 복귀.
                string normalThemeName = ThemeManager.Current.DetectTheme(Current)?.Name;
                bool normalThemeOk = normalThemeName == "Light.Cobalt";
                sb.AppendLine($"원래대로 테마 : {normalThemeName} (기대 Light.Cobalt) {(normalThemeOk ? "PASS" : "FAIL")}");
                ok &= normalThemeOk;

                sb.AppendLine("CHEAT: " + (ok ? "PASS" : "FAIL"));
            }
            finally
            {
                // 어떤 경우에도 사용자의 원래 기록 파일을 되돌린다.
                try
                {
                    if (backup != null) AtomicFile.WriteText(recPath, backup);
                    else if (File.Exists(recPath)) File.Delete(recPath);
                }
                catch { /* 무시 */ }
            }
            sb.AppendLine("기록 파일 복원 완료");
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// qksqhr 1라운드 edge-test: 정밀 엣지케이스 탐색에서 확인해 고친 항목들을 실제로 검증한다.
        /// 치트 값을 건드리므로 CHEAT-TEST와 같은 방식으로 기록 파일을 백업/복원한다.
        /// </summary>
        private static void RunEdgeTest(string outPath)
        {
            Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // <260927_3>(0.5.1) 기록 파일은 자판마다 따로다. 이 검사는 지금 자판(자판을 읽기 전이면 한글)의 것을 쓴다.
            string recPath = Path.Combine(StageRecords.RecordsDirectory,
                                          "stage_records_" + StageSets.Current.Key + ".json");
            string backup = File.Exists(recPath) ? File.ReadAllText(recPath) : null;

            var sb = new StringBuilder();
            int fail = 0;
            try
            {
                // 1. 반올림: CountLetter 가 이 파일의 다른 반올림(AwayFromZero)과 일치하는지.
                //    한글 한 음절=2.5타 → 은행원 반올림(옛 Convert.ToInt32)이면 2, AwayFromZero면 3.
                int letter1 = TypingMeasurer.CountLetter("가");
                bool roundOk = letter1 == 3;
                sb.AppendLine($"CountLetter(\"가\")={letter1} (기대 3) {(roundOk ? "PASS" : "FAIL")}");
                if (!roundOk) fail++;

                // 2. 서로게이트 쌍(이모지 등)이 2타가 아니라 1타로 세지는지.
                int emoji = TypingMeasurer.CountStrokes("\U0001F600");
                bool emojiOk = emoji == 1;
                sb.AppendLine($"CountStrokes(이모지)={emoji} (기대 1) {(emojiOk ? "PASS" : "FAIL")}");
                if (!emojiOk) fail++;

                // 3. Differ.CalculateAccuracy(빈 diffs) → NaN 아닌 0 (문장연습 크래시 예방).
                double acc = Differ.CalculateAccuracy(new List<Differ.DiffData>());
                bool accOk = acc == 0;
                sb.AppendLine($"CalculateAccuracy(빈 diffs)={acc} (기대 0) {(accOk ? "PASS" : "FAIL")}");
                if (!accOk) fail++;

                // 4. DictationState.Wrong 이 칸(7글자)을 넘지 않는지.
                var dictation = new DictationState();
                dictation.Start("가나다");
                for (int i = 0; i < 15; i++) dictation.Wrong("x");
                int totalLen = dictation.Segments().Sum(s => s.Text.Length);
                bool dictOk = totalLen <= 7;
                sb.AppendLine($"DictationState.Wrong 15회 후 칸 길이={totalLen} (기대 <=7) {(dictOk ? "PASS" : "FAIL")}");
                if (!dictOk) fail++;

                // 5. SyllablePracticeWindow("") 가 알아보기 힘든 IndexOutOfRangeException 대신
                //    바로 원인을 알 수 있는 예외로 막는지.
                bool threw = false;
                try { _ = new SyllablePracticeWindow(""); }
                catch (ArgumentException) { threw = true; }
                sb.AppendLine($"SyllablePracticeWindow(\"\") → ArgumentException={threw} {(threw ? "PASS" : "FAIL")}");
                if (!threw) fail++;

                // 6. 빈 자판 폴더 오류 메시지가 저장된 설정값이 아니라 실제로 넘긴 경로를 담는지.
                string emptyDir = Path.Combine(Path.GetTempPath(), "otp_edgetest_empty_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(emptyDir);
                bool pathInMsg = false;
                try { KeyLayout.LoadFromDirectory(emptyDir); }
                catch (KeyLayoutLoadFail ex) { pathInMsg = ex.Message.Contains(emptyDir); }
                finally { try { Directory.Delete(emptyDir); } catch { /* 무시 */ } }
                sb.AppendLine($"빈 자판 폴더 오류 메시지에 실제 경로 포함={pathInMsg} {(pathInMsg ? "PASS" : "FAIL")}");
                if (!pathInMsg) fail++;

                // 7. 자판 데이터의 한 행이 null 이면 NullReferenceException 이 아니라
                //    InvalidKeyLayoutDataException(원인을 알 수 있는 진단)으로 막히는지.
                bool nullRowOk = false;
                string nullRowActual = "";
                try
                {
                    string layoutPath = Path.Combine(AppContext.BaseDirectory, "layouts", "DubeolsikStandard.json");
                    var jobj = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(layoutPath));
                    ((Newtonsoft.Json.Linq.JArray)jobj["KeyLayoutData"])[0] = Newtonsoft.Json.Linq.JValue.CreateNull();
                    KeyLayout.Parse(jobj.ToString());
                }
                catch (InvalidKeyLayoutDataException) { nullRowOk = true; }
                catch (Exception ex) { nullRowActual = ex.GetType().Name; }
                sb.AppendLine($"자판 데이터 첫 행 null → InvalidKeyLayoutDataException={nullRowOk}" +
                              (nullRowActual.Length > 0 ? $" (실제: {nullRowActual})" : "") +
                              $" {(nullRowOk ? "PASS" : "FAIL")}");
                if (!nullRowOk) fail++;

                // 8. 치트 창: 단계 콤보를 안 건드리고 '확인'만 눌러도 원래 값(옛 '전부 열기')이
                //    지금 단계 수로 조용히 줄어들지 않고 그대로 보존되는지.
                StageRecords.SetCheatUpTo(int.MaxValue);
                var csw = new CheatStageWindow(StageSets.Current.Stages)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -20000, Top = -20000, ShowActivated = false
                };
                csw.Show();
                csw.UpdateLayout();
                csw.ConfirmWithoutTouchingForTest();
                bool preserved = csw.Stages == int.MaxValue;
                sb.AppendLine($"치트 '확인'만 눌렀을 때 원래 값 보존={preserved} (Stages={csw.Stages}) {(preserved ? "PASS" : "FAIL")}");
                if (!preserved) fail++;
                csw.Close();

                // 9. 완료 창: 이번 판 타수는 목표 미달이어도, 실제로(치트로) 이미 통과된 단계면
                //    '아직 활성화되지 않음'이 아니라 '활성화되었습니다'로 보이는지.
                StageRecords.SetCheatUpTo(5); // 1~5단계를 통과 처리
                int gameId3 = StageSets.Current.Stages[2].GameStageId;
                var fw2 = new StageFinishWindow(3, StageSets.Current.Stages.Count, StageRecords.TargetTa(3) - 1, gameId3)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -20000, Top = -20000, ShowActivated = false
                };
                fw2.Show();
                fw2.UpdateLayout();
                string expectedUnlock = StageFinishWindow.BuildUnlockLine(3, false, true, true);
                bool reachedOk = fw2.UnlockText.Text == expectedUnlock;
                sb.AppendLine($"완료창(치트로 이미 통과, 이번 판은 미달): \"{fw2.UnlockText.Text}\" {(reachedOk ? "PASS" : "FAIL")}");
                if (!reachedOk) fail++;
                fw2.Close();
                StageRecords.SetCheatUpTo(null);

                // 10. 산성비: 평소(치트 아님) 무작위 뽑기에는 '지구 환경을 지켜라'가 안 나오는지,
                //     치트 효과 선택을 바꾸면 이미 채워 둔 주머니 대신 곧바로 새 선택이 반영되는지.
                var arcade2 = new AcidRainWindow(StageSets.DubeolsikStandard, 1);
                bool earthExcluded = arcade2.NormalEventNeverEarthForTest();
                sb.AppendLine($"평소 무작위 뽑기에 지구 효과 제외={earthExcluded} {(earthExcluded ? "PASS" : "FAIL")}");
                if (!earthExcluded) fail++;

                arcade2.CheatEffects.Clear();
                arcade2.CheatEffects.Add(1);
                arcade2.CheatEffects.Add(2);
                arcade2.NextCheatEffectForTest();          // 주머니를 채우고 하나 소비
                arcade2.CheatEffects.Clear();
                arcade2.CheatEffects.Add(9);
                arcade2.ResetCheatEffectBag();              // 선택이 바뀌었으니 곧바로 비운다
                int picked = arcade2.NextCheatEffectForTest();
                bool bagOk = picked == 9;
                sb.AppendLine($"치트 효과 선택을 바꾸면 주머니가 즉시 반영={bagOk} (뽑힘={picked}) {(bagOk ? "PASS" : "FAIL")}");
                if (!bagOk) fail++;

                // 11. 자판 통계·연습 키 저장이 자판 파일이 아니라 AppData(KeyLayoutUserDataStore)에
                //     남는지, 그리고 다른 자판의 저장 데이터를 지우지 않고 병합되는지 (<260828_1>).
                string layoutDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "OTP", "OpenTypingPlus", "key_layout_data.json");
                string layoutDataBackup = File.Exists(layoutDataPath) ? File.ReadAllText(layoutDataPath) : null;
                try
                {
                    const string otherName = "__edgetest_other__", targetName = "__edgetest_target__";

                    var other = new KeyLayout(otherName, "한글", null, new List<KeyPos>());
                    other.Stats.KeyIncorrectCount[new KeyPos(0, 0)] = 5;
                    KeyLayout.TrySaveKeyLayout(other, out _);

                    var target = new KeyLayout(targetName, "한글", null,
                                               new List<KeyPos> { new KeyPos(1, 2) });
                    target.Stats.KeyIncorrectCount[new KeyPos(2, 3)] = 7;
                    bool saveOk = KeyLayout.TrySaveKeyLayout(target, out _);

                    var reloadedTarget = new KeyLayout(targetName, "한글", null, new List<KeyPos>());
                    KeyLayoutUserDataStore.ApplyTo(reloadedTarget);
                    bool statsRoundTrip = reloadedTarget.Stats.KeyIncorrectCount
                        .TryGetValue(new KeyPos(2, 3), out int cnt) && cnt == 7;
                    bool defaultKeysRoundTrip = reloadedTarget.DefaultKeys.Contains(new KeyPos(1, 2));

                    var reloadedOther = new KeyLayout(otherName, "한글", null, new List<KeyPos>());
                    KeyLayoutUserDataStore.ApplyTo(reloadedOther);
                    bool otherPreserved = reloadedOther.Stats.KeyIncorrectCount
                        .TryGetValue(new KeyPos(0, 0), out int otherCnt) && otherCnt == 5;

                    bool storeOk = saveOk && statsRoundTrip && defaultKeysRoundTrip && otherPreserved;
                    sb.AppendLine($"자판 통계 AppData 저장·병합: 저장={saveOk} 통계복원={statsRoundTrip} " +
                                  $"연습키복원={defaultKeysRoundTrip} 다른자판보존={otherPreserved} {(storeOk ? "PASS" : "FAIL")}");
                    if (!storeOk) fail++;

                    // 11-1. 파일에 "자판 이름": null 로 적힌 항목이 있어도 시작 도중 예외로 앱이 안 뜨는 일이
                    //       없는지(그 자판은 저장된 것이 없는 것으로 친다).
                    File.WriteAllText(layoutDataPath, "{\"" + targetName + "\": null}");
                    bool nullEntryOk;
                    try
                    {
                        var withNull = new KeyLayout(targetName, "한글", null, new List<KeyPos>());
                        KeyLayoutUserDataStore.ApplyTo(withNull);
                        nullEntryOk = withNull.Stats != null;
                    }
                    catch (Exception) { nullEntryOk = false; }
                    sb.AppendLine($"자판 통계 파일의 null 항목에도 시작 가능={nullEntryOk} {(nullEntryOk ? "PASS" : "FAIL")}");
                    if (!nullEntryOk) fail++;
                }
                finally
                {
                    try
                    {
                        if (layoutDataBackup != null) AtomicFile.WriteText(layoutDataPath, layoutDataBackup);
                        else if (File.Exists(layoutDataPath)) File.Delete(layoutDataPath);
                    }
                    catch { /* 무시 */ }
                }

                // 12. 빈 연습 데이터 폴더 오류 메시지가 저장된 설정값이 아니라 실제로 넘긴 경로를
                //     담는지 (<260828_2-1>, KeyLayout.LoadFromDirectory와 같은 유형의 버그였음).
                string emptyPracticeDir = Path.Combine(Path.GetTempPath(), "otp_edgetest_empty_practice_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(emptyPracticeDir);
                bool practicePathInMsg = false;
                try { PracticeData.LoadFromDirectory(emptyPracticeDir); }
                catch (PracticeDataLoadFail ex) { practicePathInMsg = ex.Message.Contains(emptyPracticeDir); }
                finally { try { Directory.Delete(emptyPracticeDir); } catch { /* 무시 */ } }
                sb.AppendLine($"빈 연습 데이터 폴더 오류 메시지에 실제 경로 포함={practicePathInMsg} {(practicePathInMsg ? "PASS" : "FAIL")}");
                if (!practicePathInMsg) fail++;

                // 13. 단어 목록이 자모를 풀어 쓴 형식(NFD)으로 저장돼 있어도 완성형으로 맞춰 읽는지.
                string nfd = "나라".Normalize(System.Text.NormalizationForm.FormD);
                WordCatalog.UseJsonForTest("{\"hangul\":{\"stages\":[{\"stage\":1,\"levels\":[{\"level\":1,\"words\":[\"" + nfd + "\"]}]}]}}");
                IReadOnlyList<string> nfcWords = WordCatalog.RawLevelWords(WordCatalog.Hangul, 1, 1);
                WordCatalog.UseJsonForTest(null);
                bool nfcOk = nfcWords.Count == 1 && nfcWords[0] == "나라";
                sb.AppendLine($"NFD 단어 → 완성형으로 읽기={nfcOk} {(nfcOk ? "PASS" : "FAIL")}");
                if (!nfcOk) fail++;

                // 14. 원자적 쓰기가 교체에 실패해도 .tmp 파일을 남기지 않는지(대상 경로가 폴더라 교체 불가).
                string atomicDir = Path.Combine(Path.GetTempPath(), "otp_edgetest_atomic_" + Guid.NewGuid().ToString("N"));
                string blocked = Path.Combine(atomicDir, "target.json");
                Directory.CreateDirectory(blocked);   // 같은 이름의 폴더가 있어 파일로 옮길 수 없다
                bool atomicThrew = false;
                try { AtomicFile.WriteText(blocked, "{}"); }
                catch (Exception) { atomicThrew = true; }
                bool noLeftover = Directory.GetFiles(atomicDir, "*.tmp").Length == 0;
                try { Directory.Delete(atomicDir, true); } catch { /* 무시 */ }
                bool atomicOk = atomicThrew && noLeftover;
                sb.AppendLine($"원자적 쓰기 실패 시 .tmp 남지 않음: 예외={atomicThrew} 남은tmp없음={noLeftover} {(atomicOk ? "PASS" : "FAIL")}");
                if (!atomicOk) fail++;

                sb.AppendLine();
                sb.AppendLine("EDGETEST: " + (fail == 0 ? "PASS" : $"FAIL ({fail}건)"));
            }
            catch (Exception ex)
            {
                sb.AppendLine("EXCEPTION: " + ex);
            }
            finally
            {
                try
                {
                    if (backup != null) AtomicFile.WriteText(recPath, backup);
                    else if (File.Exists(recPath)) File.Delete(recPath);
                }
                catch { /* 무시 */ }
            }

            TryWrite(outPath, sb.ToString());
        }

        /// <summary>
        /// <260812_13> 음절연습 '입력 칸'에 2음절 이상 오타를 넣으면 그 입력이 통째로 버려지고,
        /// 이어서 제시어를 맞게 치면 바로 통과해야 한다. IME 없이 Text 를 직접 넣어 같은 경로를 태운다.
        /// </summary>
        private static void RunSyllableTest(string outPath)
        {
            var sb = new StringBuilder();
            var win = new SyllablePracticeWindow("가나다라마")
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20000,
                Top = -20000,
                ShowActivated = false
            };
            win.Show();
            win.UpdateLayout();

            char target = win.CurrentSyllable;
            string wrong = new string(new[] { target == '가' ? '나' : '가', target == '다' ? '라' : '다' });
            sb.AppendLine($"제시어='{target}' 2음절 오타='{wrong}'");

            win.CurrentTextBox.Text = wrong;
            win.UpdateLayout();
            // <260812_20> 타일 줄: '타속' 타일이 입력 칸과 좌우 가운데가 맞는가 + 창 그림
            win.AlignSpeedTileToInputBox();
            win.UpdateLayout();
            double BoxCenter(FrameworkElement el) =>
                el.TransformToAncestor(win).Transform(new Point(el.ActualWidth / 2, 0)).X;
            sb.AppendLine($"타일 정렬(1차): 입력칸 중심={BoxCenter(win.CurrentTextBoxBorder):F1} " +
                          $"타속타일 중심={BoxCenter(win.SpeedTile):F1} 타일폭={win.SpeedTile.ActualWidth:F1} " +
                          $"여백={win.TileRow.Margin.Left:F1}");
            win.AlignSpeedTileToInputBox();
            win.UpdateLayout();
            sb.AppendLine($"타일 정렬(2차): 입력칸 중심={BoxCenter(win.CurrentTextBoxBorder):F1} " +
                          $"타속타일 중심={BoxCenter(win.SpeedTile):F1} 여백={win.TileRow.Margin.Left:F1}");
            try
            {
                var rtb = new RenderTargetBitmap((int)win.ActualWidth, (int)win.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(win);
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(rtb));
                using (var fs = File.Create(Path.ChangeExtension(outPath, ".png"))) enc.Save(fs);
            }
            catch { /* 그림은 참고용 */ }

            bool whiteAfterWrong = ReferenceEquals(win.CurrentTextBoxBorder.Background, System.Windows.Media.Brushes.White);
            sb.AppendLine($"오타 뒤: 맞은 개수={win.CorrectCount} 칸 배경={(whiteAfterWrong ? "흰색(비워짐)" : "빨강(남아 있음)")}");

            // 이어서 제시어를 맞게 친다 — 버려진 오타 뒤에 붙여도 통과해야 한다.
            win.CurrentTextBox.Text = wrong + target;
            win.UpdateLayout();
            bool passed = win.CorrectCount == 1;
            sb.AppendLine($"이어서 '{target}' 입력: 맞은 개수={win.CorrectCount} (기대 1)");

            sb.AppendLine("SYLLABLE: " + (whiteAfterWrong && passed ? "PASS" : "FAIL"));
            win.Close();

            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// <260812_15> 자리연습 새 창에 실제 키 입력을 흘려 넣어, 오타에 빨간 경고가 뜨고 다음 정타에
        /// 원래 문구로 돌아가는지 확인한다. 손가락 레이어 인트로(약 1.1초)가 끝나야 연습 값이 제시되므로
        /// 타이머로 기다렸다가 두드린다.
        /// </summary>
        private static void RunTypoWarnTest(string outPath)
        {
            var sb = new StringBuilder();
            var main = new MainWindow { WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000 };
            Current.MainWindow = main;
            main.Show();

            var win = new KeyPracticeWindow(StageSets.DubeolsikStandard, StageSets.DubeolsikStandard.Stages[0])
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20000,
                Top = -20000,
            };
            win.Show();

            // 1단계의 '원래 문구'는 NoticeText 가 아니라 GuideTextPanel("10개의 손가락을…")이므로 함께 본다.
            string Notice() => (win.NoticeText.Visibility == Visibility.Visible
                                    ? "\"" + win.NoticeText.Text + "\"" : "(경고 없음)")
                               + " / 1단계 안내문=" + (win.GuideTextPanel.Visibility == Visibility.Visible ? "보임" : "숨김");

            void Press(System.Windows.Input.Key key)
            {
                var args = new System.Windows.Input.KeyEventArgs(
                    System.Windows.Input.Keyboard.PrimaryDevice,
                    PresentationSource.FromVisual(win), 0, key)
                { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
                win.RaiseEvent(args);
            }

            // 제시된 자리를 실제로 누를 Key 값을 되찾는다(KeyPos.FromKeyCode 의 역방향).
            System.Windows.Input.Key KeyFor(KeyPos pos)
            {
                foreach (System.Windows.Input.Key k in Enum.GetValues(typeof(System.Windows.Input.Key)))
                {
                    KeyPos p = null;
                    try { p = KeyPos.FromKeyCode(k); } catch { }
                    if (p != null && p.Row == pos.Row && p.Column == pos.Column) return k;
                }
                return System.Windows.Input.Key.None;
            }

            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                try
                {
                    KeyPos expected = win.ExpectedPos;
                    sb.AppendLine($"제시어=\"{win.CurrentKey?.KeyData}\" 자리={(expected == null ? "없음" : expected.Row + "-" + expected.Column)}");
                    sb.AppendLine($"시작    : {Notice()}");

                    // 제시된 자리가 아닌 키를 눌러 오타를 낸다.
                    KeyPos wrongPos = expected != null && expected.Row == 1 && expected.Column == 0
                        ? new KeyPos(1, 1) : new KeyPos(1, 0);
                    Press(KeyFor(wrongPos));
                    string afterWrong = Notice();
                    sb.AppendLine($"오타 뒤 : {afterWrong}");

                    Press(KeyFor(expected));
                    string afterRight = Notice();
                    sb.AppendLine($"정타 뒤 : {afterRight}");

                    // <260812_17> 정확도 타일이 정타·오타를 따라 갱신되는가
                    sb.AppendLine($"정확도: 정타={win.CorrectCount} 오타={win.IncorrectCount} → {win.TypingAccuracy}%");

                    // <260812_16> 손 모양 창의 견본 단추가 마우스 올림에 색을 바꾸지 않는가
                    var hand = new HandSettingsWindow(win.FingerLayer)
                    {
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Left = -20000,
                        Top = -20000,
                        ShowActivated = false
                    };
                    hand.Show();
                    hand.UpdateLayout();
                    var swatches = hand.ColorSwatchPanel.Children.OfType<System.Windows.Controls.Button>().ToList();
                    var seventh = swatches[6];
                    string seventhColor = ((SolidColorBrush)seventh.Background).Color.ToString();
                    object defaultColor = OpenTyping.UserSettingsStore.DefaultFingerLayerColor;
                    sb.AppendLine($"윤곽선 색 견본 {swatches.Count}개, 7번째={seventhColor}, 설정 기본값={defaultColor}");
                    sb.AppendLine($"    견본 단추 전용 모양={(seventh.Style != null ? "적용됨" : "없음")} " +
                                  $"(마우스 올림에 테두리만 회색, 칠은 그대로)");
                    hand.Close();

                    // 안내 문구 영역에 뜨는 글자들의 실제 세로 위치를 잰다(색 때문인지 요소 때문인지 확인).
                    string Where(FrameworkElement el, string label)
                    {
                        if (el.ActualHeight <= 0) return $"    {label}: 배치 안 됨";
                        GeneralTransform t = el.TransformToAncestor(win);
                        Rect r = t.TransformBounds(new Rect(0, 0, el.ActualWidth, el.ActualHeight));
                        return $"    {label}: 꼭대기={r.Top:F1} 바닥={r.Bottom:F1} 높이={r.Height:F1} " +
                               $"글꼴크기={(el as System.Windows.Controls.TextBlock)?.FontSize.ToString("F1") ?? "-"}";
                    }
                    sb.AppendLine("── 안내 문구 영역 세로 위치 ──");
                    win.NoticeText.Visibility = Visibility.Visible;
                    win.NoticeText.Text = KeyPracticeWindow.TypoWarning;
                    win.GuideTextPanel.Visibility = Visibility.Visible;
                    win.UpdateLayout();
                    sb.AppendLine(Where(win.NoticeText, "경고/축하 문구(NoticeText)"));
                    sb.AppendLine(Where(win.GuideTextPanel, "1단계 안내문(GuideTextPanel)"));
                    foreach (var color in new[] { "빨강", "파랑", "회색" })
                    {
                        win.NoticeText.Foreground = color == "빨강" ? Brushes.Red
                                                  : color == "파랑" ? Brushes.Blue : Brushes.Gray;
                        win.UpdateLayout();
                        sb.AppendLine(Where(win.NoticeText, $"NoticeText({color})"));
                    }

                    bool ok = afterWrong.StartsWith("\"" + KeyPracticeWindow.TypoWarning + "\"")
                              && afterRight.StartsWith("(경고 없음)")
                              && afterRight.EndsWith("보임");   // 1단계 원래 문구가 되돌아왔는가
                    sb.AppendLine("TYPO-FLOW: " + (ok ? "PASS" : "FAIL"));
                }
                catch (Exception ex) { sb.AppendLine("ERR " + ex.Message); }

                Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
                Environment.Exit(0);
            };
            timer.Start();
        }

        /// <summary>
        /// 자판 파일에서 만든 '글자 → 키 입력' 규칙 검사. 두벌식·QWERTY는 예전 코드 표(HangulJamo)와 모든
        /// 음절·글자에서 같아야 하고, 세벌식 390·Dvorak은 대표 사례가 자판 파일의 실제 키 자리와 맞아야 한다.
        /// 끝으로 세벌식에 임시 단계 정의("from": "all")를 붙여 단계 생성까지 되는지 본다.
        /// </summary>
        private static void RunLayoutTest(string outPath)
        {
            var sb = new StringBuilder();
            int fail = 0;
            string S(List<HangulJamo.Stroke> ss) =>
                string.Join(" ", ss.Select(k => $"({k.Pos.Row},{k.Pos.Column}{(k.IsShift ? "S" : "")})"));
            bool Same(List<HangulJamo.Stroke> a, List<HangulJamo.Stroke> b) =>
                a.Count == b.Count && a.Zip(b, (x, y) => x.Pos.Row == y.Pos.Row && x.Pos.Column == y.Pos.Column
                                                          && x.IsShift == y.IsShift).All(v => v);

            // 1) 두벌식 표준 한글: 모든 음절 + 낱자·숫자·기호가 예전 표와 같은가
            KeyboardMap ko = KeyboardMaps.For("두벌식 표준 한글");
            int koDiff = 0;
            var koChars = Enumerable.Range(0xAC00, 11172).Select(c => (char)c)
                .Concat("ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎㅏㅐㅑㅒㅓㅔㅕㅖㅗㅛㅜㅠㅡㅣ1234567890!@#$%^&*()-_=+;:,<.>/?'\"[]{}`~");
            foreach (char ch in koChars)
                if (!Same(ko.DecomposeChar(ch), HangulJamo.DecomposeChar(ch))) { if (koDiff++ < 5) sb.AppendLine($"  다름: {ch}"); }
            // 입력 진행 표시: 한글 제시어 목록의 모든 단어를 0타부터 끝까지 올려 가며 예전 방식과 견준다.
            int typedDiff = 0, typedChecked = 0;
            foreach (string w in WordCatalog.AllWords(WordCatalog.Hangul).Concat(new[] { "닭갈비", "왜냐하면", "읽었다" }))
            {
                int total = HangulJamo.Decompose(w).Count;
                for (int k = 0; k <= total; k++)
                {
                    typedChecked++;
                    if (ko.TypedPrefix(w, k) != HangulJamo.TypedPrefix(w, k) && typedDiff++ < 5)
                        sb.AppendLine($"  입력 진행 다름: {w} {k}타 → {ko.TypedPrefix(w, k)} (예전 {HangulJamo.TypedPrefix(w, k)})");
                }
            }
            sb.AppendLine($"두벌식: 예전 표와 다른 글자 {koDiff}개, 입력 진행 {typedChecked:N0}건 중 다른 것 {typedDiff}건");
            if (koDiff > 0 || typedDiff > 0) fail++;

            // 2) QWERTY 영문: 영문 글자·숫자·기호가 예전 표와 같은가
            KeyboardMap en = KeyboardMaps.For("QWERTY 영문");
            int enDiff = 0;
            foreach (char ch in "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*()-_=+;:,<.>/?'\"")
                if (!Same(en.DecomposeChar(ch), HangulJamo.DecomposeChar(ch))) enDiff++;
            bool enCase = en.HasCaseLetters && en.IsCaseLetterKey(new KeyPos(2, 0)) && !en.IsCaseLetterKey(new KeyPos(2, 9))
                          && !en.CanTypeHangul && en.AlphabetOf(en.DecomposeChar('D')[0]) == "d";
            sb.AppendLine($"QWERTY: 예전 표와 다른 글자 {enDiff}개, 대소문자 키 판정={enCase}");
            if (enDiff > 0 || !enCase) fail++;

            // 2-1) 자판 파일을 못 찾을 때의 내장 예비 규칙이 자판 파일로 만든 규칙과 같은가
            KeyboardMap koFb = KeyboardMap.BuiltInFallback("두벌식 표준 한글");
            KeyboardMap enFb = KeyboardMap.BuiltInFallback("QWERTY 영문");
            int fbDiff = koChars.Count(ch => !Same(koFb.DecomposeChar(ch), ko.DecomposeChar(ch)))
                         + "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ;:,<.>/?".Count(ch => !Same(enFb.DecomposeChar(ch), en.DecomposeChar(ch)));
            bool fbFlags = !koFb.HasCaseLetters && koFb.CanTypeHangul && enFb.HasCaseLetters && !enFb.CanTypeHangul;
            sb.AppendLine($"내장 예비 규칙: 자판 파일 규칙과 다른 글자 {fbDiff}개, 한글/대소문자 판정={fbFlags}");
            if (fbDiff > 0 || !fbFlags) fail++;

            // 3) 세벌식 390: 자판 파일의 실제 자리와 대조
            KeyboardMap s390 = KeyboardMaps.For("세벌식 390 한글");
            var cases390 = new (string Text, string Want)[]
            {
                ("각", "(2,7) (2,3) (3,1)"),              // 초성 ㄱ, ㅏ, 받침 ㄱ
                ("까", "(2,7) (2,7) (2,3)"),              // 쌍초성은 같은 키 두 번
                ("와", "(2,6) (3,9) (2,3)"),              // 겹모음: ㅗ(이중 모음) + ㅏ
                ("닭", "(1,6) (2,3) (2,2S)"),             // 겹받침 키 ㄺ(받침)
                ("넋", "(2,5) (1,4) (3,1) (1,0)"),        // 없는 겹받침 ㄳ = ㄱ(받침) + ㅅ(받침)
                ("의", "(2,6) (0,8)"),                    // ㅢ 키(0행 8번째 칸)
                ("있", "(2,6) (2,2) (0,2)"),              // ㅆ(받침) 키
            };
            foreach ((string text, string want) in cases390)
            {
                string got = S(s390.Decompose(text));
                bool ok = got == want;
                if (!ok) fail++;
                sb.AppendLine($"{(ok ? "OK  " : "NG  ")}세벌식 \"{text}\": {got} (기대 {want})");
            }
            string typed390 = string.Join("/", Enumerable.Range(0, 4).Select(k => s390.TypedPrefix("까", k)))
                              + " | " + string.Join("/", Enumerable.Range(0, 4).Select(k => s390.TypedPrefix("와", k)));
            bool typed390Ok = typed390 == "/ㄱ/ㄲ/까 | /ㅇ/오/와";
            if (!typed390Ok) fail++;
            sb.AppendLine($"{(typed390Ok ? "OK  " : "NG  ")}세벌식 입력 진행: {typed390}");
            string a390 = string.Join(",", s390.AlphabetsOf("각").OrderBy(x => x, StringComparer.Ordinal));
            bool alphaOk = a390 == "ㄱ,ㄱ (받침),ㅏ";
            if (!alphaOk) fail++;
            sb.AppendLine($"{(alphaOk ? "OK  " : "NG  ")}세벌식 \"각\"의 알파벳: {a390}");

            // 4) Dvorak 영문
            KeyboardMap dv = KeyboardMaps.For("Dvorak 영문");
            string dvGot = S(dv.Decompose("Hello"));
            bool dvOk = dvGot == "(2,6S) (2,2) (1,9) (1,9) (2,1)" && dv.HasCaseLetters && !dv.CanTypeHangul;
            if (!dvOk) fail++;
            sb.AppendLine($"{(dvOk ? "OK  " : "NG  ")}Dvorak \"Hello\": {dvGot}");

            // 5) 세벌식에 임시 단계 정의를 붙여 본다(기존 한글 단어 목록을 "from": "all" 로 재사용)
            string json = @"{ ""stages"": [ {
                ""name"": ""시험 단계"", ""game"": 1, ""target_ta"": 200, ""targets"": [""ㅇ"", ""ㅏ"", ""ㄴ (받침)""],
                ""words"": { ""from"": ""all"", ""required"": [""ㅏ""],
                  ""levels"": [ { ""parts"": [[""기본"", [""ㅇ"", ""ㄴ"", ""ㅁ"", ""ㅏ"", ""ㅣ"", ""ㅓ"", ""ㄴ (받침)"", ""ㅇ (받침)"", ""ㅁ (받침)""]]] },
                                { ""parts"": [[""추가"", [""ㄱ"", ""ㄷ"", ""ㄹ"", ""ㄹ (받침)""]]] } ] },
                ""rounds"": [ { ""kind"": ""sequential"", ""keys"": [""ㅇ"", ""ㅏ"", ""ㄴ (받침)""] },
                              { ""kind"": ""syllables"", ""count"": 5, ""no_repeat"": true },
                              { ""kind"": ""words"", ""level"": 2, ""count"": 5, ""no_repeat"": true } ] } ] }";
            List<PracticeStage> trial = StageDefinitionLoader.Parse(json, WordCatalog.Hangul, s390);
            PracticeStage t0 = trial.FirstOrDefault();
            List<PracticePrompt> prog = t0?.Generate(new Random(7)) ?? new List<PracticePrompt>();
            bool typable = prog.All(p => !p.IsText || s390.Decompose(p.Text).Count > 0);
            bool trialOk = t0 != null && t0.Words.Singles.Count > 0 && t0.Words.UpTo(2).Count > 0 && prog.Count == 13 && typable;
            if (!trialOk) fail++;
            sb.AppendLine($"{(trialOk ? "OK  " : "NG  ")}세벌식 임시 단계: 한 음절 {t0?.Words.Singles.Count}개, 1수준 단어 {t0?.Words.Unique(1).Count}개 " +
                          $"(예: {string.Join(" ", t0?.Words.Unique(1).Take(8) ?? new string[0])}), 2고유 {t0?.Words.Unique(2).Count}개, " +
                          $"제시어 {prog.Count}개: {string.Join(" ", prog.Select(p => p.IsText ? p.Text : "[" + p.Key.Row + "," + p.Key.Column + "]"))}");

            sb.AppendLine("LAYOUT: " + (fail == 0 ? "PASS" : $"FAIL ({fail}건)"));
            TryWrite(outPath, sb.ToString());
        }

        private static void SavePng(FrameworkElement el, string path)
        {
            try
            {
                el.UpdateLayout();
                var rtb = new RenderTargetBitmap((int)Math.Ceiling(el.ActualWidth), (int)Math.Ceiling(el.ActualHeight),
                                                 96, 96, PixelFormats.Pbgra32);
                rtb.Render(el);
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(rtb));
                using (FileStream fs = File.Create(path)) enc.Save(fs);
            }
            catch { /* 그림은 참고용 */ }
        }

        /// <summary>
        /// <260927_5>·<260927_6> 한글·영문 자리연습 화면의 타일 배치를 그림으로 남기고,
        /// <260927_3>(2) 영문 단계 창에서 Caps Lock이 켜지면 글자 키가 오답이 되고 경고 문구·[Caps Lock]
        /// 깜빡임이 나타났다가, 끄면 곧바로 사라지는지 실제 키 입력으로 확인한다.
        /// </summary>
        private static void RunEnglishTest(string outPath)
        {
            var sb = new StringBuilder();
            string dir = Path.GetDirectoryName(outPath);
            Directory.CreateDirectory(dir);
            var main = new MainWindow { WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000 };
            Current.MainWindow = main;
            main.Show();
            // 메인 창이 설정의 자판을 다 읽은 뒤에 시작한다 — 안 그러면 뒤늦게 읽힌 설정 자판이 아래에서
            // 검사용으로 바꿔 둔 자판을 덮어쓴다.
            main.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                new Action(() =>
                {
                    try { RunEnglishTestBody(main, sb, dir, outPath); }
                    catch (Exception ex) { TryWrite(outPath, sb + "\nEXCEPTION: " + ex); Environment.Exit(0); }
                }));
        }

        private static void RunEnglishTestBody(MainWindow main, StringBuilder sb, string dir, string outPath)
        {
            KeyLayout original = OpenTyping.MainWindow.CurrentKeyLayout;

            // 단계 묶음의 자판 이름과 같은 이름의 자판 파일(layouts\*.json)을 찾는다.
            KeyLayout LayoutFor(IStageSet s)
            {
                foreach (string f in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "layouts"), "*.json"))
                {
                    try
                    {
                        KeyLayout k = KeyLayout.Parse(File.ReadAllText(f));
                        if (k.Name == s.LayoutName) return k;
                    }
                    catch { /* 다른 파일 */ }
                }
                return null;
            }

            // ── 자리연습 화면 타일 ──
            foreach (IStageSet set in StageSets.All)
            {
                KeyLayout kl = LayoutFor(set);
                if (kl == null) { sb.AppendLine($"{set.LayoutName}: 자판 파일 없음"); continue; }
                OpenTyping.MainWindow.SetCurrentKeyLayoutForTest(kl);
                var menu = new KeyPracticeMenu();
                var host = new Window
                {
                    Content = menu, Width = 900, Height = 520, ShowActivated = false,
                    WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000,
                };
                host.Show();
                host.UpdateLayout();
                var rows = menu.StageTilePanel.Children.OfType<System.Windows.Controls.StackPanel>().ToList();
                sb.AppendLine($"{set.LayoutName}: 타일 행 {rows.Count}개, 행별 타일 수 = " +
                              string.Join("/", rows.Select(r => r.Children.Count)) +
                              $", 오락 드롭다운 항목 = " + string.Join(" | ",
                                  menu.GameStageCombo.Items.OfType<System.Windows.Controls.ComboBoxItem>()
                                      .Skip(1).Select(i => i.Content)));
                SavePng(menu, Path.Combine(dir, "tiles_" + set.Key + ".png"));
                host.Close();
            }

            // ── 영문 단계 창 ──
            OpenTyping.MainWindow.SetCurrentKeyLayoutForTest(LayoutFor(StageSets.QwertyEnglish));
            KeyPracticeWindow.CapsLockOverrideForTest = false;
            var win = new KeyPracticeWindow(StageSets.QwertyEnglish, StageSets.QwertyEnglish.Stages[0])
            {
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000,
            };
            win.Show();

            void Press(System.Windows.Input.Key key)
            {
                var args = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
                    PresentationSource.FromVisual(win), 0, key) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
                win.RaiseEvent(args);
            }
            System.Windows.Input.Key KeyFor(KeyPos pos)
            {
                foreach (System.Windows.Input.Key k in Enum.GetValues(typeof(System.Windows.Input.Key)))
                {
                    KeyPos p = null;
                    try { p = KeyPos.FromKeyCode(k); } catch { }
                    if (p != null && p.Row == pos.Row && p.Column == pos.Column) return k;
                }
                return System.Windows.Input.Key.None;
            }
            string Notice() => win.NoticeText.Visibility == Visibility.Visible ? "\"" + win.NoticeText.Text + "\"" : "(없음)";

            bool ok = true;
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            int phase = 0;
            timer.Tick += (s, e) =>
            {
                try
                {
                    if (phase == 0)
                    {
                        sb.AppendLine($"영문 1단계 안내: \"{win.IndexGuideText.Text}\" 제시어=\"{win.CurrentKey?.KeyData}\"");
                        ok &= win.IndexGuideText.Text == (StageSets.QwertyEnglish.IndexGuide ?? win.IndexGuideText.Text);
                        // Caps Lock 켬 → 제시된 글자를 맞게 눌러도 오답
                        KeyPracticeWindow.CapsLockOverrideForTest = true;
                        timer.Interval = TimeSpan.FromSeconds(0.6);   // 감시 타이머(150ms)가 알아차릴 시간
                        phase = 1;
                        return;
                    }
                    if (phase == 1)
                    {
                        string warn = Notice();
                        bool blink = win.CapsLockBlinkingForTest;
                        int wrongBefore = win.IncorrectCount;
                        Press(KeyFor(win.ExpectedPos));
                        bool countedWrong = win.IncorrectCount == wrongBefore + 1;
                        sb.AppendLine($"Caps Lock 켬: 문구={warn} 깜빡임={blink} 맞는 자리 눌러도 오답={countedWrong} 문구(누른 뒤)={Notice()}");
                        ok &= warn == "\"" + KeyPracticeWindow.CapsLockWarning + "\"" && blink && countedWrong
                              && Notice() == "\"" + KeyPracticeWindow.CapsLockWarning + "\"";
                        SavePng(win, Path.Combine(dir, "english_capslock.png"));
                        KeyPracticeWindow.CapsLockOverrideForTest = false;
                        phase = 2;
                        return;
                    }
                    if (phase == 2)
                    {
                        string after = Notice();
                        bool blink = win.CapsLockBlinkingForTest;
                        int correctBefore = win.CorrectCount;
                        Press(KeyFor(win.ExpectedPos));
                        bool countedRight = win.CorrectCount == correctBefore + 1;
                        sb.AppendLine($"Caps Lock 끔: 문구={after} 깜빡임={blink} 맞는 자리=정답={countedRight}");
                        ok &= after == "(없음)" && !blink && countedRight;
                        SavePng(win, Path.Combine(dir, "english_stage1.png"));
                        timer.Stop();
                        sb.AppendLine("ENGLISH-CAPS: " + (ok ? "PASS" : "FAIL"));
                        KeyPracticeWindow.CapsLockOverrideForTest = null;
                        OpenTyping.MainWindow.SetCurrentKeyLayoutForTest(original);
                        File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
                        Environment.Exit(0);
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine("ERR " + ex);
                    File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
                    Environment.Exit(0);
                }
            };
            timer.Start();
        }

        private static Point CenterOf(Window w) =>
            new Point(w.Left + w.ActualWidth / 2, w.Top + w.ActualHeight / 2);

        private static string Fmt(Point p) => $"({p.X:F1},{p.Y:F1})";

        /// <summary><260811_31> 키별 포즈를 실제 SetPose 경로로 렌더링해 png 로 남긴다.</summary>
        private static void RunPoseDump(int row, int col, bool isShift, string outPngPath)
        {
            var layer = new FingerLayer();
            layer.SetPose(new KeyPos(row, col), isShift);
            layer.Measure(new Size(777, 260));
            layer.Arrange(new Rect(0, 0, 777, 260));
            layer.UpdateLayout();

            var rtb = new RenderTargetBitmap(777, 260, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(layer);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            Directory.CreateDirectory(Path.GetDirectoryName(outPngPath));
            using (FileStream fs = File.Create(outPngPath)) enc.Save(fs);
        }

        private static void RunHomeDump(string outPngPath)
        {
            var layer = new FingerLayer();
            layer.Measure(new Size(777, 260));
            layer.Arrange(new Rect(0, 0, 777, 260));
            layer.UpdateLayout();

            var rtb = new RenderTargetBitmap(777, 260, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(layer);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            Directory.CreateDirectory(Path.GetDirectoryName(outPngPath));
            using (FileStream fs = File.Create(outPngPath)) enc.Save(fs);
        }

        private static SKTypeface LoadChartTypeface()
        {
            try
            {
                var resource = Application.GetResourceStream(
                    new Uri("pack://application:,,,/Resources/Fonts/NanumGothic.ttf"));
                if (resource is null) return null;
                // SKTypeface.FromStream 은 스트림을 즉시 읽어 들일 뿐 소유권을 가져가지 않으므로,
                // 다 쓴 뒤 직접 닫아야 한다(안 그러면 앱 시작마다 핸들이 하나씩 샌다).
                using (resource.Stream) return SKTypeface.FromStream(resource.Stream);
            }
            catch (Exception)
            {
                // 폰트 로드 실패가 앱 시작을 막으면 안 된다.
                // null이면 LiveCharts가 기본 폰트 매칭으로 동작한다 (한글 폰트가 있는 시스템에서는 정상 표시).
                return null;
            }
        }
    }
}
