using RaceDay.Api.Security;

namespace RaceDay.Tests;
// Unit tests for the PasswordHasher class
public class PasswordHasherTests
{
    [Fact]
    public void Hash_DoesNotContainPlainTextPassword()
    {
        var hash = PasswordHasher.Hash("Passw0rd!");
        Assert.DoesNotContain("Passw0rd!", hash);
        Assert.True(hash.Length <= 255, "Hash must fit the PasswordHash VARCHAR(255) column.");
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var hash = PasswordHasher.Hash("Passw0rd!");
        Assert.True(PasswordHasher.Verify("Passw0rd!", hash));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = PasswordHasher.Hash("Passw0rd!");
        Assert.False(PasswordHasher.Verify("passw0rd!", hash));
    }

    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentHashes_BecauseOfSalt()
    {
        Assert.NotEqual(PasswordHasher.Hash("Passw0rd!"), PasswordHasher.Hash("Passw0rd!"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain-text-password")]
    [InlineData("v1.abc.def.ghi")]
    public void Verify_MalformedStoredHash_ReturnsFalse(string stored)
    {
        Assert.False(PasswordHasher.Verify("Passw0rd!", stored));
    }
}
