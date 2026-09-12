using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls;

namespace OpenTyping
{
    /// <summary>
    /// <260812_15> 산성비 타자 오락의 치트 창.
    ///  - "파란 글씨 산성비를 50%로": 떨어지는 산성비의 절반을 파란 글씨(특수 단어)로 만든다.
    ///  - "원래대로": 단계별 처치 개수 기준(<260812_13>(1))으로 되돌린다.
    ///  - 아래 체크 상자로 그때 나올 효과를 고른다(체크한 것들이 고루 나온다).
    /// 고른 값은 창을 닫을 때가 아니라 누르는 즉시 게임에 반영된다.
    /// </summary>
    public partial class AcidCheatWindow : MetroWindow
    {
        /// <summary>효과 번호(0~8)와 사람이 읽는 이름. 번호는 AcidRainWindow.TriggerSpecial 과 같다.</summary>
        internal static readonly (int Type, string Name)[] Effects =
        {
            (0, "산성비 가속 (10초)"),
            (1, "산성비 감속 (10초)"),
            (2, "단어 가림 ■ (3초)"),
            (3, "화면 흔들림 (10초)"),
            (4, "낮은 2개가 하늘로 사라짐"),
            (5, "낮은 3개가 튕겨 오름"),
            (6, "낮은 4개가 대각선으로 떨어짐"),
            (7, "새가 단어 하나를 데려감"),
            (8, "메뚜기가 단어 하나를 데려감"),
            (9, "큰 새가 만나는 단어를 모두 데려감"),
            (10, "큰 메뚜기가 만나는 단어를 모두 데려감"),
            (11, "새 두 마리가 양쪽에서 (각자 하나)"),
            (12, "메뚜기 두 마리가 양쪽에서 (각자 하나)"),
            (13, "큰 새 두 마리가 양쪽에서 (모두)"),
            (14, "큰 메뚜기 두 마리가 양쪽에서 (모두)"),
            (15, "지구 환경을 지켜라 — 가운데서 큰 불꽃"),
        };

        private readonly AcidRainWindow game;
        private readonly List<CheckBox> boxes = new List<CheckBox>();

        public AcidCheatWindow(AcidRainWindow game)
        {
            InitializeComponent();
            Title = VersionInfo.WindowTitle; // <2600912_5-1>
            this.game = game;

            foreach ((int type, string name) in Effects)
            {
                var box = new CheckBox
                {
                    Content = name,
                    Tag = type,
                    IsChecked = game.CheatEffects.Contains(type),
                    Margin = new Thickness(0, 3, 0, 3),
                    FontSize = 13,
                };
                box.Checked += Effect_Changed;
                box.Unchecked += Effect_Changed;
                boxes.Add(box);
                EffectPanel.Children.Add(box);
            }

            ShowState();
        }

        private void ShowState()
        {
            StateText.Text = game.CheatHalfSpecial
                ? "지금은 산성비의 50%가 파란 글씨입니다."
                : "지금은 단계별 기준(처치 개수마다 하나)대로 나옵니다.";
            HalfButton.IsEnabled = !game.CheatHalfSpecial;
            NormalButton.IsEnabled = game.CheatHalfSpecial;
        }

        private void Effect_Changed(object sender, RoutedEventArgs e)
        {
            game.CheatEffects.Clear();
            foreach (CheckBox b in boxes.Where(b => b.IsChecked == true))
                game.CheatEffects.Add((int)b.Tag);
            game.ResetCheatEffectBag();   // 예전 선택으로 채워 둔 주머니가 남아 있으면 곧바로 비워 새로 채우게 한다
        }

        /// <summary><260812_27> 체크 상자를 한 번에 켜고 끈다.</summary>
        private void SetAll(bool @checked)
        {
            foreach (CheckBox b in boxes) b.IsChecked = @checked;   // 각 상자의 이벤트가 게임 쪽을 갱신한다
        }

        private void CheckAll_Click(object sender, RoutedEventArgs e) => SetAll(true);

        private void UncheckAll_Click(object sender, RoutedEventArgs e) => SetAll(false);

        internal void CheckAllForTest() => SetAll(true);

        internal void UncheckAllForTest() => SetAll(false);

        private void Half_Click(object sender, RoutedEventArgs e)
        {
            // <260812_23> 체크 상자를 자동으로 켜지 않는다. 하나도 고르지 않았다면 게임 쪽에서
            // 전체를 대상으로 삼는다(AcidRainWindow.NextCheatEffect).
            game.CheatHalfSpecial = true;
            ShowState();
        }

        private void Normal_Click(object sender, RoutedEventArgs e)
        {
            game.CheatHalfSpecial = false;
            ShowState();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
