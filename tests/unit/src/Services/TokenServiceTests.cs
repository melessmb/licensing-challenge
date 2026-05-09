using FluentAssertions;
using LicensingChallenge.Tests;
using Xunit;

namespace LicensingChallenge.Tests.Services;

public class TokenServiceTests
{
    private readonly LicensingService.Services.Implementations.TokenService _sut;

    public TokenServiceTests()
    {
        _sut = TestFixtures.CreateTokenService();
    }

    // ── GenerateToken ──────────────────────────────────────────────────────

    [Fact]
    public void GenerateToken_ValidLicense_ReturnsNonEmptyString()
    {
        var license = TestFixtures.ActiveLicense();
        var token   = _sut.GenerateToken(license);
        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateToken_ValidLicense_ReturnsValidJwtFormat()
    {
        var license = TestFixtures.ActiveLicense();
        var token   = _sut.GenerateToken(license);
        token.Split('.').Should().HaveCount(3, "JWT must have header.payload.signature");
    }

    [Fact]
    public void GenerateToken_TwoDifferentTenants_ReturnsDifferentTokens()
    {
        var token1 = _sut.GenerateToken(TestFixtures.ActiveLicense("tenant-a"));
        var token2 = _sut.GenerateToken(TestFixtures.ActiveLicense("tenant-b"));
        token1.Should().NotBe(token2);
    }

    // ── ValidateToken ──────────────────────────────────────────────────────

    [Fact]
    public void ValidateToken_ValidToken_ReturnsIsValidTrue()
    {
        var license         = TestFixtures.ActiveLicense("my-tenant");
        var token           = _sut.GenerateToken(license);
        var (isValid, _)    = _sut.ValidateToken(token);
        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateToken_ValidToken_ReturnsTenantId()
    {
        var license          = TestFixtures.ActiveLicense("my-tenant");
        var token            = _sut.GenerateToken(license);
        var (_, tenantId)    = _sut.ValidateToken(token);
        tenantId.Should().Be("my-tenant");
    }

    [Fact]
    public void ValidateToken_InvalidToken_ReturnsIsValidFalse()
    {
        var (isValid, tenantId) = _sut.ValidateToken("not.a.valid.jwt");
        isValid.Should().BeFalse();
        tenantId.Should().BeNull();
    }

    [Fact]
    public void ValidateToken_EmptyToken_ReturnsIsValidFalse()
    {
        var (isValid, _) = _sut.ValidateToken(string.Empty);
        isValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateToken_TamperedPayload_ReturnsIsValidFalse()
    {
        var license = TestFixtures.ActiveLicense();
        var token   = _sut.GenerateToken(license);

        var parts    = token.Split('.');
        parts[1]     = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes("{\"tenantId\":\"hacker\",\"maxApps\":9999}"));
        var tampered = string.Join('.', parts);

        var (isValid, _) = _sut.ValidateToken(tampered);
        isValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateToken_ExpiredLicense_ReturnsIsValidFalse()
    {
        var license      = TestFixtures.ExpiredLicense();
        var token        = _sut.GenerateToken(license);
        var (isValid, _) = _sut.ValidateToken(token);
        isValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateToken_DifferentSecret_ReturnsIsValidFalse()
    {
        var otherService = TestFixtures.CreateTokenService("OtherSecret!AnotherKey256bitsXXXXXXXXXXXXXXXXX");
        var token        = otherService.GenerateToken(TestFixtures.ActiveLicense());
        var (isValid, _) = _sut.ValidateToken(token);
        isValid.Should().BeFalse();
    }
}
