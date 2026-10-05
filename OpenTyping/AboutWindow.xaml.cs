using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;
using MahApps.Metro.Controls;

namespace OpenTyping
{
    /// <summary>
    /// '프로그램 정보' 창 (<260718_11>). 버전은 <see cref="VersionInfo"/>(AssemblyVersion 그대로,
    /// <2600912_5-1>), 빌드 시각은 어셈블리 메타데이터에서 읽어 표시하고, '라이선스' 버튼으로 MIT
    /// 라이선스 전문 창(LicenseWindow)을 모달로 띄운다(닫으면 이 창으로 복귀).
    /// </summary>
    public partial class AboutWindow : MetroWindow
    {
        public AboutWindow()
        {
            InitializeComponent();

            VersionText.Text = "Ver " + VersionInfo.Version4 + " win64 (C# .NET 10)";

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

        /// <summary>
        /// 제작자 줄의 링크(<261005_3>)를 클릭하면 그 주소를 기본 브라우저로 연다. 주소는 XAML에 적은 고정 값이며,
        /// 그래도 http(s) 주소만 연다. 브라우저를 못 열어도 이 창은 그대로 둔다.
        /// </summary>
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            e.Handled = true;
            Uri uri = e.Uri;
            if (uri == null || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                return;
            try
            {
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception)
            {
                // 기본 브라우저가 없거나 실행이 막힌 경우 — 정보 창은 그대로 둔다.
            }
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
