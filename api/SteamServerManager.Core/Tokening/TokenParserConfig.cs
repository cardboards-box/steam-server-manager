namespace SteamServerManager.Core.Tokening;

/// <summary>
/// Represents the configuration for the token parser
/// </summary>
public class TokenParserConfig
{
	/// <summary>
	/// The character that delimits the start of a token
	/// </summary>
	public string StartToken { get; set; } = "/*";

	/// <summary>
	/// The character that delimits the end of a token
	/// </summary>
	public string EndToken { get; set; } = "*/";

	/// <summary>
	/// The character to use to escape the <see cref="StartToken"/>
	/// </summary>
	public string EscapeToken { get; set; } = "\\";

	/// <summary>
	/// The default string comparison options
	/// </summary>
	public StringComparison StringComparer { get; set; } = StringComparison.CurrentCulture;

	/// <summary>
	/// Represents the configuration for the token parser
	/// </summary>
	public TokenParserConfig() { }

	/// <summary>
	/// Represents the configuration for the token parser
	/// </summary>
	/// <param name="startToken">The character that delimits the start of a token</param>
	/// <param name="endToken">The character that delimits the end of a token</param>
	/// <param name="escapeToken">The character to use to escape the <see cref="StartToken"/></param>
	public TokenParserConfig(string startToken, string endToken, string escapeToken)
	{
		StartToken = startToken;
		EndToken = endToken;
		EscapeToken = escapeToken;
	}

	/// <summary>
	/// Deconstructs the token
	/// </summary>
	/// <param name="start">The character that delimits the start of a token</param>
	/// <param name="end">The character that delimits the end of a token</param>
	/// <param name="escape">The character to use to escape the <see cref="StartToken"/></param>
	public void Deconstruct(out string start, out string end, out string escape)
	{
		start = StartToken;
		end = EndToken;
		escape = EscapeToken;
	}
}
