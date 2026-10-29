using ChatCommands;
using Qx.Game.Messages;
using Qx.Hosting;
using Qx.Interception.GEarth;

using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    stop.Cancel();
};

await using var host = new GameHost(new GameHostOptions
{
    GEarth = GEarthOptions.Parse(args, new GEarthOptions
    {
        Title = "Local Chat Commands",
        Description = "Private room tools: .qx help",
        Author = "QX",
        Version = "1.0.0"
    }),
    ReconnectTransport = true
});

await using var commands = new Commands(host, Console.WriteLine);
using var subscription = host.Extension.Intercept(MessageContracts.Room.Chat.TalkSend.Key, intercept =>
{
    int position = 0;
    var reader = intercept.Packet.ReaderAt(ref position);
    var message = MessageContracts.Room.Chat.TalkSend.Parse(in reader);
    if (!Commands.IsCommand(message.Text)) return;
    intercept.Block();
    if (!commands.Enqueue(message.Text)) Console.WriteLine("Command queue is full. Try again shortly.");
});

Console.WriteLine("Type .qx help in the hotel chat. Commands stay local; output appears here.");
try
{
    await host.StartAsync(stop.Token);
    await host.TransportTask.WaitAsync(stop.Token);
}
catch (OperationCanceledException) when (stop.IsCancellationRequested)
{
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    Environment.ExitCode = 1;
}
