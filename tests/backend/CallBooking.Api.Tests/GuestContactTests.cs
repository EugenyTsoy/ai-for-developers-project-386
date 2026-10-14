using CallBooking.Domain.Entities;

namespace CallBooking.Api.Tests;

public class GuestContactTests
{
    [Fact]
    public void Valid_contact_is_trimmed_without_changing_name_or_email_case()
    {
        var guest = GuestContact.TryCreate("  Alice Liddell  ", "  Alice@example.com  ");

        Assert.NotNull(guest);
        Assert.Equal("Alice Liddell", guest.Name);
        Assert.Equal("Alice@example.com", guest.Email);
    }

    [Theory]
    [InlineData(null, "guest@example.com")]
    [InlineData("", "guest@example.com")]
    [InlineData(" \t\r\n ", "guest@example.com")]
    [InlineData("Guest", null)]
    [InlineData("Guest", "")]
    [InlineData("Guest", " \t ")]
    [InlineData("Guest", "not-an-email")]
    [InlineData("Guest", "@example.com")]
    [InlineData("Guest", "guest@")]
    [InlineData("Guest", "guest@@example.com")]
    [InlineData("Guest", "guest@example.com\r\nBcc: other@example.com")]
    public void Invalid_contact_is_rejected(string? name, string? email)
    {
        Assert.Null(GuestContact.TryCreate(name, email));
    }
}
