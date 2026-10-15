using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Core.Domain.Configurations;
using Core.Domain.Enums;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Core.Infrastructure.Security;

public class JwtTokenHandler(IOptions<JwtOption> option) : IJwtTokenHandler
{
    private readonly JwtOption _jwtOption = option.Value;

    public (string refreshToken, DateTime expireDate) GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var randomGenerator = RandomNumberGenerator.Create();
        randomGenerator.GetBytes(bytes);

        return (Convert.ToBase64String(bytes), DateTime.UtcNow.AddMinutes(_jwtOption.ExpirationRefreshTokenInMinutes));
    }

    public (JwtSecurityToken token, string jti, DateTime expireDate) GenerateAccessToken(
        long userId, 
        IEnumerable<long>? structureIds = null, 
        IEnumerable<Claim>? additionalClaims = null)
    {
        var claims = new List<Claim>
        {
            new(CustomClaimNames.UserId, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (structureIds != null)
        {
            claims.Add(new Claim(CustomClaimNames.Structures, string.Join(',', structureIds)));
        }

        if (additionalClaims != null)
        {
            claims.AddRange(additionalClaims);
        }

        return GenerateAccessToken(claims);
    }

    public (JwtSecurityToken token, string jti, DateTime expireDate) GenerateAccessToken(IEnumerable<Claim> claims)
    {
        var claimList = claims.ToList();
        var jtiClaim = claimList.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);
        string jti;

        if (jtiClaim == null)
        {
            jti = Guid.NewGuid().ToString();
            claimList.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));
        }
        else
        {
            jti = jtiClaim.Value;
        }

        var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOption.SecretKey));
        var expire = DateTime.UtcNow.AddMinutes(_jwtOption.ExpirationInMinutes);

        var token = new JwtSecurityToken(
            issuer: _jwtOption.Issuer,
            audience: _jwtOption.Audience,
            expires: expire,
            claims: claimList,
            signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
        );

        return (token, jti, expire);
    }
}
