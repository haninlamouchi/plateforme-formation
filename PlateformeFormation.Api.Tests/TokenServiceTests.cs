using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using PlateformeFormation.Api.Models;
using PlateformeFormation.Api.Services;

namespace PlateformeFormation.Api.Tests;

public class TokenServiceTests
{
    private static TokenService BuildService(Dictionary<string, string?>? overrides = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "une-cle-de-test-suffisamment-longue-pour-hmac-sha256",
            ["Jwt:Issuer"] = "PlateformeFormationTests",
            ["Jwt:Audience"] = "PlateformeFormationTestsUsers",
            ["Jwt:ExpiryMinutes"] = "120",
        };
        if (overrides is not null)
            foreach (var (k, v) in overrides) settings[k] = v;

        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new TokenService(config);
    }

    private static Utilisateur BuildUser(RoleUtilisateur role = RoleUtilisateur.RESPONSABLE_PEDAGOGIQUE) => new()
    {
        Id = 42,
        Nom = "Ada Lovelace",
        Email = "ada@example.com",
        Role = role,
    };

    [Fact]
    public void GenerateToken_EmbedsUserClaims()
    {
        var token = BuildService().GenerateToken(BuildUser());

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("42", jwt.Claims.Single(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);
        Assert.Equal("Ada Lovelace", jwt.Claims.Single(c => c.Type == System.Security.Claims.ClaimTypes.Name).Value);
        Assert.Equal("ada@example.com", jwt.Claims.Single(c => c.Type == System.Security.Claims.ClaimTypes.Email).Value);
        Assert.Equal("RESPONSABLE_PEDAGOGIQUE", jwt.Claims.Single(c => c.Type == System.Security.Claims.ClaimTypes.Role).Value);
    }

    [Fact]
    public void GenerateToken_UsesConfiguredIssuerAndAudience()
    {
        var token = BuildService().GenerateToken(BuildUser());

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("PlateformeFormationTests", jwt.Issuer);
        Assert.Equal("PlateformeFormationTestsUsers", jwt.Audiences.Single());
    }

    [Fact]
    public void GenerateToken_UsesDefaultExpiry_WhenNoCustomExpiryGiven()
    {
        var token = BuildService().GenerateToken(BuildUser());
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        var expected = DateTime.UtcNow.AddMinutes(120);
        Assert.True(Math.Abs((jwt.ValidTo - expected).TotalMinutes) < 1);
    }

    [Fact]
    public void GenerateToken_UsesCustomExpiry_WhenProvided()
    {
        var token = BuildService().GenerateToken(BuildUser(), TimeSpan.FromMinutes(5));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        var expected = DateTime.UtcNow.AddMinutes(5);
        Assert.True(Math.Abs((jwt.ValidTo - expected).TotalMinutes) < 1);
    }

    [Fact]
    public void GenerateToken_Throws_WhenJwtKeyMissing()
    {
        var service = BuildService(new Dictionary<string, string?> { ["Jwt:Key"] = null });

        Assert.Throws<InvalidOperationException>(() => service.GenerateToken(BuildUser()));
    }

    [Fact]
    public void GenerateToken_ReflectsAdministratorRole()
    {
        var token = BuildService().GenerateToken(BuildUser(RoleUtilisateur.ADMINISTRATEUR));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("ADMINISTRATEUR", jwt.Claims.Single(c => c.Type == System.Security.Claims.ClaimTypes.Role).Value);
    }
}
