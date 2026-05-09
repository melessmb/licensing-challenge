using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using LicensingService.Models.Entities;
using LicensingService.Services.Interfaces;

namespace LicensingService.Services.Implementations;

public class TokenService : ITokenService
{
    private readonly string _secret;
    private const string Issuer   = "licensing-service";
    private const string Audience = "platform-clients";

    public TokenService(IConfiguration config)
    {
        _secret = config["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret is not configured");
    }

    public string GenerateToken(License license)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("tenantId",              license.TenantId),
            new Claim("licenseId",             license.Id.ToString()),
            new Claim("maxApps",               license.MaxApps.ToString()),
            new Claim("maxExecutionsPer24h",   license.MaxExecutionsPer24h.ToString()),
            new Claim("status",                license.Status.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer:            Issuer,
            audience:          Audience,
            claims:            claims,
            notBefore:         license.ValidFrom,
            expires:           license.ValidTo,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (bool isValid, string? tenantId) ValidateToken(string token)
    {
        try
        {
            var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
            var handler = new JwtSecurityTokenHandler();

            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey         = key,
                ValidateIssuer           = true,
                ValidIssuer              = Issuer,
                ValidateAudience         = true,
                ValidAudience            = Audience,
                ValidateLifetime         = true,
                ClockSkew                = TimeSpan.Zero
            }, out _);

            var tenantId = principal.FindFirst("tenantId")?.Value;
            return (tenantId is not null, tenantId);
        }
        catch
        {
            return (false, null);
        }
    }
}
