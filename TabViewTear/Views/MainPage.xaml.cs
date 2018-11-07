using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TabViewTear.Models;
using Windows.UI.Xaml.Controls;

namespace TabViewTear.Views
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        ObservableCollection<DataItem> TabItems = new ObservableCollection<DataItem>();

        public MainPage()
        {
            InitializeComponent();

            TabItems.Add(new DataItem()
            {
                Title = "Item 1",
                Content = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Curabitur a consectetur arcu, eu imperdiet nisl. Nunc id interdum odio. Aliquam non vulputate sem. Proin lacinia, lacus vitae finibus malesuada, leo libero interdum nisl, et dictum justo tortor semper tortor. Phasellus suscipit malesuada ultrices. Cras sodales vel lectus quis mattis. Sed consequat mollis ultrices. Nam eleifend purus sit amet massa mattis facilisis. Donec fringilla convallis nibh eget venenatis. Morbi ac venenatis ex. Integer ultrices velit eget dictum ultrices. Nunc aliquet lectus vitae feugiat varius. Nulla erat nisi, scelerisque ut sollicitudin id, vestibulum at mi. Donec neque velit, ornare consectetur aliquet id, egestas nec sapien. Nulla nec magna sed nunc varius bibendum."
            });
            TabItems.Add(new DataItem()
            {
                Title = "Item 2",
                Content = "Aliquam fringilla euismod neque sit amet porta. Aliquam et ligula in neque ullamcorper interdum sit amet et magna. Quisque maximus accumsan lorem at rhoncus. Pellentesque mattis, eros non accumsan auctor, libero turpis sodales urna, id porta mi dolor at elit. Interdum et malesuada fames ac ante ipsum primis in faucibus. Donec lacinia leo arcu, vitae malesuada sapien consequat eget. Pellentesque vestibulum interdum convallis. Mauris nulla elit, tempus sit amet enim finibus, suscipit tempor ante. Nullam pulvinar libero sed tincidunt sagittis. Suspendisse potenti. Nulla porta lacinia lacus vel bibendum. Sed sagittis dignissim leo, ac gravida sem mattis pellentesque."
            });
            TabItems.Add(new DataItem()
            {
                Title = "Item 3",
                Content = "Donec tellus nisl, volutpat vel urna eu, vestibulum sollicitudin sapien. Aliquam libero ex, egestas ut dapibus ullamcorper, mattis non nisl. Pellentesque quis hendrerit nibh. In lobortis placerat interdum. Aliquam et eleifend velit. Nunc ipsum orci, auctor eget eros non, euismod accumsan quam. Nam sit amet convallis est. Integer eget mauris pharetra, fringilla elit a, eleifend felis. Nullam vel ex posuere, blandit tellus nec, lobortis mauris. Nulla rhoncus nisi vel leo condimentum, non cursus lacus tempus. "
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Set<T>(ref T storage, T value, [CallerMemberName]string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return;
            }

            storage = value;
            OnPropertyChanged(propertyName);
        }

        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
