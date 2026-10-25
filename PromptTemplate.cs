using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace QuickPrompt;

public static class PromptTemplate
{
    // Complete brace pairs only: code, nested braces and spaced names stay literal.
    private static readonly Regex Placeholder = new(@"(?<!\{)\{([A-Z][A-Za-z]*)\}(?!\})", RegexOptions.CultureInvariant);

    public static IReadOnlyList<string> GetNames(string content) => Placeholder.Matches(content)
        .Select(match => match.Groups[1].Value).Distinct(StringComparer.Ordinal).ToArray();

    public static string Fill(string content, IReadOnlyDictionary<string, string> values) =>
        Placeholder.Replace(content, match => values.TryGetValue(match.Groups[1].Value, out string? value) ? value : match.Value);
}
