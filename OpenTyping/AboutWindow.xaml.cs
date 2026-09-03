using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using MahApps.Metro.Controls;

namespace OpenTyping
{
    /// <summary>
    /// '프로그램 정보' 창 (<260718_11>). 버전·빌드 시각은 어셈블리 메타데이터에서 읽어 표시하고,
    /// '라이선스' 버튼으로 MIT 라이선스 전문 창(LicenseWindow)을 모달로 띄운다(닫으면 이 창으로 복귀).
    /// </summary>
    public partial class AboutWindow : MetroWindow
    {
        public AboutWindow()
        {
            InitializeComponent();

            string displayVersion = ReadMetadata("DisplayVersion") ?? "0.5.0";
            VersionText.Text = "Ver " + displayVersion + " win64 (C# .NET 10)";

            string buildTimestamp = ReadMetadata("BuildTimestamp");
            BuildText.Text = string.IsNullOrEmpty(buildTimestamp)
                ? "빌드: (알 수 없음)"
                : "빌드: " + buildTimestamp;
        }

        /// <summary>어셈블리에 주입된 AssemblyMetadata(key) 값을 읽는다(csproj의 SetBuildTimestamp 타깃).</summary>
        private static string ReadMetadata(string key)
        {
            return Assembly.GetExecutingAssembly()
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == key)?.Value;
        }

        private void LicenseButton_Click(object sender, RoutedEventArgs e)
        {
            var licenseWindow = new LicenseWindow { Owner = this };
            licenseWindow.ShowDialog(); // 닫히면 이 '프로그램 정보' 창으로 되돌아온다
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
