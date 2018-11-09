using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using TabViewTear.Models;
using TabViewTear.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace TabViewTear.Views
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        private const string DataIdentifier = "TabData";
        private const string DataIndex = "TabIndex";
        private const string DataWindow = "TabWindow";
        private const string CommandClose = "Close";

        ObservableCollection<DataItem> TabItems = new ObservableCollection<DataItem>();

        public MainPage()
        {
            InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private ViewLifetimeControl _viewLifetimeControl;

        #region Handle Window Lifetime
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            _viewLifetimeControl = e.Parameter as ViewLifetimeControl;
            if (_viewLifetimeControl != null)
            {
                _viewLifetimeControl.StartViewInUse();
                // Register for window close
                _viewLifetimeControl.Released += OnViewLifetimeControlReleased;
                _viewLifetimeControl.MessageReceived += OnViewLifetimeControlMessageReceived;
                // Deserialize passed in item to display in this window
                TabItems.Add(JsonConvert.DeserializeObject<DataItem>(_viewLifetimeControl.Context.ToString()));
                _viewLifetimeControl.Context = null;
                _viewLifetimeControl.StopViewInUse();
            }
            else
            {
                // Main Window Start
                InitializeTestData();

                WindowManagerService.Current.MainWindowMessageReceived += OnViewLifetimeControlMessageReceived;
            }
        }

        private MessageEventArgs _lastMsg;

        private async void OnViewLifetimeControlMessageReceived(object sender, MessageEventArgs e)
        {
            _lastMsg = e; // Store to complete in DragItemsCompleted.
        }

        private async void OnViewLifetimeControlReleased(object sender, EventArgs e)
        {
            _viewLifetimeControl.Released -= OnViewLifetimeControlReleased;
            await WindowManagerService.Current.MainDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                WindowManagerService.Current.SecondaryViews.Remove(_viewLifetimeControl);
            });
        }
        #endregion

        #region Handle Dragging Tab to Create Window
        private async void Items_TabDraggedOutside(object sender, Microsoft.Toolkit.Uwp.UI.Controls.TabDraggedOutsideEventArgs e)
        {
            if (e.Item is DataItem data)
            {
                // Need to serialize item to better provide transfer across window threads.
                var lifetimecontrol = await WindowManagerService.Current.TryShowAsStandaloneAsync(data.Title, typeof(MainPage), JsonConvert.SerializeObject(data));

                // Remove Dragged Tab from this window
                TabItems.Remove(data);

                if (TabItems.Count == 0)
                {
                    // TODO: If drag wasn't received by another window and last tab, ignore?
                    // No tabs left on main window, 'switch' to window just created to hide the main view
                    await ApplicationViewSwitcher.SwitchAsync(lifetimecontrol.Id, ApplicationView.GetForCurrentView().Id, ApplicationViewSwitchingOptions.ConsolidateViews);
                }
            }
        }
        #endregion

        #region Handle Tab Change Updating Window Title
        private void Items_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Update window title with current item
            var first = e.AddedItems.FirstOrDefault();
            if (first is DataItem data)
            {
                ApplicationView.GetForCurrentView().Title = data.Title;
            }
        }
        #endregion

        #region Handle Dragging Tabs between windows
        private void Items_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            // In Initial Window we need to serialize our tab data.
            var item = e.Items.FirstOrDefault();

            if (item is DataItem data)
            {
                // Add actual data
                e.Data.Properties.Add(DataIdentifier, JsonConvert.SerializeObject(data));
                // Add our index so we know where to remove from later (if needed)
                e.Data.Properties.Add(DataIndex, Items.IndexFromContainer(Items.ContainerFromItem(data)));
                // Add Window Id to know if we're transferring to a different window.
                e.Data.Properties.Add(DataWindow, ApplicationView.GetForCurrentView().Id);
            }
        }

        private void Items_DragOver(object sender, DragEventArgs e)
        {
            // Called before we drop to see if we will accept a drop.

            // Do we have Tab Data?
            if (e.DataView.Properties.ContainsKey(DataIdentifier))
            {
                // Tell OS that we allow moving item.
                e.AcceptedOperation = DataPackageOperation.Move;
            }
        }

        private void Items_Drop(object sender, DragEventArgs e)
        {
            // Called when we actually get the drop, let's get the data and add our tab.
            var pos = e.GetPosition(this);

            if (e.DataView.Properties.TryGetValue(DataIdentifier, out object value) && value is string str)
            {
                var data = JsonConvert.DeserializeObject<DataItem>(str);

                if (data != null)
                {
                    //var minpos = pos;
                    //var mindist = 1000.0;
                    //var minindex = -1;
                    //foreach (var item in Items.Items)
                    //{
                    //    var tab = Items.ContainerFromItem(item);
                    //    var p = e.GetPosition(tab as UIElement);
                    //    var amt = Math.Abs(p.X - minpos.X) + Math.Abs(p.Y - minpos.Y);
                    //    if (amt < mindist)
                    //    {
                    //        minindex = Items.IndexFromContainer(tab);
                    //        mindist = amt;
                    //        minpos = p;
                    //    }
                    //}


                    // TODO: Figure out how to insert this in the right place.
                    TabItems.Add(data);

                    Items.SelectedItem = data; // Select new item.

                    // Send message to origintator to remove the tab.
                    WindowManagerService.Current.SendMessage((e.DataView.Properties[DataWindow] as int?).Value, CommandClose, e.DataView.Properties[DataIndex]);
                }
            }
        }

        private async void Items_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            if (args.DropResult == DataPackageOperation.Move && _lastMsg != null)
            {
                switch (_lastMsg.Message)
                {
                    case CommandClose:
                        if (_lastMsg.Data is int value)
                        {
                            TabItems.RemoveAt(value);

                            if (TabItems.Count == 0)
                            {
                                // No tabs left on main window, 'switch' to window just created to hide the main view
                                await ApplicationViewSwitcher.SwitchAsync(_lastMsg.FromId, ApplicationView.GetForCurrentView().Id, ApplicationViewSwitchingOptions.ConsolidateViews);
                            }
                        }

                        _lastMsg = null;
                        break;
                }
            }
        }
        #endregion

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

        private void InitializeTestData()
        {
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
                Content = "Donec tellus nisl, volutpat vel urna eu, vestibulum sollicitudin sapien. Aliquam libero ex, egestas ut dapibus ullamcorper, mattis non nisl. Pellentesque quis hendrerit nibh. In lobortis placerat interdum. Aliquam et eleifend velit. Nunc ipsum orci, auctor eget eros non, euismod accumsan quam. Nam sit amet convallis est. Integer eget mauris pharetra, fringilla elit a, eleifend felis. Nullam vel ex posuere, blandit tellus nec, lobortis mauris. Nulla rhoncus nisi vel leo condimentum, non cursus lacus tempus."
            });
            TabItems.Add(new DataItem()
            {
                Title = "Item 4",
                Content = "Nullam sollicitudin magna dui, imperdiet vulputate arcu pharetra eu. Vivamus lobortis lectus ut diam pretium, ut fermentum est malesuada. Sed eget pretium nisi. Cras eget vestibulum purus. Vivamus tincidunt luctus maximus. Cras erat enim, molestie sit amet tortor sit amet, porttitor tincidunt neque. Nam malesuada odio justo, sed sagittis tellus mollis in. Proin congue enim quis libero faucibus, eu condimentum dolor convallis. Mauris blandit ipsum sit amet maximus convallis. Integer porta dolor id purus hendrerit, a semper mi blandit. In malesuada lacus a tellus interdum, vel consequat turpis molestie. Curabitur eget venenatis massa."
            });
        }
    }
}
