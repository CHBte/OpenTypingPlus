using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls;

namespace OpenTyping
{
    /// <summary>
    /// <260812_20>(2) 산성비 타자 오락의 단계별 최고 기록을 보여 주는 창.
    /// 문구는 게임 화면과 같은 <see cref="AcidRainWindow.FormatBestRecord"/>를 쓴다.
    /// </summary>
    public partial class BestRecordWindow : MetroWindow
    {
        public BestRecordWindow()
        {
            InitializeComponent();

            List<(int StageId, int Score, string When)> records = AcidRainWindow.LoadBestRecords();
            IArcadeGame game = ArcadeGames.Default;

            if (records.Count == 0)
            {
                RecordPanel.Children.Add(Line("최고 기록이 아직 없습니다"));
                return;
            }

            foreach ((int stageId, int score, string when) in records)
            {
                string stageName = game?.StageName(stageId) ?? (stageId + "단계");
                RecordPanel.Children.Add(Line(stageName, bold: true));
                RecordPanel.Children.Add(Line("    " + AcidRainWindow.FormatBestRecord(score, when)));
            }
        }

        private static TextBlock Line(string text, bool bold = false) => new TextBlock
        {
            Text = text,
            Style = (Style)Application.Current.FindResource("NormalTextBlock"),
            FontSize = bold ? 14 : 15,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            Foreground = bold ? System.Windows.Media.Brushes.Gray : System.Windows.Media.Brushes.Black,
            Margin = new Thickness(0, bold ? 8 : 2, 0, 0),
        };

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
