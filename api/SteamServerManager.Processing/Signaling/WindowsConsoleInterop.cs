namespace SteamServerManager.Processing.Signaling;

internal partial class WindowsConsoleInterop
{
	[DllImport("kernel32.dll", SetLastError = true)]
	static extern bool AttachConsole(uint dwProcessId);

	[DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
	static extern bool FreeConsole();

	[DllImport("kernel32.dll", SetLastError = true)]
	static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate HandlerRoutine, bool Add);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GenerateConsoleCtrlEvent(WindowsSignal dwCtrlEvent, uint dwProcessGroupId);

	// Delegate type to be used as the Handler Routine for SCCH
	delegate bool ConsoleCtrlDelegate(WindowsSignal CtrlType);

	public static int SendSignal(Process proc, WindowsSignal? signal = null)
	{
		//This does not require the console window to be visible.
		if (!AttachConsole((uint)proc.Id))
			return Marshal.GetLastWin32Error();

		//Disable Ctrl-C handling for our program
		SetConsoleCtrlHandler(null!, true);
		GenerateConsoleCtrlEvent(signal ?? 0, 0);

		//Must wait here. If we don't and re-enable Ctrl-C handling below too fast, we might terminate ourselves.
		proc.WaitForExit();

		FreeConsole();

		//Re-enable Ctrl-C handling or any subsequently started programs will inherit the disabled state.
		SetConsoleCtrlHandler(null!, false);
		return 0;
	}
}
