using System.Windows;
using MahApps.Metro.Controls;

namespace OpenTyping
{
    /// <summary>
    /// MIT 라이선스 전문 창 (<260718_11>). '프로그램 정보' 창에서 모달로 띄우며,
    /// '프로그램 정보로 돌아가기' 버튼으로 닫으면 다시 '프로그램 정보' 창으로 돌아간다.
    /// 저작권 고지("Copyright (c) 2018 Gear")를 포함한 원문을 그대로 싣는다(MIT 준수).
    /// </summary>
    public partial class LicenseWindow : MetroWindow
    {
        // 저장소 루트의 LICENSE 파일 원문. 배포본에는 이 파일이 함께 있지 않을 수 있으므로
        // 어셈블리에 직접 담아 항상 표시되게 한다. 원문을 글자 그대로 유지한다.
        private const string MitLicense =
@"MIT License

Copyright (c) 2018 Gear

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the ""Software""), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.";

        public LicenseWindow()
        {
            InitializeComponent();
            LicenseText.Text = MitLicense;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
