using System.Text.RegularExpressions;

namespace CallBooking.Architecture.Tests;

/// <summary>
/// Minimal dependency scanner used by the architecture guard. Walks
/// Domain/Application sources, strips comments, and reports every
/// transport-layer reference that would leak design-time or wiring
/// concerns into the inner layers.
///
/// <para>
/// What this guard detects (per AC20 of issue #19):
/// <list type="bullet">
///   <item>References to <c>CallBooking.Api.Generated</c>, via
///   either <c>using</c> declaration (plain, <c>using static</c>, or
///   <c>using alias = ...</c>) or fully-qualified type access.
///   </item>
///   <item>References into the <c>Microsoft.AspNetCore</c> namespace
///   family — which carries <c>ProblemDetails</c>, controller
///   attributes, HTTP verbs and content types, and the ASP.NET
///   MVC / HTTP plumbing. A single prefix match per line keeps the
///   guard from emitting duplicate reports.
///   </item>
/// </list>
/// </para>
///
/// <para>
/// What this guard does NOT cover, intentionally, because the ticket
/// calls for a *minimal* guard: literal ISO-8601 wire strings
/// (<c>"...Z"</c> format suffixes) and UI/presentation date
/// formatting are runtime/review concerns. They are detectable by
/// inspection, the integration tests, and the contract drift check,
/// but not by a <c>using</c>-prefix scan.
/// </para>
/// </summary>
internal static class BoundaryScanner
{
    /// <summary>
    /// Namespaces whose <c>using</c> declaration is forbidden in
    /// Domain / Application sources.
    /// </summary>
    private static readonly string[] ForbiddenUsingPrefixes =
    {
        "Microsoft.AspNetCore",
        "CallBooking.Api.Generated",
    };

    /// <summary>
    /// Namespace prefixes forbidden when referenced fully qualified
    /// (without a <c>using</c> declaration). <c>Microsoft.AspNetCore</c>
    /// covers every longer sibling (Mvc, Http, etc.) so children are
    /// not listed; matches at most once per line to avoid duplicate
    /// reports on the same access.
    /// </summary>
    private static readonly string[] ForbiddenReferencePrefixes =
    {
        "Microsoft.AspNetCore",
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
            foreach (var violation in ScanLine(label, path, index + 1, lines[index]))
            {
                yield return violation;
            }
        }
    }

    private static IEnumerable<string> ScanLine(string label, string path, int lineNumber, string line)
    {
        var trimmed = line.TrimStart();

        if (LooksLikeUsingDeclaration(trimmed))
        {
            var usingViolation = CheckUsingDeclaration(trimmed);
            if (usingViolation is not null)
            {
                yield return $"{label}:{path}: line {lineNumber}: {usingViolation}";
            }
        }

        foreach (var prefix in ForbiddenReferencePrefixes)
        {
            if (ReferencePrefixAppearsPrefixed(line, prefix))
            {
                yield return $"{label}:{path}: line {lineNumber}: forbidden reference to '{prefix}'";
                // Don't iterate further — siblings of an already-flagged
                // prefix would only produce duplicates here.
                yield break;
            }
        }
    }

    private static bool LooksLikeUsingDeclaration(string trimmed)
    {
        return trimmed.StartsWith("using ", StringComparison.Ordinal)
            || trimmed.StartsWith("using\t", StringComparison.Ordinal);
    }

    private static string? CheckUsingDeclaration(string trimmed)
    {
        var body = trimmed["using".Length..].TrimStart();
        if (body.StartsWith("static ", StringComparison.Ordinal))
        {
            body = body["static ".Length..].TrimStart();
        }

        var end = body.IndexOf(';');
        if (end < 0)
        {
            return null;
        }

        var unaliased = body[..end].Trim();
        var equalIdx = unaliased.IndexOf('=');
        if (equalIdx >= 0)
        {
            // `using X = Y.Z;` — inspect the right-hand side only.
            unaliased = unaliased[(equalIdx + 1)..].Trim();
        }

        return MatchForbiddenUsing(unaliased);
    }

    private static string? MatchForbiddenUsing(string ns)
    {
        foreach (var prefix in ForbiddenUsingPrefixes)
        {
            if (ns == prefix || ns.StartsWith(prefix + ".", StringComparison.Ordinal))
            {
                return $"forbidden using '{ns}'";
            }
        }

        return null;
    }

    private static bool ReferencePrefixAppearsPrefixed(string line, string prefix)
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
                return true;
            }

            idx = line.IndexOf(marker, idx + 1, StringComparison.Ordinal);
        }

        return false;
    }

    private static string StripComments(string source)
    {
        var withoutBlocks = BlockComment.Replace(source, string.Empty);
        return LineComment.Replace(withoutBlocks, string.Empty);
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';
}
