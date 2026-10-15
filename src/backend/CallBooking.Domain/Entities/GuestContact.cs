using System.ComponentModel.DataAnnotations;

namespace CallBooking.Domain.Entities;

/// <summary>Pure guest validation and normalization, independent of transport.</summary>
public sealed record GuestContact
{
    public string Name { get; }
    public string Email { get; }

    private GuestContact(string name, string email)
    {
        Name = name;
        Email = email;
    }

    public static GuestContact? TryCreate(string? name, string? email)
    {
        var trimmedName = name?.Trim();
        var trimmedEmail = email?.Trim();
        return string.IsNullOrEmpty(trimmedName) || string.IsNullOrEmpty(trimmedEmail)
            || !new EmailAddressAttribute().IsValid(trimmedEmail)
            ? null
            : new GuestContact(trimmedName, trimmedEmail);
    }
}
