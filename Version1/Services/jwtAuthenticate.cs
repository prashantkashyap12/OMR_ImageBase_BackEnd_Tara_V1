using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using SixLabors.ImageSharp;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Data.SqlClient;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using SQCScanner.Modal;
using Newtonsoft.Json.Linq;
using System.Net.Quic;
using System;
using System.Globalization;
using Microsoft.EntityFrameworkCore.Query;

namespace SQCScanner.Services
{
    public static class JwtServiceExtensions
    {
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            var _conn = configuration.GetConnectionString("dbc");
            var jwtSettings = configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings.GetValue<string>("SecretKey");
            var issuer = jwtSettings.GetValue<string>("Issuer");
            var audience = jwtSettings.GetValue<string>("Audience");

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = "DotNet_PrashantKashyap",
                        ValidAudience = "OMR_IOS_Pvt_Ltd",
                        RoleClaimType = ClaimTypes.Role,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("aEj7A6mr5yVoDx0wq1jUj0A6xhb/8I+YJ0T+Y8h2sJk=")),
                    };

                    options.Events = new JwtBearerEvents
                    {
                        // Token Time Expired 
                        OnAuthenticationFailed = async context =>
                        {
                            if (context.Exception is SecurityTokenExpiredException)
                            {
                                var expiredToken = context.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "").Trim();
                                if (string.IsNullOrWhiteSpace(expiredToken))
                                {
                                    context.Response.StatusCode = 401;
                                    await context.Response.WriteAsync("{\"message\": \"No token provided\"}");
                                    return;
                                }
                                context.Fail("Unauthorized");
                                return;
                            }
                            context.Response.StatusCode = 401;
                            await context.Response.WriteAsync("{\"message\": \"Authentication failed\"}");
                        },

                        // Token Time Validate 
                        OnTokenValidated = async context =>
                        {
                            // If Token Role is will be ONLY admin, moderator, operator then responce  -- DONE
                            dynamic res;
                            var tokenString = context.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "").Trim();
                            var validRoles = new[] { "admin", "moderator", "operator" };
                            var handler = new JwtSecurityTokenHandler();
                            var jwtToken = handler.ReadJwtToken(tokenString);
                            var role = jwtToken.Claims.FirstOrDefault(c => c.Type == "role")?.Value;
                            if (string.IsNullOrEmpty(role) || !validRoles.Contains(role.ToLower()))
                            {
                                context.Fail("Unauthorized");
                            }
                            else
                            {
                                Console.WriteLine("Success Authrized");
                            }

                            // If Token Match from table token then Responce -- DONE
                            var empId = jwtToken.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;
                            var empId2 = Convert.ToInt32(empId);
                            var expiryTimeUtc1 = jwtToken.ValidFrom.ToLocalTime();
                            var expiryTimeUtc2 = jwtToken.ValidTo;
                            var expiryTimeLocal2 = expiryTimeUtc2.ToLocalTime();
                            using (var conn = new SqlConnection(context.HttpContext.RequestServices.GetRequiredService<IConfiguration>().GetConnectionString("dbc")))
                            {
                                await conn.OpenAsync();
                                var querry = await conn.QueryFirstOrDefaultAsync<dynamic>($@"select * from LoginTokenRec where EmpId = {empId2}");
                                var expiry = "";
                                var Token = "";
                                if (querry == null)
                                {
                                    context.Fail("Record Not Founded");
                                }
                                else
                                {
                                    foreach (var kvp in querry)
                                    {
                                        if (kvp.Key == "Expiry")
                                        {
                                            expiry = kvp.Value;
                                        }
                                    }
                                    DateTime expiryDate2 = DateTime.ParseExact(expiry, "dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                                    DateTime ctDate2 = DateTime.Now;
                                    if (expiryDate2 > ctDate2) 
                                    {
                                        context.Success();
                                    }
                                    else
                                    {
                                        context.Fail("Unauthorized");
                                    }
                                }
                            }
                        },
                    
                    };
                });
            return services;  
        }                                                                                                                                                                                         
    }
}
