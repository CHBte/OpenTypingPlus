using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace OpenTyping
{
    /// <summary>
    /// KeyLayoutBox.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class KeyLayoutBox : UserControl
    {
        private List<List<KeyBox>> keyLayout;

        public bool Clickable
        {
            get => (bool)GetValue(ClickableProperty);
            set => SetValue(ClickableProperty, value);
        }
        public static readonly DependencyProperty ClickableProperty =
            DependencyProperty.Register("Clickable", typeof(bool), typeof(KeyLayoutBox), new PropertyMetadata(true));

        public KeyLayoutBox()
        {
            InitializeComponent();
            Loaded += KeyLayoutBox_Loaded; 
        }

        private void KeyLayoutBox_Loaded(object sender, RoutedEventArgs e)
        {
            LoadKeyLayout();
        }

        public void LoadKeyLayout()
        {
            NumberRow.Children.Clear();
            FirstRow.Children.Clear();
            SecondRow.Children.Clear();
            ThirdRow.Children.Clear();

            keyLayout = new List<List<KeyBox>>();

            // 검지손가락 홈 포지션의 물리적 위치: 두벌식 표준 기준 2행(SecondRow)의 3번째(ㄹ=F)와
            // 6번째(ㅓ=J) 칸(0-인덱스). 어떤 자판이든 행/열 구조가 같으므로(4행, 각 행 길이 동일)
            // 이 위치 기준으로 밑줄을 매기면 자판과 무관하게 항상 같은 물리적 자리가 표시된다.
            const int HomeRowIndex = 2;
            const int HomeColumnF = 3;
            const int HomeColumnJ = 6;

            int rowIndex = 0;
            foreach (IList<Key> keyRow in MainWindow.CurrentKeyLayout.KeyLayoutData)
            {
                var keyBoxes = new List<KeyBox>();

                int colIndex = 0;
                foreach (Key key in keyRow)
                {
                    var keyBox = new KeyBox
                    {
                        Key = key,
                        Width = 50,
                        Height = 50,
                        Margin = new Thickness(0, 0, 2, 0),
                        IsHomePosition = rowIndex == HomeRowIndex &&
                                         (colIndex == HomeColumnF || colIndex == HomeColumnJ)
                    };

                    if (Clickable)
                    {
                        keyBox.MouseDown += KeyBox_MouseDown;
                    }

                    keyBoxes.Add(keyBox);
                    colIndex++;
                }

                keyLayout.Add(keyBoxes);
                rowIndex++;
            }

            var keyRows = new List<StackPanel> { NumberRow, FirstRow, SecondRow, ThirdRow };
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < keyLayout[i].Count; j++)
                {
                    if (i == 1 && j == keyLayout[i].Count - 1)
                    {
                        keyLayout[i][j].Width = 70;
                    }
                    keyRows[i].Children.Add(keyLayout[i][j]);
                }
            }

            if (Clickable)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,
                    new Action(PressDefaultKeys));
            }
        }

        public void PressCorrectKey(KeyPos pos)
        {
            keyLayout[pos.Row][pos.Column].PressCorrect();
        }

        public void PressIncorrectKey(KeyPos pos)
        {
            keyLayout[pos.Row][pos.Column].PressIncorrect();
        }

        public void ReleaseKey(KeyPos pos)
        {
            keyLayout[pos.Row][pos.Column].Release();
        }

        public void PressDefaultKeys()
        {
            List<KeyPos> defaultKeys = MainWindow.CurrentKeyLayout.DefaultKeys;

            for (int i = 0; i < keyLayout.Count; i++)
            {
                for (int j = 0; j < keyLayout[i].Count; j++)
                {
                    if (defaultKeys.Contains(new KeyPos(i, j)))
                    {
                        keyLayout[i][j].PressCorrect();
                    }
                }
            }
        }

        public List<KeyPos> PressedKeys()
        {
            var result = new List<KeyPos>();

            for (int i=0; i<keyLayout.Count; i++)
            {
                for (int j=0; j<keyLayout[i].Count; j++)
                {
                    if (keyLayout[i][j].Pressed)
                    {
                        result.Add(new KeyPos(i, j));
                    }
                }
            }

            return result;
        }

        private void KeyBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ((KeyBox)sender).PressToggle();
            MainWindow.CurrentKeyLayout.DefaultKeys = PressedKeys();
        }
    }
}
