using Microsoft.Toolkit.Uwp.UI.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using TabViewTear.Models;
using TabViewTear.Services;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
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

        public bool IsFullScreen
        {
            get { return (bool)GetValue(IsFullScreenProperty); }
            set { SetValue(IsFullScreenProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsFullScreen.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsFullScreenProperty =
            DependencyProperty.Register(nameof(IsFullScreen), typeof(bool), typeof(MainPage), new PropertyMetadata(false));

        public MainPage()
        {
            InitializeComponent();

            // Hide default title bar.
            // https://docs.microsoft.com/en-us/windows/uwp/design/shell/title-bar
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;

            // Register for changes
            coreTitleBar.LayoutMetricsChanged += this.CoreTitleBar_LayoutMetricsChanged;
            CoreTitleBar_LayoutMetricsChanged(coreTitleBar, null);

            coreTitleBar.IsVisibleChanged += this.CoreTitleBar_IsVisibleChanged;

            // Set XAML element as draggable region.
            Window.Current.SetTitleBar(AppTitleBar);

            // Listen for Fullscreen Changes from Shift+Win+Enter or our F11 shortcut
            ApplicationView.GetForCurrentView().VisibleBoundsChanged += this.MainPage_VisibleBoundsChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private ViewLifetimeControl _viewLifetimeControl;

        private MessageEventArgs _lastMsg;

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
            if (e.Item is DataItem data && TabItems.Count > 1) // Don't bother creating a new window if we're the last tab, no-op.
            {
                // Need to serialize item to better provide transfer across window threads.
                var lifetimecontrol = await WindowManagerService.Current.TryShowAsStandaloneAsync(data.Title, typeof(MainPage), JsonConvert.SerializeObject(data));

                // Remove Dragged Tab from this window
                TabItems.Remove(data);
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
            if (e.DataView.Properties.TryGetValue(DataIdentifier, out object value) && value is string str)
            {
                var data = JsonConvert.DeserializeObject<DataItem>(str);

                if (data != null)
                {
                    // First we need to get the position in the List to drop to
                    var listview = sender as TabView;
                    var index = -1;

                    // Determine which items in the list our pointer is inbetween.
                    for (int i = 0; i < listview.Items.Count; i++)
                    {
                        var item = listview.ContainerFromIndex(i) as TabViewItem;

                        if (e.GetPosition(item).X - item.ActualWidth < 0)
                        {
                            index = i;
                            break;
                        }
                    }

                    if (index < 0)
                    {
                        // We didn't find a transition point, so we're at the end of the list
                        TabItems.Add(data);
                    }
                    else if (index < listview.Items.Count)
                    {
                        // Otherwise, insert at the provided index.
                        TabItems.Insert(index, data);
                    }                    

                    Items.SelectedItem = data; // Select new item.

                    // Send message to originator to remove the tab.
                    WindowManagerService.Current.SendMessage((e.DataView.Properties[DataWindow] as int?).Value, CommandClose, e.DataView.Properties[DataIndex]);
                }
            }
        }

        private void OnViewLifetimeControlMessageReceived(object sender, MessageEventArgs e)
        {
            _lastMsg = e; // Store to complete in DragItemsCompleted.
        }

        private async void Items_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            // Remove tab from old window after drag completed, if done when message received, item is not 'back' yet from drag processing.
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

        #region Handle App TitleBar
        private void CoreTitleBar_IsVisibleChanged(CoreApplicationViewTitleBar sender, object args)
        {
            // Adjust our content based on the Titlebar's visibility
            // This is used when fullscreen to hide/show the titlebar when the mouse is near the top of the window automatically.
            Items.Visibility = sender.IsVisible ? Visibility.Visible : Visibility.Collapsed;
            AppTitleBar.Visibility = Items.Visibility;
        }

        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            // Get the size of the caption controls area and back button 
            // (returned in logical pixels), and move your content around as necessary.
            LeftPaddingColumn.Width = new GridLength(sender.SystemOverlayLeftInset);
            RightPaddingColumn.Width = new GridLength(sender.SystemOverlayRightInset);

            // Update title bar control size as needed to account for system size changes.
            AppTitleBar.Height = sender.Height;
        }
        #endregion

        #region Handle FullScreen
        private void MainPage_VisibleBoundsChanged(ApplicationView sender, object args)
        {
            // Update Fullscreen from other modes of adjusting view (keyboard shortcuts)
            IsFullScreen = ApplicationView.GetForCurrentView().IsFullScreenMode;
        }

        private void AppFullScreenShortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            // Toggle FullScreen from F11 Keyboard Shortcut
            if (!IsFullScreen)
            {
                IsFullScreen = ApplicationView.GetForCurrentView().TryEnterFullScreenMode();
            }
            else
            {
                ApplicationView.GetForCurrentView().ExitFullScreenMode();
                IsFullScreen = false;
            }
        }

        private void Button_FullScreen_Click(object sender, RoutedEventArgs e)
        {
            // Redirect to our shortcut key.
            AppFullScreenShortcut(null, null);
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
            TabItems.Add(new DataItem()
            {
                Title = "Item 5",
                Content = "Etiam egestas, tellus ut molestie cursus, odio eros accumsan nulla, ut tempor libero nisi a ante. Sed posuere, velit id dictum lobortis, magna lorem dapibus urna, vitae mattis tellus libero et ligula. Praesent vel orci vehicula, accumsan ipsum ac, venenatis erat. Vestibulum consequat nulla eget arcu accumsan, tempus condimentum nulla euismod. Cras mattis tellus tortor, vitae vulputate lectus vulputate ac. Nunc nisl est, porttitor vitae diam a, pulvinar faucibus augue. Morbi vitae bibendum sem, non porta dolor. Cras turpis sem, rhoncus eget ultrices a, pretium venenatis libero. Fusce convallis eu sapien eu imperdiet. Nullam pulvinar ante a lobortis commodo. Aenean at est vel est faucibus efficitur in eget turpis. In efficitur bibendum dolor vitae dapibus. Mauris dapibus risus sit amet lectus ornare, et eleifend urna pretium. Integer non semper nibh, sit amet bibendum nulla. Nulla facilisi."
            });
            TabItems.Add(new DataItem()
            {
                Title = "Item 6",
                Content = "Integer in pulvinar justo, non venenatis leo. Nam quis pulvinar libero, id laoreet elit. Nunc vehicula vitae lectus et venenatis. Etiam et porta dui. Nulla rutrum lacinia dolor. Nullam convallis libero eget nisi tristique, quis convallis enim finibus. Suspendisse consectetur lorem eleifend sem venenatis ultrices. Interdum et malesuada fames ac ante ipsum primis in faucibus. Nunc ligula urna, aliquam vitae est a, dictum gravida nulla. Sed eu vestibulum nisl. Phasellus rhoncus volutpat mauris, vitae semper quam molestie et. Fusce mattis turpis a congue maximus. Suspendisse justo dui, varius non metus vel, euismod pretium velit."
            });
        }
    }
}
