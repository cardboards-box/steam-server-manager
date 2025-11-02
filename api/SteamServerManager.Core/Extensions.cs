namespace SteamServerManager.Core;

/// <summary>
/// Various extension methods
/// </summary>
public static class Extensions
{
	/// <summary>
	/// Splits a string by the given character while allowing it to be escaped
	/// </summary>
	/// <param name="content">The string to split</param>
	/// <param name="split">The string to split by</param>
	/// <param name="escape">The escape character</param>
	/// <param name="equality">The comparison function for the split</param>
	/// <returns>The parts of the string</returns>
	internal static IEnumerable<string> InternalEscapeSplit(string content, string split, string escape, StringComparison equality)
	{
		//If the split token doesn't exist - skip everything
		if (!content.Contains(split, equality))
		{
			yield return content;
			yield break;
		}

		//Track the full section of the string to return
		int current = 0;
		//Track the last time the escape token was found
		int escapeOffset = 0;
		//Keep iterating through the string for the split characters
		while (current <= content.Length)
		{
			//Get the next index of the split character
			var index = content.IndexOf(split, escapeOffset, equality);
			//Split character doesn't exist? Return the result of the string and end
			if (index < 0)
			{
				yield return content[current..];
				break;
			}
			//Get the current portion of the string until the index
			var portion = content[current..index];
			//If the portion ends with the escape character then move the escape offset forward and continue
			if (portion.EndsWith(escape, equality))
			{
				escapeOffset = index + 1;
				continue;
			}

			//Set the escape offset and current offset to the current index + 1
			//Then return the portion of the string that was found and then continue
			escapeOffset = current = index + 1;
			yield return portion;
		}
	}

	/// <summary>
	/// Splits a string by the given character while allowing it to be escaped
	/// </summary>
	/// <param name="content">The string to split</param>
	/// <param name="split">The string to split by</param>
	/// <param name="escape">The escape character</param>
	/// <param name="options">The options for splitting</param>
	/// <param name="comparison">The string comparison type</param>
	/// <returns>The parts of the string</returns>
	public static IEnumerable<string> EscapeSplit(this string content, string split,
		string escape = "\\", StringSplitOptions options = StringSplitOptions.None,
		StringComparison comparison = StringComparison.CurrentCulture)
	{
		var splits = InternalEscapeSplit(content, split, escape, comparison);
		if (options.HasFlag(StringSplitOptions.TrimEntries))
			splits = splits.Select(t => t.Trim());
		if (options.HasFlag(StringSplitOptions.RemoveEmptyEntries))
			splits = splits.Where(t => !string.IsNullOrWhiteSpace(t));
		return splits;
	}

	/// <summary>
	/// Replaces part of a string based on the given indexes
	/// </summary>
	/// <param name="input">The string to perform the replacement on</param>
	/// <param name="replace">The string to replace with</param>
	/// <param name="start">Where to start the replacement at</param>
	/// <param name="length">How long the portion of the string to replace is</param>
	/// <returns>The formatted string</returns>
	public static string Replace(this string input, string replace, int start, int length)
	{
		if (string.IsNullOrEmpty(input) ||
			start < 0 ||
			start + length > input.Length ||
			length == 0)
			return input;

		var bob = new StringBuilder();
		if (start != 0)
			bob.Append(input.AsSpan(0, start + 1));
		bob.Append(replace);
		if (start + length != input.Length)
			bob.Append(input.AsSpan(start + length));
		return bob.ToString();
	}
}
