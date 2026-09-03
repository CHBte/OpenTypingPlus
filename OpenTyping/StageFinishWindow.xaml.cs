using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls;

namespace OpenTyping
{
    /// <summary>
    /// <260812_5>, <260812_5-1> 단계 자리연습을 마치면 뜨는 창.
    ///
    /// 문구·버튼 구성은 어느 것도 손으로 적어 두지 않고 그때그때 계산한다 — 사용자가 앞으로 단계
    /// 구성을 바꾸거나(단계 수·순서), 새 오락을 이식하거나, 목표 타수의 기준·계산 방식을 바꾸어도
    /// 규칙이 그대로 따라가야 하기 때문이다. 판단의 근거는 세 곳뿐이다:
    ///  - 마지막 단계인가 → <see cref="DubeolsikStages.Stages"/>의 개수
    ///  - 이 단계에 오락이 있는가 → <see cref="ArcadeGames"/> 레지스트리에 그 단계 id가 있는가
    ///  - 목표 타수를 넘겼는가 → <see cref="StageRecords.PassThreshold"/>
    ///
    /// 이 창은 무엇을 할지 고르기만 하고 실행하지 않는다. 고른 결과가 <see cref="Action"/>에 담겨
    /// 닫히고, 연습 창이 그 값을 호출자(자리연습 화면)에게 넘겨 거기서 다음 창을 연다 — 창을 안으로
    /// 겹쳐 쌓지 않기 위함 (<260811_34>).
    /// </summary>
    public partial class StageFinishWindow : MetroWindow
    {
        public enum Choice { Close, Retry, NextStage, Game, Exit }

        public Choice Action { get; private set; } = Choice.Close;

        /// <summary>이 단계에 실제로 이식된 오락이 있는가(레지스트리에 그 단계 id가 있는가).</summary>
        public static bool HasGame(int gameStageId)
        {
            if (gameStageId == 0) return false;
            IArcadeGame game = ArcadeGames.Default;
            return game != null && game.StageIds.Contains(gameStageId);
        }

        /// <summary>첫 줄: 이번 타수. 목표에 못 미쳤으면 그 사실과 목표 타수를 함께 알린다.</summary>
        internal static string BuildTaLine(int tpm, int target) =>
            tpm >= target ? tpm + "타입니다."
                          : tpm + "타입니다. " + target + "타에 도달하지 못했습니다.";

        /// <summary>
        /// 둘째 줄: 무엇이 활성화되었는지(또는 아직 아닌지). 없는 대상은 문장에서 빠진다.
        /// </summary>
        internal static string BuildUnlockLine(int stageNumber, bool isLast, bool hasGame, bool reached)
        {
            string gamePhrase = hasGame ? stageNumber + "단계 오락" : null;

            if (reached)
            {
                if (isLast)
                    return (gamePhrase == null ? "" : gamePhrase + "이 활성화되었습니다. ")
                           + "한글 타자 완성을 축하합니다!";

                string next = (stageNumber + 1) + "단계 자리연습";
                return (gamePhrase == null ? "" : gamePhrase + "과 ") + next + "이 활성화되었습니다.";
            }

            if (isLast)
                return gamePhrase == null ? "" : "아직 " + gamePhrase + "이 활성화되지 않았습니다.";

            return "아직 " + (gamePhrase == null ? "" : gamePhrase + "과 ")
                   + (stageNumber + 1) + "단계 자리연습이 활성화되지 않았습니다.";
        }

        /// <param name="stageNumber">방금 마친 단계 번호(1부터).</param>
        /// <param name="lastStage">마지막 단계 번호.</param>
        /// <param name="gameStageId">이 단계에 딸린 오락의 단계 id(없으면 0).</param>
        public StageFinishWindow(int stageNumber, int lastStage, int tpm, int gameStageId)
        {
            InitializeComponent();

            bool isLast = stageNumber >= lastStage;
            bool hasGame = HasGame(gameStageId);
            // 이번 판 타수가 아니라 실제로 해금됐는지(치트 또는 이제까지의 최고 기록)를 봐야 한다 —
            // 안 그러면 이미 통과한 단계를 '다시 연습'하다 이번엔 목표에 못 미쳤을 때, 실제로는 계속
            // 열려 있는 다음 단계·오락을 '아직 활성화되지 않음'으로 잘못 알린다.
            bool reached = StageRecords.IsPassed(stageNumber);

            TaText.Text = BuildTaLine(tpm, StageRecords.PassThreshold);

            string unlock = BuildUnlockLine(stageNumber, isLast, hasGame, reached);
            UnlockText.Text = unlock;
            if (unlock.Length == 0) UnlockText.Visibility = Visibility.Collapsed;

            // 버튼: 다시 연습 / (다음 단계) / (오락) / 나가기.
            // 목표에 못 미쳤으면 아직 열리지 않은 곳으로 가는 버튼은 비활성으로 둔다.
            AddButton("다시 연습", true, Choice.Retry);
            if (!isLast) AddButton("다음 " + (stageNumber + 1) + "단계 자리 연습", reached, Choice.NextStage);
            if (hasGame) AddButton(stageNumber + "단계 오락", reached, Choice.Game);
            AddButton("나가기", true, Choice.Exit);
        }

        private void AddButton(string text, bool enabled, Choice choice)
        {
            var button = new Button
            {
                IsEnabled = enabled,
                Margin = new Thickness(ButtonPanel.Children.Count == 0 ? 0 : 10, 0, 0, 0),
                Content = new TextBlock
                {
                    Text = text,
                    Style = (Style)FindResource("ControlButtonText")
                }
            };
            button.Click += (sender, e) => { Action = choice; Close(); };
            ButtonPanel.Children.Add(button);
        }

        /// <summary>검사용: 지금 보이는 버튼들을 "글자(활성/비활성)" 꼴로.</summary>
        internal string DescribeButtons() =>
            string.Join(" ", ButtonPanel.Children.OfType<Button>()
                .Select(b => $"\"{((TextBlock)b.Content).Text}\"({(b.IsEnabled ? "활성" : "비활성")})"));
    }
}
