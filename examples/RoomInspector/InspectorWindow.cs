using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Qx;
using Qx.Game.Api;
using Qx.Game.Rooms;
using Qx.Game.Snapshots;
using Qx.Hosting;
using Qx.Model;

namespace RoomInspector;

public sealed class InspectorWindow : Window
{
    readonly GameHost host;
    readonly CancellationTokenSource stop = new();
    readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromMilliseconds(250) };
    readonly TextBox search = new() { PlaceholderText = "Search name, class name or furniture ID" };
    readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    readonly TextBlock details = new() { TextWrapping = TextWrapping.Wrap };
    readonly ListBox items = new();
    readonly Button walk = new() { Content = "Walk to tile", IsEnabled = false };
    readonly Button use = new() { Content = "Use furniture", IsEnabled = false };
    readonly Button export = new() { Content = "Export results", IsEnabled = false };
    QueryEnvelope<FurniCollectionSnapshot>? snapshot;
    int pending = 1;
    bool closing;
    bool disposed;

    public InspectorWindow(GameHost host)
    {
        this.host = host;
        Title = "Room Inspector";
        Width = 820;
        Height = 650;
        MinWidth = 620;
        MinHeight = 450;
        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { walk, use, export }
        };
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto"), Margin = new Thickness(20), RowSpacing = 12 };
        Control[] controls = [status, search, items, details, toolbar];
        for (int index = 0; index < controls.Length; index++)
        {
            Grid.SetRow(controls[index], index);
            layout.Children.Add(controls[index]);
        }
        Content = layout;
        search.TextChanged += (_, _) => Filter();
        items.SelectionChanged += (_, _) => Selection();
        walk.Click += (_, _) => Act(false);
        use.Click += (_, _) => Act(true);
        export.Click += async (_, _) => await Export();
        refresh.Tick += (_, _) =>
        {
            if (Interlocked.Exchange(ref pending, 0) != 0) ReadRoom();
        };
        host.Extension.Activated += ActivateWindow;
        host.Extension.InterceptorConnected += InvalidateRoom;
        host.Extension.InterceptorDisconnected += InvalidateRoom;
        host.Extension.Disconnected += InvalidateRoom;
        host.Game.Room.Ready += InvalidateRoom;
        host.Game.Room.Left += InvalidateRoom;
        host.Game.Room.Entering += ItemRemoved;
        host.Game.Room.FloorItemsLoaded += InvalidateRoom;
        host.Game.Room.FloorItemAdded += ItemChanged;
        host.Game.Room.FloorItemUpdated += ItemChanged;
        host.Game.Room.FloorItemRemoved += ItemRemoved;
        host.Game.GameData.Loaded += InvalidateRoom;
        Opened += async (_, _) =>
        {
            refresh.Start();
            ReadRoom();
            try { await host.StartAsync(stop.Token); }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            catch (Exception error) { status.Text = error.Message; }
        };
        Closing += CloseWindow;
    }

    void InvalidateRoom() => Interlocked.Exchange(ref pending, 1);
    void ItemChanged(FloorItem item) => InvalidateRoom();
    void ItemRemoved(Id id) => InvalidateRoom();

    void ActivateWindow() => Dispatcher.UIThread.Post(() =>
    {
        if (closing) return;
        Show();
        WindowState = WindowState.Normal;
        Activate();
    });

    void ReadRoom()
    {
        snapshot = host.Queries.Furni();
        status.Text = snapshot.Error?.Message ?? (!snapshot.Metadata.Ready || snapshot.Metadata.Stale
            ? (!host.Extension.IsInterceptorConnected ? "Waiting for G-Earth. Check that extensions are enabled." : "Connected to G-Earth. Enter a room in Habbo.")
            : $"Room {snapshot.Data?.RoomId} · {snapshot.Data?.FloorItemCount} floor items" +
              (snapshot.Metadata.Loaded ? "" : $" · Still loading: {string.Join(", ", snapshot.Metadata.Pending)}") +
              (snapshot.Metadata.Truncated ? " · Results are capped." : ""));
        Filter();
    }

    void Filter()
    {
        var selected = (items.SelectedItem as FurnitureRow)?.Item.Id;
        string query = search.Text?.Trim() ?? "";
        var rows = snapshot?.Data?.FloorItems
            .Where(item => !item.IsRemoved && (query.Length == 0 ||
                item.Id.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (item.Definition?.Name ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (item.Identifier ?? "").Contains(query, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(item => item.Definition?.Name ?? item.Identifier)
            .ThenBy(item => item.Id)
            .Select(item => new FurnitureRow(item)).ToArray() ?? [];
        items.ItemsSource = rows;
        items.SelectedItem = rows.FirstOrDefault(row => row.Item.Id == selected);
        export.IsEnabled = rows.Length > 0 && snapshot?.Metadata is { Ready: true, Stale: false };
        Selection();
    }

    void Selection()
    {
        var item = (items.SelectedItem as FurnitureRow)?.Item;
        walk.IsEnabled = use.IsEnabled = item is not null && snapshot?.Metadata is { Ready: true, Stale: false };
        details.Text = item is null ? "Select furniture to inspect it." :
            $"{item.Definition?.Name ?? item.Identifier ?? "Unknown furniture"}\n" +
            $"ID {item.Id} · Class {item.Identifier ?? "loading"} · State {item.State}\n" +
            $"Tile {item.Position.X}, {item.Position.Y} · Height {item.Position.Z} · Owner {item.OwnerName}";
    }

    void Act(bool activate)
    {
        if (items.SelectedItem is not FurnitureRow selected || snapshot?.Data is not { } previous) return;
        try
        {
            host.Game.Room.Capture(room =>
            {
                if (!room.IsReady || room.RoomId != previous.RoomId || room.Generation != previous.Generation)
                    throw new InvalidOperationException("The room changed. Select the furniture again.");
                var current = room.FloorItems.FirstOrDefault(item => item.Id == selected.Item.Id);
                if (current is null) throw new InvalidOperationException("That furniture was removed.");
                if (activate) host.Game.RoomActions.UseFloorItem(current.Id);
                else host.Game.RoomActions.Walk(current.X, current.Y);
                return true;
            });
            status.Text = activate ? "Use request sent." : "Walk requested. The server decides whether the tile is reachable.";
        }
        catch (Exception error) { status.Text = error.Message; }
    }

    async Task Export()
    {
        if (snapshot is null || items.ItemsSource is not FurnitureRow[] rows) return;
        var data = new { snapshot.Metadata, snapshot.Data?.RoomId, snapshot.Data?.Generation, Filter = search.Text, Items = rows.Select(row => row.Item).ToArray() };
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export furniture",
                SuggestedFileName = $"room-{snapshot.Data?.RoomId}.json",
                DefaultExtension = "json",
                FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
            });
            if (file is null) return;
            await using var stream = await file.OpenWriteAsync();
            stream.SetLength(0);
            await JsonSerializer.SerializeAsync(stream, data, new JsonSerializerOptions { WriteIndented = true }, stop.Token);
            status.Text = $"Exported {data.Items.Length} items.";
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception error) { status.Text = error.Message; }
    }

    async void CloseWindow(object? sender, WindowClosingEventArgs args)
    {
        if (disposed) return;
        args.Cancel = true;
        if (closing) return;
        closing = true;
        refresh.Stop();
        stop.Cancel();
        host.Extension.Activated -= ActivateWindow;
        host.Extension.InterceptorConnected -= InvalidateRoom;
        host.Extension.InterceptorDisconnected -= InvalidateRoom;
        host.Extension.Disconnected -= InvalidateRoom;
        host.Game.Room.Ready -= InvalidateRoom;
        host.Game.Room.Left -= InvalidateRoom;
        host.Game.Room.Entering -= ItemRemoved;
        host.Game.Room.FloorItemsLoaded -= InvalidateRoom;
        host.Game.Room.FloorItemAdded -= ItemChanged;
        host.Game.Room.FloorItemUpdated -= ItemChanged;
        host.Game.Room.FloorItemRemoved -= ItemRemoved;
        host.Game.GameData.Loaded -= InvalidateRoom;
        try { await host.DisposeAsync(); }
        catch (Exception error) { Console.Error.WriteLine(error.Message); }
        finally
        {
            stop.Dispose();
            disposed = true;
            Close();
        }
    }

    sealed record FurnitureRow(FloorItemSnapshot Item)
    {
        public override string ToString() =>
            $"{Item.Definition?.Name ?? Item.Identifier ?? "Unknown"}   ·   #{Item.Id}   ·   ({Item.Position.X}, {Item.Position.Y})   ·   State {Item.State}";
    }
}
