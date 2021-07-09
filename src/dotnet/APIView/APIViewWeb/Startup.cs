using System;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using APIViewWeb.Respositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using APIViewWeb.Repositories;
using System.Threading.Tasks;
using APIViewWeb.HostedServices;

namespace APIViewWeb
{
    public class Startup
    {
        public static string RequireOrganizationPolicy = "RequireOrganization";

        public static string VersionHash { get; set; }

        static Startup()
        {
            var version = typeof(Startup).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            var indexOfPlus = version.IndexOf("+", StringComparison.OrdinalIgnoreCase);
            VersionHash = indexOfPlus == -1 ? "dev" : version.Substring(indexOfPlus + 1);
        }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddApplicationInsightsTelemetry();

            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.Configure<OrganizationOptions>(options => Configuration
                .GetSection("Github")
                .Bind(options));

            services.AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Latest)
                .AddRazorRuntimeCompilation();

            services.AddRazorPages(options =>
            {
                options.Conventions.AuthorizeFolder("/Assemblies", RequireOrganizationPolicy);
                options.Conventions.AddPageRoute("/Assemblies/Index", "");
            });

            services.AddSingleton<BlobCodeFileRepository>();
            services.AddSingleton<BlobOriginalsRepository>();
            services.AddSingleton<CosmosReviewRepository>();
            services.AddSingleton<CosmosCommentsRepository>();

            services.AddSingleton<ReviewManager>();
            services.AddSingleton<CommentsManager>();
            services.AddSingleton<NotificationManager>();

            services.AddSingleton<LanguageService, JsonLanguageService>();
            services.AddSingleton<LanguageService, CSharpLanguageService>();
            services.AddSingleton<LanguageService, CLanguageService>();
            services.AddSingleton<LanguageService, JavaLanguageService>();
            services.AddSingleton<LanguageService, PythonLanguageService>();
            services.AddSingleton<LanguageService, JavaScriptLanguageService>();
            services.AddSingleton<LanguageService, CppLanguageService>();
            services.AddSingleton<LanguageService, GoLanguageService>();
            services.AddSingleton<LanguageService, ProtocolLanguageService>();

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                })
                .AddCookie(options =>
                {
                    options.LoginPath = "/Login";
                    options.AccessDeniedPath = "/Unauthorized";
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

            services.AddAuthorization();
            services.AddSingleton<IConfigureOptions<AuthorizationOptions>, ConfigureOrganizationPolicy>();

            services.AddSingleton<IAuthorizationHandler, OrganizationRequirementHandler>();
            services.AddSingleton<IAuthorizationHandler, CommentOwnerRequirementHandler>();
            services.AddSingleton<IAuthorizationHandler, ReviewOwnerRequirementHandler>();
            services.AddSingleton<IAuthorizationHandler, RevisionOwnerRequirementHandler>();
            services.AddSingleton<IAuthorizationHandler, ApproverRequirementHandler>();
            services.AddSingleton<IAuthorizationHandler, AutoReviewModifierRequirementHandler>();
            services.AddHostedService<ReviewBackgroundHostedService>();
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

            app.UseCookiePolicy();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints => {
                endpoints.MapRazorPages();
                endpoints.MapDefaultControllerRoute();
            });
        }
    }
}
