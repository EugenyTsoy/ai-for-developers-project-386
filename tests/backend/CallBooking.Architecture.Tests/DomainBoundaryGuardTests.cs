namespace CallBooking.Architecture.Tests;

/// <summary>
/// Architecture guard: scans every Domain and Application source file
/// and asserts that no transport-layer reference has leaked into an
/// inner layer. Per Issue #19 / AC20 this is the minimal CI guard
/// that protects the structural boundaries.
/// </summary>
public class DomainBoundaryGuardTests
{
    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        // tests/backend/CallBooking.Architecture.Tests/bin/Release/net10.0/ -> repo root
        return Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", "..", ".."));
    }

    [Fact]
    public void Domain_sources_have_no_forbidden_transport_references()
    {
        var violations = ScanDirectory(
            Path.Combine(RepoRoot(), "src", "backend", "CallBooking.Domain"),
            label: "Domain").ToArray();

        Assert.True(
            violations.Length == 0,
            "Forbidden transport references in Domain sources:\n  "
                + string.Join("\n  ", violations));
    }

    [Fact]
    public void Application_sources_have_no_forbidden_transport_references()
    {
        var violations = ScanDirectory(
            Path.Combine(RepoRoot(), "src", "backend", "CallBooking.Application"),
            label: "Application").ToArray();

        Assert.True(
            violations.Length == 0,
            "Forbidden transport references in Application sources:\n  "
                + string.Join("\n  ", violations));
    }

    [Fact]
    public void Guard_detects_artificially_forbidden_reference_in_application_source()
    {
        // Path 1: forbidden using of the generated namespace.
        var withGeneratedUsing = Path.Combine(Path.GetTempPath(), "leak-generated-using.cs");
        File.WriteAllText(
            withGeneratedUsing,
            """
            namespace CallBooking.Application.Forbidden;
            using CallBooking.Api.Generated;
            public static class Leak { }
            """);

        try
        {
            var violations = BoundaryScanner.ScanFile("Application", withGeneratedUsing).ToArray();
            Assert.Contains(
                violations,
                v => v.Contains("CallBooking.Api.Generated", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(withGeneratedUsing);
        }

        // Path 2: forbidden using of ASP.NET MVC namespace.
        var withMvcUsing = Path.Combine(Path.GetTempPath(), "leak-mvc-using.cs");
        File.WriteAllText(
            withMvcUsing,
            """
            namespace CallBooking.Application.Forbidden;
            using Microsoft.AspNetCore.Mvc;
            public static class Leak { }
            """);

        try
        {
            var violations = BoundaryScanner.ScanFile("Application", withMvcUsing).ToArray();
            Assert.Contains(
                violations,
                v => v.Contains("Microsoft.AspNetCore", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(withMvcUsing);
        }

        // Path 3: fully qualified reference without using.
        var withFullReference = Path.Combine(Path.GetTempPath(), "leak-full-ref.cs");
        File.WriteAllText(
            withFullReference,
            """
            namespace CallBooking.Application.Forbidden;
            public static class Leak
            {
                public static readonly object Value = Microsoft.AspNetCore.Mvc.ProblemDetails.Empty;
            }
            """);

        try
        {
            var violations = BoundaryScanner.ScanFile("Application", withFullReference).ToArray();
            // Exactly one violation per line even though "Microsoft.AspNetCore"
            // and "Microsoft.AspNetCore.Mvc" both technically match.
            Assert.Single(
                violations,
                v => v.Contains("Microsoft.AspNetCore", StringComparison.Ordinal)
                    && !v.Contains("Microsoft.AspNetCore.Mvc", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(withFullReference);
        }

        // Path 4: forbidden alias to a generated type.
        var withAlias = Path.Combine(Path.GetTempPath(), "leak-alias.cs");
        File.WriteAllText(
            withAlias,
            """
            namespace CallBooking.Application.Forbidden;
            using LeakAlias = CallBooking.Api.Generated.EventType;
            public static class Leak { }
            """);

        try
        {
            var violations = BoundaryScanner.ScanFile("Application", withAlias).ToArray();
            Assert.Contains(
                violations,
                v => v.Contains("CallBooking.Api.Generated", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(withAlias);
        }

        // Path 5: forbidden `using static` of an MVC type.
        var withStatic = Path.Combine(Path.GetTempPath(), "leak-using-static.cs");
        File.WriteAllText(
            withStatic,
            """
            namespace CallBooking.Application.Forbidden;
            using static Microsoft.AspNetCore.Mvc.ControllerBase;
            public static class Leak { }
            """);

        try
        {
            var violations = BoundaryScanner.ScanFile("Application", withStatic).ToArray();
            Assert.Contains(
                violations,
                v => v.Contains("Microsoft.AspNetCore", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(withStatic);
        }
    }

    [Fact]
    public void Guard_does_not_flag_comments_or_adjacent_identifiers()
    {
        var benign = Path.Combine(Path.GetTempPath(), "guard-benign.cs");
        File.WriteAllText(
            benign,
            """
            namespace CallBooking.Domain.Benign;

            // using CallBooking.Api.Generated;
            /* using Microsoft.AspNetCore.Mvc; */

            public static class C
            {
                // The previous line is a comment, not a using declaration.
                public static readonly string CallBookingExample = "CallBooking.Api.Generated docs";
                public static readonly int MyMicrosoft = 5;
            }
            """);

        try
        {
            var violations = BoundaryScanner.ScanFile("Domain", benign).ToArray();
            Assert.Empty(violations);
        }
        finally
        {
            File.Delete(benign);
        }
    }

    private static IEnumerable<string> ScanDirectory(string path, string label)
    {
        if (!Directory.Exists(path))
        {
            return Array.Empty<string>();
        }

        return Directory
            .EnumerateFiles(path, "*.cs", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal)
            .SelectMany(file => BoundaryScanner.ScanFile(label, file));
    }
}
