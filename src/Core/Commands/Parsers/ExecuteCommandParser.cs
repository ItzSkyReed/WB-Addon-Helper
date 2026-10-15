using Core.Commands.Impl;
using Core.Commands.Models.Interfaces;
using JetBrains.Annotations;
using Pidgin;

namespace Core.Commands.Parsers;

/// <summary>
/// Handles the parsing of the /execute command using a context-aware lexical scan.
/// It isolates modifiers from the top-level "run" keyword (ignoring "run" inside quotes,
/// target selectors, or SNBT compounds) and recursively parses the nested command.
/// </summary>
public static class ExecuteCommandParser
{
    /// <summary>
    /// A parser that consumes the remaining execute command string, locates the top-level 'run' keyword,
    /// and recursively evaluates the nested command using <see cref="CommandParser.Root"/>.
    /// </summary>
    /// <returns>A parser yielding an <see cref="ICommand"/> instance (specifically <see cref="ExecuteCommand"/>).</returns>
    /// <exception cref="ParseException">Thrown implicitly by Pidgin if the nested run command fails to parse.</exception>
    /// <example>
    /// <code>
    /// ICommand cmd = ExecuteCommandParser.Parser.ParseOrThrow("as @a at @s run give @s diamond 1");
    /// </code>
    /// </example>
    [PublicAPI]
    public static readonly Parser<char, ICommand> Parser =
        Parser<char>.Any.ManyString().Bind(rest =>
        {
            SplitModifiersAndRunCommand(rest.AsSpan(), out var modifiers, out var runString);

            if (string.IsNullOrEmpty(runString))
            {
                // Return execute without a run block (e.g. "execute if entity @s")
                return Parser<char>.Return<ICommand>(new ExecuteCommand(modifiers, null));
            }

            // Strip optional leading slash if someone wrote "execute ... run /say hi"
            if (runString.StartsWith('/'))
            {
                runString = runString[1..].TrimStart();
            }

            // Recursively parse the nested command using the Root parser
            var runResult = CommandParser.Root.Parse(runString);

            return runResult.Success
                ? Parser<char>.Return<ICommand>(new ExecuteCommand(modifiers, runResult.Value))
                // If nested parsing fails, fail the whole execute block so it falls back to RawCommandLine
                : Parser<char>.Fail<ICommand>($"Failed to parse nested run command: {runString}");
        });

    /// <summary>
    /// Splits the raw execute arguments into the modifiers substring and the nested run command substring,
    /// ignoring any occurrences of "run" inside quotes (<c>"..."</c>, <c>'...'</c>), selectors (<c>[...]</c>), or SNBT (<c>{...}</c>).
    /// </summary>
    /// <param name="rawInput">The raw execute arguments span.</param>
    /// <param name="modifiers">When this method returns, contains the trimmed modifiers string.</param>
    /// <param name="runCommand">When this method returns, contains the trimmed command string following 'run'.</param>
    private static void SplitModifiersAndRunCommand(
        ReadOnlySpan<char> rawInput,
        out string modifiers,
        out string runCommand)
    {
        var input = rawInput.Trim();
        if (input.IsEmpty)
        {
            modifiers = string.Empty;
            runCommand = string.Empty;
            return;
        }

        var runTokenIndex = FindTopLevelRunKeyword(input);
        if (runTokenIndex < 0)
        {
            modifiers = input.ToString();
            runCommand = string.Empty;
            return;
        }

        modifiers = input[..runTokenIndex].TrimEnd().ToString();

        var afterRun = input[(runTokenIndex + 3)..].TrimStart();
        runCommand = afterRun.ToString();
    }

    /// <summary>
    /// Finds the starting index of the standalone top-level <c>run</c> keyword in the span,
    /// or returns <c>-1</c> if not present at the top level.
    /// </summary>
    private static int FindTopLevelRunKeyword(ReadOnlySpan<char> span)
    {
        var braceDepth = 0;   // Tracks {...}
        var bracketDepth = 0; // Tracks [...]
        var activeQuote = '\0';
        var isEscaped = false;

        for (var i = 0; i < span.Length; i++)
        {
            var c = span[i];

            if (activeQuote != '\0')
            {
                if (isEscaped)
                {
                    isEscaped = false;
                }
                else if (c == '\\')
                {
                    isEscaped = true;
                }
                else if (c == activeQuote)
                {
                    activeQuote = '\0';
                }

                continue;
            }

            switch (c)
            {
                case '"' or '\'':
                    activeQuote = c;
                    continue;

                case '{':
                    braceDepth++;
                    continue;

                case '}':
                    if (braceDepth > 0)
                        braceDepth--;
                    continue;

                case '[':
                    bracketDepth++;
                    continue;

                case ']':
                    if (bracketDepth > 0)
                        bracketDepth--;
                    continue;
            }

            // Only match "run" when outside any brackets/braces and at a whitespace boundary
            if (braceDepth != 0 || bracketDepth != 0)
                continue;

            var isStartBoundary = i == 0 || char.IsWhiteSpace(span[i - 1]);
            if (!isStartBoundary || i + 3 > span.Length)
                continue;

            var isEndBoundary = i + 3 == span.Length || char.IsWhiteSpace(span[i + 3]);
            if (!isEndBoundary)
                continue;

            if (span.Slice(i, 3).Equals("run", StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}