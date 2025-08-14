using System;
using System.Linq;
using APIViewWeb.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace APIViewWeb.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthTestController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    public AuthTestController(IWebHostEnvironment env)
    {
        _env = env;
    }

    /// <summary>
    ///     Test endpoint that requires either GitHub organization membership or Managed Identity authentication
    /// </summary>
    [HttpGet("test")]
    [Authorize("RequireOrganizationOrManagedIdentity")]
    public IActionResult TestAuth()
    {
        string authMethod = GetAuthenticationMethod();
        string userName = User.Identity.Name ?? User.GetGitHubLogin() ?? "Unknown";

        return Ok(new
        {
            Message = "Combined authentication successful!",
            AuthenticationMethod = authMethod,
            UserName = userName,
            IsAuthenticated = User.Identity.IsAuthenticated,
            AuthenticationType = User.Identity.AuthenticationType,
            IsManagedIdentity = AuthenticationValidator.IsValidAzureIdentity(User),
            HasGitHubOrganization = AuthenticationValidator.HasAnyOrganizationAccess(User),
            Claims = User.Claims.Select(c => new { c.Type, c.Value }).ToArray()
        });
    }

    /// <summary>
    ///     Test endpoint that only allows cookie-based authentication (GitHub OAuth via browser)
    /// </summary>
    [HttpGet("cookie-only")]
    [Authorize("RequireCookieAuthentication")]
    public IActionResult TestCookieOnly()
    {
        return Ok(new
        {
            Message = "Cookie-based authentication successful!",
            UserName = User.GetGitHubLogin(),
            Organizations = AuthenticationValidator.GetUserOrganizations(User),
            AuthenticationType = User.Identity.AuthenticationType,
            IsGitHubAuthenticated = AuthenticationValidator.HasAnyOrganizationAccess(User)
        });
    }

    /// <summary>
    ///     Test endpoint that only allows token-based authentication (Bearer tokens)
    /// </summary>
    [HttpGet("token-only")]
    [Authorize("RequireTokenAuthentication")]
    public IActionResult TestTokenOnly()
    {
        return Ok(new
        {
            Message = "Token-based authentication successful!",
            UserName = User.Identity.Name ?? User.GetGitHubLogin() ?? "Unknown",
            AuthenticationType = User.Identity.AuthenticationType,
            IsManagedIdentity = AuthenticationValidator.IsValidAzureIdentity(User),
            HasGitHubOrganization = AuthenticationValidator.HasAnyOrganizationAccess(User),
            TokenType = AuthenticationValidator.IsValidAzureIdentity(User) ? "Managed Identity" : "GitHub Token"
        });
    }

    /// <summary>
    ///     Test endpoint that only allows GitHub organization members (original behavior)
    /// </summary>
    [HttpGet("github-only")]
    [Authorize("RequireOrganization")]
    public IActionResult TestGitHubOnly()
    {
        return Ok(new
        {
            Message = "GitHub organization authentication successful!",
            UserName = User.GetGitHubLogin(),
            Organizations = AuthenticationValidator.GetUserOrganizations(User),
            IsGitHubAuthenticated = AuthenticationValidator.HasAnyOrganizationAccess(User)
        });
    }

    /// <summary>
    ///     Public endpoint to check authentication status without requiring authorization
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetAuthStatus()
    {
        return Ok(new
        {
            User.Identity.IsAuthenticated,
            AuthenticationMethod = GetAuthenticationMethod(),
            UserName =
                User.Identity.IsAuthenticated ? User.Identity.Name ?? User.GetGitHubLogin() ?? "Unknown" : null,
            IsManagedIdentity = AuthenticationValidator.IsValidAzureIdentity(User),
            HasGitHubOrganization = AuthenticationValidator.HasAnyOrganizationAccess(User)
        });
    }

    /// <summary>
    ///     Test endpoint that accepts either cookie OR token authentication (hybrid approach)
    /// </summary>
    [HttpGet("hybrid")]
    [Authorize("RequireTokenOrCookieAuthentication")]
    public IActionResult TestHybridAuth()
    {
        return Ok(new
        {
            Message = "Hybrid authentication successful! This endpoint accepts both cookies and tokens.",
            AuthenticationMethod = GetAuthenticationMethod(),
            UserName = User.Identity.Name ?? User.GetGitHubLogin() ?? "Unknown",
            AuthenticationType = User.Identity.AuthenticationType,
            IsManagedIdentity = AuthenticationValidator.IsValidAzureIdentity(User),
            HasGitHubOrganization = AuthenticationValidator.HasAnyOrganizationAccess(User),
            Note = "This endpoint should work with both Bearer tokens and browser cookies"
        });
    }

    /// <summary>
    ///     Test endpoint to verify cookie-only authentication REJECTS tokens
    /// </summary>
    [HttpGet("verify-cookie-rejects-token")]
    [Authorize("RequireCookieAuthentication")]
    public IActionResult VerifyCookieRejectsToken()
    {
        string authHeader = Request.Headers["Authorization"].FirstOrDefault();
        bool hasAuthHeader = !string.IsNullOrEmpty(authHeader);
        
        return Ok(new
        {
            Message = "Cookie-only endpoint accessed successfully!",
            AuthenticationType = User.Identity.AuthenticationType,
            UserName = User.GetGitHubLogin(),
            HasAuthorizationHeader = hasAuthHeader,
            AuthHeaderPreview = hasAuthHeader ? authHeader.Substring(0, Math.Min(20, authHeader.Length)) + "..." : null,
            ExpectedBehavior = "This should only work with cookies, not Bearer tokens",
            Note = "If you see this with a Bearer token, the authentication scheme isolation is broken!"
        });
    }

    /// <summary>
    ///     Test endpoint to verify token-only authentication REJECTS cookies
    /// </summary>
    [HttpGet("verify-token-rejects-cookie")]
    [Authorize("RequireTokenAuthentication")]
    public IActionResult VerifyTokenRejectsCookie()
    {
        string authHeader = Request.Headers["Authorization"].FirstOrDefault();
        bool hasAuthHeader = !string.IsNullOrEmpty(authHeader);
        
        return Ok(new
        {
            Message = "Token-only endpoint accessed successfully!",
            AuthenticationType = User.Identity.AuthenticationType,
            UserName = User.Identity.Name ?? User.GetGitHubLogin() ?? "Unknown",
            HasAuthorizationHeader = hasAuthHeader,
            TokenType = AuthenticationValidator.IsValidAzureIdentity(User) ? "Managed Identity" : "GitHub Token",
            ExpectedBehavior = "This should only work with Bearer tokens, not browser cookies",
            Note = "If you see this from a browser without a token, the authentication scheme isolation is broken!"
        });
    }

    /// <summary>
    ///     Helper endpoint to test authentication rejection patterns
    /// </summary>
    [HttpGet("test-rejection")]
    public IActionResult TestRejection()
    {
        string authHeader = Request.Headers["Authorization"].FirstOrDefault();
        bool hasAuthHeader = !string.IsNullOrEmpty(authHeader);
        bool hasBearerToken = authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true;
        bool isAuthenticated = User.Identity.IsAuthenticated;
        
        return Ok(new
        {
            Message = "This endpoint shows current authentication state (no authorization required)",
            IsAuthenticated = isAuthenticated,
            AuthenticationType = User.Identity?.AuthenticationType ?? "None",
            HasAuthorizationHeader = hasAuthHeader,
            HasBearerToken = hasBearerToken,
            UserName = isAuthenticated ? (User.Identity.Name ?? User.GetGitHubLogin() ?? "Unknown") : null,
            AuthenticationMethod = isAuthenticated ? GetAuthenticationMethod() : "Anonymous",
            TestInstructions = new
            {
                CookieTest = "Call /api/authtest/verify-cookie-rejects-token with a Bearer token - should get 401/403",
                TokenTest = "Call /api/authtest/verify-token-rejects-cookie from browser with cookies but no token - should get 401/403",
                HybridTest = "Call /api/authtest/hybrid with either authentication method - should get 200"
            }
        });
    }

    /// <summary>
    ///     Development-only endpoint that accepts any Bearer token for testing managed identity flow
    /// </summary>
    [HttpGet("dev-test")]
    public IActionResult DevTest()
    {
        // Only allow this in development mode for security
        if (!_env.IsDevelopment())
        {
            return NotFound();
        }

        // Accept any Bearer token in development to simulate successful managed identity auth
        string authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (authHeader?.StartsWith("Bearer ") == true)
        {
            return Ok(new
            {
                Message = "🎉 Development managed identity authentication successful!",
                AuthenticationMethod = "Managed Identity (Development Simulation)",
                UserName = "managed-identity-dev-user",
                IsAuthenticated = true,
                AuthenticationType = "Bearer",
                TokenPreview = authHeader.Substring(0, Math.Min(50, authHeader.Length)) + "...",
                Environment = "Development",
                Note = "This simulates successful managed identity authentication"
            });
        }

        return Unauthorized(new
        {
            Message = "Bearer token required for development testing",
            Hint = "Add 'Authorization: Bearer <any-token>' header"
        });
    }

#if DEBUG
    /// <summary>
    ///     Debug endpoint to test JWT token parsing - no authorization required
    /// </summary>
    [HttpGet("jwt-debug")]
    public IActionResult JwtDebug()
    {
        // Only allow this in development mode for security
        if (!_env.IsDevelopment())
        {
            return NotFound();
        }

        string authHeader = Request.Headers["Authorization"].FirstOrDefault();

        return Ok(new
        {
            Message = "JWT Debug Information",
            HasAuthorizationHeader = authHeader != null,
            AuthorizationHeader = authHeader?.Substring(0, Math.Min(100, authHeader?.Length ?? 0)) + "...",
            UserAuthenticated = User.Identity?.IsAuthenticated ?? false,
            AuthenticationType = User.Identity?.AuthenticationType ?? "None",
            UserName = User.Identity?.Name ?? "Anonymous",
            ClaimsCount = User.Claims?.Count() ?? 0,
            Claims = User.Claims?.Select(c => new { c.Type, c.Value }).Take(20).ToArray() ?? new object[0],
            IsManagedIdentity =
                User.Identity?.IsAuthenticated == true && AuthenticationValidator.IsValidAzureIdentity(User),
            Environment = "Development"
        });
    }
#endif

    private string GetAuthenticationMethod()
    {
        return AuthenticationValidator.GetAuthenticationMethod(User);
    }
}
