using System.Text.RegularExpressions;

namespace CallBooking.Architecture.Tests;

/// <summary>
/// Minimal dependency scanner used by the architecture guard. Walks
/// Domain/Application sources, strips comments, and reports every
/// transport-layer reference that would leak design-time or wire
/// concerns into the inner layers. Intentionally small: per the
/// ticket, this is a minimal CI guard, not a Roslyn rule.
/// </summary>
internal static class BoundaryScanner
{
    private static readonly HashSet<string> ForbiddenUsingNamespaces = new(StringComparer.Ordinal)
    {
        "CallBooking.Api.Generated",
    };

    private static readonly string[] ForbiddenUsingPrefixes =
    {
        "Microsoft.AspNetCore",
    };

    /// <summary>
    /// Fully qualified type references that are forbidden in Domain
    /// and Application source: matching the prefix and the next
    /// identifier character after the dot.
    /// </summary>
    private static readonly string[] ForbiddenReferencePrefixes =
    {
        "Microsoft.AspNetCore",
        "Microsoft.AspNetCore.Mvc",
        "Microsoft.AspNetCore.Mvc.Core",
        "Microsoft.AspNetCore.Mvc.ModelBinding",
        "Microsoft.AspNetCore.Http",
        "CallBooking.Api.Generated",
    };

    private static readonly Regex BlockComment = new(
        @"/\*[\s\S]*?\*/",
        RegexOptions.Compiled);

    private static readonly Regex LineComment = new(
        @"//[^\n]*",
        RegexOptions.Compiled);

    public static IEnumerable<string> ScanFile(string label, string path)
    {
        var raw = File.ReadAllText(path);
        var stripped = StripComments(raw);
        var lines = stripped.Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            foreach (var violation in ScanLine(label, path, index + 1, line))
            {
                yield return violation;
            }
        }
    }

    private static IEnumerable<string> ScanLine(string label, string path, int lineNumber, string line)
    {
        var trimmed = line.TrimStart();

        if (trimmed.StartsWith("using ", StringComparison.Ordinal))
        {
            foreach (var violation in CheckUsingDeclaration(label, path, lineNumber, trimmed))
            {
                yield return violation;
            }
        }

        foreach (var prefix in ForbiddenReferencePrefixes)
        {
            foreach (var violation in CheckFullyQualifiedReference(label, path, lineNumber, line, prefix))
            {
                yield return violation;
            }
        }
    }

    private static IEnumerable<string> CheckUsingDeclaration(string label, string path, int lineNumber, string trimmed)
    {
        var trailing = trimmed["using ".Length..].Trim();
        var end = trailing.IndexOf(';');
        if (end < 0)
        {
            yield break;
        }

        var ns = trailing[..end].Trim();
        if (ForbiddenUsingNamespaces.Contains(ns))
        {
            yield return $"{label}:{path}: line {lineNumber}: forbidden using '{ns}'";
            yield break;
        }

        foreach (var prefix in ForbiddenUsingPrefixes)
        {
            if (ns == prefix || ns.StartsWith(prefix + ".", StringComparison.Ordinal))
            {
                yield return $"{label}:{path}: line {lineNumber}: forbidden using '{ns}'";
                yield break;
            }
        }
    }

    private static IEnumerable<string> CheckFullyQualifiedReference(
        string label,
        string path,
        int lineNumber,
        string line,
        string prefix)
    {
        var marker = prefix + ".";
        var idx = line.IndexOf(marker, StringComparison.Ordinal);
        while (idx >= 0)
        {
            var beforeOk = idx == 0 || !IsIdentifierPart(line[idx - 1]);
            var afterIdx = idx + marker.Length;
            var afterOk = afterIdx < line.Length && IsIdentifierStart(line[afterIdx]);

            if (beforeOk && afterOk)
            {
                yield return $"{label}:{path}: line {lineNumber}: forbidden reference to '{prefix}'";
                yield break;
            }

            idx = line.IndexOf(marker, idx + 1, StringComparison.Ordinal);
        }
    }

    private static string StripComments(string source)
    {
        var withoutBlocks = BlockComment.Replace(source, string.Empty);
        return LineComment.Replace(withoutBlocks, string.Empty);
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';
}
