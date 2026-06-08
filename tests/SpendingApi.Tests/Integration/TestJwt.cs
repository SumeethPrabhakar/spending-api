using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SpendingApi.Tests.Integration;

// Generates test JWTs signed with a key that matches the test server config
public static class TestJwt
{
    private const string Key = "test-signing-key-at-least-32-chars!!";

    public static string ForCustomer(Guid customerId)
    {
        var claims = new[]
        {
            new Claim("sub", customerId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "spending-api-test",
            audience: "spending-api",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
