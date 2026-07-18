using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using OpenTyping.Properties;

namespace OpenTyping
{
    public class PracticeMenuBase : UserControl, INotifyPropertyChanged
    {
        protected ObservableCollection<PracticeData> practiceDataList;
        public ObservableCollection<PracticeData> PracticeDataList
        {
            get => practiceDataList;
            private set => SetField(ref practiceDataList, value);
        }

        protected PracticeData selectedPracticeData;
        public PracticeData SelectedPracticeData
        {
            get => selectedPracticeData;
            set => SetField(ref selectedPracticeData, value);
        }

        public PracticeMenuBase()
        {
            LoadData();
        }

        public void LoadData()
        {
            try
            {
                PracticeDataList =
                    new ObservableCollection<PracticeData>(
                        PracticeData.LoadFromDirectory((string)Settings.Default[MainWindow.PracticeDataDirStr], MainWindow.CurrentKeyLayout.Character));
            }
            catch (Exception ex)
            {
                // 연습 데이터는 없어도 앱은 계속 쓸 수 있고 설정에서 복구할 수 있으므로,
                // 종료하지 않고 알린 뒤 빈 목록으로 진행한다. (설정창을 닫을 때마다 재호출되는 경로이기도 함)
                string message = ex is PracticeDataLoadFail || ex is InvalidPracticeDataException
                    ? ex.Message
                    : "연습 데이터를 불러오는 중 예상하지 못한 오류가 발생했습니다.\n" + ex.Message;

                MessageBox.Show(message, "열린타자+", MessageBoxButton.OK, MessageBoxImage.Error);

                if (PracticeDataList is null)
                {
                    PracticeDataList = new ObservableCollection<PracticeData>();
                }
                // 기존에 불러온 목록이 있으면 그대로 유지한다.
            }

            SelectedPracticeData = null;
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
