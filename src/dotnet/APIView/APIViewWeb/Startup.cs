using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using APIView.Identity;
using APIViewWeb.Account;
using APIViewWeb.Filters;
using APIViewWeb.Helpers;
using APIViewWeb.HostedServices;
using APIViewWeb.Hubs;
using APIViewWeb.LeanControllers;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.MiddleWare;
using APIViewWeb.Repositories;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json.Linq;
using System.Text.Encodings.Web;
using Polly;

namespace APIViewWeb
{
    public class Startup
    {
        public static string RequireOrganizationPolicy = "RequireOrganization";
        public static string RequireOrganizationOrManagedIdentityPolicy = "RequireOrganizationOrManagedIdentity";

        public static string VersionHash { get; set; }

        static Startup()
        {
            var version = typeof(Startup).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            var indexOfPlus = version.IndexOf("+", StringComparison.OrdinalIgnoreCase);
            VersionHash = indexOfPlus == -1 ? "dev" : version.Substring(indexOfPlus + 1);
        }

        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        public IConfiguration Configuration { get; }
        public IWebHostEnvironment Environment { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddApplicationInsightsTelemetry();
            services.AddApplicationInsightsTelemetryProcessor<TelemetryIpAddressFilter>();

            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.Lax;
            });

            services.Configure<OrganizationOptions>(options => Configuration
                .GetSection("Github")
                .Bind(options));

#pragma warning disable ASP5001 // Type or member is obsolete
#pragma warning disable CS0618 // Type or member is obsolete
            services.AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Latest)
                .AddRazorRuntimeCompilation();
#pragma warning restore CS0618 // Type or member is obsolete
#pragma warning restore ASP5001 // Type or member is obsolete

            services.AddRazorPages(options =>
            {
                options.Conventions.AuthorizeFolder("/Assemblies", RequireOrganizationPolicy);
                options.Conventions.AddPageRoute("/Assemblies/Index", "");
            });

            services.AddHttpClient();
            services.AddSingleton<IPollingJobQueueManager, PollingJobQueueManager>();
            services.AddSingleton<IBlobCodeFileRepository, BlobCodeFileRepository>();
            services.AddSingleton<IBlobOriginalsRepository, BlobOriginalsRepository>();
            services.AddSingleton<IBlobUsageSampleRepository, BlobUsageSampleRepository>();

            services.AddSingleton<ICosmosReviewRepository,CosmosReviewRepository>();
            services.AddSingleton<ICosmosAPIRevisionsRepository, CosmosAPIRevisionsRepository>();
            services.AddSingleton<ICosmosCommentsRepository, CosmosCommentsRepository>();
            services.AddSingleton<ICosmosPullRequestsRepository, CosmosPullRequestsRepository>();
            services.AddSingleton<ICosmosSamplesRevisionsRepository, CosmosSamplesRevisionsRepository>();
            services.AddSingleton<ICosmosUserProfileRepository, CosmosUserProfileRepository>();
            services.AddSingleton<IDevopsArtifactRepository, DevopsArtifactRepository>();

            services.AddSingleton<IReviewManager, ReviewManager>();
            services.AddSingleton<IAPIRevisionsManager, APIRevisionsManager>();
            services.AddSingleton<ICommentsManager, CommentsManager>();
            services.AddSingleton<INotificationManager, NotificationManager>();
            services.AddSingleton<IPullRequestManager, PullRequestManager>();
            services.AddSingleton<IPackageNameManager, PackageNameManager>();
            services.AddSingleton<ISamplesRevisionsManager, SamplesRevisionsManager>();
            services.AddSingleton<ICodeFileManager, CodeFileManager>();
            services.AddSingleton<IUserProfileManager, UserProfileManager>();
            services.AddSingleton<UserProfileCache>();

            services.AddSingleton<LanguageService, JsonLanguageService>();
            services.AddSingleton<LanguageService, CSharpLanguageService>();
            services.AddSingleton<LanguageService, CLanguageService>();
            services.AddSingleton<LanguageService, JavaLanguageService>();
            services.AddSingleton<LanguageService, PythonLanguageService>();
            services.AddSingleton<LanguageService, JavaScriptLanguageService>();
            services.AddSingleton<LanguageService, RustLanguageService>();
            services.AddSingleton<LanguageService, CppLanguageService>();
            services.AddSingleton<LanguageService, GoLanguageService>();
            services.AddSingleton<LanguageService, ProtocolLanguageService>();
            services.AddSingleton<LanguageService, SwaggerLanguageService>();
            services.AddSingleton<LanguageService, SwiftLanguageService>();
            services.AddSingleton<LanguageService, XmlLanguageService>();
            services.AddSingleton<LanguageService, TypeSpecLanguageService>();

            if (Environment.IsDevelopment() && Configuration["AuthenticationScheme"] == "Test")
            {
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });
                services.AddSingleton<IStartupFilter, UITestsStartUpFilter>();
            }
            else
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "CookieFirst";
                    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, CookieFirstAuthenticationHandler>("CookieFirst", options => { })
                .AddCookie(options =>
                {
                    options.LoginPath = "/Login";
                    options.AccessDeniedPath = "/Unauthorized";
                })
                .AddJwtBearer("Bearer", options =>
                {
                    var tenantId = Configuration["AzureAd:TenantId"];
                    var clientId = Configuration["AzureAd:ClientId"];

                    Console.WriteLine($"JWT Configuration - TenantId: {tenantId}, ClientId: {clientId}");

                    if (!string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(clientId))
                    {
                        Console.WriteLine($"Configuring JWT Bearer authentication with Authority: https://login.microsoftonline.com/{tenantId}");
                        
                        options.Authority = $"https://login.microsoftonline.com/{tenantId}";
                        options.Audience = clientId;
                        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidateAudience = true,
                            ValidateLifetime = true,
                            ValidateIssuerSigningKey = true,
                            ClockSkew = TimeSpan.FromMinutes(5),
                            ValidAudiences = [clientId, $"api://{clientId}"],
                            // Accept both v1.0 and v2.0 Azure AD tokens
                            ValidIssuers = [
                                $"https://login.microsoftonline.com/{tenantId}/v2.0",
                                $"https://login.microsoftonline.com/{tenantId}/",
                                $"https://sts.windows.net/{tenantId}/"
                            ]
                        };

                        // Add comprehensive JWT authentication logging
                        options.Events = new JwtBearerEvents
                        {
                            OnMessageReceived = context =>
                            {
                                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Startup>>();
                                logger.LogWarning("🔍 JWT: OnMessageReceived called - Request path: {Path}", context.Request.Path);
                                
                                // Check Authorization header manually
                                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                                logger.LogWarning("🔍 JWT: Authorization header: {AuthHeader}", authHeader ?? "NULL");
                                
                                if (!string.IsNullOrEmpty(context.Token))
                                {
                                    logger.LogWarning("🔍 JWT: Token present, length: {TokenLength}", context.Token.Length);
                                    logger.LogWarning("🔍 JWT: Token starts with: {TokenStart}", context.Token.Substring(0, Math.Min(50, context.Token.Length)));
                                    
                                    // Check if token looks like a valid JWT (should have 3 parts separated by dots)
                                    var parts = context.Token.Split('.');
                                    logger.LogWarning("🔍 JWT: Token has {PartCount} parts (should be 3)", parts.Length);
                                }
                                else
                                {
                                    logger.LogWarning("🔍 JWT: No token found in context.Token");
                                }
                                
                                return Task.CompletedTask;
                            },
                            OnTokenValidated = context =>
                            {
                                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Startup>>();
                                logger.LogWarning("JWT: Token validated successfully for user: {UserName}", 
                                    context.Principal?.Identity?.Name ?? "Anonymous");
                                    
                                // Log all claims
                                var claims = context.Principal?.Claims?.Select(c => $"{c.Type}={c.Value}") ?? Enumerable.Empty<string>();
                                logger.LogDebug("JWT: Token claims: {Claims}", string.Join(", ", claims));
                                
                                return Task.CompletedTask;
                            },
                            OnAuthenticationFailed = context =>
                            {
                                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Startup>>();
                                logger.LogError("JWT: Authentication failed - {Exception}", context.Exception?.Message);
                                logger.LogError("JWT: Exception details: {ExceptionDetails}", context.Exception?.ToString());
                                
                                return Task.CompletedTask;
                            },
                            OnChallenge = context =>
                            {
                                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Startup>>();
                                logger.LogWarning("JWT: Authentication challenge triggered - {Error}: {ErrorDescription}", 
                                    context.Error, context.ErrorDescription);
                                    
                                return Task.CompletedTask;
                            }
                        };
                    }
                    else
                    {

                        Console.WriteLine("All wrong");
                        // Configuration is missing - this will be logged during startup
                        // Logger not available in configuration context
                    }
                })
                .AddOAuth("GitHub", options =>
                {
                    options.ClientId = Configuration["Github:ClientId"];
                    options.ClientSecret = Configuration["Github:ClientSecret"];
                    options.CallbackPath = new PathString("/signin-github");
                    options.Scope.Add("user:email");

                    options.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
                    options.TokenEndpoint = "https://github.com/login/oauth/access_token";
                    options.UserInformationEndpoint = "https://api.github.com/user";

                    options.SaveTokens = true;

                    options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                    options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
                    options.ClaimActions.MapJsonKey(ClaimConstants.Login, "login");
                    options.ClaimActions.MapJsonKey(ClaimConstants.Url, "html_url");
                    options.ClaimActions.MapJsonKey(ClaimConstants.Avatar, "avatar_url");
                    options.ClaimActions.MapJsonKey(ClaimConstants.Name, "name");

                    options.Events = new OAuthEvents
                    {
                        OnCreatingTicket = async context =>
                        {
                            var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

                            var response = await context.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
                            response.EnsureSuccessStatusCode();

                            var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

                            context.RunClaimActions(user.RootElement);
                            if (user.RootElement.TryGetProperty("organizations_url", out var organizationsUrlProperty))
                            {
                                request = new HttpRequestMessage(HttpMethod.Get, organizationsUrlProperty.GetString());
                                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);

                                response = await context.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
                                response.EnsureSuccessStatusCode();

                                var orgs = JArray.Parse(await response.Content.ReadAsStringAsync());
                                var orgNames = new StringBuilder();

                                bool isFirst = true;
                                foreach (var org in orgs)
                                {
                                    if (isFirst)
                                    {
                                        isFirst = false;
                                    }
                                    else
                                    {
                                        orgNames.Append(",");
                                    }
                                    orgNames.Append(org["login"]);
                                }

                                string msEmail = await GetMicrosoftEmailAsync(context);
                                if (msEmail != null)
                                {
                                    context.Identity.AddClaim(
                                        new Claim(ClaimConstants.Email, msEmail));
                                }
                                context.Identity.AddClaim(new Claim(ClaimConstants.Orgs, orgNames.ToString()));
                            }
                        }
                    };
                });
            }

            services.AddAuthorization();
            services.AddCors(options => {
                options.AddPolicy("AllowCredentials", builder =>
                {
                    string [] origins = (Environment.IsDevelopment()) ? URlHelpers.GetAllowedStagingOrigins() : URlHelpers.GetAllowedProdOrigins();
                    builder.WithOrigins(origins)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials()
                        .SetPreflightMaxAge(TimeSpan.FromHours(20));
                });
            });
            services.AddSingleton<IConfigureOptions<AuthorizationOptions>, ConfigureOrganizationPolicy>();
            services.AddSingleton<IConfigureOptions<AuthorizationOptions>, ConfigureOrganizationOrManagedIdentityPolicy>();
            services.AddSingleton<IAuthorizationHandler, OrganizationRequirementHandler>();
            services.AddSingleton<IAuthorizationHandler, CommentOwnerRequirementHandler>();
            services.AddSingleton<IAuthorizationHandler, ReviewOwnerRequirementHandler>();
            services.AddSingleton<IAuthorizationHandler, APIRevisionOwnerRequirementHandler>();
            services.AddSingleton<IAuthorizationHandler, ApproverRequirementHandler>();
            services.AddSingleton<IAuthorizationHandler, ResolverRequirementHandler>();
            services.AddSingleton<IAuthorizationHandler, AutoAPIRevisionModifierRequirementHandler>();
            services.AddSingleton<IAuthorizationHandler, SamplesRevisionOwnerRequirementHandler>();
            services.AddSingleton(x =>
            {
                return new CosmosClient(Configuration["CosmosEndpoint"], new DefaultAzureCredential());
            });
            services.AddSingleton(x =>
            {
                return new BlobServiceClient(new Uri(Configuration["StorageAccountUrl"]), new DefaultAzureCredential());
            });

            services.AddHostedService<ReviewBackgroundHostedService>();
            services.AddHostedService<PullRequestBackgroundHostedService>();
            services.AddHostedService<LinesWithDiffBackgroundHostedService>();
            services.AddHostedService<CopilotPollingBackgroundHostedService>();

            services.AddSingleton<Services.IBackgroundTaskQueue, Services.BackgroundTaskQueue>();
            services.AddHostedService<QueuedHostedService>();

            services.AddControllersWithViews()
                .AddJsonOptions(options => options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve)
                .PartManager.ApplicationParts.Add(new AssemblyPart(typeof(BaseApiController).Assembly));
            services.AddSignalR(options => {
                options.EnableDetailedErrors = true;
                options.MaximumReceiveMessageSize =  1024 * 1024;
            });
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = "v1",
                    Title = "APIView API",
                    Description = "API Endpoints for consuming APIView application",
                    Contact = new OpenApiContact
                    {
                        Name = "Azure SDK Engineering Systems",
                        Url = new Uri("https://teams.microsoft.com/l/channel/19%3a3adeba4aa1164f1c889e148b1b3e3ddd%40thread.skype/APIView?groupId=3e17dcb0-4257-4a30-b843-77f47f1d4121&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47")
                    }
                });
                var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
            });
        }

        private static async Task<string> GetMicrosoftEmailAsync(OAuthCreatingTicketContext context)
        {
            var message = new HttpRequestMessage(
                HttpMethod.Get,
                "https://api.github.com/user/emails");
            message.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                context.AccessToken);

            var response = await context.Backchannel.SendAsync(message);

            var respString = await response.Content.ReadAsStringAsync();
            try
            {
                var emails = JArray.Parse(respString);
                foreach (var email in emails)
                {
                    var address = email["email"]?.Value<string>();
                    if (address != null && address.EndsWith("@microsoft.com", StringComparison.OrdinalIgnoreCase))
                    {
                        return address;
                    }
                }
                return null;
            }
            catch (Exception e)
            {
                throw new Exception(respString, e);
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseCors("AllowCredentials");
            app.UseCookiePolicy();
            app.UseAuthentication();
            
            // Add custom middleware to log authentication details
            app.Use(async (context, next) =>
            {
                var logger = context.RequestServices.GetRequiredService<ILogger<Startup>>();
                
                logger.LogWarning("🔍 Authentication Debug - Path: {Path}, Method: {Method}", 
                    context.Request.Path, context.Request.Method);
                    
                // Check if Authorization header is present BEFORE authentication
                if (context.Request.Headers.ContainsKey("Authorization"))
                {
                    var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                    logger.LogWarning("🔍 BEFORE Auth - Authorization header present: {AuthHeader}", 
                        authHeader?.Substring(0, Math.Min(50, authHeader?.Length ?? 0)) + "...");
                }
                else
                {
                    logger.LogWarning("🔍 BEFORE Auth - No Authorization header found in request");
                }
                
                await next();
                
                // Check authentication AFTER middleware processing
                logger.LogWarning("🔍 AFTER Auth - User authenticated: {IsAuthenticated}, Identity type: {AuthType}, Name: {Name}", 
                    context.User?.Identity?.IsAuthenticated ?? false,
                    context.User?.Identity?.AuthenticationType ?? "None",
                    context.User?.Identity?.Name ?? "Anonymous");
                    
                if (context.User?.Identity?.IsAuthenticated == true)
                {
                    var claims = context.User.Claims.Select(c => $"{c.Type}={c.Value}").Take(5);
                    logger.LogWarning("🔍 AFTER Auth - User claims (first 5): {Claims}", string.Join(", ", claims));
                }
            });
            
            app.UseAuthorization();
            app.UseMiddleware<SwaggerAuthMiddleware>();
            app.UseMiddleware<RequestLoggingMiddleware>();
            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseEndpoints(endpoints => {
                endpoints.MapRazorPages();
                endpoints.MapDefaultControllerRoute();
                endpoints.MapHub<SignalRHub>("hubs/notification");
            });
        }
    }

    /// <summary>
    /// Custom authentication handler that tries Cookie authentication first, then JWT Bearer as fallback
    /// </summary>
    public class CookieFirstAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public CookieFirstAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            logger.LogWarning("🔍 CookieFirst: Starting authentication for {Path}", Request.Path);

            // Try Cookie authentication first
            logger.LogWarning("🔍 CookieFirst: Trying Cookie authentication...");
            var cookieResult = await Context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            
            if (cookieResult.Succeeded)
            {
                logger.LogWarning("🔍 CookieFirst: Cookie authentication succeeded");
                return cookieResult;
            }

            logger.LogWarning("🔍 CookieFirst: Cookie authentication failed, checking for Bearer token...");

            // If Cookie failed, check for Authorization header with Bearer token
            if (Request.Headers.ContainsKey("Authorization"))
            {
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (authHeader?.StartsWith("Bearer ") == true)
                {
                    logger.LogWarning("🔍 CookieFirst: Bearer token found, trying JWT authentication...");
                    var jwtResult = await Context.AuthenticateAsync("Bearer");
                    
                    if (jwtResult.Succeeded)
                    {
                        logger.LogWarning("🔍 CookieFirst: JWT authentication succeeded");
                        return jwtResult;
                    }
                    else
                    {
                        Logger.LogWarning("🔍 CookieFirst: JWT authentication failed: {Failure}", jwtResult.Failure?.Message);
                    }
                }
                else
                {
                    logger.LogWarning("🔍 CookieFirst: Authorization header present but not Bearer token");
                }
            }
            else
            {
                logger.LogWarning("🔍 CookieFirst: No Authorization header found");
            }

            logger.LogWarning("🔍 CookieFirst: All authentication methods failed");
            return AuthenticateResult.NoResult();
        }
    }
}
