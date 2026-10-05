using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using MahApps.Metro.Controls;
// OpenTyping 네임스페이스에 키보드 레이아웃용 'Key' 클래스가 따로 있어, WPF 입력키 열거형은 별칭으로 구분한다.
using WinKey = System.Windows.Input.Key;

namespace OpenTyping
{
    /// <summary>
    /// 산성비 타자 오락을 OpenTypingPlus에 통합한 창 (<260723_2>). 원래 독립 프로그램(AcidRain)에서
    /// 옮겨 왔다. 단어 목록은 제시어 목록(WordCatalog, wordslist\words.json), 오락 단계 해금은 StageRecords(자리연습 목표 타수 통과)로 일원화한다.
    /// 자리연습 화면의 '오락' 드롭다운에서 특정 단계로 바로 진입할 수 있다.
    /// <2600919_5>: 다른 창들과 같은 mah:MetroWindow로 바꿔 파란 테두리·제목표시줄 디자인을 통일했다.
    /// </summary>
    public partial class AcidRainWindow : MetroWindow
    {
        // ── 진행도(단계 잠금 해제 + 단계별 최고 기록) ──
        private class BestRecord
        {
            [JsonPropertyName("score")] public int Score { get; set; }
            [JsonPropertyName("when")] public string When { get; set; } // "yyyy-MM-dd HH:mm"
        }
        private class ProgressData
        {
            // 오락 해금은 StageRecords로 옮겼으므로 여기서는 단계별 최고 기록만 저장한다 (<260723_2>).
            [JsonPropertyName("bestRecords")] public Dictionary<int, BestRecord> BestRecords { get; set; }
        }

        // ── 낙하 단어 ──
        private sealed class FallingWord
        {
            public TextBlock Block;
            public string Text;
            public double X;
            public double Y;
            public double Width;  // 생성 시 측정한 크기(ActualWidth/Height가 첫 프레임에 0일 수 있어 이 값을 쓴다)
            public double Height;
            public double Speed;  // px/s

            public bool IsSpecial;      // 파란 특수 단어 여부
            public int EventType;       // 특수 단어에 배정된 이벤트(0~8)
            public int Phase;           // 0 낙하, 1 중간점까지 반등 상승, 2 하늘로 퇴장, 3 동물을 따라감
            public double PhaseTargetY; // Phase 1의 목표 y
            public double VelX;         // 대각선 낙하의 가로 속도(px/s), 0이면 수직 낙하
            public double FallFactor = 1; // 세로 속도 배수. 대각선으로 꺾이면 1보다 작아져 더 늦게 닿는다
        }

        // ── 특수 이벤트로 지나가는 동물(새·메뚜기) ──
        private sealed class Critter
        {
            public Canvas Sprite;
            public bool IsBird;
            public double X, Y;    // 궤적 기준점(흔들림 제외)
            public double VX, VY;  // px/s
            public double Wave;    // 날갯짓·뜀뛰기 흔들림용 누적 시간
            public double DrawY;   // 흔들림을 반영한 실제 표시 y
            public readonly List<FallingWord> Caught = new List<FallingWord>(); // 데려가는 단어들
            public bool CatchAll;  // true 면 지나가며 만나는 단어를 모두 데려간다 (<산성비 타자 오락 260812_22>)
            public double Scale = 1; // 1보다 크면 '조금 더 큰' 새·메뚜기
            public RotateTransform Wing; // 새의 날개(퍼덕임). 메뚜기는 null
        }

        private static readonly Brush WordNormalBrush = new SolidColorBrush(Color.FromRgb(0x21, 0x25, 0x29));
        private static readonly Brush WordWarnBrush = new SolidColorBrush(Color.FromRgb(0xf7, 0x67, 0x07));
        private static readonly Brush WordDangerBrush = new SolidColorBrush(Color.FromRgb(0xfa, 0x52, 0x52));
        private static readonly Brush WordSpecialBrush = new SolidColorBrush(Color.FromRgb(0x1c, 0x7e, 0xd6));
        private static readonly Brush GroundNormalBrush = new SolidColorBrush(Color.FromRgb(0x69, 0xdb, 0x7c));
        private static readonly Brush GroundHitBrush = new SolidColorBrush(Color.FromRgb(0xff, 0x87, 0x87));

        // 하늘색: 생명이 줄수록 기본색(맑은 하늘)에서 산성색(주황)으로 점점 번진다.
        private static readonly Color SkyTopBase = Color.FromRgb(0xd0, 0xeb, 0xff);
        private static readonly Color SkyBottomBase = Color.FromRgb(0xf8, 0xfb, 0xff);
        private static readonly Color SkyTopAcid = Color.FromRgb(0xf7, 0x67, 0x07);
        private static readonly Color SkyBottomAcid = Color.FromRgb(0xff, 0xe8, 0xcc);

        private const int StartLives = 10;          // 땅에 산성비가 10개 닿으면 게임 종료
        private const double BaseFallSpeed = 34;    // px/s (레벨 1)
        private const double BaseSpawnInterval = 2.4; // s (레벨 1)
        private const int CatchesPerLevel = 10;

        // 특수(파란) 단어 출현 간격: 1단계에서는 매우 드물다가 단계가 오를수록 잦아진다.
        //
        // 예전에는 '경과 시간'으로 셌는데(1단계 360초·마지막 120초), 한 판이 그만큼 이어지는 일이
        // 거의 없어 파란 단어를 볼 수 없었다. 그래서 **처치한 단어 수**로 센다 — 판이 짧아도
        // 일정 개수를 잡으면 반드시 나온다.
        // 출현 간격(처치 개수 최소~최대)은 <산성비 타자 오락 260812_13>(1)에서 정한 값을 그대로 쓰되, <260927_4>로
        // 단계 구성이 자판마다 달라졌으므로 단계 id 가 아니라 '그 자판 오락 단계 중 몇 번째인가'로
        // 고른다. 마지막 단계('연습한 키 전체')는 늘 가장 잦은 마지막 값을 쓴다.
        internal static readonly (int Min, int Max)[] SpecialCatchRanges =
        {
            (24, 36), (19, 34), (17, 31), (15, 27), (13, 24), (11, 21), (9, 19), (7, 16), (5, 13),
        };

        /// <summary>
        /// 파란 글씨 산성비의 효과 가짓수(0 ~ EffectCount-1). <산성비 타자 오락 260812_22>로 9 → 15가 되었다.
        /// 치트 창의 목록(AcidCheatWindow.Effects)과 개수가 같아야 한다.
        /// </summary>
        internal const int EffectCount = 16;

        /// <summary>'지구 환경을 지켜라' 큰 불꽃 효과의 번호 (<산성비 타자 오락 260812_28>).</summary>
        internal const int EarthEffectType = 15;

        // 효과 지속 시간 (<산성비 타자 오락 260812_13>(2), 가림은 <산성비 타자 오락 260812_18>로 3초).
        private const double SpeedEffectDuration = 10;   // s (가속·감속)
        private const double MaskDuration = 3;           // s (■ 가림)
        private const double ShakeDuration = 10;         // s (화면 흔들림)

        private readonly DispatcherTimer frameTimer = new DispatcherTimer();
        private readonly DispatcherTimer groundFlashTimer = new DispatcherTimer();
        private readonly DispatcherTimer capsLockTimer = new DispatcherTimer();   // <261005_4> 영문일 때만 돌린다
        private readonly Stopwatch clock = new Stopwatch();
        private readonly Random random = new Random();
        private readonly List<FallingWord> active = new List<FallingWord>();
        private readonly List<Critter> critters = new List<Critter>();
        private readonly TranslateTransform shakeShift = new TranslateTransform();

        // <산성비 타자 오락 260812_21> 산성비가 땅에 닿을 때의 한 번짜리 상하 흔들림. 특수 이벤트의 '화면 흔들림'과
        // 겹쳐도 서로 방해하지 않도록 변환을 따로 두고 묶어서 건다(그쪽은 매 프레임 값을 직접 넣고,
        // 이쪽은 애니메이션이라 같은 속성을 나눠 쓰면 충돌한다).
        private readonly TranslateTransform groundBump = new TranslateTransform();

        // <260927_4> 이 창이 다루는 자판(한글/영문)과 그 자판의 오락 단계. 단계 순서가 곧 배경 장식·
        // 파란 단어 빈도의 진행 순서다. (오락 해금은 자리연습 목표 타수 통과로 StageRecords에서 관리한다.)
        private readonly IStageSet stageSet;
        private readonly bool isEnglish;
        private List<GameStage> stages = new List<GameStage>();
        private GameWordFeed feed;
        private Dictionary<int, BestRecord> bestRecords = new Dictionary<int, BestRecord>();
        private int currentStageId;
        private readonly int targetStageId; // 드롭다운으로 특정 단계 진입 시 그 단계 id(0이면 시작 화면)
        private double lastSeconds;
        private double spawnCooldown;
        private bool running;
        private bool paused;

        private int score;
        private int lives;
        private int level;
        private int caught;
        private int missed;
        private int attempts;
        private int combo;
        private int maxCombo;

        // ── 치트 (<산성비 타자 오락 260812_15>) ──
        // "효범미남"/"gyqjaalska"/"GYQJAALSKA"를 치면 치트 창이 뜬다. 자리연습 치트와 같은 감지기.
        private readonly CheatCodeDetector cheatDetector = new CheatCodeDetector();

        /// <summary>켜면 떨어지는 산성비의 50%가 파란 글씨가 된다.</summary>
        internal bool CheatHalfSpecial { get; set; }

        /// <summary>치트로 켤 때 나올 효과 번호들(비어 있으면 전부).</summary>
        internal HashSet<int> CheatEffects { get; } = new HashSet<int>();

        private readonly List<int> cheatEffectBag = new List<int>();   // 고루 나오게 하는 주머니

        // 특수 이벤트 상태
        private int specialCatchesLeft;   // 다음 특수 단어까지 더 처치해야 할 단어 수
        private double speedEffectTimer;  // 남은 가속/감속 시간, 0 이하면 효과 없음
        private double speedEffectMul = 1.0;
        private double maskTimer;         // 남은 단어 가림 시간
        private double shakeTimer;        // 남은 화면 흔들림 시간

        // ── '지구 환경을 지켜라' 큰 불꽃 (<산성비 타자 오락 260812_28>) ──
        private const double EarthDuration = 2.4;      // s 불꽃이 퍼지는 시간
        private const int EarthStreams = 5;            // 줄기 수
        private const double EarthSpin = 2.2;          // rad/s 원운동
        private const double EarthSpread = 165;        // px/s 사방으로 퍼지는 속도
        private const double EarthBurnRadius = 16;     // px 이 안에 든 산성비는 사라진다
        private const int EarthScoreGapMin = 250;      // 점수 간격(최소)
        private const int EarthScoreGapMax = 350;      // 점수 간격(최대)
        private const int EarthFromLevel = 9;          // 이 레벨부터 나온다

        // <산성비 타자 오락 260812_28.1> 불꽃 그림 자체를 스프라이트시트 애니메이션으로 교체. 그림은
        // Resources\earth_fire_spritesheet.png(8열 격자, 100px 칸)에 있는 61장이다.
        // <산성비 타자 오락 260812_28.1.1.1> 61장을 한 장씩 건너뛰어(0, 2, 4, …, 60번째 31장) 드문드문 바꾼다 —
        // 이웃한 그림끼리는 차이가 작아 차례로 다 쓰면 불꽃 자체의 움직임이 잘 느껴지지 않았다.
        // 또 줄기마다 매 틱 그림을 남기므로, 그림 크기·개수를 작게 잡아 겹쳐 그리는 양을 줄였다
        // (예전엔 끝 무렵 300px 그림이 약 250장 겹쳐 렌더링이 버벅였다).
        private const int EarthFrameCols = 8;
        private const int EarthFrameSize = 100;        // px, 스프라이트시트 한 칸의 가로·세로
        private const int EarthFrameCount = 61;         // 실제 그림이 있는 칸 수
        private const int EarthFrameStep = 2;           // 몇 장마다 하나씩 쓰는가(2 = 한 장씩 건너뜀)
        private const double EarthImageMinSize = 16;   // px, 시작(progress=0) 그림 지름
        private const double EarthImageMaxSize = 90;   // px, 끝(progress=1) 그림 지름

        private static IReadOnlyList<CroppedBitmap> earthFireFrames;
        private int earthFireFrameIndex;
        private double earthFireLastSize;   // 검사용: 이번 틱에 남긴 불꽃 그림의 크기(px)

        private static IReadOnlyList<CroppedBitmap> EarthFireFrames()
        {
            if (earthFireFrames != null) return earthFireFrames;
            var sheet = new BitmapImage();
            sheet.BeginInit();
            sheet.UriSource = new Uri("pack://application:,,,/Resources/earth_fire_spritesheet.png");
            sheet.CacheOption = BitmapCacheOption.OnLoad;
            sheet.EndInit();
            sheet.Freeze();

            var list = new List<CroppedBitmap>(EarthFrameCount);
            for (int i = 0; i < EarthFrameCount; i++)
            {
                int col = i % EarthFrameCols, row = i / EarthFrameCols;
                var crop = new CroppedBitmap(sheet, new Int32Rect(col * EarthFrameSize, row * EarthFrameSize,
                                                                  EarthFrameSize, EarthFrameSize));
                crop.Freeze();
                list.Add(crop);
            }
            earthFireFrames = list;
            return earthFireFrames;
        }

        /// <summary>검사용: 지금 재생 중인 불꽃 그림의 가로 크기(px). 없으면 0.</summary>
        internal double EarthFireImageSizeForTest => earthFireLastSize;

        /// <summary>검사용: 지금 재생 중인 불꽃 그림의 프레임 번호(0부터).</summary>
        internal int EarthFireFrameIndexForTest => earthFireFrameIndex;

        private double earthTimer;                     // 남은 불꽃 시간(0 이하면 꺼짐)
        private double earthCenterX, earthCenterY;
        private readonly double[] earthAngles = new double[EarthStreams];
        private double earthRadius;
        private int earthNextScore = int.MaxValue;      // 이 점수를 넘으면 다시 터진다

        /// <summary>시작 화면(단계 선택 오버레이)부터 여는 기본 생성자. 지금 '설정'의 자판을 쓴다.</summary>
        public AcidRainWindow() : this(StageSets.Current, 0) { }

        /// <summary>자리연습 화면 '오락' 드롭다운에서 특정 자판·단계(게임 단계 id)로 바로 진입한다.</summary>
        public AcidRainWindow(IStageSet stageSet, int targetStageId)
        {
            this.stageSet = stageSet ?? StageSets.Current;
            // 한글을 칠 수 있는 자판이면 한글 입력기를 켜고, 아니면(영문 자판) 끈다.
            isEnglish = !this.stageSet.Keyboard.CanTypeHangul;
            this.targetStageId = targetStageId;
            InitializeComponent();
            // 특수 이벤트 '화면 흔들림'(shakeShift) + 땅에 닿을 때의 한 번 흔들림(groundBump)
            GameArea.RenderTransform = new TransformGroup { Children = { shakeShift, groundBump } };

            frameTimer.Interval = TimeSpan.FromMilliseconds(33);
            frameTimer.Tick += FrameTimer_Tick;

            groundFlashTimer.Interval = TimeSpan.FromMilliseconds(250);
            groundFlashTimer.Tick += (s, e) =>
            {
                groundFlashTimer.Stop();
                GroundStrip.Background = GroundNormalBrush;
            };

            // <261005_4> 영문 산성비의 Caps Lock 경고: 창 밖에서 Caps Lock을 바꾸고 돌아오는 경우도 놓치지 않도록 짧게 살핀다.
            capsLockTimer.Interval = TimeSpan.FromMilliseconds(150);
            capsLockTimer.Tick += (s, e) => RefreshCapsLockWarning();

            PreviewKeyDown += Window_PreviewKeyDown;
            Loaded += AcidRainWindow_Loaded;
            // 게임 도중 창을 닫아도 33ms 루프가 계속 돌지 않도록 타이머를 멈춘다(누수·예외 방지).
            // QuitToStart()/GameOver()와 같은 이유로, 창을 어떻게 닫든(제목표시줄 X, Alt+F4 포함)
            // 그때까지의 점수가 최고 기록이면 남기고 닫는다.
            Closed += (s, e) => { TryRecordScore(); running = false; frameTimer.Stop(); groundFlashTimer.Stop(); capsLockTimer.Stop(); };

            // 게임 중에는 입력창이 포커스를 잃지 않게 유지한다(화면 클릭 등으로 타이핑이 무시되는 것 방지).
            InputBox.LostFocus += (s, e) =>
            {
                if (running && !paused) InputBox.Dispatcher.BeginInvoke(new Action(() => InputBox.Focus()));
            };
        }

        private void AcidRainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // <261005_4> 영문이면 Caps Lock 경고를 살피기 시작한다(아래 LoadStages 가 경고창에서 멈춰 있어도 먼저 시작).
            if (isEnglish)
            {
                capsLockTimer.Start();
                RefreshCapsLockWarning();
            }

            LoadStages();
            LoadProgress();
            StageCombo.DisplayMemberPath = "Name";
            RefreshStageCombo();

            // 드롭다운에서 특정 단계로 진입했으면 그 단계를 골라 바로 시작한다.
            if (targetStageId != 0)
            {
                GameStage target = (StageCombo.ItemsSource as IEnumerable<GameStage>)?
                    .FirstOrDefault(s => s.Id == targetStageId);
                if (target != null)
                {
                    StageCombo.SelectedItem = target;
                    StartButton_Click(this, new RoutedEventArgs());
                }
            }
        }

        // ── 진행도(잠금 해제 + 최고 기록) 저장/불러오기 ──

        // 최고 기록은 OpenTypingPlus 쪽(LocalAppData)에 저장한다(산성비 자체 progress.json 대체, <260723_2>).
        // <260927_3>(0.5)(0.5.1) 자판마다 따로(game_records_ko.json / game_records_en.json). 옛 단계 체계의
        // game_records.json 은 읽지 않으므로 새 체계를 처음 실행할 때 오락 기록이 한 번 초기화된다.
        internal static string ProgressPathFor(IStageSet set) => System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OTP", "OpenTypingPlus", "game_records_" + (set ?? StageSets.Current).Key + ".json");

        private string ProgressPath => ProgressPathFor(stageSet);

        /// <summary>저장된 최고 기록을 읽어(점수 0 이하·손상 항목은 거르고) 돌려준다.
        /// LoadProgress()와 정적 LoadBestRecords()가 같은 파일을 같은 규칙으로 읽던 것을 모았다.</summary>
        private static Dictionary<int, BestRecord> ReadStoredBestRecords(string path)
        {
            var result = new Dictionary<int, BestRecord>();
            try
            {
                if (File.Exists(path))
                {
                    ProgressData data = JsonSerializer.Deserialize<ProgressData>(File.ReadAllText(path));
                    if (data?.BestRecords != null)
                        foreach (KeyValuePair<int, BestRecord> kv in data.BestRecords)
                            if (kv.Value != null && kv.Value.Score > 0) result[kv.Key] = kv.Value;
                }
            }
            catch
            {
                // 저장 파일이 손상돼도 기본값으로 계속 진행한다.
            }
            return result;
        }

        private void LoadProgress()
        {
            bestRecords = ReadStoredBestRecords(ProgressPath);
        }

        private void SaveProgress()
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ProgressPath));

                // StageRecords.Save()와 같은 이유: 두 인스턴스(또는 창)가 동시에 저장하면 나중에
                // 쓰는 쪽이 먼저 저장된 다른 단계의 최고 기록을 지울 수 있다. 저장 직전 디스크의
                // 최신 기록과 병합한다(더 높은 점수만 반영 — TryRecordScore()와 같은 정책).
                // 디스크의 기록을 일시적으로 못 읽었으면(다른 프로그램이 잡고 있는 경우 등) 덮어쓰지 않는다 —
                // 안 그러면 읽지 못한 다른 단계의 최고 기록이 지워진다. 다음 저장 때 다시 시도한다.
                if (!MergeBestRecordsFromDisk()) return;

                var data = new ProgressData { BestRecords = bestRecords };
                AtomicFile.WriteText(ProgressPath, JsonSerializer.Serialize(data));
            }
            catch
            {
                // 저장에 실패해도(권한 등) 이번 세션 진행에는 지장 없다.
            }
        }

        /// <summary>저장 직전 디스크의 기록을 메모리에 병합한다. 파일이 일시적으로 읽히지 않아 병합하지 못했으면
        /// false(저장하면 읽지 못한 기록을 지우므로 호출부가 저장을 건너뛴다). 파일이 없거나 손상돼 병합할 것이
        /// 없으면 true — 손상 파일은 덮어쓰기 전에 .bad 사본을 남긴다.</summary>
        private bool MergeBestRecordsFromDisk()
        {
            if (!File.Exists(ProgressPath)) return true;
            try
            {
                ProgressData onDisk = JsonSerializer.Deserialize<ProgressData>(File.ReadAllText(ProgressPath));
                if (onDisk?.BestRecords == null) return true;
                foreach (KeyValuePair<int, BestRecord> kv in onDisk.BestRecords)
                {
                    if (kv.Value == null) continue;
                    if (!bestRecords.TryGetValue(kv.Key, out BestRecord mine) || kv.Value.Score > mine.Score)
                        bestRecords[kv.Key] = kv.Value;
                }
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }
            catch
            {
                AtomicFile.BackUpCorrupt(ProgressPath);   // 손상: 메모리 값 그대로 저장하되 사본을 남긴다
                return true;
            }
        }

        // 이번 판 점수가 이 단계의 최고 기록이면 갱신(단계마다 기록은 하나만 유지). 갱신했으면 true.
        // <2600919_3-1>: 치트 중엔 StageRecords와 같은 원칙으로 메모리(bestRecords)에만 반영하고
        // 디스크(game_records_<자판 키>.json)에는 저장하지 않는다 — 이 오락 단계 자체가 치트로 해금된
        // 것일 수 있어서, 치트를 끄면(자연히 이 창도 닫혀 있을 것이다) 저장 안 된 이 변경은 그냥
        // 사라지고 다음에 창을 열 때 디스크의 진짜 기록을 다시 읽는다.
        private bool TryRecordScore()
        {
            if (score <= 0) return false;
            if (bestRecords.TryGetValue(currentStageId, out BestRecord old) && score <= old.Score) return false;

            bestRecords[currentStageId] = new BestRecord
            {
                Score = score,
                When = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            };
            if (!StageRecords.CheatOn) SaveProgress();
            return true;
        }

        /// <summary>
        /// <산성비 타자 오락 260812_20> 최고 기록 한 줄. "최고 기록 1234점. 2026년 8월 12일 오후 04:20" 꼴.
        /// 게임 화면과 자리연습 화면이 같은 문구를 쓰도록 여기 한 곳에 둔다.
        /// </summary>
        internal static string FormatBestRecord(int score, string when) =>
            "최고 기록 " + score + "점. " + FormatWhen(when);

        /// <summary>
        /// 저장된 단계별 최고 기록을 (단계 id, 점수, 시각) 목록으로 읽는다 (<산성비 타자 오락 260812_20>(2)).
        /// 게임 창을 열지 않고도 볼 수 있도록 static 으로 둔다.
        /// </summary>
        internal static List<(int StageId, int Score, string When)> LoadBestRecords(IStageSet set)
        {
            List<(int StageId, int Score, string When)> list = ReadStoredBestRecords(ProgressPathFor(set))
                .Select(kv => (kv.Key, kv.Value.Score, kv.Value.When))
                .ToList();
            list.Sort((a, b) => a.StageId.CompareTo(b.StageId));
            return list;
        }

        // 저장된 "yyyy-MM-dd HH:mm"을 "2026년 7월 19일 오후 09:00" 꼴로 바꾼다.
        private static string FormatWhen(string stored)
        {
            if (DateTime.TryParseExact(stored, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture,
                                       DateTimeStyles.None, out DateTime dt))
            {
                return dt.ToString("yyyy년 M월 d일 tt hh:mm", new CultureInfo("ko-KR"));
            }
            return stored;
        }

        private void UpdateBestRecordText(int stageId)
        {
            if (bestRecords.TryGetValue(stageId, out BestRecord r))
            {
                BestRecordText.Text = FormatBestRecord(r.Score, r.When);   // <산성비 타자 오락 260812_20>(1)
            }
            else
            {
                BestRecordText.Text = "최고 기록이 아직 없습니다";
            }
        }

        private void RefreshStageCombo()
        {
            // 해금 여부는 자리연습 단계별 최고 타 기록(StageRecords, 목표 타수 통과)을 따른다 (<260724_2>(1)).
            List<GameStage> available = stages.Where(s => StageRecords.IsGameStageUnlocked(s.Id)).ToList();
            // 아무 단계도 해금 안 됐으면(예: 기록 없음) 목록상 첫 단계는 보여 준다(방어적 대비).
            if (available.Count == 0) available = stages.Take(1).ToList();

            // '다시 하기'·'처음으로' 뒤에는 방금 고른(친) 단계를 그대로 선택해 둔다. 처음 열 때(고른 단계 없음)만
            // 가장 최근에 해금된 단계를 기본 선택한다.
            int keepId = (StageCombo.SelectedItem as GameStage)?.Id ?? -1;
            StageCombo.ItemsSource = available;
            int keepIndex = available.FindIndex(s => s.Id == keepId);
            StageCombo.SelectedIndex = keepIndex >= 0 ? keepIndex : available.Count - 1;

            // 같은 항목이 다시 선택되면 SelectionChanged가 안 오므로 여기서도 직접 갱신한다.
            if (StageCombo.SelectedItem is GameStage sel)
            {
                UpdateBestRecordText(sel.Id);
                BuildScenery(sel.Id);
            }
        }

        private void StageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 시작 화면에서 단계를 고르면 그 단계의 최고 기록과 배경(누적 장식) 미리보기를 보여 준다.
            if (StageCombo.SelectedItem is GameStage sel)
            {
                UpdateBestRecordText(sel.Id);
                BuildScenery(sel.Id);
            }
        }

        /// <summary>
        /// <260927_4> 이 자판의 오락 단계를 제시어 목록(<see cref="WordCatalog"/>, wordslist\words.json →
        /// 내장 예비본)에서 만든다. 예비본이 늘 있으므로 보통은 비지 않지만, 그래도 비면 기본 단어로 연다.
        /// </summary>
        private void LoadStages()
        {
            stages = GameStages.For(stageSet).ToList();

            if (stages.Count == 0)
            {
                MessageBox.Show(
                    "단어 목록 파일을 읽을 수 없어 기본 단어 16개로 시작합니다.\n" +
                    "찾아본 위치:\n" + string.Join("\n", WordCatalog.CandidatePaths()),
                    "열린타자+", MessageBoxButton.OK, MessageBoxImage.Warning);

                List<string> basic = isEnglish
                    ? new List<string> { "sea", "sky", "tree", "book", "home", "sun", "moon", "star",
                                         "cat", "dog", "bird", "fish", "cake", "milk", "hand", "ball" }
                    : new List<string> { "나라", "바다", "하늘", "구름", "나무", "사람", "사랑", "우리",
                                         "머리", "언니", "엄마", "아이", "가방", "다리", "거미", "기린" };
                stages = new List<GameStage> { new GameStage(0, "기본 단어(내장)", basic, null) };
            }
        }

        /// <summary>이 단계가 이 자판 오락 단계 중 몇 번째인가(0부터). 없으면 마지막으로 친다.</summary>
        private int StageOrdinal(int stageId)
        {
            int idx = stages.FindIndex(s => s.Id == stageId);
            return idx < 0 ? Math.Max(0, stages.Count - 1) : idx;
        }

        // ── 게임 시작/종료 ──

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // words.json 로드 실패 경고창이 떠 있는 동안(Loaded 진행 중, stages가 아직 빈 상태)
            // 시작 버튼이 눌리면 stages[0]에서 크래시가 나므로 방어한다.
            if (stages.Count == 0) return;

            GameStage stage = StageCombo.SelectedItem as GameStage ?? stages[0];
            feed = new GameWordFeed(stage, random);   // <260927_4>(0.2) 9:1 섞기·5개 단위 중복 금지
            currentStageId = stage.Id;
            StageNameText.Text = stage.Name;

            score = 0; lives = StartLives; level = 1;
            caught = 0; missed = 0; attempts = 0;
            combo = 0; maxCombo = 0;
            spawnCooldown = 0.6; // 첫 단어는 살짝 뜸 들였다 시작
            ClearWords();
            ResetSky();
            ResetEffects();
            BuildScenery(stage.Id);
            UpdateHud();

            StartOverlay.Visibility = Visibility.Collapsed;
            GameOverOverlay.Visibility = Visibility.Collapsed;
            PauseOverlay.Visibility = Visibility.Collapsed;
            paused = false;
            running = true;

            clock.Restart();
            lastSeconds = 0;
            frameTimer.Start();
            InputBox.Clear();
            InputBox.Focus();
            SwitchInputLanguage();   // <산성비 타자 오락 260812_16>(1) 시작하자마자 그 자판 글자로 칠 수 있게
        }

        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            ClearWords(); // 종료 화면 뒤에 비쳐 보이던 이전 게임 단어를 지운다
            GameOverOverlay.Visibility = Visibility.Collapsed;
            RefreshStageCombo(); // 갱신된 기록을 목록에 반영(고른 단계는 그대로)
            StartOverlay.Visibility = Visibility.Visible; // 단계 재선택 가능
        }

        private void QuitButton_Click(object sender, RoutedEventArgs e)
        {
            QuitToStart();
        }

        // 게임 종료 화면의 '나가기': 이 오락 창 자체를 닫는다(연 곳 — 자리연습 화면 등 — 으로 돌아간다).
        private void GameOverQuitButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // 게임 진행 중 첫 화면으로 되돌아간다(포기하고 나가기). 그때까지의 점수도 기록 대상이다.
        private void QuitToStart()
        {
            TryRecordScore();

            running = false;
            paused = false;
            frameTimer.Stop();
            ClearWords();
            ResetSky();
            ResetEffects();

            PauseOverlay.Visibility = Visibility.Collapsed;
            GameOverOverlay.Visibility = Visibility.Collapsed;
            RefreshStageCombo();
            StartOverlay.Visibility = Visibility.Visible;
        }

        private void GameOver()
        {
            running = false;
            frameTimer.Stop();
            shakeShift.X = 0; shakeShift.Y = 0;

            bool newRecord = TryRecordScore();
            double accuracy = attempts > 0 ? (double)caught / attempts * 100.0 : 0;
            ResultText.Text =
                "점수  " + score + "\n" +
                "잡은 단어  " + caught + "개 · 놓친 단어  " + missed + "개\n" +
                "최대 연속 성공  x" + maxCombo + "\n" +
                "입력 정확도  " + accuracy.ToString("0.#", CultureInfo.InvariantCulture) + "%  (입력 " + attempts + "회)";
            NewRecordText.Visibility = newRecord ? Visibility.Visible : Visibility.Collapsed;
            GameOverOverlay.Visibility = Visibility.Visible;
        }

        private void ClearWords()
        {
            foreach (FallingWord w in active)
            {
                GameCanvas.Children.Remove(w.Block);
            }
            active.Clear();

            foreach (Critter c in critters)
            {
                GameCanvas.Children.Remove(c.Sprite);
            }
            critters.Clear();
        }

        // 특수 이벤트 효과를 모두 초기 상태로 되돌린다.
        private void ResetEffects()
        {
            speedEffectTimer = 0; speedEffectMul = 1.0;
            maskTimer = 0;
            shakeTimer = 0; shakeShift.X = 0; shakeShift.Y = 0;
            specialCatchesLeft = SpecialCatchGap();
            EventBanner.BeginAnimation(UIElement.OpacityProperty, null);
            EventBanner.Opacity = 0;

            // <산성비 타자 오락 260812_28> 큰 불꽃도 함께 정리한다(다음 판은 레벨 9에 닿아야 다시 예약된다).
            earthTimer = 0;
            earthNextScore = int.MaxValue;
            FireworkLayer.Children.Clear();
        }

        // ── 하늘색(산성비 피해 표시) ──

        private static Color LerpColor(Color a, Color b, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            byte L(byte x, byte y) => (byte)(x + (y - x) * t);
            return Color.FromRgb(L(a.R, b.R), L(a.G, b.G), L(a.B, b.B));
        }

        // 남은 생명이 줄수록(맞은 횟수가 늘수록) 하늘이 산성(주황)색으로 점점 물든다.
        private void UpdateSky()
        {
            double t = (double)(StartLives - lives) / StartLives;
            Color top = LerpColor(SkyTopBase, SkyTopAcid, t);
            Color bottom = LerpColor(SkyBottomBase, SkyBottomAcid, t);
            var duration = new Duration(TimeSpan.FromMilliseconds(500));
            SkyTopStop.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(top, duration));
            SkyBottomStop.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(bottom, duration));
        }

        private void ResetSky()
        {
            SkyTopStop.BeginAnimation(GradientStop.ColorProperty, null);
            SkyBottomStop.BeginAnimation(GradientStop.ColorProperty, null);
            SkyTopStop.Color = SkyTopBase;
            SkyBottomStop.Color = SkyBottomBase;
        }

        // ── 땅 위 장식(산·나무·꽃·동물·사람) ──

        // 단계가 오를수록 장식을 하나씩 누적해 추가한다. 1단계는 빈 땅.
        private void BuildScenery(int stageId)
        {
            SceneryLayer.Children.Clear();

            // 이 자판 오락 단계 중 몇 번째인가. 마지막('연습한 키 전체') 단계는 장식을 전부 보인다.
            int idx = StageOrdinal(stageId);
            if (idx >= stages.Count - 1) idx = 8;

            if (idx >= 1)
            {
                AddFlower(230, Color.FromRgb(0xff, 0x87, 0x87));
                AddFlower(280, Color.FromRgb(0xff, 0xd4, 0x3b));
            }
            if (idx >= 2) AddTree(80);
            if (idx >= 3) AddBunny(430);
            if (idx >= 4)
            {
                AddFlower(660, Color.FromRgb(0xe5, 0x99, 0xf7));
                AddFlower(710, Color.FromRgb(0xff, 0x87, 0x87));
            }
            if (idx >= 5) AddMountain(170, 54, 160, Color.FromRgb(0xc3, 0xcd, 0xb0));
            if (idx >= 6) AddPerson(560);
            if (idx >= 7) AddTree(820);
            if (idx >= 8) AddMountain(20, 70, 200, Color.FromRgb(0xad, 0xb8, 0x9e));
        }

        private void AddMountain(double baseX, double height, double width, Color color)
        {
            var tri = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(baseX, 0), new Point(baseX + width / 2, height), new Point(baseX + width, 0),
                },
                Fill = new SolidColorBrush(color),
                Opacity = 0.6,
            };
            Canvas.SetBottom(tri, 0);
            Panel.SetZIndex(tri, -1); // 산은 나중에 추가돼도 항상 다른 장식 뒤에 있다
            SceneryLayer.Children.Add(tri);
        }

        private void AddTree(double x)
        {
            var trunk = new Rectangle { Width = 8, Height = 24, Fill = new SolidColorBrush(Color.FromRgb(0x8a, 0x5a, 0x2c)) };
            Canvas.SetLeft(trunk, x - 4);
            Canvas.SetBottom(trunk, 0);
            SceneryLayer.Children.Add(trunk);

            var canopy1 = new Ellipse { Width = 46, Height = 40, Fill = new SolidColorBrush(Color.FromRgb(0x51, 0xcf, 0x66)) };
            Canvas.SetLeft(canopy1, x - 23);
            Canvas.SetBottom(canopy1, 18);
            SceneryLayer.Children.Add(canopy1);

            var canopy2 = new Ellipse { Width = 30, Height = 26, Fill = new SolidColorBrush(Color.FromRgb(0x37, 0xb2, 0x4d)) };
            Canvas.SetLeft(canopy2, x - 15);
            Canvas.SetBottom(canopy2, 38);
            SceneryLayer.Children.Add(canopy2);
        }

        private void AddFlower(double x, Color petalColor)
        {
            var stem = new Rectangle { Width = 3, Height = 20, Fill = new SolidColorBrush(Color.FromRgb(0x2f, 0x9e, 0x44)) };
            Canvas.SetLeft(stem, x - 1.5);
            Canvas.SetBottom(stem, 0);
            SceneryLayer.Children.Add(stem);

            Point[] petalOffsets = { new Point(-6, 0), new Point(6, 0), new Point(0, -6), new Point(0, 6) };
            foreach (Point o in petalOffsets)
            {
                var petal = new Ellipse { Width = 9, Height = 9, Fill = new SolidColorBrush(petalColor) };
                Canvas.SetLeft(petal, x - 4.5 + o.X);
                Canvas.SetBottom(petal, 20 - 4.5 + o.Y);
                SceneryLayer.Children.Add(petal);
            }

            var center = new Ellipse { Width = 7, Height = 7, Fill = new SolidColorBrush(Color.FromRgb(0xff, 0xd4, 0x3b)) };
            Canvas.SetLeft(center, x - 3.5);
            Canvas.SetBottom(center, 20 - 3.5);
            SceneryLayer.Children.Add(center);
        }

        private void AddBunny(double x)
        {
            var fur = new SolidColorBrush(Color.FromRgb(0xf1, 0xf3, 0xf5));
            var outline = new SolidColorBrush(Color.FromRgb(0xad, 0xb5, 0xbd));

            var body = new Ellipse { Width = 34, Height = 22, Fill = fur, Stroke = outline, StrokeThickness = 1 };
            Canvas.SetLeft(body, x - 17);
            Canvas.SetBottom(body, 0);
            SceneryLayer.Children.Add(body);

            var head = new Ellipse { Width = 18, Height = 16, Fill = fur, Stroke = outline, StrokeThickness = 1 };
            Canvas.SetLeft(head, x + 6);
            Canvas.SetBottom(head, 14);
            SceneryLayer.Children.Add(head);

            var ear1 = new Ellipse { Width = 6, Height = 18, Fill = fur, Stroke = outline, StrokeThickness = 1 };
            Canvas.SetLeft(ear1, x + 8);
            Canvas.SetBottom(ear1, 27);
            SceneryLayer.Children.Add(ear1);

            var ear2 = new Ellipse { Width = 6, Height = 18, Fill = fur, Stroke = outline, StrokeThickness = 1 };
            Canvas.SetLeft(ear2, x + 16);
            Canvas.SetBottom(ear2, 27);
            SceneryLayer.Children.Add(ear2);

            var tail = new Ellipse { Width = 8, Height = 8, Fill = Brushes.White, Stroke = outline, StrokeThickness = 1 };
            Canvas.SetLeft(tail, x - 21);
            Canvas.SetBottom(tail, 6);
            SceneryLayer.Children.Add(tail);
        }

        private void AddPerson(double x)
        {
            var skinBrush = new SolidColorBrush(Color.FromRgb(0xff, 0xd8, 0xa8));
            var pantsBrush = new SolidColorBrush(Color.FromRgb(0x34, 0x3a, 0x40));

            var head = new Ellipse { Width = 16, Height = 16, Fill = skinBrush };
            Canvas.SetLeft(head, x - 8);
            Canvas.SetBottom(head, 40);
            SceneryLayer.Children.Add(head);

            var body = new Rectangle
            {
                Width = 20, Height = 24, RadiusX = 6, RadiusY = 6,
                Fill = new SolidColorBrush(Color.FromRgb(0x1c, 0x7e, 0xd6)),
            };
            Canvas.SetLeft(body, x - 10);
            Canvas.SetBottom(body, 16);
            SceneryLayer.Children.Add(body);

            var leg1 = new Rectangle { Width = 6, Height = 16, Fill = pantsBrush };
            Canvas.SetLeft(leg1, x - 7);
            Canvas.SetBottom(leg1, 0);
            SceneryLayer.Children.Add(leg1);

            var leg2 = new Rectangle { Width = 6, Height = 16, Fill = pantsBrush };
            Canvas.SetLeft(leg2, x + 1);
            Canvas.SetBottom(leg2, 0);
            SceneryLayer.Children.Add(leg2);

            var arm1 = new Rectangle { Width = 5, Height = 16, Fill = skinBrush, RadiusX = 2, RadiusY = 2 };
            Canvas.SetLeft(arm1, x - 15);
            Canvas.SetBottom(arm1, 18);
            SceneryLayer.Children.Add(arm1);

            var arm2 = new Rectangle { Width = 5, Height = 16, Fill = skinBrush, RadiusX = 2, RadiusY = 2 };
            Canvas.SetLeft(arm2, x + 10);
            Canvas.SetBottom(arm2, 18);
            SceneryLayer.Children.Add(arm2);
        }

        // ── 프레임 루프 ──

        private void FrameTimer_Tick(object sender, EventArgs e)
        {
            if (!running || paused) return;

            double now = clock.Elapsed.TotalSeconds;
            double dt = now - lastSeconds;
            lastSeconds = now;
            if (dt <= 0 || dt > 0.5) return; // 창 끌기 등으로 인한 비정상 프레임 보호

            double groundY = GameCanvas.ActualHeight - GroundStrip.Height;
            double canvasW = GameCanvas.ActualWidth;

            UpdateEffectTimers(dt);
            UpdateCritters(dt, canvasW);

            double mul = speedEffectTimer > 0 ? speedEffectMul : 1.0;

            // 이동 + 착지 판정
            for (int i = active.Count - 1; i >= 0; i--)
            {
                FallingWord w = active[i];

                if (w.Phase == 1) // 중간 지점까지 반등 상승(내려오던 속도 그대로)
                {
                    w.Y -= w.Speed * mul * dt;
                    if (w.Y <= w.PhaseTargetY) { w.Y = w.PhaseTargetY; w.Phase = 0; }
                    Canvas.SetTop(w.Block, w.Y);
                    continue;
                }
                if (w.Phase == 2) // 하늘 위로 올라가 사라짐
                {
                    w.Y -= w.Speed * 3 * dt;
                    Canvas.SetTop(w.Block, w.Y);
                    if (w.Y < -w.Height)
                    {
                        active.RemoveAt(i);
                        GameCanvas.Children.Remove(w.Block);
                    }
                    continue;
                }
                if (w.Phase == 3) continue; // 동물을 따라감(이동은 UpdateCritters에서)

                w.Y += w.Speed * w.FallFactor * mul * dt;
                if (w.VelX != 0) // 대각선 낙하
                {
                    w.X += w.VelX * dt;
                    double maxX = Math.Max(4, canvasW - w.Width - 4);
                    if (w.X < 4) { w.X = 4; w.VelX = 0; }
                    else if (w.X > maxX) { w.X = maxX; w.VelX = 0; }
                    Canvas.SetLeft(w.Block, w.X);
                }
                Canvas.SetTop(w.Block, w.Y);

                double limit = groundY - w.Height;
                if (!w.IsSpecial) // 특수 단어는 항상 파란색을 유지해 구별되게 한다
                {
                    double ratio = limit > 0 ? w.Y / limit : 1;
                    w.Block.Foreground = ratio > 0.88 ? WordDangerBrush
                                       : ratio > 0.70 ? WordWarnBrush
                                       : WordNormalBrush;
                }

                if (w.Y >= limit)
                {
                    active.RemoveAt(i);
                    GameCanvas.Children.Remove(w.Block);
                    missed++;
                    lives--;
                    combo = 0; // 땅에 닿으면 연속 성공이 끊긴다
                    GroundStrip.Background = GroundHitBrush;
                    groundFlashTimer.Stop();
                    groundFlashTimer.Start();
                    ShakeOnGroundHit();   // <산성비 타자 오락 260812_21>
                    UpdateHud();
                    UpdateSky();
                    if (lives <= 0)
                    {
                        GameOver();
                        return;
                    }
                }
            }

            // 새 단어 생성
            spawnCooldown -= dt;
            if (spawnCooldown <= 0)
            {
                SpawnWord();
                double interval = Math.Max(0.9, BaseSpawnInterval - 0.15 * (level - 1));
                spawnCooldown = interval * (0.75 + random.NextDouble() * 0.5);
            }
        }

        // ── 특수 이벤트 ──

        /// <summary>
        /// 다음 특수(파란) 단어까지 처치해야 할 단어 수. 단계가 오를수록 적어진다(= 더 자주 나온다).
        /// 기계적으로 반복되지 않도록 ±20% 흔들림을 준다.
        /// </summary>
        private int SpecialCatchGap() => SpecialCatchGap(currentStageId);

        internal int SpecialCatchGap(int stageId)
        {
            (int Min, int Max) r = SpecialCatchRangeOf(stageId);
            return random.Next(r.Min, r.Max + 1);   // 최솟값~최댓값(양 끝 포함)에서 균등하게
        }

        /// <summary>이 단계의 파란 단어 출현 간격. 마지막 단계는 표의 마지막 값(가장 잦음).</summary>
        internal (int Min, int Max) SpecialCatchRangeOf(int stageId)
        {
            if (stages.Count == 0) LoadStages();
            int idx = StageOrdinal(stageId);
            bool last = idx >= stages.Count - 1;
            return last ? SpecialCatchRanges[SpecialCatchRanges.Length - 1]
                        : SpecialCatchRanges[Math.Min(idx, SpecialCatchRanges.Length - 1)];
        }

        private void UpdateEffectTimers(double dt)
        {
            UpdateEarthFirework(dt);   // <산성비 타자 오락 260812_28>

            if (speedEffectTimer > 0) speedEffectTimer -= dt;

            if (maskTimer > 0)
            {
                maskTimer -= dt;
                if (maskTimer <= 0) ApplyMask(false);
            }

            if (shakeTimer > 0)
            {
                shakeTimer -= dt;
                if (shakeTimer <= 0)
                {
                    shakeShift.X = 0;
                    shakeShift.Y = 0;
                }
                else
                {
                    shakeShift.X = (random.NextDouble() * 2 - 1) * 7;
                    shakeShift.Y = (random.NextDouble() * 2 - 1) * 5;
                }
            }
        }

        // 낮은 순(땅에 가까운 순)으로 일반 낙하 중인 단어 n개를 고른다.
        private List<FallingWord> LowestWords(int n)
        {
            return active.Where(w => w.Phase == 0)
                         .OrderByDescending(w => w.Y)
                         .Take(n)
                         .ToList();
        }

        private void TriggerSpecial(int type)
        {
            switch (type)
            {
                case 0:
                    speedEffectMul = 1.6;
                    speedEffectTimer = SpeedEffectDuration;
                    Banner("산성비 가속! (" + (int)SpeedEffectDuration + "초)");
                    break;
                case 1:
                    speedEffectMul = 0.5;
                    speedEffectTimer = SpeedEffectDuration;
                    Banner("산성비 감속! (" + (int)SpeedEffectDuration + "초)");
                    break;
                case 2:
                    maskTimer = MaskDuration;
                    ApplyMask(true);
                    Banner("단어 가림! 외워서 입력하세요 (" + (int)MaskDuration + "초)");
                    break;
                case 3:
                    shakeTimer = ShakeDuration;
                    Banner("화면 흔들림! (" + (int)ShakeDuration + "초)");
                    break;
                case 4:
                    foreach (FallingWord w in LowestWords(2)) w.Phase = 2;
                    Banner("단어 2개가 하늘로 사라진다!");
                    break;
                case 5:
                    foreach (FallingWord w in LowestWords(3))
                    {
                        if (w.Y <= 0) continue; // 아직 화면 위(스폰 직후)면 반등 목표가 아래가 되므로 제외
                        w.Phase = 1;
                        w.PhaseTargetY = w.Y * 0.5; // 그 자리에서 화면 위 끝까지의 가운데 지점
                    }
                    Banner("단어 3개가 튕겨 오른다!");
                    break;
                case 6:
                    // <산성비 타자 오락 260812_13>(2) 속도의 크기는 그대로 두고 방향만 대각선으로 꺾는다. 그래서 세로
                    // 성분이 줄어(cos) 곧장 떨어질 때보다 땅에 닿기까지 시간이 늘어난다.
                    foreach (FallingWord w in LowestWords(4))
                    {
                        double angle = (30 + random.NextDouble() * 15) * Math.PI / 180; // 30~45°
                        w.VelX = (random.Next(2) == 0 ? -1 : 1) * w.Speed * Math.Sin(angle);
                        w.FallFactor = Math.Cos(angle);
                    }
                    Banner("단어 4개가 대각선으로 떨어진다!");
                    break;
                case 7:
                    SpawnCritter(true);
                    Banner("새가 단어 하나를 물어 간다!");
                    break;
                case 8:
                    SpawnCritter(false);
                    Banner("메뚜기가 단어 하나를 데려간다!");
                    break;

                // ── <산성비 타자 오락 260812_22> 큰 개체(만나는 단어를 모두)·양쪽에서 두 마리 ──
                case 9:
                    SpawnCritter(true, big: true, catchAll: true);
                    Banner("큰 새가 만나는 단어를 모두 물어 간다!");
                    break;
                case 10:
                    SpawnCritter(false, big: true, catchAll: true);
                    Banner("큰 메뚜기가 만나는 단어를 모두 데려간다!");
                    break;
                case 11:
                    SpawnCritter(true, fromLeft: true);
                    SpawnCritter(true, fromLeft: false);
                    Banner("새 두 마리가 양쪽에서 온다!");
                    break;
                case 12:
                    SpawnCritter(false, fromLeft: true);
                    SpawnCritter(false, fromLeft: false);
                    Banner("메뚜기 두 마리가 양쪽에서 온다!");
                    break;
                case 13:
                    SpawnCritter(true, fromLeft: true, big: true, catchAll: true);
                    SpawnCritter(true, fromLeft: false, big: true, catchAll: true);
                    Banner("큰 새 두 마리가 양쪽에서 온다!");
                    break;
                case 14:
                    SpawnCritter(false, fromLeft: true, big: true, catchAll: true);
                    SpawnCritter(false, fromLeft: false, big: true, catchAll: true);
                    Banner("큰 메뚜기 두 마리가 양쪽에서 온다!");
                    break;

                case EarthEffectType:   // <산성비 타자 오락 260812_28> 큰 불꽃(문구는 StartEarthFirework 안에서 깜빡인다)
                    StartEarthFirework();
                    break;
            }
        }

        /// <summary>
        /// <산성비 타자 오락 260812_21> 산성비가 땅에 닿을 때 화면을 상하로 한 번 약하게 흔든다.
        /// 내려갔다 올라와 제자리로 돌아오는 짧은 한 번짜리 움직임이다.
        /// </summary>
        private void ShakeOnGroundHit()
        {
            var bump = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
            bump.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            bump.KeyFrames.Add(new EasingDoubleKeyFrame(6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(60)))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
            bump.KeyFrames.Add(new EasingDoubleKeyFrame(-3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(130))));
            bump.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200)))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });

            groundBump.BeginAnimation(TranslateTransform.YProperty, bump);
        }

        // ── 폭죽·불꽃 (<산성비 타자 오락 260812_24-1>, <산성비 타자 오락 260812_28>) ──

        private static readonly Color SparkColor = Color.FromRgb(0xff, 0xd4, 0x3b);   // 노랑
        private static readonly Color SparkColorDeep = Color.FromRgb(0xfa, 0xb0, 0x05);

        /// <summary>
        /// 불꽃 알갱이 하나를 <see cref="FireworkLayer"/>에 놓고, 스스로 사라지게 한다.
        /// 매 프레임 새 알갱이를 남기면 그것들이 곧 궤적으로 보인다 (<산성비 타자 오락 260812_28>).
        /// </summary>
        private void Spark(double x, double y, double size, double life, double vx = 0, double vy = 0, bool deep = false)
        {
            var dot = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(deep ? SparkColorDeep : SparkColor),
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(dot, x - size / 2);
            Canvas.SetTop(dot, y - size / 2);

            var move = new TranslateTransform();
            dot.RenderTransform = move;
            FireworkLayer.Children.Add(dot);

            var dur = new Duration(TimeSpan.FromSeconds(life));
            if (vx != 0 || vy != 0)
            {
                move.BeginAnimation(TranslateTransform.XProperty,
                    new DoubleAnimation(0, vx * life, dur) { FillBehavior = FillBehavior.Stop });
                move.BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(0, vy * life, dur) { FillBehavior = FillBehavior.Stop });
            }

            var fade = new DoubleAnimation(1, 0, dur) { FillBehavior = FillBehavior.Stop };
            fade.Completed += (s, e) => FireworkLayer.Children.Remove(dot);
            dot.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        /// <summary>
        /// <산성비 타자 오락 260812_24-1> 레벨업 문구 뒤 양쪽에서 터지는 노란 폭죽. 문구·폭죽 모두 산성비보다 앞이다.
        /// </summary>
        private void LevelUpFireworks()
        {
            double w = GameCanvas.ActualWidth, h = GameCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            // 문구가 실제로 놓인 자리를 받아 그 좌우에 터뜨린다.
            double bannerY = h * 0.35;
            try
            {
                Point p = EventBanner.TranslatePoint(
                    new Point(EventBanner.ActualWidth / 2, EventBanner.ActualHeight / 2), FireworkLayer);
                if (!double.IsNaN(p.Y) && p.Y > 0 && p.Y < h) bannerY = p.Y;
            }
            catch { /* 배치 전이면 기본값을 쓴다 */ }

            double gap = Math.Min(200, w * 0.28);
            foreach (double cx in new[] { w / 2 - gap, w / 2 + gap })
            {
                for (int i = 0; i < 14; i++)
                {
                    double angle = random.NextDouble() * Math.PI * 2;
                    double speed = 70 + random.NextDouble() * 90;
                    Spark(cx, bannerY,
                          3 + random.NextDouble() * 3,
                          0.7 + random.NextDouble() * 0.5,
                          Math.Cos(angle) * speed,
                          Math.Sin(angle) * speed - 20,     // 살짝 위로 솟았다가
                          deep: i % 3 == 0);
                }
            }
        }

        /// <summary><산성비 타자 오락 260812_28> 화면 가운데에서 큰 불꽃을 터뜨린다.</summary>
        private void StartEarthFirework()
        {
            double w = GameCanvas.ActualWidth, h = GameCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            double groundTop = h - GroundStrip.Height;
            earthCenterX = w / 2;                 // 좌우 가운데
            earthCenterY = groundTop / 2;         // 땅 경계면 ~ 하늘 꼭대기의 가운데
            earthRadius = 6;
            earthTimer = EarthDuration;

            double start = random.NextDouble() * Math.PI * 2;
            for (int i = 0; i < EarthStreams; i++)
                earthAngles[i] = start + i * (Math.PI * 2 / EarthStreams);

            earthFireFrameIndex = 0;
            earthFireLastSize = EarthImageMinSize;
            BannerBlink("지구 환경을 지켜라!!", 2);
        }

        /// <summary>
        /// 큰 불꽃을 한 프레임 진행시킨다 (<산성비 타자 오락 260812_28.1>). 가운데 중심점에서 다섯 줄기가 원운동하며
        /// 사방으로 퍼져 나가고(<산성비 타자 오락 260812_28>과 같은 물리), 줄기마다 지금 이 순간 자리에 스프라이트시트
        /// 불꽃 그림(점점 커짐 + 프레임이 차례로 바뀌어 그 자체가 애니메이션)을 남겨 궤적이 보이게 한다.
        /// 이 그림에 닿은 산성비는 사라진다.
        /// </summary>
        private void UpdateEarthFirework(double dt)
        {
            if (earthTimer <= 0) return;
            earthTimer -= dt;

            double progress = 1 - Math.Max(0, earthTimer) / EarthDuration;   // 0 → 1
            earthRadius += EarthSpread * dt;

            // 불꽃 그림 자체의 애니메이션: 한 장씩 건너뛴 31장(0, 2, …, 60)을 이벤트 동안 차례로 재생한다.
            int shownFrames = (EarthFrameCount - 1) / EarthFrameStep + 1;
            earthFireFrameIndex = Math.Min(shownFrames - 1, (int)(progress * shownFrames)) * EarthFrameStep;
            // 불꽃 오브젝트 자체의 확대: 뒤로 갈수록 큰 그림을 남긴다. 커지는 속도 자체를 높이려고
            // (초반에 빨리 커지고 끝에는 완만해지도록) progress 그대로가 아니라 제곱근을 쓴다 —
            // 예를 들어 진행도 25%(0.6초) 시점에 이미 절반 크기(sqrt(0.25)=0.5)까지 자란다.
            double sizeProgress = Math.Sqrt(progress);
            double size = EarthImageMinSize + sizeProgress * (EarthImageMaxSize - EarthImageMinSize);
            earthFireLastSize = size;

            for (int i = 0; i < EarthStreams; i++)
            {
                earthAngles[i] += EarthSpin * dt;   // 원운동
                double x = earthCenterX + Math.Cos(earthAngles[i]) * earthRadius;   // 사방으로 퍼지는 운동
                double y = earthCenterY + Math.Sin(earthAngles[i]) * earthRadius;

                // 줄기 하나당 틱마다 한 장만 남긴다(렌더링 부하를 줄이려고, 늘리지 않는다).
                double jitter = (random.NextDouble() * 2 - 1) * (2 + progress * 5);
                EarthSpark(x + jitter, y + jitter, size, 0.55);

                BurnWordsNear(x, y);
            }

            if (earthTimer <= 0) earthTimer = 0;
        }

        /// <summary>
        /// <산성비 타자 오락 260812_28.1> 다섯 줄기가 지나는 자리에 스프라이트시트 불꽃 그림 한 장을 놓고, 서서히
        /// 옅어지다 스스로 없어지게 한다(<see cref="Spark"/>와 같은 방식이되 그림이 다르다). 여러 장이
        /// 매 틱 새로 남으므로 줄기의 궤적이 눈에 보인다.
        /// </summary>
        private void EarthSpark(double x, double y, double size, double life)
        {
            var img = new Image
            {
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform,
                IsHitTestVisible = false,
                Source = EarthFireFrames()[earthFireFrameIndex],
            };
            Canvas.SetLeft(img, x - size / 2);
            Canvas.SetTop(img, y - size / 2);
            FireworkLayer.Children.Add(img);

            var fade = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(life)))
            { FillBehavior = FillBehavior.Stop };
            fade.Completed += (s, e) => FireworkLayer.Children.Remove(img);
            img.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        /// <summary>불꽃에 닿은 산성비를 없앤다 (<산성비 타자 오락 260812_28>). 점수·생명에는 영향이 없다.</summary>
        private void BurnWordsNear(double x, double y)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                FallingWord w = active[i];
                if (w.Phase == 3) continue;   // 동물이 데려가는 중인 단어는 건드리지 않는다

                double cx = w.X + w.Width / 2, cy = w.Y + w.Height / 2;
                double halfW = w.Width / 2 + EarthBurnRadius, halfH = w.Height / 2 + EarthBurnRadius;
                if (Math.Abs(cx - x) > halfW || Math.Abs(cy - y) > halfH) continue;

                for (int k = 0; k < 4; k++)   // 사라질 때 작은 불티
                {
                    double a = random.NextDouble() * Math.PI * 2;
                    Spark(cx, cy, 3, 0.4, Math.Cos(a) * 40, Math.Sin(a) * 40, deep: true);
                }
                active.RemoveAt(i);
                GameCanvas.Children.Remove(w.Block);
            }
        }

        /// <summary>
        /// <산성비 타자 오락 260812_28> 문구를 주어진 횟수만큼 깜빡인다. 문구와 불꽃은 산성비보다 앞에 둔다.
        /// </summary>
        private void BannerBlink(string text, int times)
        {
            Panel.SetZIndex(EventBanner, BannerInFrontOfRain);
            EventBanner.Text = text;

            var anim = new DoubleAnimationUsingKeyFrames();
            double t = 0;
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            for (int i = 0; i < times; i++)
            {
                anim.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(t + 0.18))));
                anim.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(t + 0.75))));
                anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(t + 1.0))));
                t += 1.1;
            }
            EventBanner.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        // 단어 가림(■) 적용/해제. 실제 단어(Text)는 그대로라 외워서 입력하면 맞는다.
        private void ApplyMask(bool on)
        {
            foreach (FallingWord w in active)
            {
                w.Block.Text = on ? new string('■', w.Text.Length) : w.Text;
            }
        }

        /// <summary>
        /// 동물 한 마리를 내보낸다 (<산성비 타자 오락 260812_22>로 크기·욕심·방향을 고를 수 있게 넓혔다).
        /// </summary>
        /// <param name="bird">새면 true, 메뚜기면 false.</param>
        /// <param name="fromLeft">왼쪽에서 들어오면 true. null 이면 무작위.</param>
        /// <param name="big">'조금 더 큰' 개체인지.</param>
        /// <param name="catchAll">지나가며 만나는 단어를 모두 데려가는지(아니면 처음 하나만).</param>
        private void SpawnCritter(bool bird, bool? fromLeft = null, bool big = false, bool catchAll = false)
        {
            double h = GameCanvas.ActualHeight;
            double wdt = GameCanvas.ActualWidth;
            if (h <= 0 || wdt <= 0) return;

            bool left = fromLeft ?? random.Next(2) == 0;
            var c = new Critter { IsBird = bird, CatchAll = catchAll, Scale = big ? 1.6 : 1.0 };
            if (bird)
            {
                // 옆 하늘에서 들어와 반대편 옆 하늘로 수평 비행
                c.X = left ? -60 : wdt + 60;
                c.Y = 50 + random.NextDouble() * (h * 0.3);
                c.VX = (left ? 1 : -1) * 150;
                c.VY = 0;
            }
            else
            {
                // 옆 땅에서 들어와 반대편 옆 '하늘'로 나가도록 서서히 상승
                double groundTop = h - GroundStrip.Height;
                c.X = left ? -60 : wdt + 60;
                c.Y = groundTop - 14;
                c.VX = (left ? 1 : -1) * 130;
                double crossTime = (wdt + 120) / 130.0;
                c.VY = (60 - c.Y) / crossTime;
            }
            c.DrawY = c.Y;
            c.Sprite = BuildCritterSprite(bird, left, c.Scale);
            if (bird) c.Wing = WingOf(c.Sprite);
            Canvas.SetLeft(c.Sprite, c.X - c.Sprite.Width / 2);
            Canvas.SetTop(c.Sprite, c.Y - c.Sprite.Height / 2);
            GameCanvas.Children.Add(c.Sprite);
            critters.Add(c);
        }

        private void UpdateCritters(double dt, double canvasW)
        {
            for (int i = critters.Count - 1; i >= 0; i--)
            {
                Critter c = critters[i];
                c.X += c.VX * dt;
                c.Y += c.VY * dt;
                c.Wave += dt;
                c.DrawY = c.IsBird
                    ? c.Y + Math.Sin(c.Wave * 6) * 6                  // 새: 몸이 위아래로 일렁임
                    : c.Y - Math.Abs(Math.Sin(c.Wave * 5)) * 14;      // 메뚜기: 뜀뛰기

                // 새의 날개 퍼덕임. 몸의 일렁임보다 두 배 빠르게 오르내리고, 위로 크게
                // 접었다가(-55°) 아래로 조금 펴는(+25°) 비대칭이라 실제 날갯짓처럼 보인다.
                if (c.Wing != null)
                {
                    double t = (Math.Sin(c.Wave * 12) + 1) / 2;       // 0~1
                    c.Wing.Angle = -55 + t * 80;                      // -55° ~ +25°
                }
                Canvas.SetLeft(c.Sprite, c.X - c.Sprite.Width / 2);
                Canvas.SetTop(c.Sprite, c.DrawY - c.Sprite.Height / 2);

                // 지나가다 만나는 일반 낙하 단어를 데려간다 (<산성비 타자 오락 260812_22>: 욕심쟁이는 만나는 대로 전부).
                if (c.CatchAll || c.Caught.Count == 0)
                {
                    double rx = 18 * c.Scale, ry = 12 * c.Scale;
                    foreach (FallingWord w in active)
                    {
                        if (w.Phase != 0) continue;
                        bool hit = w.X < c.X + rx && w.X + w.Width > c.X - rx &&
                                   w.Y < c.DrawY + ry && w.Y + w.Height > c.DrawY - ry;
                        if (!hit) continue;

                        c.Caught.Add(w);
                        w.Phase = 3;
                        if (!c.CatchAll) break;
                    }
                }

                // 데려가는 단어들은 동물 아래에 차례로 매달려 따라간다
                for (int k = 0; k < c.Caught.Count; k++)
                {
                    FallingWord w = c.Caught[k];
                    w.X = c.X - w.Width / 2;
                    w.Y = c.DrawY + 14 * c.Scale + k * (w.Height + 2);
                    Canvas.SetLeft(w.Block, w.X);
                    Canvas.SetTop(w.Block, w.Y);
                }

                if (c.X < -90 || c.X > canvasW + 90) // 반대편으로 퇴장
                {
                    foreach (FallingWord w in c.Caught)
                    {
                        active.Remove(w); // 입력으로 이미 사라졌어도 무해
                        GameCanvas.Children.Remove(w.Block);
                    }
                    GameCanvas.Children.Remove(c.Sprite);
                    critters.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 눈 하나를 그린다: 흰자 + 앞쪽·아래쪽으로 조금 치우친 검은
        /// 눈동자 + 윗눈꺼풀 선. 흰자만 있으면 표정이 없어 무섭게 보인다. 새와 메뚜기가 함께 쓴다.
        /// </summary>
        /// <param name="rotationDegrees">흰자+눈동자+눈꺼풀 셋을 한 그룹으로 묶어 <paramref name="cx"/>,
        /// <paramref name="cy"/>를 축으로 시계 방향으로 돌리는 각도(기본 0 = 안 돌림).</param>
        private static void AddEye(Canvas canvas, double cx, double cy, double r, double rotationDegrees = 0)
        {
            // 흰자·눈동자·눈꺼풀을 한 그룹(작은 Canvas)으로 묶어야, 그룹 전체를 하나의 RotateTransform으로
            // 돌릴 수 있다(따로 돌리면 저마다 다른 좌표계 보정이 필요해진다). 감싸는 Canvas 자체는
            // 위치를 안 옮기므로(왼쪽 위가 그대로 0,0) 안의 좌표는 바깥 캔버스와 같은 값을 그대로 쓴다.
            var group = new Canvas();

            var white = new Ellipse { Width = r * 2, Height = r * 2, Fill = Brushes.White };
            Canvas.SetLeft(white, cx - r);
            Canvas.SetTop(white, cy - r);
            group.Children.Add(white);

            // 눈동자: 중심보다 앞(부리 쪽)으로 0.4r, 아래로 0.35r (조금 더 내렸다)
            double pupilR = r * 0.37;
            var pupil = new Ellipse { Width = pupilR * 2, Height = pupilR * 2, Fill = Brushes.Black };
            Canvas.SetLeft(pupil, cx + r * 0.40 - pupilR);
            Canvas.SetTop(pupil, cy + r * 0.35 - pupilR);
            group.Children.Add(pupil);

            // 눈꺼풀: 앞쪽 수평선보다 20° 위 → 뒤쪽으로 25° 위(대각선보다 수평에 가깝게)
            double Rad(double deg) => deg * Math.PI / 180;
            group.Children.Add(new Line
            {
                X1 = cx + r * Math.Cos(Rad(20)),
                Y1 = cy - r * Math.Sin(Rad(20)),
                X2 = cx - r * Math.Cos(Rad(25)),
                Y2 = cy - r * Math.Sin(Rad(25)),
                Stroke = Brushes.Black,
                StrokeThickness = r * 0.4,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            });

            if (rotationDegrees != 0) group.RenderTransform = new RotateTransform(rotationDegrees, cx, cy);
            canvas.Children.Add(group);
        }

        /// <summary>새 그림에서 날개의 회전 변환을 찾는다(몸통 다음, 두 번째 자식).</summary>
        private static RotateTransform WingOf(Canvas sprite) =>
            (sprite != null && sprite.Children.Count > 1 ? sprite.Children[1] as Polygon : null)
                ?.RenderTransform as RotateTransform;

        /// <summary>검사용: 새 그림에 날개 회전 변환이 실제로 달려 있는지.</summary>
        internal bool BirdHasWingTransform() => WingOf(BuildCritterSprite(true, true, 1)) != null;

        /// <summary>검사용: 날개를 주어진 각도로 돌린 새 그림(퍼덕임 모양을 눈으로 보기 위함).</summary>
        internal Canvas BuildBirdSpriteForTest(double wingAngle)
        {
            Canvas sprite = BuildCritterSprite(true, true, 1);
            RotateTransform wing = WingOf(sprite);
            if (wing != null) wing.Angle = wingAngle;
            return sprite;
        }

        /// <summary>
        /// 검사용 (<산성비 타자 오락 260812_28>): 가운데 둘레에 단어를 늘어놓고 큰 불꽃을 끝까지 돌린 뒤,
        /// (처음 개수, 남은 개수, 남은 불티 수, 문구)를 돌려준다. 불꽃이 산성비를 지우는지 확인한다.
        /// </summary>
        internal (int Before, int After, int Sparks, string Banner) EarthFireworkTest()
        {
            active.Clear();
            GameCanvas.Children.Clear();

            double w = GameCanvas.ActualWidth, h = GameCanvas.ActualHeight;
            double cx = w / 2, cy = (h - GroundStrip.Height) / 2;

            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4;
                var block = new TextBlock { Text = "시험", FontSize = 23 };
                block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var word = new FallingWord
                {
                    Block = block,
                    Text = "시험",
                    Width = block.DesiredSize.Width,
                    Height = block.DesiredSize.Height,
                    X = cx + Math.Cos(a) * 80 - block.DesiredSize.Width / 2,
                    Y = cy + Math.Sin(a) * 80 - block.DesiredSize.Height / 2,
                    Speed = 0,
                };
                active.Add(word);
                GameCanvas.Children.Add(block);
            }

            int before = active.Count;
            StartEarthFirework();
            for (int f = 0; f < 90 && earthTimer > 0; f++) UpdateEarthFirework(1.0 / 30);

            return (before, active.Count, FireworkLayer.Children.Count, EventBanner.Text);
        }

        /// <summary>
        /// 검사용 (<산성비 타자 오락 260812_26>): 보통 문구와 '앞으로 내보낸' 문구의 겹침 차례, 그리고 산성비의 차례.
        /// </summary>
        internal (int Normal, int Front, int Rain) BannerZTest()
        {
            Banner("검사");
            int normal = Panel.GetZIndex(EventBanner);
            Banner("검사", inFront: true);
            int front = Panel.GetZIndex(EventBanner);
            Banner("검사");   // 기본값으로 되돌려 둔다
            return (normal, front, Panel.GetZIndex(GameCanvas));
        }

        /// <summary>검사용: 게임 영역(하늘+산성비+불꽃) 자체.</summary>
        internal FrameworkElement GameAreaForTest => GameArea;

        /// <summary>
        /// 검사용 (<산성비 타자 오락 260812_28>): 큰 불꽃을 주어진 시점까지만 진행시켜 그 순간의 화면을 볼 수 있게 한다.
        /// 알갱이가 스스로 사라지는 애니메이션은 시간이 흘러야 도는데, 검사에서는 시간이 흐르지 않으므로
        /// 뿌린 알갱이가 그대로 남아 '궤적'이 한눈에 보인다.
        /// </summary>
        internal void EarthFireworkFrameForTest(double seconds)
        {
            FireworkLayer.Children.Clear();
            StartEarthFirework();
            for (double t = 0; t < seconds; t += 1.0 / 30) UpdateEarthFirework(1.0 / 30);
        }

        /// <summary>검사용: 진행 중인 큰 불꽃을 dt 초만큼 한 틱 더 진행시킨다.</summary>
        internal void EarthFireworkStepForTest(double dt) => UpdateEarthFirework(dt);

        /// <summary>검사용: 레벨업 폭죽이 실제로 알갱이를 만드는지.</summary>
        internal int LevelUpFireworksTest()
        {
            FireworkLayer.Children.Clear();
            LevelUpFireworks();
            return FireworkLayer.Children.Count;
        }

        /// <summary>검사용: 메뚜기 그림.</summary>
        internal Canvas BuildGrasshopperSpriteForTest() => BuildCritterSprite(false, true, 1);

        /// <summary>검사용: 나비 그림.</summary>
        internal Canvas BuildButterflySpriteForTest() => BuildButterflySprite(true, 1);

        /// <summary>
        /// 나비를 그린다. 새·메뚜기와 마찬가지로 **옆에서 본 모습**이라, 반대쪽 것은 몸에 가려 보이지
        /// 않는다 — 날개는 넉 장 중 이쪽 두 장(앞날개·뒷날개)만, 더듬이는 둘 중 하나만 그린다
        /// (새가 두 날개 중 하나만, 메뚜기가 두 더듬이 중 하나만 보이는 것과 같다).
        ///
        /// 틀은 메뚜기를 그대로 따른다 — 머리는 몸통 앞쪽의 작은 원, 눈은 공용 AddEye(흰자+눈동자+
        /// 눈꺼풀로 표정을 준다), 더듬이는 왼쪽 위 모서리를 축으로 돌린 가는 막대. 나비만의 특징은
        /// 더듬이 끝의 곤봉과 무늬 있는 넓은 날개다. 오른쪽이 머리이며 좌우 뒤집기는 다른 동물과
        /// 같은 규칙을 쓴다 (<산성비 타자 오락 260812_22>).
        /// </summary>
        private Canvas BuildButterflySprite(bool facingRight, double scale)
        {
            var canvas = new Canvas { Width = 40, Height = 26, IsHitTestVisible = false };

            // 날개: 새·메뚜기와 구분되도록 주황(모나크 나비풍) 바탕에 짙은 안점 무늬를 찍는다.
            var wingFill = new SolidColorBrush(Color.FromRgb(0xF7, 0x67, 0x07));
            var wingSpot = new SolidColorBrush(Color.FromRgb(0x21, 0x25, 0x29));

            void Wing(double cx, double cy, double w, double h, double angle, double spotR)
            {
                var wing = new Ellipse
                {
                    Width = w,
                    Height = h,
                    Fill = wingFill,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new RotateTransform(angle),
                };
                Canvas.SetLeft(wing, cx - w / 2);
                Canvas.SetTop(wing, cy - h / 2);
                canvas.Children.Add(wing);

                var spot = new Ellipse { Width = spotR * 2, Height = spotR * 2, Fill = wingSpot };
                Canvas.SetLeft(spot, cx - spotR);
                Canvas.SetTop(spot, cy - spotR);
                canvas.Children.Add(spot);
            }

            // 이쪽 편 두 장만. 타원의 긴 축을 세로로 두어(폭보다 높이를 크게) 회전 전 기본자세가
            // '위로 뻗음'이 되게 하고, 살짝만 돌려 뒤(왼쪽)로 기울인다 — 새 날개(밑변을 몸통 속에
            // 묻은 삼각형)와 같은 이치로, 아랫부분이 가슴 속에 묻히도록 머리·몸통 이음매 바로 위에
            // 자리를 잡는다. 앞날개가 크고 위, 뒷날개가 작고 뒤·아래다(나비의 실제 배치이며, 둘을
            // 확실히 떨어뜨려 겹쳐 뭉쳐 보이지 않게 했다).
            // 몸통·머리보다 먼저 그려 밑동이 그 아래 묻히게 한다(새 날개와 같은 순서).
            Wing(23, 7, 9, 16, -12, 1.6);    // 앞날개
            Wing(16, 14, 6, 11, -22, 1.0);   // 뒷날개(더 작고, 더 뒤로 기움)

            // 몸통: 메뚜기·새보다 훨씬 가는 캡슐형. 나비 몸통은 대개 짙은 색이다.
            var bodyFill = new SolidColorBrush(Color.FromRgb(0x34, 0x2e, 0x2b));
            var body = new Ellipse { Width = 18, Height = 4, Fill = bodyFill };
            Canvas.SetLeft(body, 12);
            Canvas.SetTop(body, 11);
            canvas.Children.Add(body);

            // 머리: 메뚜기와 같은 틀(몸통 앞쪽의 작은 원)이되 짙은 색.
            const double headR = 3, headCx = 32, headCy = 13;
            var head = new Ellipse { Width = headR * 2, Height = headR * 2, Fill = bodyFill };
            Canvas.SetLeft(head, headCx - headR);
            Canvas.SetTop(head, headCy - headR);
            canvas.Children.Add(head);

            // 더듬이 하나(옆에서 보므로 반대쪽 것은 가려진다). 메뚜기의 더듬이를 그대로 흉내 낸다:
            // 세로 막대를 **왼쪽 위 모서리를 축으로 35°** 돌려, 머리의 위-뒤쪽에 붙이고 위-앞으로 뻗게 한다.
            //
            // 메뚜기(머리 중심 (30,12)·반지름 4)의 막대는 1.5×8, left=31/top=2다. 사각형이 왼쪽 위
            // 모서리를 축으로 도므로 머리에 닿는 쪽(밑변)의 중심은
            //   (left, top) + (w/2·cos35 − h·sin35, w/2·sin35 + h·cos35) = (27.02, 8.98)
            // 이고, 이는 머리 중심에서 반지름의 (−0.745, −0.755)배 떨어진 지점이다. 나비 머리는 더
            // 작으므로(반지름 3) 같은 **비율**로 붙도록 아래에서 위치를 역산한다.
            const double antAngle = 85, antW = 1.5, antH = 8;
            double antRad = antAngle * Math.PI / 180;
            double baseX = headCx + headR * -0.745;   // 더듬이가 머리에 닿는 지점
            double baseY = headCy + headR * -0.755;
            double baseDx = antW / 2 * Math.Cos(antRad) - antH * Math.Sin(antRad);
            double baseDy = antW / 2 * Math.Sin(antRad) + antH * Math.Cos(antRad);
            double rodLeft = baseX - baseDx, rodTop = baseY - baseDy;
            // 자유단(곤봉이 붙는 곳)은 막대의 윗변 중심 — 메뚜기와 같은 각도·부착 비율에서 그대로 유도된다.
            double farX = rodLeft + antW / 2 * Math.Cos(antRad);
            double farY = rodTop + antW / 2 * Math.Sin(antRad);

            // 곧은 막대 대신, 나비 머리가 향한 뒤쪽(-x, 몸통 쪽)으로 완만하게 부푸는 곡선(2차
            // 베지어)으로 그려 굵기를 줄인다(메뚜기의 곧은 더듬이와 달리, 나비다운 완만한 휨을
            // 준다). 시작·끝점은 위의 부착·자유단 위치를 그대로 쓰므로 각도·부착 위치는 메뚜기와
            // 여전히 같다.
            double dx = farX - baseX, dy = farY - baseY;
            double len = Math.Sqrt(dx * dx + dy * dy);
            double nx = dy / len, ny = -dx / len;         // 직선에 수직인 방향
            if (nx > 0) { nx = -nx; ny = -ny; }            // 뒤쪽(-x)을 향하는 쪽을 고른다
            const double bow = 1.4;
            var control = new Point((baseX + farX) / 2 + nx * bow, (baseY + farY) / 2 + ny * bow);

            var antennaFigure = new PathFigure { StartPoint = new Point(baseX, baseY) };
            antennaFigure.Segments.Add(new QuadraticBezierSegment(control, new Point(farX, farY), true));
            var antenna = new System.Windows.Shapes.Path
            {
                Data = new PathGeometry(new[] { antennaFigure }),
                Stroke = bodyFill,
                StrokeThickness = 1.0,   // 기존(1.5)보다 살짝 가늘게
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            };
            canvas.Children.Add(antenna);

            // 나비만의 특징인 곤봉(끝이 볼록). 자유단(위에서 구한 farX,farY)에 붙인다.
            const double clubR = 1.1;
            var club = new Ellipse { Width = clubR * 2, Height = clubR * 2, Fill = bodyFill };
            Canvas.SetLeft(club, farX - clubR);
            Canvas.SetTop(club, farY - clubR);
            canvas.Children.Add(club);

            // 눈: 메뚜기와 같은 비율 자리(머리 중심에서 앞으로 0.25r, 위로 0.125r)에 공용 눈을 둔다.
            // 눈알 그룹(흰자+눈동자+눈꺼풀)을 시계 방향 53도 돌린다.
            AddEye(canvas, headCx + headR * 0.25, headCy - headR * 0.125, 1.2, rotationDegrees: 53);

            // 몸통 기울기: 배 끝(꼬리)에서 머리까지의 선이 수평선과 이루는 오른쪽 각이 53도가
            // 되도록 통째로 돌린다 — 화면 기준 시계 방향이 양수이므로, 머리가 위로 들리게(오른쪽
            // 각 53도) 하려면 반시계 방향(음수) 53도를 준다.
            const double bodyTilt = -53;

            // 방향(좌우 뒤집기)과 크기, 몸통 기울기를 한 번에 건다 — 다른 동물과 같은 규칙(<산성비 타자 오락 260812_22>)에
            // 기울기를 더했다.
            canvas.RenderTransformOrigin = new Point(0.5, 0.5);
            var transforms = new TransformGroup();
            transforms.Children.Add(new RotateTransform(bodyTilt));
            if (!facingRight || scale != 1)
                transforms.Children.Add(new ScaleTransform(facingRight ? scale : -scale, scale));
            canvas.RenderTransform = transforms;
            return canvas;
        }

        private Canvas BuildCritterSprite(bool bird, bool facingRight, double scale)
        {
            var canvas = new Canvas { Width = 40, Height = 26, IsHitTestVisible = false };
            if (bird)
            {
                var bodyFill = new SolidColorBrush(Color.FromRgb(0x49, 0x50, 0x57));
                var body = new Ellipse { Width = 24, Height = 13, Fill = bodyFill };
                Canvas.SetLeft(body, 8);
                Canvas.SetTop(body, 8);
                canvas.Children.Add(body);

                // 날개.
                // 밑변을 몸통 속 깊숙이(y=15) 넣은 큰 삼각형으로 만들고, 몸통 한가운데(19,14)를 축으로
                // 돌린다. 어느 각도에서도 밑변 두 점이 몸통 타원 안에 머물러 날개와 몸통 사이에 파인
                // 틈이 생기지 않는다. 색도 몸통과 같게 두어, 몸 밖으로 나온 부분만 날개로 보인다.
                var wing = new Polygon
                {
                    // 꼭짓점을 낮춰(y=3) 몸 밖으로 보이는 길이를 줄였다 — 너무 길면 뿔처럼 보인다.
                    Points = new PointCollection { new Point(23, 3), new Point(12, 15), new Point(26, 15) },
                    Fill = bodyFill,
                    RenderTransform = new RotateTransform(0, 19, 14),
                };
                canvas.Children.Add(wing);

                // 부리: 밑변의 두 꼭짓점이 몸통 타원 위에 놓이도록 뒤로 당겼다.
                // 타원은 중심 (20,14.5)·반지름 (12,6.5)이므로 y=12·17에서 가장자리 x ≈ 31.1이다.
                var beak = new Polygon
                {
                    Points = new PointCollection { new Point(31.1, 12), new Point(38, 14.5), new Point(31.1, 17) },
                    Fill = new SolidColorBrush(Color.FromRgb(0xf7, 0x67, 0x07)),
                };
                canvas.Children.Add(beak);

                AddEye(canvas, 28.5, 11.5, 1.5);
            }
            else
            {
                var green = new SolidColorBrush(Color.FromRgb(0x2f, 0x9e, 0x44));
                var greenDark = new SolidColorBrush(Color.FromRgb(0x2b, 0x8a, 0x3e));

                var body = new Ellipse { Width = 22, Height = 9, Fill = green };
                Canvas.SetLeft(body, 6);
                Canvas.SetTop(body, 10);
                canvas.Children.Add(body);

                var head = new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(Color.FromRgb(0x37, 0xb2, 0x4d)) };
                Canvas.SetLeft(head, 26);
                Canvas.SetTop(head, 8);
                canvas.Children.Add(head);

                // 다리 셋: 메뚜기는 한쪽에 앞·가운데·뒷다리가 하나씩 있다.
                // 뒷다리는 도약용이라 굵고 길며 뒤로 크게 젖혀진다.
                void Leg(double left, double top, double w, double h, double angle)
                {
                    var leg = new Rectangle
                    {
                        Width = w,
                        Height = h,
                        Fill = greenDark,
                        RenderTransform = new RotateTransform(angle),
                    };
                    Canvas.SetLeft(leg, left);
                    Canvas.SetTop(leg, top);
                    canvas.Children.Add(leg);
                }

                // 세 발끝 '변의 중심점'을 같은 높이(y=22, 뒷다리 발끝)에 맞춘다.
                // 사각형은 왼쪽 위 모서리를 축으로 도므로, 발끝 변의 중심(가로/2, 세로)은
                //   y = top + (가로/2)·sin θ + 세로·cos θ.
                //   앞다리(-30°):  1·sin(-30) + 8·cos(-30) = 6.43 → top = 22 − 6.43 = 15.57
                //   가운뎃다리(-5°): 1·sin(-5) + 8·cos(-5) = 7.88 → top = 22 − 7.88 = 14.12
                // 앞다리만 0.4 올렸다(발끝 중심 21.6). 눈으로 볼 때 살짝 들린 모습.
                Leg(21, 15.17, 2, 8, -30);   // 앞다리(머리 쪽)
                Leg(16, 14.12, 2, 8, -5);    // 가운뎃다리

                // 뒷다리(도약용)는 '＾' 모양으로 꺾인다: 몸통에서 위로 뻗어 무릎이
                // 몸통보다 높이 솟았다가, 거기서 뒤아래로 내려와 발끝이 몸통보다 아래에 놓인다.
                // 뒷다리 전체를 앞으로 2.5 옮겼다(붙는 자리 14.5 < 가운뎃다리 16).
                //
                // 두 마디를 선 두 개가 아니라 꺾인 선 하나로 그린다: 무릎은 이어진 채로
                // 두고, 양 끝은 다른 다리(직사각형)처럼 **평평하게** 끊는다.
                canvas.Children.Add(new Polyline
                {
                    Points = new PointCollection
                    {
                        new Point(14.5, 16),   // 몸통 뒤쪽
                        new Point(10.5, 5),    // 무릎(몸통 위)
                        new Point(6.5, 22),    // 발끝(몸통 아래, 가장 뒤)
                    },
                    Stroke = greenDark,
                    StrokeThickness = 2.4,
                    StrokeStartLineCap = PenLineCap.Flat,
                    StrokeEndLineCap = PenLineCap.Flat,
                    StrokeLineJoin = PenLineJoin.Round,
                });

                var antenna = new Rectangle { Width = 1.5, Height = 8, Fill = greenDark, RenderTransform = new RotateTransform(35) };
                Canvas.SetLeft(antenna, 31);
                Canvas.SetTop(antenna, 2);
                canvas.Children.Add(antenna);

                AddEye(canvas, 31, 11.5, 1.5);   // 새와 같은 눈
            }

            // 방향(좌우 뒤집기)과 크기를 한 번에 건다 (<산성비 타자 오락 260812_22>). 가운데를 기준으로 하므로
            // 위치 계산은 원래 크기(40x26) 그대로 두어도 그림의 중심이 그 자리에 온다.
            if (!facingRight || scale != 1)
            {
                canvas.RenderTransformOrigin = new Point(0.5, 0.5);
                canvas.RenderTransform = new ScaleTransform(facingRight ? scale : -scale, scale);
            }
            return canvas;
        }

        /// <summary>겹침 차례 (<산성비 타자 오락 260812_26>). 배너는 기본으로 산성비(20)보다 뒤(10)에 있다.</summary>
        private const int BannerBehindRain = 10;
        private const int BannerInFrontOfRain = 40;

        /// <summary>
        /// 게임 화면 가운데에 안내 문구를 잠깐 띄운다(특수 이벤트·레벨업).
        /// </summary>
        /// <param name="text">보여 줄 문구.</param>
        /// <param name="inFront">
        /// true 면 산성비 글자보다 **앞**에 놓아 글자를 가린다. 기본은 뒤(<산성비 타자 오락 260812_26>) —
        /// 떨어지는 글자를 가리지 않게 하기 위함이며, 꼭 눈에 띄어야 하는 문구에만 true 를 준다.
        /// </param>
        private void Banner(string text, bool inFront = false)
        {
            Panel.SetZIndex(EventBanner, inFront ? BannerInFrontOfRain : BannerBehindRain);
            EventBanner.Text = text;
            var anim = new DoubleAnimationUsingKeyFrames();
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.15))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.9))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.5))));
            EventBanner.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        private void SpawnWord()
        {
            // <260927_4>(0.2) 단어 공급 규칙(9:1 섞기·5개 단위 중복 금지)을 따르면서, 이미 떠 있는
            // 단어와는 되도록 겹치지 않게 뽑는다(모두 겹치면 중복 허용).
            string text = feed?.Next(t => active.Any(w => w.Text == t));
            if (text == null) return;

            // 처치 수를 다 채웠으면 이번에 태어나는 단어가 파란 특수 단어다.
            // 화면에 이미 특수 단어가 떠 있으면 겹치지 않게 미룬다.
            // <산성비 타자 오락 260812_15> 치트가 켜져 있으면 절반을 파란 글씨로 (여러 개가 함께 떠도 된다).
            bool special = CheatHalfSpecial
                ? random.Next(2) == 0
                : specialCatchesLeft <= 0 && !active.Any(w => w.IsSpecial);

            var block = new TextBlock
            {
                Text = text,
                FontSize = 23,   // <산성비 타자 오락 260812_25>
                FontWeight = FontWeights.Bold,
                Foreground = special ? WordSpecialBrush : WordNormalBrush,
            };
            block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            double maxX = Math.Max(8, GameCanvas.ActualWidth - block.DesiredSize.Width - 8);
            double x = 8 + random.NextDouble() * (maxX - 8);

            double speed = BaseFallSpeed * (1 + 0.12 * (level - 1)) * (0.85 + random.NextDouble() * 0.4);

            var word = new FallingWord
            {
                Block = block, Text = text,
                X = x, Y = -block.DesiredSize.Height,
                Width = block.DesiredSize.Width, Height = block.DesiredSize.Height,
                Speed = speed,
                IsSpecial = special,
                // 치트로 고른 효과가 있으면 그 안에서 고루, 아니면 무작위 (<산성비 타자 오락 260812_15>).
                EventType = special ? (CheatHalfSpecial ? NextCheatEffect() : PickNormalEventType()) : 0,
            };
            if (special)
            {
                specialCatchesLeft = SpecialCatchGap();
            }
            if (maskTimer > 0) // 가림 효과 중에 태어난 단어도 가려진다
            {
                block.Text = new string('■', text.Length);
            }
            Canvas.SetLeft(block, x);
            Canvas.SetTop(block, word.Y);
            GameCanvas.Children.Add(block);
            active.Add(word);
        }

        // ── 입력 ──

        private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            WinKey key = e.Key == WinKey.ImeProcessed ? e.ImeProcessedKey : e.Key;
            if (key != WinKey.Enter && key != WinKey.Space) return; // [Enter]와 [Space] 모두 제출

            e.Handled = true;
            if (!running || paused) return;
            Submit();
        }

        /// <summary>
        /// 친 글이 떨어지는 단어와 같은가. **대소문자를 구분한다**(서수 비교) — 영문 오락의 대문자 단어(DJ·DVD 등)는
        /// [Shift]로 대문자를 정확히 쳐야 정답이고 소문자(dj·dvd)로 치면 오답이다(썰렁이 지시, 2026-10-02).
        /// 입력 칸에 **결과로 남은 글자**만 비교하므로, [Caps Lock]을 켜고 쳐서 생긴 대문자(DJ·DVD)도 [Shift]로 친 것과
        /// 똑같이 정답이다(썰렁이 지시, 2026-10-02). 입력 칸은 입력기를 끈 평범한 TextBox 라 Windows 의 글쇠 변환이
        /// 그대로 글자가 된다 — [Caps Lock] 켜짐 + [Shift]는 소문자가 되는 것도 Windows 규칙 그대로다.
        /// </summary>
        internal static bool MatchesTyped(string word, string typed) =>
            string.Equals(word, typed, StringComparison.Ordinal);

        private void Submit()
        {
            string text = InputBox.Text.Trim();
            InputBox.Clear();
            if (text.Length == 0) return;

            attempts++;

            // 같은 단어가 여러 개면 땅에 가장 가까운 것부터 제거
            FallingWord match = active.Where(w => MatchesTyped(w.Text, text))
                                      .OrderByDescending(w => w.Y)
                                      .FirstOrDefault();
            if (match != null)
            {
                active.Remove(match);
                GameCanvas.Children.Remove(match.Block);
                caught++;
                combo++;
                if (specialCatchesLeft > 0) specialCatchesLeft--;   // 처치 수로 센다
                if (combo > maxCombo) maxCombo = combo;

                int gained = 10 + text.Length * 5;
                if (combo >= 2) gained += combo * 2;     // 연속 성공 보너스
                if (match.IsSpecial)
                {
                    gained += 30;                        // 특수 단어 보너스
                    TriggerSpecial(match.EventType);
                }
                score += gained;

                // <산성비 타자 오락 260812_28> 레벨 9부터, 점수가 250~350점 오를 때마다 큰 불꽃이 터진다.
                // (레벨이 오를수록 점수가 빨리 쌓이므로 자연히 자주 나온다.)
                if (level >= EarthFromLevel && score >= earthNextScore)
                {
                    earthNextScore = score + random.Next(EarthScoreGapMin, EarthScoreGapMax + 1);
                    TriggerSpecial(EarthEffectType);
                }

                if (caught % CatchesPerLevel == 0)
                {
                    level++;
                    // 오락 단계 해금은 자리연습 목표 타수로 일원화했으므로 게임 안에서는 레벨만 올린다 (<260723_4> (1)).
                    // <산성비 타자 오락 260812_24>, <산성비 타자 오락 260812_24-1> 문구는 산성비보다 앞에 두고 양쪽에서 폭죽을 터뜨린다.
                    Banner("레벨 " + level + "에 도달하였습니다!", inFront: true);
                    LevelUpFireworks();

                    // <산성비 타자 오락 260812_28> 레벨 9에 들어서면 그때부터 점수 간격마다 큰 불꽃이 터진다.
                    if (level == EarthFromLevel && earthNextScore == int.MaxValue)
                        earthNextScore = score + random.Next(EarthScoreGapMin, EarthScoreGapMax + 1);
                }
                UpdateHud();
            }
            else
            {
                combo = 0; // 오타는 연속 성공이 끊긴다
                UpdateHud();
            }
        }

        // ── Caps Lock 경고 (<261005_4>) ──

        /// <summary>검사용: 실제 키보드 대신 이 값을 Caps Lock 상태로 본다(null이면 실제 상태).</summary>
        internal static bool? CapsLockOverrideForTest;

        /// <summary>영문 산성비에서 지금 Caps Lock이 켜져 있는가(한글 산성비는 늘 false).</summary>
        private bool CapsLockOn =>
            isEnglish && (CapsLockOverrideForTest ?? Keyboard.IsKeyToggled(WinKey.CapsLock));

        /// <summary>
        /// 영문 산성비에서 [Caps Lock]이 켜져 있으면 땅 영역의 두 조작 안내 사이에 빨간 경고 문구를 보이고, 꺼지면
        /// 감춘다. 사용자가 일부러 켰을 수도 있으므로 경고만 띄운다 — 입력과 정답 판정에는 아무 영향이 없다
        /// (대문자 단어는 [Caps Lock]을 켜고 쳐도 정답이다, <see cref="MatchesTyped"/>).
        /// </summary>
        internal void RefreshCapsLockWarning()
        {
            Visibility want = CapsLockOn ? Visibility.Visible : Visibility.Collapsed;
            if (CapsLockWarningText.Visibility != want) CapsLockWarningText.Visibility = want;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (TryCheatCode(e)) return;

            // Esc 를 꾹 누르면 KeyDown 이 반복돼 일시정지가 계속 뒤집히므로 반복 입력은 무시한다. 한글 조합 중에는
            // Esc 가 ImeProcessed 로 오므로 위 치트·제출 입력처럼 원래 키를 꺼내 본다.
            WinKey escKey = e.Key == WinKey.ImeProcessed ? e.ImeProcessedKey : e.Key;
            if (escKey != WinKey.Escape || !running) return;
            if (e.IsRepeat)
            {
                e.Handled = true;
                return;
            }

            paused = !paused;
            PauseOverlay.Visibility = paused ? Visibility.Visible : Visibility.Collapsed;
            if (!paused)
            {
                lastSeconds = clock.Elapsed.TotalSeconds; // 정지 시간을 dt에서 제외
                InputBox.Focus();
            }
            e.Handled = true;
        }

        /// <summary>
        /// <산성비 타자 오락 260812_15> 치트코드를 감지해 치트 창을 연다. 글자 키만 버퍼에 쌓으므로 게임 중 아무 때나
        /// 칠 수 있다(입력 칸에 그 글자가 들어가는 것은 평소의 오타와 같아 게임에 영향이 없다).
        /// </summary>
        private bool TryCheatCode(KeyEventArgs e)
        {
            WinKey k = e.Key == WinKey.ImeProcessed ? e.ImeProcessedKey : e.Key;
            if (!cheatDetector.Feed(k)) return false;

            InputBox.Clear();          // 치트코드가 입력 칸에 남지 않게
            OpenCheatWindow();
            e.Handled = true;
            return true;
        }

        /// <summary>
        /// <산성비 타자 오락 260812_16>(1) 입력 칸의 입력기를 그 자판에 맞춘다. 한글 오락이면 한글 입력기를 켜고(영문
        /// 상태로 시작하면 첫 단어를 칠 수 없다), 영문 오락(<260927_4>(2))이면 입력기를 꺼 영문이 바로
        /// 들어가게 한다. 입력기가 없거나 한글 IME 가 아닌 환경에서는 조용히 넘어간다.
        /// </summary>
        private void SwitchInputLanguage()
        {
            try
            {
                if (isEnglish)
                {
                    InputMethod.SetPreferredImeState(InputBox, InputMethodState.Off);
                    InputMethod.SetPreferredImeConversionMode(InputBox, ImeConversionModeValues.Alphanumeric);
                    InputMethod.Current.ImeState = InputMethodState.Off;
                    InputMethod.Current.ImeConversionMode = ImeConversionModeValues.Alphanumeric;
                    return;
                }
                InputMethod.SetPreferredImeState(InputBox, InputMethodState.On);
                InputMethod.SetPreferredImeConversionMode(InputBox, ImeConversionModeValues.Native);
                InputMethod.Current.ImeState = InputMethodState.On;
                InputMethod.Current.ImeConversionMode = ImeConversionModeValues.Native;
            }
            catch { /* 입력기 환경에 따라 실패할 수 있으나 게임 진행에는 지장 없다 */ }
        }

        private void OpenCheatWindow()
        {
            bool wasRunning = running && !paused;
            if (wasRunning) paused = true;   // 창을 보는 동안 산성비가 떨어지지 않게

            new AcidCheatWindow(this) { Owner = this }.ShowDialog();

            if (wasRunning)
            {
                paused = false;
                lastSeconds = clock.Elapsed.TotalSeconds;   // 멈춰 있던 시간을 dt에서 뺀다
                InputBox.Focus();
            }
        }

        /// <summary>
        /// 평소(치트 아님) 파란 단어의 효과를 무작위로 고른다. '지구 환경을 지켜라'(EarthEffectType)는
        /// 파란 단어를 잡아서가 아니라 레벨 9부터 점수 간격마다 저절로 터지는 전용 효과이므로 여기서는
        /// 제외한다(EarthEffectType 이 마지막 번호라 EffectCount-1 로 그 하나만 뺄 수 있다).
        /// </summary>
        internal int PickNormalEventType() => random.Next(EffectCount - 1);

        /// <summary>검사용: 평소 무작위 뽑기에서 지구 효과가 나오지 않는지.</summary>
        internal bool NormalEventNeverEarthForTest()
        {
            for (int i = 0; i < 1000; i++)
                if (PickNormalEventType() == EarthEffectType) return false;
            return true;
        }

        /// <summary>
        /// 치트로 고른 효과들을 고루 뽑는다. 한 바퀴를 다 쓸 때까지 같은 효과가 다시 나오지 않으므로
        /// 무작위보다 고르게 퍼진다(주머니에서 꺼내는 방식).
        /// </summary>
        /// <summary>검사용: 위 주머니 방식이 고른 효과만 고루 내보내는지 확인한다.</summary>
        internal int NextCheatEffectForTest() => NextCheatEffect();

        /// <summary>
        /// 치트 창에서 고른 효과가 바뀌었을 때 부른다. 주머니에 예전 선택으로 이미 채워 둔 항목이
        /// 남아 있으면 그게 다 나갈 때까지 새 선택이 반영되지 않으므로("누르는 즉시 게임에
        /// 반영된다"는 설명과 어긋남), 다음에 새로 채우도록 비운다.
        /// </summary>
        internal void ResetCheatEffectBag() => cheatEffectBag.Clear();

        private int NextCheatEffect()
        {
            if (cheatEffectBag.Count == 0)
            {
                IEnumerable<int> source = CheatEffects.Count > 0 ? (IEnumerable<int>)CheatEffects : Enumerable.Range(0, EffectCount);
                cheatEffectBag.AddRange(source.OrderBy(_ => random.Next()));
            }
            int pick = cheatEffectBag[0];
            cheatEffectBag.RemoveAt(0);
            return pick;
        }

        private void InputBox_PreviewExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            // 연습 창들과 동일하게 복사/잘라내기/붙여넣기를 차단한다 (타자 없이 입력 방지)
            if (e.Command == ApplicationCommands.Copy ||
                e.Command == ApplicationCommands.Cut ||
                e.Command == ApplicationCommands.Paste)
            {
                e.Handled = true;
            }
        }

        // ── HUD ──

        private void UpdateHud()
        {
            ScoreText.Text = "점수 " + score;
            LevelText.Text = "  레벨 " + level;
            // <산성비 타자 오락 260812_17> 연속 성공으로 실제로 더 받은 점수(콤보 보너스)를 함께 알려 준다.
            ComboText.Text = combo >= 2
                ? "연속 성공 ×" + combo + "  보너스 점수 " + (combo * 2) + "점 추가!"
                : "";
            LivesText.Text = new string('●', Math.Max(0, lives)) + new string('○', Math.Max(0, StartLives - lives));
        }
    }
}
