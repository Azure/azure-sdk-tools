using System.Linq;
using System;
using APIView.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using APIViewWeb.Account;

namespace APIViewWeb.Controllers
{
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
        /// Test endpoint that requires either GitHub organization membership or Managed Identity authentication
        /// </summary>
        [HttpGet("test")]
        [Authorize("RequireOrganizationOrManagedIdentity")]
        public IActionResult TestAuth()
        {
            var authMethod = GetAuthenticationMethod();
            var userName = User.Identity.Name ?? User.GetGitHubLogin() ?? "Unknown";
            
            return Ok(new
            {
                Message = "Authentication successful!",
                AuthenticationMethod = authMethod,
                UserName = userName,
                IsAuthenticated = User.Identity.IsAuthenticated,
                AuthenticationType = User.Identity.AuthenticationType,
                IsManagedIdentity = AuthenticationValidator.IsValidManagedIdentity(User),
                HasGitHubOrganization = AuthenticationValidator.HasAnyOrganizationAccess(User),
                Claims = User.Claims.Select(c => new { c.Type, c.Value }).ToArray()
            });
        }

        /// <summary>
        /// Test endpoint that only allows GitHub organization members (original behavior)
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
        /// Public endpoint to check authentication status without requiring authorization
        /// </summary>
        [HttpGet("status")]
        public IActionResult GetAuthStatus()
        {
            return Ok(new
            {
                IsAuthenticated = User.Identity.IsAuthenticated,
                AuthenticationMethod = GetAuthenticationMethod(),
                UserName = User.Identity.IsAuthenticated ? (User.Identity.Name ?? User.GetGitHubLogin() ?? "Unknown") : null,
                IsManagedIdentity = AuthenticationValidator.IsValidManagedIdentity(User),
                HasGitHubOrganization = AuthenticationValidator.HasAnyOrganizationAccess(User)
            });
        }

        /// <summary>
        /// Development-only endpoint that accepts any Bearer token for testing managed identity flow
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
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
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

        private string GetAuthenticationMethod()
        {
            return AuthenticationValidator.GetAuthenticationMethod(User);
        }
    }
}
