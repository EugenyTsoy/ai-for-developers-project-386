namespace CallBooking.Domain.Tests;

public class GuestContactTests
{
    [Fact]
    public void EmailAddress_rejects_blank_input()
    {
        Assert.Throws<ArgumentException>(() => new EmailAddress("   "));
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("@host")]
    [InlineData("user@")]
    [InlineData("user@host")]
    [InlineData("user@host.")]
    public void EmailAddress_rejects_invalid_format(string input)
    {
        Assert.Throws<ArgumentException>(() => new EmailAddress(input));
    }

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("first.last@sub.example.co.uk")]
    public void EmailAddress_accepts_well_formed_addresses(string input)
    {
        var email = new EmailAddress(input);
        Assert.Equal(input, email.Value);
    }

    [Fact]
    public void EmailAddress_trims_surrounding_whitespace()
    {
        var email = new EmailAddress("  user@example.com  ");
        Assert.Equal("user@example.com", email.Value);
    }

    [Fact]
    public void GuestContact_rejects_blank_name()
    {
        Assert.Throws<ArgumentException>(() =>
            new GuestContact("   ", new EmailAddress("user@example.com")));
    }

    [Fact]
    public void GuestContact_trims_name()
    {
        var guest = new GuestContact("  Alice  ", new EmailAddress("alice@example.com"));
        Assert.Equal("Alice", guest.Name);
    }
}
