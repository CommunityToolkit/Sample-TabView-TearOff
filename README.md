---
topic: sample
languages:
- csharp
products:
- windows
- uwp
---

TabView Tear-Off Sample
=======================

This sample demonstrates how to use the [Windows Community Toolkit](https://github.com/windows-toolkit/WindowsCommunityToolkit/)'s [TabView](https://docs.microsoft.com/en-us/windows/communitytoolkit/controls/tabview
) control in combination with [Windows Template Studio](https://github.com/microsoft/windowsTemplateStudio)'s [Multiple Views](https://github.com/Microsoft/WindowsTemplateStudio/blob/dev/docs/features/multiple-views.md) support to show how to emulate Microsoft Edge's tear-off tab windowing in your UWP app.

Requirements
------------
Requires VS 2017 and Windows 10 version 16299 or above.

Dependencies
------------
- [Windows Community Toolkit 5.0](https://github.com/windows-toolkit/WindowsCommunityToolkit/)
- [Json.NET](https://www.newtonsoft.com/json)

Considerations
--------------
1. Each Window runs its own Thread, this has implications on data transfer, Window messaging, and UI Page/Control construction.

2. When constructing a new Window, it needs it's own UI shell to be reconstructed.

3. This samples assumes the implementor will be using a collection of custom data items bound to the TabView.

4. This sample assumes all Windows are managed by the same process and shares the same implementation for each Window.

Known Issues
------------
1. Dragging a tab to another monitor/position doesn't open the window on the other monitor/position.

    This is a platform limitation for two reasons, A) we can't determine which monitor the user has dropped the item on, and B) we can't request the window to be opened at a specific location.

2. The right-most tab will disappear when dragging a tab to another window.

    This is a known bug which needs to be resolved in the TabView control, see [Issue #2670](https://github.com/windows-toolkit/WindowsCommunityToolkit/issues/2670).

About the Sample
----------------

For many years, browsers have allowed users to drag tabs out of their windows to move tabs between monitors.  They also let users drag tabs between windows.  This scenario is alluring for other document based apps as well.

This sample demonstrates the main building blocks needed to provide this experience with the new TabView control.  There are a few main technical pieces we need to make this scenario work harmoniously:

1. Detect dragging a Tab out of the window.
2. Create a secondary window to display content.
3. Transfer our tab data to the other window.
4. Move a tab between two existing windows.
5. Close a window if the last tab is moved.

The rest of this article will share how this sample addresses each of these challenges.

## Detecting Tab Drag

Fortunately, this is an easy one as the TabView control [provides a `TabDraggedOutside` event](https://docs.microsoft.com/en-us/windows/communitytoolkit/controls/tabview#events).  We can listen to this event to know when the user has requested a tab to leave its window.

The TabView does this by looking for a drag which had no operation accepted.  This means another window or application didn't accept the drag as a valid operation and in our case is an excellent indicator that the user dragged the tab outside the window and wants to 'tear' it off.

## Create a Secondary Window

Once the user has dragged a tab outside of the window, we need to create a Secondary window in order to display the tab.  We also need to remove this tab from our original window.

Fortunately, the Windows Template Studio provides a feature template ("[Multiple views](https://github.com/Microsoft/WindowsTemplateStudio/blob/dev/docs/features/multiple-views.md
)") for setting up and controlling the life-cycle of Secondary Windows.

In our case, we needed to provide some context for the new window to create itself (the tab's data), so I added a `Context` property to the `ViewLifetimeControl` and modified the `TryShowAsStandaloneAsync` method on `WindowManagerService` to accept this context and add it to the construction of the ViewLifetimeControl.

This allows us in the `OnNavigatedTo` event of our page to grab this context out of the `Parameter` argument when a secondary window is created.

Now we can simply create a new window using our same page type and pass it our data (more on this in the next section):

```
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
```

## Transferring Tab Data

One thing we have to be conscious of when creating a new window is the new window will run on its new thread.  While we can technically pass a reference across to the new window to our tab data, this will cause complications from it being originally created on a different thread when we try and access it again.

To circumvent these issues we use JSON serialization to create a thread neutral package (as a string) to pass data between our windows.  This is pretty painless with the help of the [Json.NET](https://www.newtonsoft.com/json) library.

The sample uses TabView with an `ItemsSource` bound to an `ObservableCollection` of `DataItem` objects.  `DataItem` is a custom class we've used to represent our tab data.  In this example it simply has `Title` and `Content` properties used to represent the tab header and what is displayed within the tab.  However, it could have additional properties.  The important thing is that our data is easily serializable into JSON.

Then we can easily convert between our object and JSON with the following code:

```
// Convert an object to a string
var data = new DataItem() { Title = "Test Tab", Content = "Our content." };
var str = JsonConvert.SerializeObject(data);

// Convert it back
var datanew = JsonConvert.DeserializeObject<DataItem>(str);
```

However, you could use any other serialization technique here, if desired.  JSON is nice as it's still human readable if you need to diagnose any odd problems or also use for saving or interoperate between implementations in other languages or platforms.

## Moving Tabs between Windows

Surprisingly, this is the most difficult task.  There are a number of challenges here in this space:

1. How to enable drag and drop.
2. Storing information about the tab.
3. Creating a new tab when its dropped.
4. Ensuring the tab is dropped where the user wanted it.
5. Closing the tab in the originating window.

### Drag and Drop

To enable the Drag and Drop scenario for our TabView we need to enable the following properties and events in XAML:

```
<controls:TabView
            ...
            CanDragItems="True"
            CanReorderItems="True"
            AllowDrop="True"
            DragItemsStarting="Items_DragItemsStarting"
            DragItemsCompleted="Items_DragItemsCompleted"
            DragOver="Items_DragOver"
            Drop="Items_Drop">
```

The first three will let us drag items out of the TabView and let the TabView accept drops.  The other events are all the ones we need to register to perform different steps of our drag operation and occur in the following order:

The `DragItemsStarting` event is where we will save the data and information about the tab needed to move it to a new window.

The `DragOver` event is needed so the target TabView can accept the drag operation.

The `Drop` event is used by the target TabView to receive the tab data and construct a new tab in its own window.  This is also where we can figure out where the user was trying to drop the tab to put it in the right spot in the target TabView.

Finally, the `DragItemsCompleted` event is called in our originating window. This is where we do our final clean-up and remove the original tab that was now dragged to the Secondary window.

### Tab Info

Fortunately, we can save our tab just like we did in our other case and add it to our drag properties in our `DragItemsStarting` event:

```
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
```

The first argument to each of these `Add` calls is just a string which we've made a constant at the top of the class for convenience and consistency.

We also add information about where the tab was located originally and in which window this tab is from.  This will be all the information we need in order to accomplish our task.

We use properties here rather than storing text as we don't want other applications allowing the tab to be dragged with some text as a target.  This should increase the likelihood that an application won't accept the drag so that we know we want to create a new window.

### Creating a new Tab on Drop

In order to accept a drop, we need to first indicate that we want to accept an incoming drop.  We do this in the `DragOver` event by accepting the operation:

```
private void Items_DragOver(object sender, DragEventArgs e)
{
    // Do we have Tab Data?
    if (e.DataView.Properties.ContainsKey(DataIdentifier))
    {
        // Tell OS that we allow moving item.
        e.AcceptedOperation = DataPackageOperation.Move;
    }
}
```

We simply check if the thing looks like a tab and then if so, say that we'll accept it.

This is a requirement for us to get our `Drop` event next.

The bulk of our `Drop` event in the sample deals with the next section of how we place the tab where the user indicated.  The main part we use to get our tab data is at the top:

```
if (e.DataView.Properties.TryGetValue(DataIdentifier, out object value) && value is string str)
{
    var data = JsonConvert.DeserializeObject<DataItem>(str);
```

Then, we'll then insert the tab data into our `TabItems` collection (more in the next section on that).

And finally, we'll select the tab that was dropped, see below.  However, if we just stopped there the original tab would remain in the first window, so we also need to send a message back to remove it (more on this two sections down).

```
Items.SelectedItem = data; // Select new item.

// Send message to originator to remove the tab.
WindowManagerService.Current.SendMessage((e.DataView.Properties[DataWindow] as int?).Value, CommandClose, e.DataView.Properties[DataIndex]);
```

### Tab Placement during drag

Most Drag and Drop examples to another list simply just add the item dropped to the end of the collection.  This is in contrast to how drag and drop works within a single list.  And the operating system by default shows the nice separation animation to indicate to the user where the item will be dropped in both cases.  We'd like our tabs to respect this request by the user.

To do so, we need to determine where the drop location is in relation to our `TabViewItem` headers.  First we get our `TabView` as the sender of the `Drop` event and create a tracker for which index we should drop our tab into our collection:

```
// First we need to get the position in the List to drop to
var listview = sender as TabView;
var index = -1;
```

Next we loop through each of our `TabViewItem` objects and check their position in relation to our drop point:

```
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
```

Since our tabs our horizontal, we use the `X` value, but this same method works for vertical lists as well (swapping it out to `Y` and `ActualHeight`). 

We are getting the relative position of the item in relation to the drop point, so we subtract the size of the item to understand where the cursor is in relation to the bounding box.  We know that when these values transition to a negative value we're in the vicinity of the mouse cursor's actual location and should use that index to insert our new tab.

If we go through all our tabs and still have positive values, it means our cursor is at the end of the list.  This finally allows us to simply do a check to determine where we need to insert our new tab into our collection:

```
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
```

### Closing the original Tab

The last piece of our big puzzle with transferring a tab between windows is removing the tab from the originating window.  This isn't a simple task because when the `DragItemsCompleted` event fires we have no information to help us distinguish between a drag within the window and a drag to another window. Both signatures and parameters values in both cases are the same.

Therefore, we need our receiving window to send a message to tell us to remove our tab. You may have noticed we used a `SendMessage` command on the `WindowManagerService`.  This is something that we had to add for this scenario.

We first created a new `MessageEventArgs` class which could contain information about a message sent between windows. This contained properties such as `FromId` and `ToId` for storing Window identifiers and `Message` and `Data` to aid in routing and information storage.

**It's important to note**, just like our tab dragging and windowing scenarios, sending messages between windows has the same inherent threading issues.  So, we need to be careful about the type of data we send.

With this structure in place, I added both a `SendMessage` method to the `WindowManagerService` and `ViewLifetimeControl` as well as a `MessageReceived` and `MainMessageReceived` event.  The Main Window of the app is a special case, so it needed its own event that the first window could subscribe to, as seen in our `OnNavigatedTo` event where we detect this condition, subscribe to the event, and initialize our starting tabs.

We now had the infrastructure to send and receive a message between our windows, recalling from before:

```
// Send message to originator to remove the tab.
WindowManagerService.Current.SendMessage((e.DataView.Properties[DataWindow] as int?).Value, CommandClose, e.DataView.Properties[DataIndex]);

// Registered in OnNavigatedTo:
_viewLifetimeControl.MessageReceived += OnViewLifetimeControlMessageReceived;
// Or for Main Window:
WindowManagerService.Current.MainWindowMessageReceived += OnViewLifetimeControlMessageReceived;
```

However, when we receive this message in our drag and drop phase, it's not the right time to act on closing the tab as it has temporarily been removed from the collection already by the drag operation.  Therefore we simply store it in a private variable:

```
private void OnViewLifetimeControlMessageReceived(object sender, MessageEventArgs e)
{
    _lastMsg = e; // Store to complete in DragItemsCompleted.
}
```

Then we can act on it in the case where we detected a move and received a message that it was to another window in our `DragItemsCompleted` event:

```
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
                        // To cover in the next section, as we want to close the window here.
                    }
                }

                _lastMsg = null;
                break;
        }
    }
}
```

Above, we look at the message we received and if its a 'Close' command (our only one right now) then we remove the tab at the specified index (which we had set originally back in our `DragItemsStarting` event and passed forward).

Now, we have a functioning drag of a tab across to our other window!

## 'Closing' windows no longer needed

What happens when we drag the last tab out of our window?  We normally would expect in this pattern to close the window.

However, UWP doesn't provide a straight-forward way to tell a window to close, especially our Main Window.

We can use the following trick though to Consolidate our view to another using the `ApplicationViewSwitcher.SwitchAsync` method.  This will let us specify that we want to really be showing a different view instead of our current one.  And if that view is already open, then it should just clean-up our old one...

```
// No tabs left on main window, 'switch' to window just created to hide the main view
await ApplicationViewSwitcher.SwitchAsync(_lastMsg.FromId, ApplicationView.GetForCurrentView().Id, ApplicationViewSwitchingOptions.ConsolidateViews);
```

With this simple call, we've now cleaned up our empty view and finished our example.
