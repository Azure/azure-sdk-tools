using System;
using System.IO;
using System.IO.Compression;
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
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using APIViewWeb.Services;
using Microsoft.AspNetCore.ResponseCompression;

namespace APIViewWeb
{
    public class Startup
    {
        public static string RequireOrganizationPolicy = "RequireOrganization";
        public static string RequireCookieAuthenticationPolicy = "RequireCookieAuthentication";
        public static string RequireTokenAuthenticationPolicy = "RequireTokenAuthentication";
        public static string RequireTokenOrCookieAuthenticationPolicy = "RequireTokenOrCookieAuthentication"; 

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
            services.AddAzureAppConfiguration();
            services.AddMemoryCache();

            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.MimeTypes = new[] { "application/json" };
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();
            });
            services.Configure<BrotliCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.Fastest;
            });
            services.Configure<GzipCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.Fastest;
            });

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
            services.AddSingleton<ICopilotAuthenticationService, CopilotAuthenticationService>();
            services.AddSingleton<IPollingJobQueueManager, PollingJobQueueManager>();
            services.AddSingleton<ICopilotJobProcessor, CopilotJobProcessor>();
            services.AddSingleton<IBlobCodeFileRepository, BlobCodeFileRepository>();
            services.AddSingleton<IBlobOriginalsRepository, BlobOriginalsRepository>();
            services.AddSingleton<IBlobUsageSampleRepository, BlobUsageSampleRepository>();

            services.AddSingleton<ICosmosReviewRepository,CosmosReviewRepository>();
            services.AddSingleton<ICosmosAPIRevisionsRepository, CosmosAPIRevisionsRepository>();
            services.AddSingleton<ICosmosCommentsRepository, CosmosCommentsRepository>();
            services.AddSingleton<ICosmosPullRequestsRepository, CosmosPullRequestsRepository>();
            services.AddSingleton<ICosmosSamplesRevisionsRepository, CosmosSamplesRevisionsRepository>();
            services.AddSingleton<ICosmosUserProfileRepository, CosmosUserProfileRepository>();
            services.AddSingleton<ICosmosPermissionsRepository, CosmosPermissionsRepository>();
            services.AddSingleton<ICosmosProjectRepository, CosmosProjectRepository>();
            services.AddSingleton<IDevopsArtifactRepository, DevopsArtifactRepository>();

            services.AddSingleton<IDiagnosticCommentService, DiagnosticCommentService>();
            services.AddSingleton<IReviewManager, ReviewManager>();
            services.AddSingleton<IAPIRevisionsManager, APIRevisionsManager>();
            services.AddSingleton<IProjectsManager, ProjectsManager>();
            services.AddSingleton<IReviewSearch, ReviewSearch>();
            services.AddSingleton<IAutoReviewService, AutoReviewService>();
            services.AddSingleton<ICommentsManager, CommentsManager>();
            services.AddSingleton<INotificationManager, NotificationManager>();
            services.AddSingleton<IEmailTemplateService, EmailTemplateService>();
            services.AddSingleton<IPullRequestManager, PullRequestManager>();
            services.AddSingleton<IPackageNameManager, PackageNameManager>();
            services.AddSingleton<ISamplesRevisionsManager, SamplesRevisionsManager>();
            services.AddSingleton<ICodeFileManager, CodeFileManager>();
            services.AddSingleton<IUserProfileManager, UserProfileManager>();
            services.AddSingleton<IPermissionsManager, PermissionsManager>();
            services.AddSingleton<IGitHubClientFactory, GitHubClientFactory>();
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
                    options.DefaultAuthenticateScheme = "CookieOrTokenAuth";
                    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, CookieOrTokenAuthenticationHandler>("CookieOrTokenAuth", options => { })
                .AddCookie(options =>
                {
                    options.LoginPath = "/Login";
                    options.AccessDeniedPath = "/Unauthorized";
                    options.Events.OnRedirectToAccessDenied = context =>
                    {
                        if (context.Request.Path.StartsWithSegments("/api"))
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            return Task.CompletedTask;
                        }
                        
                        context.Response.Redirect(context.RedirectUri);
                        return Task.CompletedTask;
                    };
                })
                .AddScheme<AuthenticationSchemeOptions, TokenAuthenticationHandler>("TokenAuth", options => { })
                .AddJwtBearer("Bearer", options =>
                {
                    string tenantId = Configuration["AzureAd:TenantId"];
                    string clientId = Configuration["AzureAd:ClientId"];

                    if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId))
                    {
                        return;
                    }

                    options.Authority = $"https://login.microsoftonline.com/{tenantId}";
                    options.Audience = clientId;
                    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ClockSkew = TimeSpan.FromMinutes(5),
                        ValidAudiences = [
                            clientId, 
                            $"api://{clientId}"
                        ],
                        ValidIssuers = [
                            $"https://login.microsoftonline.com/{tenantId}/v2.0",
                            $"https://login.microsoftonline.com/{tenantId}/",
                            $"https://sts.windows.net/{tenantId}/"
                        ]
                    };
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

                                var orgsJson = await response.Content.ReadAsStringAsync();
                                using var orgsDoc = JsonDocument.Parse(orgsJson);
                                var orgNames = new StringBuilder();

                                bool isFirst = true;
                                foreach (var org in orgsDoc.RootElement.EnumerateArray())
                                {
                                    if (isFirst)
                                    {
                                        isFirst = false;
                                    }
                                    else
                                    {
                                        orgNames.Append(",");
                                    }
                                    if (org.TryGetProperty("login", out var loginProperty))
                                    {
                                        orgNames.Append(loginProperty.GetString());
                                    }
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
            services.AddSingleton<IConfigureOptions<AuthorizationOptions>, ConfigureCookieOrTokenPolicy>();
            services.AddSingleton<IConfigureOptions<AuthorizationOptions>, ConfigureCookieAuthenticationPolicy>();
            services.AddSingleton<IConfigureOptions<AuthorizationOptions>, ConfigureTokenAuthenticationPolicy>();
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
                var cosmosClientOptions = new CosmosClientOptions
                {
                    UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                        Converters = { new JsonStringEnumConverter() }
                    }
                };
                
                return new CosmosClient(Configuration["CosmosEndpoint"], CredentialProvider.GetAzureCredential(), cosmosClientOptions);
            });
            services.AddSingleton(x =>
            {
                return new BlobServiceClient(new Uri(Configuration["StorageAccountUrl"]), CredentialProvider.GetAzureCredential());
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
            }).AddJsonProtocol(options => {
                options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
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
                using var emailsDoc = JsonDocument.Parse(respString);
                foreach (var email in emailsDoc.RootElement.EnumerateArray())
                {
                    if (email.TryGetProperty("email", out var emailProperty))
                    {
                        var address = emailProperty.GetString();
                        if (address != null && address.EndsWith("@microsoft.com", StringComparison.OrdinalIgnoreCase))
                        {
                            return address;
                        }
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
            app.UseResponseCompression();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseCors("AllowCredentials");
            app.UseCookiePolicy();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseAzureAppConfiguration();
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
}
