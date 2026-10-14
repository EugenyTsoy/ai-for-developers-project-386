using System.Text.RegularExpressions;

namespace CallBooking.Domain;

/// <summary>
/// A standard email address. The format requirement is "valid email
/// address"; the implementation chooses a conventional regex that is
/// consistent with the approved spec.
/// </summary>
public readonly partial record struct EmailAddress
{
    // Conservative, classic format check; matches the requirement and uses
    // the framework's standard regex engine.
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();

    private readonly string? _value;

    public EmailAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Email must not be empty after trim.",
                nameof(value));
        }

        var trimmed = value.Trim();
        if (!EmailRegex().IsMatch(trimmed))
        {
            throw new ArgumentException(
                "Email is not a valid address.",
                nameof(value));
        }

        _value = trimmed;
    }

    /// <summary>
    /// The trimmed address. Throws when read off a value that was
    /// never constructed (i.e. <c>default(EmailAddress)</c>), instead
    /// of silently returning an empty string.
    /// </summary>
    public string Value => _value
        ?? throw new InvalidOperationException(
            "EmailAddress default value is not a valid address; construct an instance first.");
}

/// <summary>
/// Guest contact: a name and an email address. Both must be non-empty
/// after trim.
/// </summary>
public sealed record GuestContact
{
    public GuestContact(string name, EmailAddress email)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Guest name must not be empty after trim.",
                nameof(name));
        }

        Name = name.Trim();
        Email = email;
    }

    public string Name { get; }

    public EmailAddress Email { get; }
}
