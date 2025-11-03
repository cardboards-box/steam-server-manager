namespace SteamServerManager.Processing;

using Exceptions;

/// <summary>
/// The implementation of the <see cref="TextWriter"/> that forwards all operations to <see cref="Process.StandardInput"/>
/// </summary>
public partial class ProcessProxy
{
	/// <summary>
	/// Ensures the process is running before attempting to write to standard input
	/// </summary>
	/// <returns>
	/// <para><see langword="true"/> if the process can be written to</para>
	/// <para><see langword="false"/> if the process is not running</para>
	/// </returns>
	internal bool EnsureWrite()
	{
		if (_process is not null && Running)
			return true;

		SetError(ProcessErrorCode.Write, new ProcessNotRunningException());
		return false;
	}

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

	/// <inheritdoc />
	protected override void Dispose(bool disposing)
    {
		if (!disposing) return;

		_process?.Dispose();
		_accessControl.Dispose();
		_standardOutput.Dispose();
		_standardError.Dispose();
		_started.Dispose();
		_exited.Dispose();
		_exception.Dispose();
	}
}
