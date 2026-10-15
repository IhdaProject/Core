using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Core.Infrastructure.Security;

public interface IJwtTokenHandler
{
    (string refreshToken, DateTime expireDate) GenerateRefreshToken();
    (JwtSecurityToken token, string jti, DateTime expireDate) GenerateAccessToken(long userId, IEnumerable<long>? structureIds = null, IEnumerable<Claim>? additionalClaims = null);
    (JwtSecurityToken token, string jti, DateTime expireDate) GenerateAccessToken(IEnumerable<Claim> claims);
}
