using Mono.Unix.Native;

namespace SteamServerManager.Processing;

using Exceptions;
using Signaling;

/// <summary>
/// Represents a proxy for managing processes
/// </summary>
/// <param name="startInfo">The information used for starting the process</param>
/// <param name="maxErrors">The maximum number of errors to track</param>
/// <remarks>
/// <para>The following properties on the <see cref="ProcessStartInfo"/> parameter (<paramref name="startInfo"/>) will be overwritten:</para>
/// <para><see cref="ProcessStartInfo.CreateNoWindow"/> will always be <see langword="true"/></para>
/// <para><see cref="ProcessStartInfo.RedirectStandardError"/> will always be <see langword="true"/></para>
/// <para><see cref="ProcessStartInfo.RedirectStandardInput"/> will always be <see langword="true"/></para>
/// <para><see cref="ProcessStartInfo.RedirectStandardOutput"/> will always be <see langword="true"/></para>
/// <para><see cref="ProcessStartInfo.UseShellExecute"/> will always be <see langword="false"/></para>
/// <para><see cref="ProcessStartInfo.WindowStyle"/> will always be <see cref="ProcessWindowStyle.Hidden"/></para>
/// </remarks>
public class ProcessProxy(
	ProcessStartInfo startInfo,
	int maxErrors = ProcessProxy.MAX_ERRORS_TRACKED) : TextWriter, IAsyncDisposable, IDisposable
{
	#region Constants
	/// <summary>
	/// The maximum number of errors to track by default
	/// </summary>
	public const int MAX_ERRORS_TRACKED = 10;
	/// <summary>
	/// The file name for executing PowerShell scripts
	/// </summary>
	public const string POWERSHELL_FILENAME = "powershell.exe";
	/// <summary>
	/// Command arguments for executing PowerShell scripts
	/// </summary>
	public const string POWERSHELL_ARGUMENTS = "-NoProfile -ExecutionPolicy unrestricted";
	/// <summary>
	/// Command arguments for executing encoded in-line PowerShell scripts
	/// </summary>
	public const string POWERSHELL_ARGUMENTS_ENCODED = POWERSHELL_ARGUMENTS + " -EncodedCommand {0}";
	/// <summary>
	/// Command arguments for executing PowerShell script files
	/// </summary>
	public const string POWERSHELL_ARGUMENTS_FILE = POWERSHELL_ARGUMENTS + " -File {0}";
	/// <summary>
	/// The default encoding to use for all process proxies
	/// </summary>
	public static Encoding DefaultEncoding { get; set; } = Encoding.UTF8;
	#endregion

	#region Fields
	private readonly Stopwatch _timer = new();
	private readonly CancellationTokenSource _cancel = new();
	private readonly SemaphoreSlim _accessControl = new(1, 1);
	private readonly SemaphoreSlim _cleanControl = new(1, 1);
	private readonly FixedQueue<ProcessError> _errors = new(maxErrors);
	private readonly Subject<string> _standardOutput = new();
	private readonly Subject<string> _standardError = new();
	private readonly Subject<int> _started = new();
	private readonly BehaviorSubject<ProcessResult> _exited = new(ProcessResult.Default);
	private readonly Subject<ProcessError> _exception = new();

	private IObservable<string>? _standardOutputObs;
	private IObservable<string>? _standardErrorObs;
	private IObservable<int>? _startedObs;
	private IObservable<ProcessResult>? _exitedObs;
	private IObservable<ProcessError>? _exceptionObs;
	private Process? _process;
	private Task? _task;
	private Encoding? _encoding;
	private string? _logName;
	#endregion

	#region Properties
	/// <summary>
	/// Triggered whenever a line is received from standard output
	/// </summary>
	public IObservable<string> OnStandardOutput => _standardOutputObs ??= _standardOutput.AsObservable();
	/// <summary>
	/// Triggered whenever a line is received from standard error
	/// </summary>
	public IObservable<string> OnStandardError => _standardErrorObs ??= _standardError.AsObservable();
	/// <summary>
	/// Triggered when the process has started
	/// </summary>
	public IObservable<int> OnStarted => _startedObs ??= _started.AsObservable();
	/// <summary>
	/// Triggered when the process has exited
	/// </summary>
	public IObservable<ProcessResult> OnExited => _exitedObs ??= _exited.AsObservable().Skip(1);
	/// <summary>
	/// Triggered when an exception occurs during process execution
	/// </summary>
	public IObservable<ProcessError> OnException => _exceptionObs ??= _exception.AsObservable();
	/// <summary>
	/// The amount of time the process has been running
	/// </summary>
	public TimeSpan Elapsed => _timer.Elapsed;
	/// <summary>
	/// Indicates whether or not cancellation has been requested for the process
	/// </summary>
	public bool Cancelled => _cancel.IsCancellationRequested;
	/// <summary>
	/// The exit code of the process or -1 if the process has not exited
	/// </summary>
	public int ExitCode => _process?.ExitCode ?? -1;
	/// <summary>
	/// The writer to the standard input of the process
	/// </summary>
	/// <remarks>Only available if <see cref="Running"/> is <see langword="true"/></remarks>
	public StreamWriter? Writer => _process?.StandardInput;
	/// <summary>
	/// Whether or not the process is currently running
	/// </summary>
	public bool Running { get; private set; } = false;
	/// <summary>
	/// The exception that was thrown by the process
	/// </summary>
	public ProcessError? LastError => _errors.LastOrDefault();
	/// <summary>
	/// The last 10 errors that have occurred within the process proxy
	/// </summary>
	public IEnumerable<ProcessError> Errors => _errors;
	/// <summary>
	/// The result of the process at the current point in time
	/// </summary>
	/// <remarks>If the process hasn't exited, the <see cref="ProcessResult.ExitCode"/> will be -1</remarks>
	public ProcessResult Result => _exited.Value;
	/// <summary>
	/// The name of the file being executed
	/// </summary>
	public string FileName => startInfo.FileName;
	/// <summary>
	/// The arguments being passed to the executable
	/// </summary>
	public string Arguments => string.IsNullOrEmpty(startInfo.Arguments) ? string.Join(' ', startInfo.ArgumentList) : startInfo.Arguments;
	/// <inheritdoc />
	public override Encoding Encoding => _encoding ??= DefaultEncoding;
	/// <summary>
	/// The name to use for logging purposes
	/// </summary>
	public string Name
	{
		get => _logName ??= FileName;
		set => _logName = value;
	}
	/// <summary>
	/// Whether or not the process has exited
	/// </summary>
	public bool HasExited => CheckHasExited();
	#endregion

	/// <summary>
	/// Represents a proxy for managing processes
	/// </summary>
	/// <param name="command">The executable to run in the new process</param>
	/// <param name="maxErrors">The maximum number of errors to track</param>
	public ProcessProxy(
		string command,
		int maxErrors = MAX_ERRORS_TRACKED) 
		: this(new ProcessStartInfo { FileName = command }, maxErrors) { }

    #region Configuration methods
    /// <summary>
    /// Sets the working directory of the process
    /// </summary>
    /// <param name="directory">The directory to use</param>
    /// <returns>The current proxy for method chaining</returns>
    public ProcessProxy WithWorkingDirectory(string? directory)
	{
		startInfo.WorkingDirectory = directory ?? string.Empty;
		return this;
	}

	/// <summary>
	/// Sets the command line arguments for the proxy
	/// </summary>
	/// <param name="arguments">The arguments to use</param>
	/// <returns>The current proxy for method chaining</returns>
	public ProcessProxy WithArgs(string? arguments)
	{
		startInfo.Arguments = arguments ?? string.Empty;
		return this;
	}

	/// <inheritdoc cref="WithArgs(string?)"/>
	public ProcessProxy WithArgs(params string?[] arguments)
	{
		foreach (var arg in arguments)
			if (!string.IsNullOrWhiteSpace(arg))
				startInfo.ArgumentList.Add(arg);
		return this;
	}

	/// <summary>
	/// Sets the username for the process
	/// </summary>
	/// <param name="username">The username to use for the process</param>
	/// <returns>The current proxy for method chaining</returns>
	public ProcessProxy WithUser(string? username)
	{
		startInfo.UserName = username ?? string.Empty;
		return this;
	}

	/// <summary>
	/// Sets the given environment variable for the process
	/// </summary>
	/// <param name="key">The key of the variable</param>
	/// <param name="value">The value of the variable</param>
	/// <returns>The current proxy for method chaining</returns>
	public ProcessProxy WithEnvArg(string key, string? value)
	{
		startInfo.Environment[key] = value;
		return this;
	}

	/// <summary>
	/// Sets the given environment variables for the process
	/// </summary>
	/// <param name="args">The environment variables to set</param>
	/// <returns>The current proxy for method chaining</returns>
	public ProcessProxy WithEnvArgs(params (string key, string? value)[] args)
	{
		foreach (var (key, value) in args)
			WithEnvArg(key, value);
		return this;
	}

	/// <summary>
	/// Sets the given environment variables for the process
	/// </summary>
	/// <param name="args">The environment variables to set</param>
	/// <returns>The current proxy for method chaining</returns>
	public ProcessProxy WithEnvArgs(IEnumerable<KeyValuePair<string, string?>> args)
	{
		foreach (var (key, value) in args)
			WithEnvArg(key, value);
		return this;
	}

    /// <summary>
    /// Registers a logger for the process events
    /// </summary>
    /// <param name="logger">The logger to log the events to</param>
    /// <param name="name">The name of the logger to use</param>
    /// <returns>The current proxy for method chaining</returns>
    [SuppressMessage("Performance", "CA1873:Avoid potentially expensive logging", Justification = "Stupid warning")]
    public ProcessProxy WithLogger(ILogger logger, string? name = null)
	{
		if (!string.IsNullOrEmpty(name))
			Name = name;
		OnStandardOutput.Subscribe((line) => logger.LogInformation("[{ProcessName}::STDOUT] {Line}", Name, line));
		OnStandardError.Subscribe((line) => logger.LogWarning("[{ProcessName}::STDERR] {Line}", Name, line));
		OnException.Subscribe((error) => logger.LogError(error.Exception, "[{ProcessName}::EXCEPTION] An exception occurred: {ErrorCode}", Name, error.Code));
		OnExited.Subscribe((res) => logger.LogInformation("[{ProcessName}::EXITED] Process has exited >> {Code} ({Success})", Name, res.ExitCode, res.Success ? "Success" : "Failed"));
		OnStarted.Subscribe((id) => logger.LogInformation("[{ProcessName}::STARTED] Process has started.", Name));
		return this;
	}

	/// <summary>
	/// Writes everything from <see cref="OnStandardOutput"/> to the given <see cref="StringBuilder"/>
	/// </summary>
	/// <param name="builder">The string builder to write to</param>
	/// <param name="disposer">The subscription's disposable instance</param>
	/// <returns>The current proxy for method chaining</returns>
	public ProcessProxy WithStandardOutput(StringBuilder builder, out IDisposable disposer)
	{
		disposer = OnStandardOutput.Subscribe((line) => builder.AppendLine(line));
		return this;
	}

	/// <summary>
	/// Writes everything from <see cref="OnStandardOutput"/> to the given <see cref="TextWriter"/>
	/// </summary>
	/// <param name="writer">The <see cref="TextWriter"/> to write to</param>
	/// <param name="disposer">The subscription's disposable instance</param>
	/// <returns>The current proxy for method chaining</returns>
	public ProcessProxy WithStandardOutput(TextWriter writer, out IDisposable disposer)
	{
		disposer = OnStandardOutput.Subscribe(writer.WriteLine);
		return this;
	}

	/// <summary>
	/// Writes everything from <see cref="OnStandardError"/> to the given <see cref="StringBuilder"/>
	/// </summary>
	/// <param name="builder">The string builder to write to</param>
	/// <param name="disposer">The subscription's disposable instance</param>
	/// <returns>The current proxy for method chaining</returns>
	public ProcessProxy WithStandardError(StringBuilder builder, out IDisposable disposer)
	{
		disposer = OnStandardError.Subscribe((line) => builder.AppendLine(line));
		return this;
	}

	/// <summary>
	/// Writes everything from <see cref="OnStandardError"/> to the given <see cref="TextWriter"/>
	/// </summary>
	/// <param name="writer">The <see cref="TextWriter"/> to write to</param>
	/// <param name="disposer">The subscription's disposable instance</param>
	/// <returns>The current proxy for method chaining</returns>
	public ProcessProxy WithStandardError(TextWriter writer, out IDisposable disposer)
	{
		disposer = OnStandardError.Subscribe(writer.WriteLine);
		return this;
	}
	#endregion

	#region Public Methods
	/// <summary>
	/// Attempts to start the process
	/// </summary>
	/// <param name="token">The cancellation token to use for the process</param>
	/// <returns>
	/// <para><see langword="true"/> if the process was started.</para>
	/// <para><see langword="false"/> if the process was already running or an exception occurred</para>
	/// </returns>
	public async Task<bool> Start(CancellationToken token = default)
	{
		var tsc = new TaskCompletionSource();
		token.Register(() => tsc.TrySetCanceled());

		try
		{
			await _accessControl.WaitAsync(token);

			if (Running) return false;

			token.Register(_cancel.Cancel);
			Running = true;

			EnsureStartArgs();
			_process = new Process
			{
				StartInfo = startInfo,
				EnableRaisingEvents = true,
			};
			_process.OutputDataReceived += (_, args) =>
			{
				if (string.IsNullOrEmpty(args.Data)) return;
                _standardOutput.OnNext(args.Data);
			};
			_process.ErrorDataReceived += (_, args) =>
			{
				if (string.IsNullOrEmpty(args.Data)) return;
				_standardError.OnNext(args.Data);
			};

			using var sub = OnStarted.Subscribe((_) => tsc.TrySetResult());
			_task = Task.Run(ExecuteThread, CancellationToken.None);
			await tsc.Task;
			return true;
		}
		catch (Exception ex)
		{
			SetError(ProcessErrorCode.StartFailed, ex);
			return false;
		}
		finally
		{
			_accessControl.Release();
		}
	}

	/// <summary>
	/// Attempts to gracefully stop the process
	/// </summary>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>
	/// <para><see langword="true"/> if the process was asked to gracefully stop or the process is already stopped</para>
	/// <para><see langword="false"/> if no appropriate termination was available (use <see cref="Kill"/> to forcefully kill the process)</para>
	/// </returns>
	/// <remarks>
	/// <para>This method will attempt the following in order: SIG-INTERUPT, SIG-ABORT, SIG-TERM, <see cref="Process.CloseMainWindow"/></para>
	/// <para>If all attempts fail, the method will return <see langword="false"/></para>
	/// <para>This method returning does not mean the process has exited, be sure to await <see cref="WaitForExit(CancellationToken)"/> afterward for that.</para>
	/// </remarks>
	public async Task<bool> Stop(CancellationToken token = default)
	{
		bool Send(Signal signal, ProcessErrorCode err)
		{
			try
			{
				return SendSignal(signal, out var code) && code == 0;
			}
			catch (Exception ex)
			{
				SetError(err, ex);
				return false;
			}
		}

		bool SendCloseMainWindow()
		{
			try { return _process!.CloseMainWindow(); }
			catch (Exception ex)
			{
				SetError(ProcessErrorCode.Stop_CloseMainWindow, ex);
				return false;
			}
		}

		try
		{
			await _accessControl.WaitAsync(token);
			if (HasExited)
				return true;

			Func<bool>[] tries =
			[
				() => Send(Signal.Interupt, ProcessErrorCode.Stop_Interupt),
				() => Send(Signal.Abort, ProcessErrorCode.Stop_Abort),
				() => Send(Signal.Terminate, ProcessErrorCode.Stop_Term),
				SendCloseMainWindow,
			];

			foreach (var action in tries)
			{
				if (action())
					return true;
			}

			return false;
		}
		catch (Exception ex)
		{
			SetError(ProcessErrorCode.Stop, ex);
			return false;
		}
		finally
		{
			_accessControl.Release();
		}
	}

	/// <summary>
	/// Attempts to forcefully kill the process
	/// </summary>
	/// <returns>
	/// <para><see langword="true"/> if the process was killed or is already stopped</para>
	/// <para><see langword="false"/> if the process threw an error while attempting to kill</para>
	/// </returns>
	public async Task<bool> Kill(CancellationToken token = default)
	{
		try
		{
			await _accessControl.WaitAsync(token);

			if (HasExited)
				return true;

			_process!.Kill(true);
			return true;
		}
		catch (Exception ex)
		{
			SetError(ProcessErrorCode.Kill, ex);
			return false;
		}
		finally
		{
			_accessControl.Release();
		}
	}

	/// <summary>
	/// Waits until the process is closed
	/// </summary>
	/// <param name="token">The cancellation token for the wait operation</param>
	/// <remarks>
	/// <para>Prefer hooking <see cref="OnExited"/></para>
	/// <para><see cref="OperationCanceledException"/> is not thrown when the <paramref name="token"/> is cancelled.</para>
	/// </remarks>
	public async Task WaitForExit(CancellationToken token = default)
	{
		if (_task is null || !Running) return;

		try
		{
			await Task.WhenAny(
				_task, 
				Task.Delay(-1, token));
		}
		catch (OperationCanceledException) { }
	}

	/// <summary>
	/// Waits until the process is closed and returns the result
	/// </summary>
	/// <param name="token">The cancellation token for the wait operation</param>
	/// <returns>The result of the process</returns>
	/// <remarks>
	/// <para><see cref="OperationCanceledException"/> is not thrown when the <paramref name="token"/> is cancelled.</para>
	/// </remarks>
	public async Task<ProcessResult> WaitForResult(CancellationToken token = default)
	{
		await WaitForExit(token);
		return Result;
	}

	/// <summary>
	/// Sends a signal to the process based on the current platform
	/// </summary>
	/// <param name="signal">The signal to send</param>
	/// <param name="code">The return code of the call, or -1 if something went wrong</param>
	/// <returns>Whether or not the signal was sent, regardless of the result of the siganl</returns>
	/// <remarks>If this returns <see langword="false"/> you can check <see cref="LastError"/> for more information.</remarks>
	/// <exception cref="NotSupportedException">Thrown if the signal isn't supported on the current platform</exception>
	public bool SendSignal(Signal signal, out int code)
	{
		code = -1;
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			WindowsSignal? ws = signal switch
			{
				Signal.Abort => WindowsSignal.CTRL_CLOSE_EVENT,
				Signal.Terminate => WindowsSignal.CTRL_SHUTDOWN_EVENT,
				Signal.Interupt => WindowsSignal.CTRL_C_EVENT,
				_ => null
			};

			if (ws is not null)
				return SendSignalWindows(ws.Value, out code);

			SetError(ProcessErrorCode.Signal_NotSupported, new NotSupportedWindowsException($"Signal {signal}"));
			return false;
		}

		Signum? ps = signal switch
		{
			Signal.Abort => Signum.SIGABRT,
			Signal.Terminate => Signum.SIGTERM,
			Signal.Interupt => Signum.SIGINT,
			_ => null
		};

		if (ps is not null)
			return SendSignalPosix(ps.Value, out code);

		SetError(ProcessErrorCode.Signal_NotSupported, new NotSupportedPosixException($"Signal {signal}"));
		return false;
	}

	/// <summary>
	/// Attempts to send a Windows signal to process
	/// </summary>
	/// <param name="signal">The signal to send to the process</param>
	/// <param name="code">The return code of the call, or -1 if something went wrong</param>
	/// <returns>Whether or not the signal was sent, regardless of the result of the siganl</returns>
	/// <remarks>
	/// <para>This does not work on POSIX/UNIX, use either <see cref="SendSignalPosix(Signum, out int)"/> or <see cref="SendSignal(Signal, out int)"/></para>
	/// <para>If this returns <see langword="false"/> you can check <see cref="LastError"/> for more information.</para>
	/// </remarks>
	public bool SendSignalWindows(WindowsSignal signal, out int code)
	{
		if (HasExited)
		{
			code = -1;
			SetError(ProcessErrorCode.Signal_ProcessNotRunning, new ProcessNotRunningException());
			return false;
		}

		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			code = -1;
			SetError(ProcessErrorCode.Signal_NotSupported, new NotSupportedPosixException("Windows signals"));
			return false;
		}

		code = WindowsConsoleInterop.SendSignal(_process!, signal);
		return true;
	}

	/// <summary>
	/// Attempts to send a POSIX signal to process
	/// </summary>
	/// <param name="signal">The signal to send to the process</param>
	/// <param name="code">The return code of the call, or -1 if something went wrong</param>
	/// <returns>Whether or not the signal was sent, regardless of the result of the siganl</returns>
	/// <remarks>
	/// <para>This does not work on Windows, use either <see cref="SendSignalWindows(WindowsSignal, out int)"/> or <see cref="SendSignal(Signal, out int)"/></para>
	/// <para>If this returns <see langword="false"/> you can check <see cref="LastError"/> for more information.</para>
	/// </remarks>
	public bool SendSignalPosix(Signum signal, out int code)
	{
		if (HasExited)
		{
			code = -1;
			SetError(ProcessErrorCode.Signal_ProcessNotRunning, new ProcessNotRunningException());
			return false;
		}

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			code = -1;
			SetError(ProcessErrorCode.Signal_NotSupported, new NotSupportedWindowsException("POSIX/UNIX signals"));
			return false;
		}

		code = Syscall.kill(_process!.Id, signal);
		return true;
	}

	/// <summary>
	/// Reads all lines from the <see cref="Process.StandardOutput"/>
	/// </summary>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>All of the lines read from standard output</returns>
	public IAsyncEnumerable<string> ReadAllOutputLines(CancellationToken token = default)
	{
		var (channel, subs) = Channelable<string>();
		subs.Add(OnStandardOutput.Subscribe(async t => await channel.Writer.WriteAsync(t, token)));
		return channel.Reader.ReadAllAsync(token);
	}

	/// <summary>
	/// Reads all lines from the <see cref="Process.StandardError"/>
	/// </summary>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>All of the lines read from standard error</returns>
	public IAsyncEnumerable<string> ReadAllErrorLines(CancellationToken token = default)
	{
		var (channel, subs) = Channelable<string>();
		subs.Add(OnStandardError.Subscribe(async t => await channel.Writer.WriteAsync(t, token)));
		return channel.Reader.ReadAllAsync(token);
	}
	#endregion

	#region Text Writer Forwards
	/// <inheritdoc />
	public override void Write(char value)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(value);
	}

	/// <inheritdoc />
	public override void Write(char[]? buffer)
	{
		if (!EnsureWrite() || buffer is null) return;
		Writer!.Write(buffer, 0, buffer.Length);
	}

	/// <inheritdoc />
	public override void Write(char[] buffer, int index, int count)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(buffer, index, count);
	}

	/// <inheritdoc />
	public override void Write(ReadOnlySpan<char> buffer)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(buffer);
	}

	/// <inheritdoc />
	public override void Write(bool value)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(value);
	}

	/// <inheritdoc />
	public override void Write(int value)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(value);
	}

	/// <inheritdoc />
	public override void Write(uint value)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(value);
	}

	/// <inheritdoc />
	public override void Write(long value)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(value);
	}

	/// <inheritdoc />
	public override void Write(ulong value)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(value);
	}

	/// <inheritdoc />
	public override void Write(float value)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(value);
	}

	/// <inheritdoc />
	public override void Write(double value)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(value);
	}

	/// <inheritdoc />
	public override void Write(decimal value)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(value);
	}

	/// <inheritdoc />
	public override void Write(string? value)
	{
		if (!EnsureWrite() || value is null) return;
		Writer!.Write(value);
	}

	/// <inheritdoc />
	public override void Write(object? value)
	{
		if (!EnsureWrite() || value is null) return;
		Writer!.Write(value);
	}

	/// <inheritdoc />
	public override void Write(StringBuilder? value)
	{
		if (!EnsureWrite() || value is null) return;
		Writer!.Write(value);
	}

	/// <inheritdoc />
	public override void Write(string format, object? arg0)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(format, arg0);
	}

	/// <inheritdoc />
	public override void Write(string format, object? arg0, object? arg1)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(format, arg0, arg1);
	}

	/// <inheritdoc />
	public override void Write(string format, object? arg0, object? arg1, object? arg2)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(format, arg0, arg1, arg2);
	}

	/// <inheritdoc />
	public override void Write(string format, params object?[] arg)
	{
		if (!EnsureWrite()) return;
		Writer!.Write(format, arg);
	}

	/// <inheritdoc />
	public override void WriteLine()
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine();
	}

	/// <inheritdoc />
	public override void WriteLine(char value)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(value);
	}

	/// <inheritdoc />
	public override void WriteLine(char[]? buffer)
	{
		if (!EnsureWrite() || buffer is null) return;
		Writer!.WriteLine(buffer);
	}

	/// <inheritdoc />
	public override void WriteLine(char[] buffer, int index, int count)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(buffer, index, count);
	}

	/// <inheritdoc />
	public override void WriteLine(ReadOnlySpan<char> buffer)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(buffer);
	}

	/// <inheritdoc />
	public override void WriteLine(bool value)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(value);
	}

	/// <inheritdoc />
	public override void WriteLine(int value)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(value);
	}

	/// <inheritdoc />
	public override void WriteLine(uint value)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(value);
	}

	/// <inheritdoc />
	public override void WriteLine(long value)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(value);
	}

	/// <inheritdoc />
	public override void WriteLine(ulong value)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(value);
	}

	/// <inheritdoc />
	public override void WriteLine(float value)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(value);
	}

	/// <inheritdoc />
	public override void WriteLine(double value)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(value);
	}

	/// <inheritdoc />
	public override void WriteLine(decimal value)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(value);
	}

	/// <inheritdoc />
	public override void WriteLine(string? value)
	{
		if (!EnsureWrite() || value is null) return;
		Writer!.WriteLine(value);
	}

	/// <inheritdoc />
	public override void WriteLine(object? value)
	{
		if (!EnsureWrite() || value is null) return;
		Writer!.WriteLine(value);
	}

	/// <inheritdoc />
	public override void WriteLine(StringBuilder? value)
	{
		if (!EnsureWrite() || value is null) return;
		Writer!.WriteLine(value);
	}

	/// <inheritdoc />
	public override void WriteLine(string format, object? arg0)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(format, arg0);
	}

	/// <inheritdoc />
	public override void WriteLine(string format, object? arg0, object? arg1)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(format, arg0, arg1);
	}

	/// <inheritdoc />
	public override void WriteLine(string format, object? arg0, object? arg1, object? arg2)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(format, arg0, arg1, arg2);
	}

	/// <inheritdoc />
	public override void WriteLine(string format, params object?[] arg)
	{
		if (!EnsureWrite()) return;
		Writer!.WriteLine(format, arg);
	}

	/// <inheritdoc />
	public override void Flush()
	{
		if (!EnsureWrite()) return;
		Writer!.Flush();
	}

	/// <inheritdoc />
	public override Task FlushAsync()
	{
		if (!EnsureWrite()) return Task.CompletedTask;
		return Writer!.FlushAsync();
	}

	/// <inheritdoc />
	public override Task WriteAsync(char value)
	{
		if (!EnsureWrite()) return Task.CompletedTask;
		return Writer!.WriteAsync(value);
	}

	/// <inheritdoc />
	public override Task WriteAsync(char[] buffer, int index, int count)
	{
		if (!EnsureWrite()) return Task.CompletedTask;
		return Writer!.WriteAsync(buffer, index, count);
	}

	/// <inheritdoc />
	public override Task WriteAsync(string? value)
	{
		if (!EnsureWrite() || value is null) return Task.CompletedTask;
		return Writer!.WriteAsync(value);
	}

	/// <inheritdoc />
	public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
	{
		if (!EnsureWrite()) return Task.CompletedTask;
		return Writer!.WriteAsync(buffer, cancellationToken);
	}

	/// <inheritdoc />
	public override Task WriteAsync(StringBuilder? value, CancellationToken cancellationToken = default)
	{
		if (!EnsureWrite() || value is null) return Task.CompletedTask;
		return Writer!.WriteAsync(value, cancellationToken);
	}

	/// <inheritdoc />
	public override Task WriteLineAsync()
	{
		if (!EnsureWrite()) return Task.CompletedTask;
		return Writer!.WriteLineAsync();
	}

	/// <inheritdoc />
	public override Task WriteLineAsync(char value)
	{
		if (!EnsureWrite()) return Task.CompletedTask;
		return Writer!.WriteLineAsync(value);
	}

	/// <inheritdoc />
	public override Task WriteLineAsync(char[] buffer, int index, int count)
	{
		if (!EnsureWrite()) return Task.CompletedTask;
		return Writer!.WriteLineAsync(buffer, index, count);
	}

	/// <inheritdoc />
	public override Task WriteLineAsync(string? value)
	{
		if (!EnsureWrite() || value is null) return Task.CompletedTask;
		return Writer!.WriteLineAsync(value);
	}

	/// <inheritdoc />
	public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
	{
		if (!EnsureWrite()) return Task.CompletedTask;
		return Writer!.WriteLineAsync(buffer, cancellationToken);
	}

	/// <inheritdoc />
	public override Task WriteLineAsync(StringBuilder? value, CancellationToken cancellationToken = default)
	{
		if (!EnsureWrite() || value is null) return Task.CompletedTask;
		return Writer!.WriteLineAsync(value, cancellationToken);
	}
	#endregion

	#region Internal Methods
	/// <summary>
	/// Polyfill for <see cref="Process.HasExited"/> because microsoft (in it's infinite wisedom /s) decided 
	/// to have it throw an error if the process hasn't started yet instead of just returning false and not
	/// provide a way to see if the process has been run before.
	/// </summary>
	/// <returns>Whether or not the process has exited and isn't running</returns>
	internal bool CheckHasExited()
	{
		if (!Running || _process is null) return true;

		try
		{
			return _process.HasExited;
		}
		catch { return false; }
	}

	/// <summary>
	/// Ensures the process is running before attempting to write to standard input
	/// </summary>
	/// <returns>
	/// <para><see langword="true"/> if the process can be written to</para>
	/// <para><see langword="false"/> if the process is not running</para>
	/// </returns>
	internal bool EnsureWrite()
	{
		if (_process is not null && !HasExited)
			return true;

		SetError(ProcessErrorCode.Write, new ProcessNotRunningException());
		return false;
	}

	/// <summary>
	/// Sets <see cref="LastError"/> and triggers <see cref="OnException"/> for the given code and exception
	/// </summary>
	/// <param name="code">The code / reason the exception occurred</param>
	/// <param name="ex">The exception that was thrown</param>
	internal void SetError(ProcessErrorCode code, Exception ex)
	{
		ProcessError err = new(code, ex);
		_errors.Enqueue(err);
		_exception.OnNext(err);
	}

	/// <summary>
	/// Ensures the required <see cref="ProcessStartInfo"/> properties are set
	/// </summary>
	internal void EnsureStartArgs()
	{
		startInfo.CreateNoWindow = true;
		startInfo.RedirectStandardOutput = true;
		startInfo.RedirectStandardError = true;
		startInfo.RedirectStandardInput = true;
		startInfo.UseShellExecute = false;
		startInfo.WindowStyle = ProcessWindowStyle.Hidden;
	}

	/// <summary>
	/// The main execution thread for the process
	/// </summary>
	internal async Task ExecuteThread()
	{
		if (_process is null)
			return;

		try
		{
			_timer.Start();
			_process.Start();
			_process.BeginErrorReadLine();
			_process.BeginOutputReadLine();

			_started.OnNext(_process.Id);
			await _process.WaitForExitAsync(_cancel.Token);
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			SetError(ProcessErrorCode.RunningProcess, ex);
		}
		finally
		{
			await CleanProcess(CancellationToken.None);
		}
	}

	/// <summary>
	/// Cleans up the resources associated with the process
	/// </summary>
	internal async Task CleanProcess(CancellationToken token)
	{
		try
		{
			await _cleanControl.WaitAsync(token);

			if (_process is null) return;

			try
			{
				_process.CancelErrorRead();
				_process.CancelOutputRead();
			}
			catch { }

			if (!HasExited)
			{
				try
				{
					_process.Kill(true);
				}
				catch { }
			}

			int code = -1;
			try { code = _process.ExitCode; } 
			catch { }

			Running = false;
			_timer.Stop();
			_exited.OnNext(new(
				code,
				LastError?.Exception,
				Elapsed));
			_process.Dispose();
			_process = null;
			_task = null;
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			SetError(ProcessErrorCode.Cleanup, ex);
		}
		finally
		{
			_cleanControl.Release();
		}
	}

	/// <summary>
	/// Creates a threading channel that completes when the process exits
	/// </summary>
	/// <typeparam name="T">The type of channel</typeparam>
	/// <param name="options">The channel options</param>
	/// <returns>The channel and the subscriptions collection</returns>
	internal (Channel<T> channel, List<IDisposable> subs) Channelable<T>(UnboundedChannelOptions? options = null)
	{
		options ??= new UnboundedChannelOptions
		{
			SingleReader = false,
			SingleWriter = true,
		};
		var channel = Channel.CreateUnbounded<T>(options);
		var writer = channel.Writer;
		List<IDisposable> subs = [];
		subs.Add(OnExited.Subscribe(_ =>
		{
			writer.Complete();
			subs.ForEach(s => s.Dispose());
			subs.Clear();
		}));
		return (channel, subs);
	}

	/// <inheritdoc />
	public new void Dispose()
	{
		DisposeAsync()
			.AsTask()
			.GetAwaiter()
			.GetResult();
		base.Dispose();
		GC.SuppressFinalize(this);
	}

	/// <inheritdoc />
	public new async ValueTask DisposeAsync()
	{
		await CleanProcess(CancellationToken.None);

		if (!_cancel.IsCancellationRequested)
			_cancel.Cancel();

		if (_task is not null)
			await _task;

		_accessControl.Dispose();
		_cleanControl.Dispose();
		_standardOutput.Dispose();
		_standardError.Dispose();
		_started.Dispose();
		_exited.Dispose();
		_exception.Dispose();
		_cancel.Dispose();
		GC.SuppressFinalize(this);
	}
	#endregion

	#region Static helper methods
	/// <summary>
	/// Creates a process proxy for executing an inline PowerShell script
	/// </summary>
	/// <param name="script">The inline script to run</param>
	/// <param name="workingDirectory">The working directory for the script</param>
	/// <returns>The process proxy</returns>
	public static ProcessProxy PowerShellScript(string script, string? workingDirectory = null)
	{
		script = Convert.ToBase64String(DefaultEncoding.GetBytes(script));
		script = string.Format(POWERSHELL_ARGUMENTS_ENCODED, script);
		return new ProcessProxy(POWERSHELL_FILENAME)
			.WithArgs(script)
			.WithWorkingDirectory(workingDirectory);
	}

	/// <summary>
	/// Creates a process proxy for executing a PowerShell script file
	/// </summary>
	/// <param name="path">The path to the powershell script</param>
	/// <param name="workingDirectory">The working directory for the script</param>
	/// <returns>The process proxy</returns>
	/// <exception cref="FileNotFoundException">Thrown if the path could not be found</exception>
	public static ProcessProxy PowerShellFile(string path, string? workingDirectory = null)
	{
		if (!File.Exists(path))
			throw new FileNotFoundException("The specified PowerShell script file was not found.", path);

		path = path.Trim();
		path = Path.GetFullPath(path);
		if (!(path.StartsWith('"') && path.EndsWith('"')))
			path = $"\"{path}\"";

		var args = string.Format(POWERSHELL_ARGUMENTS_FILE, path);
		return new ProcessProxy(POWERSHELL_FILENAME)
			.WithArgs(args)
			.WithWorkingDirectory(workingDirectory);
	}
	#endregion
}