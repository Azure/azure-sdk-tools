using System;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace APIViewWeb.Services
{
    public interface IAuthTokenService
    {
        /// <summary>
        /// Creates an encrypted token containing user claims for cross-domain transfer
        /// </summary>
        string CreateToken(ClaimsPrincipal user, string returnHost, string returnUrl);

        /// <summary>
        /// Validates and decrypts a token, returning the claims if valid
        /// </summary>
        AuthTokenPayload ValidateToken(string token);
    }

    public class AuthTokenPayload
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string Login { get; set; }
        public string Email { get; set; }
        public string Avatar { get; set; }
        public string Url { get; set; }
        public string Orgs { get; set; }
        public string ReturnHost { get; set; }
        public string ReturnUrl { get; set; }
        public long ExpiresAt { get; set; }
    }

    public class AuthTokenService : IAuthTokenService
    {
        private readonly IDataProtector _protector;
        private const int TokenExpirationSeconds = 60; // Token valid for 60 seconds

        public AuthTokenService(IDataProtectionProvider dataProtectionProvider)
        {
            _protector = dataProtectionProvider.CreateProtector("APIView.CrossDomainAuth.v1");
        }

        public string CreateToken(ClaimsPrincipal user, string returnHost, string returnUrl)
        {
            var payload = new AuthTokenPayload
            {
                UserId = user.FindFirstValue(ClaimTypes.NameIdentifier),
                UserName = user.FindFirstValue(ClaimTypes.Name),
                Login = user.FindFirstValue(APIView.Identity.ClaimConstants.Login),
                Email = user.FindFirstValue(APIView.Identity.ClaimConstants.Email),
                Avatar = user.FindFirstValue(APIView.Identity.ClaimConstants.Avatar),
                Url = user.FindFirstValue(APIView.Identity.ClaimConstants.Url),
                Orgs = user.FindFirstValue(APIView.Identity.ClaimConstants.Orgs),
                ReturnHost = returnHost,
                ReturnUrl = returnUrl ?? "/",
                ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(TokenExpirationSeconds).ToUnixTimeSeconds()
            };

            var json = JsonSerializer.Serialize(payload);
            var encrypted = _protector.Protect(json);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(encrypted));
        }

        public AuthTokenPayload ValidateToken(string token)
        {
            try
            {
                var encrypted = Encoding.UTF8.GetString(Convert.FromBase64String(token));
                var json = _protector.Unprotect(encrypted);
                var payload = JsonSerializer.Deserialize<AuthTokenPayload>(json);

                // Check expiration
                if (payload.ExpiresAt < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                {
                    return null; // Token expired
                }

                return payload;
            }
            catch
            {
                return null; // Invalid token
            }
        }
    }
}
