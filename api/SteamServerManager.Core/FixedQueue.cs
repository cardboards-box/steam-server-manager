namespace SteamServerManager.Core;

/// <summary>
/// A queue that has a fixed size and will dequeue items when the max size is reached
/// </summary>
/// <typeparam name="T">The type of data in the queue</typeparam>
/// <param name="_maxSize">The maximum size of the queue</param>
public class FixedQueue<T>(int _maxSize) : Queue<T>()
{
	/// <summary>
	/// The maximum size of the queue
	/// </summary>
	public int MaxSize => _maxSize;

	/// <inheritdoc />
	public new void Enqueue(T item)
	{
		while (Count >= MaxSize)
			Dequeue();
		base.Enqueue(item);
	}
}
