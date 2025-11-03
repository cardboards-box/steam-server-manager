namespace SteamServerManager.Processing;

using Exceptions;

public partial class ProcessProxy
{
	private readonly Subject<string> _standardOutput = new();
	private readonly Subject<string> _standardError = new();
	private readonly Subject<int> _started = new();
	private readonly Subject<ProcessResult> _exited = new();
	private readonly Subject<ProcessError> _exception = new();

	private IObservable<string>? _standardOutputObs;
	private IObservable<string>? _standardErrorObs;
	private IObservable<int>? _startedObs;
	private IObservable<ProcessResult>? _exitedObs;
	private IObservable<ProcessError>? _exceptionObs;

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
	/// <remarks>Since this is a <see cref="BehaviorSubject{T}"/> we need to skip the first event to ignore the default value</remarks>
	public IObservable<ProcessResult> OnExited => _exitedObs ??= _exited.AsObservable();

	/// <summary>
	/// Triggered when an exception occurs during process execution
	/// </summary>
	public IObservable<ProcessError> OnException => _exceptionObs ??= _exception.AsObservable();

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
}
