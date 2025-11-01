
using SteamServerManager.Cli;

return await new ServiceCollection()
	.AddAppSettings()
	.AddSerilog()
	.Cli(args, c => c
		.Add<LongRunningVerb>()
		.Add<ProcessTestVerb>()
		.Add<ReadTestVerb>());