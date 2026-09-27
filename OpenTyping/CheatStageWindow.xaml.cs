using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls;

namespace OpenTyping
{
    /// <summary>
    /// <260812_10> 치트코드를 입력했을 때 뜨는 창. 몇 단계까지 통과한 것으로 칠지 고른다.
    /// 목록은 단계 정의에서 만들므로 단계 구성이 바뀌어도 그대로 따라간다.
    /// </summary>
    public partial class CheatStageWindow : MetroWindow
    {
        /// <summary>
        /// 확인을 눌렀는지. 취소면 false — 이때 <see cref="Stages"/>는 보지 않는다.
        /// </summary>
        public bool Confirmed { get; private set; }

        /// <summary>
        /// 고른 값 (<260812_10-2>). null = '원래대로'(치트를 끄고 실제 기록을 따름),
        /// 0 = '1단계도 통과 못함', n = n단계까지 통과한 것으로.
        /// </summary>
        public int? Stages { get; private set; }

        // <260812_10-3> 창을 열었을 때 걸려 있던 원래 값(옛 '전부 열기'=int.MaxValue 등, 지금
        // 단계 수보다 클 수 있다)과, 사용자가 단계 선택을 실제로 건드렸는지.
        private readonly int? originalCheatUpTo;
        private bool userChangedSelection;

        public CheatStageWindow(IReadOnlyList<PracticeStage> stages)
        {
            InitializeComponent();
            Title = VersionInfo.WindowTitle; // <2600912_5-1>

            // <260927_5>(0), <260927_6>(0.1) 치트는 지금 '설정'에 지정된 자판의 기록에만 적용된다.
            HintText.Text = "'" + StageSets.Current.LayoutName + "' 자판의 기록에만 적용됩니다. " +
                            "고른 단계까지는 목표 타수를 넘긴 것으로 쳐서, 그 다음 단계 타일과 그때까지의 " +
                            "오락이 열립니다. 실제 기록은 바뀌지 않습니다.";

            // Tag: null = 원래대로(치트 끔), 0 = 1단계도 통과 못함, n = n단계까지 (<260812_10-2>)
            StageCombo.Items.Add(new ComboBoxItem { Content = "원래대로", Tag = null });
            StageCombo.Items.Add(new ComboBoxItem { Content = "1단계도 통과 못함", Tag = 0 });
            for (int i = 0; i < stages.Count; i++)
                StageCombo.Items.Add(new ComboBoxItem { Content = stages[i].Name + " 까지", Tag = i + 1 });

            // 지금 걸려 있는 값을 골라 둔다(전부 열기였던 옛 설정은 화면에는 마지막 단계로 보인다).
            int? current = StageRecords.CheatUpTo;
            originalCheatUpTo = current;
            if (!current.HasValue) StageCombo.SelectedIndex = 0;
            else StageCombo.SelectedIndex = 1 + System.Math.Min(current.Value, stages.Count);

            // 여기서부터 일어나는 선택 변경만 '사용자가 실제로 건드림'으로 친다(위 초기 선택 제외).
            StageCombo.SelectionChanged += (s, e) => userChangedSelection = true;

            ShowMethod();
        }

        /// <summary>
        /// <260812_12> 자리연습의 타속 계산 방법을 고른다. 고르는 즉시 저장되며(확인/취소와 무관),
        /// 지금 걸린 쪽 버튼은 눌리지 않게 해 어느 쪽인지 드러낸다.
        /// </summary>
        private void ShowMethod()
        {
            bool simple = TypingMeasurer.CurrentMethod == TypingMeasurer.MethodSimple;
            SimpleButton.IsEnabled = !simple;
            OriginalButton.IsEnabled = simple;
            MethodText.Text = (simple
                ? "지금은 간단 방식 — 분당 타 = 올바르게 누른 키 수 ÷ 경과 시간(분)"
                : "지금은 원래 방식 — 타속 = (글자수 환산 ÷ 걸린 시간(분)) × 정확도")
                + "\n자리연습·문장연습·긴글연습 모두에 적용됩니다.";
        }

        private void SetMethod(string method)
        {
            UserSettingsStore.TpmMethod = method;
            UserSettingsStore.Save();
            ShowMethod();
        }

        private void Simple_Click(object sender, RoutedEventArgs e) => SetMethod(TypingMeasurer.MethodSimple);

        private void Original_Click(object sender, RoutedEventArgs e) => SetMethod(TypingMeasurer.MethodOriginal);

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
            // 사용자가 단계 선택을 건드리지 않았으면 화면에 보이는(지금 단계 수에 맞춰 잘렸을 수
            // 있는) 값이 아니라 원래 저장돼 있던 값을 그대로 돌려준다 — 안 그러면 옛 '전부 열기'
            // (int.MaxValue)가 아무 것도 바꾸지 않고 확인만 눌러도 지금 단계 수로 조용히 줄어든다.
            Stages = userChangedSelection
                ? (StageCombo.SelectedItem as ComboBoxItem)?.Tag as int?
                : originalCheatUpTo;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

        /// <summary>검사용: 단계 콤보를 건드리지 않고 바로 '확인'을 누른 상황을 재현한다.</summary>
        internal void ConfirmWithoutTouchingForTest() => Ok_Click(this, new RoutedEventArgs());
    }
}
