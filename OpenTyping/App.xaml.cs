using System;
using System.Windows;
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
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 개별 예외 처리가 놓친 예외로 앱 전체가 죽지 않도록 하는 마지막 안전망.
            DispatcherUnhandledException += (sender, args) =>
            {
                MessageBox.Show("예상하지 못한 오류가 발생했습니다.\n" + args.Exception.Message,
                                "열린타자+",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                args.Handled = true;
            };

            // SkiaSharp 기본 폰트는 한글 글리프가 없어 차트 레이블("타속" 등)이 깨진다.
            // 앱에 내장된 나눔고딕을 리소스 스트림에서 직접 로드해 전역 폰트로 지정한다.
            // (시스템 폰트 열거가 없어 시작이 빠르고, 나머지 UI와 글꼴도 일치한다.)
            LiveCharts.Configure(config => config
                .UseDefaults()
                .HasTextSettings(new TextSettings { DefaultTypeface = LoadChartTypeface() }));
        }

        private static SKTypeface LoadChartTypeface()
        {
            try
            {
                var resource = Application.GetResourceStream(
                    new Uri("pack://application:,,,/Resources/Fonts/NanumGothic.ttf"));
                return resource is null ? null : SKTypeface.FromStream(resource.Stream);
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
