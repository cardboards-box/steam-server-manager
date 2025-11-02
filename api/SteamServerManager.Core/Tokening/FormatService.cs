namespace SteamServerManager.Core.Tokening;

/// <summary>
/// A service for formatting strings using tokenized variables
/// </summary>
public interface IFormatService
{
	/// <summary>
	/// Format the given string 
	/// </summary>
	/// <param name="input">The string to format</param>
	/// <param name="settings">The settings to use for the formatter</param>
	/// <returns>The formatted string</returns>
	/// <exception cref="OverflowException">Thrown if too many passes are made through the formatter</exception>
	string Format(string input, FormatSettings? settings = null);

	/// <summary>
	/// Format the given string 
	/// </summary>
	/// <param name="input">The string to format</param>
	/// <param name="args">The variables for the format operation</param>
	/// <returns>The formatted string</returns>
	/// <exception cref="OverflowException">Thrown if too many passes are made through the formatter</exception>
	string Format(string input, Dictionary<string, Func<object?>> args);

	/// <summary>
	/// Format the given string 
	/// </summary>
	/// <param name="input">The string to format</param>
	/// <param name="args">The variables for the format operation</param>
	/// <returns>The formatted string</returns>
	/// <exception cref="OverflowException">Thrown if too many passes are made through the formatter</exception>
	string Format(string input, Dictionary<string, object?> args);

	/// <summary>
	/// Format the given string 
	/// </summary>
	/// <param name="input">The string to format</param>
	/// <param name="args">The variables for the format operation</param>
	/// <returns>The formatted string</returns>
	/// <exception cref="OverflowException">Thrown if too many passes are made through the formatter</exception>
	string Format(string input, Dictionary<string, string?> args);
}

internal class FormatService(
	ITokenParserService _parser) : IFormatService
{
	/// <summary>
	/// Removes all instances of the escape sequence
	/// </summary>
	/// <param name="input">The input string</param>
	/// <param name="escape">The escape sequence</param>
	/// <param name="compare">The string comparer</param>
	/// <returns>The modified string</returns>
	public static string RemoveEscapes(string input, string escape, StringComparison compare)
	{
		int index = 0;
		while (index < input.Length)
		{
			var current = input.IndexOf(escape, index, compare);
			if (current < 0) break;

			input = input.Remove(current, escape.Length);
			if (input.StartsWith(escape, compare))
				current += escape.Length;

			index = current + escape.Length;
		}
		return input;
	}

	/// <summary>
	/// Perform one pass of the formatter
	/// </summary>
	/// <param name="input">The input string to format</param>
	/// <param name="settings">The settings for the format operation</param>
	/// <param name="output">The formatted string</param>
	/// <returns>Whether or not we did something during the format pass</returns>
	public bool FormatPass(string input, FormatSettings settings, out string output)
	{
		output = input;
		//Deconstruct the settings to get the variables and configuration options
		var (args, parser, formatToken) = settings.Resolve();
		//Quick check to make sure the string actually contains tokens
		if (!input.Contains(parser.StartToken, parser.StringComparer) ||
			!input.Contains(parser.EndToken, parser.StringComparer))
			return false;

		//Get all of the tokens from the input string
		var tokens = _parser.Parse(input, parser);
		//Keep track of whether or not we did something
		var didSomething = false;

		int index = 0;
		while (true)
		{
			var (current, i) = _parser.FindNextToken(output, parser, index);
			if (current is null) break;

			index = i;
			var (token, start, length, fullToken) = current;

			//Get the format splitter
			var parts = token.EscapeSplit(
				formatToken, parser.EscapeToken,
				StringSplitOptions.None, parser.StringComparer)
				.ToArray();
			//The actual value is always the first item in the splitter
			var tag = parts.First();
			//If the variable doesn't exist
			if (!args.TryGetValue(tag, out var resolver)) continue;

			didSomething = true;
			//Get the value we should add to the string
			var value = resolver() ?? string.Empty;
			//Get the format string if there is one
			var format = string.Join(formatToken, parts.Skip(1));
			//Get the string format for the value
			var strVal = !string.IsNullOrEmpty(format) && value is IFormattable form
				? form.ToString(format, null)
				: value.ToString();
			strVal ??= string.Empty;

			//Set the index to the end of the replaced value
			index = start + strVal.Length;
			//Replace the token with the value
			output = output.Replace(strVal, start - 1, fullToken.Length + 1);
		}

		return didSomething;
	}

	public string Format(string input, FormatSettings? settings = null)
	{
		//Ensure there are settings
		settings ??= new();
		//Get the parser configuration
		var (_, parser, _) = settings.Resolve();
		//Keep track of how many passes we've made
		int count = 0, max = settings.MaxPasses;
		//Keep iterating while we have parser tokens in the string
		while (
			input.Contains(parser.StartToken, parser.StringComparer) &&
			input.Contains(parser.EndToken, parser.StringComparer))
		{
			count++;
			//Ensure we're below the max pass count
			if (count > max)
			{
				//If we should throw an error, ensure we do so
				if (settings.ThrowOnMaxPasses)
					throw new OverflowException($"Hit max format cycles cap: {count} for {input}");
				break;
			}

			//Do a format pass, if we don't do anything, then break the loop
			if (!FormatPass(input, settings, out string output))
				break;
			//Set the input to the result of the format pass
			input = output;
		}

		//Remove escaped strings
		input = RemoveEscapes(input, parser.EscapeToken, parser.StringComparer);
		//Return the formatted string
		return input;
	}

	public string Format(string input, Dictionary<string, Func<object?>> args)
	{
		var settings = new FormatSettings().With(args);
		return Format(input, settings);
	}

	public string Format(string input, Dictionary<string, object?> args)
	{
		var settings = new FormatSettings().With(args);
		return Format(input, settings);
	}

	public string Format(string input, Dictionary<string, string?> args)
	{
		var settings = new FormatSettings().With(args);
		return Format(input, settings);
	}
}
