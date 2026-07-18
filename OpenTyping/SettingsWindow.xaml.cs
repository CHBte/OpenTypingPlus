using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using MahApps.Metro.Controls;
using Microsoft.Win32;
using OpenTyping.Properties;

namespace OpenTyping
{
    /// <summary>
    ///     SettingWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SettingsWindow : MetroWindow, INotifyPropertyChanged
    {
        private ObservableCollection<KeyLayout> keyLayouts;
        public ObservableCollection<KeyLayout> KeyLayouts
        {
            get => keyLayouts;
            private set => SetField(ref keyLayouts, value);
        }

        private string keyLayoutDataDir = (string)Settings.Default[MainWindow.KeyLayoutDataDirStr];
        public string KeyLayoutDataDir
        {
            get => keyLayoutDataDir;
            private set => SetField(ref keyLayoutDataDir, value);
        }

        private ObservableCollection<PracticeData> practiceDataList;
        public ObservableCollection<PracticeData> PracticeDataList
        {
            get => practiceDataList;
            private set => SetField(ref practiceDataList, value);
        }

        private string practiceDataDir = (string)Settings.Default[MainWindow.PracticeDataDirStr];
        public string PracticeDataDir
        {
            get => practiceDataDir;
            private set => SetField(ref practiceDataDir, value);
        } 

        private KeyLayout selectedKeyLayout;
        public KeyLayout SelectedKeyLayout
        {
            get => selectedKeyLayout;
            set => SetField(ref selectedKeyLayout, value);
        }

        private PracticeData selectedPracticeData;
        public PracticeData SelectedPracticeData
        {
            get => selectedPracticeData;
            set => SetField(ref selectedPracticeData, value);
        }

        public bool KeyLayoutUpdated { get; set; }
        public bool KeyLayoutDataDirUpdated { get; set; }

        public SettingsWindow()
        {
            InitializeComponent();

            Closing += OnClose;

            KeyLayouts = new ObservableCollection<KeyLayout>(KeyLayout.LoadFromDirectory(KeyLayoutDataDir));

            var currentKeyLayout = (string)Settings.Default[MainWindow.KeyLayoutStr];
            foreach (KeyLayout item in KeyLayouts)
            {
                if (item.Name == currentKeyLayout)
                {
                    SelectedKeyLayout = item;
                    break;
                }
            }

            PracticeDataList = new ObservableCollection<PracticeData>(PracticeData.LoadFromDirectory(PracticeDataDir));
        }

        // json 데이터 파일을 고르는 열기 대화 상자 공통 구성 (가져오기 두 경로가 동일한 검증 플래그를 쓰도록 한 곳에 둔다)
        private static OpenFileDialog CreateJsonOpenDialog(string title, string filterLabel)
        {
            return new OpenFileDialog()
            {
                Title = title,
                Filter = filterLabel + " (*.json)|*.json",
                Multiselect = false,
                CheckFileExists = true,
                CheckPathExists = true
            };
        }

        // 파일 복사/삭제 실패(잠김·권한 등)가 앱 크래시로 이어지지 않도록 하는 공통 처리
        private static bool TryFileOperation(Action operation, string failureMessage)
        {
            try
            {
                operation();
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show(failureMessage + "\n" + ex.Message,
                                "열린타자+", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void AddKeyLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dataFileDialog = CreateJsonOpenDialog("자판 파일 열기", "자판 데이터 파일");

            if (dataFileDialog.ShowDialog() == true)
            {
                string dataFileLocation = dataFileDialog.FileName;
                string dataFileName = Path.GetFileName(dataFileLocation);
                string destLocation = Path.Combine(KeyLayoutDataDir, dataFileName);

                if (File.Exists(destLocation))
                {
                    MessageBox.Show("같은 이름의 파일이 이미 자판 데이터 경로에 존재합니다.",
                                    "열린타자+",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                }
                else
                {
                    KeyLayout keyLayout;
                    try
                    {
                        // 데이터 경로에 복사하기 전에 원본을 먼저 검증한다.
                        // (잘못된 파일이 복사되면 다음 실행부터 앱이 시작되지 않는다.)
                        keyLayout = KeyLayout.Load(dataFileLocation);
                    }
                    catch (Exception ex) when (ex is InvalidKeyLayoutDataException || ex is KeyLayoutLoadFail ||
                                               ex is IOException || ex is UnauthorizedAccessException)
                    {
                        // IO 예외: 파일이 잠겨 있거나 읽기 권한이 없는 경우 (대화 상자의 존재 검사는 통과했어도 읽기는 실패할 수 있음)
                        MessageBox.Show(ex.Message, "열린타자+", MessageBoxButton.OK, MessageBoxImage.Error);
                        Focus();
                        return;
                    }

                    if (!TryFileOperation(() => File.Copy(dataFileLocation, destLocation),
                                          "자판 데이터 파일을 복사하지 못했습니다."))
                    {
                        Focus();
                        return;
                    }
                    keyLayout.Location = destLocation;
                    KeyLayouts.Add(keyLayout);
                    SelectedKeyLayout = keyLayout;
                }

                Focus();
            }
        }

        private void RemoveKeyLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedKeyLayout is null)
            {
                MessageBox.Show("삭제할 자판 데이터를 먼저 선택해주세요.",
                                "열린타자+",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                return;
            }

            if (KeyLayouts.Count == 1)
            {
                MessageBox.Show("자판 데이터가 한 개 존재하여 삭제할 수 없습니다.",
                                "열린타자+",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                return;
            }

            MessageBoxResult result
                = MessageBox.Show("선택된 자판 데이터 \"" + SelectedKeyLayout.Name + "\" 를 삭제하시겠습니까?",
                                  "열린타자+",
                                  MessageBoxButton.OKCancel,
                                  MessageBoxImage.Warning);
            if (result == MessageBoxResult.OK)
            {
                if (!TryFileOperation(() => File.Delete(SelectedKeyLayout.Location),
                                      "자판 데이터 파일을 삭제하지 못했습니다."))
                {
                    return;
                }
                KeyLayouts.Remove(SelectedKeyLayout);
                SelectedKeyLayout = KeyLayouts[0];
            }
        }

        private void ClearStatButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedKeyLayout is null)
            {
                MessageBox.Show("통계를 삭제할 자판 데이터를 먼저 선택해주세요.",
                                "열린타자+",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                return;
            }

            MessageBoxResult result
                = MessageBox.Show("선택된 자판 데이터 \"" + SelectedKeyLayout.Name + "\" 의 통계 정보를 삭제하시겠습니까?",
                                  "열린타자+",
                                  MessageBoxButton.OKCancel,
                                  MessageBoxImage.Warning);
            if (result == MessageBoxResult.OK)
            {
                SelectedKeyLayout.Stats = new KeyLayoutStats();
                if (!KeyLayout.TrySaveKeyLayout(SelectedKeyLayout, out string error))
                {
                    MessageBox.Show("자판 데이터 파일에 저장하지 못했습니다.\n" + error,
                                    "열린타자+", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                KeyLayoutUpdated = true;
            }
        }

        private void KeyLayoutDataDirButton_Click(object sender, RoutedEventArgs e)
        {
            var dataFileDirDialog = new OpenFolderDialog
            {
                Multiselect = false
            };

            if (dataFileDirDialog.ShowDialog() == true)
            {
                try
                {
                    KeyLayouts =
                        new ObservableCollection<KeyLayout>(KeyLayout.LoadFromDirectory(dataFileDirDialog.FolderName));
                    KeyLayoutDataDir = dataFileDirDialog.FolderName;
                    SelectedKeyLayout = KeyLayouts[0];
                }
                catch (Exception ex)
                {
                    if (ex is KeyLayoutLoadFail || ex is InvalidKeyLayoutDataException)
                    {
                        MessageBox.Show(ex.Message, "열린타자+", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else throw;
                }
            }

            Focus();
        }

        private void AddPracticeDataButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dataFileDialog = CreateJsonOpenDialog(null, "연습 데이터 파일");

            if (dataFileDialog.ShowDialog() == true)
            {
                string dataFileLocation = dataFileDialog.FileName;
                string dataFileName = Path.GetFileName(dataFileLocation);
                string destLocation =
                    Path.Combine(PracticeDataDir, dataFileName);

                if (File.Exists(destLocation))
                {
                    MessageBox.Show("같은 이름의 파일이 이미 연습 데이터 경로에 존재합니다.",
                                    "열린타자+",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                }
                else
                {
                    PracticeData practiceData;
                    try
                    {
                        // 데이터 경로에 복사하기 전에 원본을 먼저 검증한다.
                        practiceData = PracticeData.Load(dataFileLocation);
                    }
                    catch (Exception ex) when (ex is InvalidPracticeDataException || ex is PracticeDataLoadFail ||
                                               ex is IOException || ex is UnauthorizedAccessException)
                    {
                        // IO 예외: 파일이 잠겨 있거나 읽기 권한이 없는 경우 (대화 상자의 존재 검사는 통과했어도 읽기는 실패할 수 있음)
                        MessageBox.Show(ex.Message, "열린타자+", MessageBoxButton.OK, MessageBoxImage.Error);
                        Focus();
                        return;
                    }

                    if (!TryFileOperation(() => File.Copy(dataFileLocation, destLocation),
                                          "연습 데이터 파일을 복사하지 못했습니다."))
                    {
                        Focus();
                        return;
                    }
                    practiceData.Location = destLocation;
                    PracticeDataList.Add(practiceData);
                }

                Focus();
            }
        }

        private void RemovePracticeDataButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPracticeData is null)
            {
                MessageBox.Show("삭제할 연습 데이터를 먼저 선택해주세요.",
                                "열린타자+",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                return;
            }

            if (PracticeDataList.Count == 1)
            {
                MessageBox.Show("연습 데이터가 한 개 존재하여 삭제할 수 없습니다.",
                                "열린타자+",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                return;
            }

            MessageBoxResult result
                = MessageBox.Show("선택된 연습 데이터 \"" + SelectedPracticeData.Name + "\" 를 삭제하시겠습니까?",
                                  "열린타자+",
                                  MessageBoxButton.OKCancel,
                                  MessageBoxImage.Warning);
            if (result == MessageBoxResult.OK)
            {
                if (!TryFileOperation(() => File.Delete(SelectedPracticeData.Location),
                                      "연습 데이터 파일을 삭제하지 못했습니다."))
                {
                    return;
                }
                PracticeDataList.Remove(SelectedPracticeData);
                SelectedPracticeData = null;
            }
        }

        private void PracticeDataDirButton_Click(object sender, RoutedEventArgs e)
        {
            var dataFileDirDialog = new OpenFolderDialog
            {
                Multiselect = false
            };

            if (dataFileDirDialog.ShowDialog() == true)
            {
                try
                {
                    PracticeDataList =
                        new ObservableCollection<PracticeData>(
                            PracticeData.LoadFromDirectory(dataFileDirDialog.FolderName));
                    PracticeDataDir = dataFileDirDialog.FolderName;
                }
                catch (Exception ex)
                {
                    if (ex is PracticeDataLoadFail || ex is InvalidPracticeDataException)
                    {
                        MessageBox.Show(ex.Message, "열린타자+", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else throw; // 자판 데이터 경로 선택과 동일하게, 알 수 없는 예외는 삼키지 않는다
                }
            }

            Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnClose(object sender, CancelEventArgs e)
        {
            // SelectedKeyLayout은 실행 중 자판 파일이 바뀌어 현재 자판을 목록에서 찾지 못하면 null일 수 있다.
            if (SelectedKeyLayout != null &&
                (string)Settings.Default[MainWindow.KeyLayoutStr] != SelectedKeyLayout.Name)
            {
                Settings.Default[MainWindow.KeyLayoutStr] = SelectedKeyLayout.Name;
                KeyLayoutUpdated = true;
            }

            if ((string)Settings.Default[MainWindow.KeyLayoutDataDirStr] != KeyLayoutDataDir)
            {
                Settings.Default[MainWindow.KeyLayoutDataDirStr] = KeyLayoutDataDir;
                KeyLayoutDataDirUpdated = true;
            }

            Settings.Default[MainWindow.PracticeDataDirStr] = PracticeDataDir;

            Settings.Default.Save();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}