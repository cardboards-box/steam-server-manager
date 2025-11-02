using SteamServerManager.Cli;
using SteamServerManager.GameServer;
using SteamServerManager.Processing;

using var tsc = new CancellationTokenSource();
if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
	PosixSignalRegistration.Create(PosixSignal.SIGINT, _ => tsc.Cancel());
else
	Console.CancelKeyPress += (_, e) =>
	{
		e.Cancel = true;
		tsc.Cancel();
	};

return await new ServiceCollection()
	.AddCore()
	.AddAppSettings()
	.AddSerilog()
	.AddProcessing()
	.AddGame()
	.Cli(args, c => c
		.Add<LongRunningVerb>()
		.Add<ProcessTestVerb>()
		.Add<ReadTestVerb>()
		.AddGame(),
		tsc.Token);