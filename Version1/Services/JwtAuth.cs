using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using SQCScanner.Modal;
using Microsoft.AspNetCore.Http.HttpResults;
namespace SQCScanner.Services
{
    public class JwtAuth
    {
        public string GenerateJwtToken(EmpModel emp)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes("aEj7A6mr5yVoDx0wq1jUj0A6xhb/8I+YJ0T+Y8h2sJk=");
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, emp.EmpId.ToString()),
                    new Claim(ClaimTypes.Name, emp.EmpName),
                    new Claim(ClaimTypes.Email, emp.EmpEmail),
                    new Claim(ClaimTypes.Role, emp.role),
                    new Claim("Phone", emp.contact),
                    new Claim("refranceId",emp.RefranceId)
                }),
                Expires = DateTime.UtcNow.AddHours(24),
                Issuer = "DotNet_PrashantKashyap",
                Audience = "OMR_IOS_Pvt_Ltd",
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
             var token = tokenHandler.CreateToken(tokenDescriptor);
             return tokenHandler.WriteToken(token);
        }
    }
}
