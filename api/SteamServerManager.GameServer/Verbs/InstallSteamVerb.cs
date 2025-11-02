using System.IO.Compression;
using System.Security.AccessControl;
using System.Security.Principal;
using SharpCompress;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SteamServerManager.Processing;

namespace SteamServerManager.GameServer.Verbs;

/// <summary>
/// Install SteamCMD on the server.
/// </summary>
[Verb("install-steam", HelpText = "Install SteamCMD")]
public class InstallSteamOptions
{
	/// <summary>
	/// Forces SteamCMD to be re-installed if it is already installed
	/// </summary>
	[Option('f', "force", HelpText = "Forces SteamCMD to be re-installed if it is already installed", Default = false)]
	public bool Force { get; set; } = false;
}

/// <summary>
/// Install SteamCMD on the server.
/// </summary>
internal class InstallSteamVerb(
    ILogger<InstallSteamVerb> logger,
    IApiService _api,
    ISteamService _steam) : BooleanVerb<InstallSteamOptions>(logger)
{
	/// <summary>
	/// Download SteamCMD from the given URL and extract it to the given directory.
	/// </summary>
	/// <param name="url">The URL to download SteamCMD from</param>
	/// <param name="dir">The directory to extract SteamCMD to</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>Whether or not the download failed</returns>
	public async Task<bool> Download(string url, string dir, CancellationToken token)
    {
		void OnFinished(Exception? ex)
        {
			if (ex is null)
			{
				_logger.LogInformation("Finished downloading: {Url}", url);
				return;
			}

			_logger.LogError(ex, "Error occurred while downloading: {Url}", url);
		}

        void OnProgress(int percent, long bytes, TimeSpan elapsed)
        {
            _logger.LogInformation("Download progress: {Percent}% ({Bytes} bytes) in {Elapsed}", percent, bytes, elapsed);
        }

        var tar = url.EndsWithIc(".tar.gz") || url.EndsWithIc(".tgz");

		using var response = await _api.Get(url, c => c
            .OnStarting(() => _logger.LogInformation("Starting download: {Url}", url))
            .OnFinished(OnFinished)
            .ProgressTracking(t => t
                .OnDownloadTimer(OnProgress)
                .ReportIncrement(TimeSpan.FromSeconds(0.5))), token);

        if (response is null)
        {
            _logger.LogError("Failed to download SteamCMD from {Url}", url);
            return false;
		}

        using var stream = await response.Content.ReadAsStreamAsync(token);
        if (tar)
        {
            using var reader = ReaderFactory.Open(stream);
            reader.WriteAllToDirectory(dir, new ExtractionOptions()
            {
                ExtractFullPath = true,
                Overwrite = true
            });
			_logger.LogInformation("SteamCMD extracted to: {Dir}", dir);
			return true;
		}

        using var archive = new ZipArchive(stream);
        await archive.ExtractToDirectoryAsync(dir, true, token);
        _logger.LogInformation("SteamCMD extracted to: {Dir}", dir);
        return true;
	}

    public async Task UpdatePermissions(CancellationToken token)
    {
        var exe = _steam.SteamCmdExe;
        if (!_steam.SteamInstalled) return;

		_logger.LogInformation("Updating permissions for SteamCMD executable >> {Exe}", exe);
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			var fileInfo = new FileInfo(exe);
			var security = fileInfo.GetAccessControl();
            var user = new NTAccount(Environment.UserDomainName + "\\" + Environment.UserName);
            var rule = new FileSystemAccessRule(user,
                FileSystemRights.ExecuteFile | FileSystemRights.ReadAndExecute,
                AccessControlType.Allow);
            security.AddAccessRule(rule);
            fileInfo.SetAccessControl(security);
			_logger.LogInformation("Updated permissions for SteamCMD executable >> {Exe}", exe);
			return;
        }

        var dir = _steam.SteamDir;
        using var proxy = new ProcessProxy("chmod")
            .WithArgs($"777 -R \"{dir}\"")
            .WithLogger(_logger);
        await proxy.Start(token);
        await proxy.WaitForExit(token);
	}

    /// <inheritdoc />
    public override async Task<bool> Execute(InstallSteamOptions options, CancellationToken token)
    {
        var url = _steam.Environment.SteamCmdUrl;
        var exe = _steam.SteamCmdExe;

        if (_steam.SteamInstalled && !options.Force)
        {
            _logger.LogInformation("SteamCMD is already installed >> {Exe}", exe);
            return true;
        }

        if (_steam.SteamInstalled && options.Force)
        {
            _logger.LogInformation("Steam was found, but force-reinstalling was enabled. Cleaning install... >> {Dir}", _steam.SteamDir);
            Directory.Delete(_steam.SteamDir, true);
            Directory.CreateDirectory(_steam.SteamDir);
		}

        _logger.LogInformation("Installing SteamCMD... >> {Exe}", _steam.SteamDir);
        var success = await Download(url, _steam.SteamDir, token);
        if (!success) return false;

        if (!_steam.SteamInstalled)
        {
            _logger.LogError("SteamCMD installation failed, executable not found >> {Exe}", exe);
            return false;
        }

        _logger.LogInformation("SteamCMD installed successfully >> {Exe}", exe);
        await UpdatePermissions(token);
		return success;
	}
}
