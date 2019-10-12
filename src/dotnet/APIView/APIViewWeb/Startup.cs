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

namespace APIViewWeb
{
    public class Startup
    {
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
                .SetCompatibilityVersion(CompatibilityVersion.Latest);
            services.AddRazorPages(options =>
            {
                options.Conventions.AuthorizeFolder("/Assemblies", "RequireOrganization");
                options.Conventions.AddPageRoute("/Assemblies/Index", "");
            });

            services.AddSingleton<BlobCodeFileRepository>();
            services.AddSingleton<BlobOriginalsRepository>();
            services.AddSingleton<CosmosReviewRepository>();
            services.AddSingleton<CosmosCommentsRepository>();

            services.AddSingleton<ReviewManager>();
            services.AddSingleton<CommentsManager>();

            services.AddSingleton<ILanguageService, JsonLanguageService>();
            services.AddSingleton<ILanguageService, CSharpLanguageService>();
            services.AddSingleton<ILanguageService, JavaLanguageService>();

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                })
                .AddCookie(options => options.LoginPath = "/Unauthorized")
                .AddOAuth("GitHub", options =>
                {
                    options.ClientId = Configuration["Github:ClientId"];
                    options.ClientSecret = Configuration["Github:ClientSecret"];
                    options.CallbackPath = new PathString("/signin-github");

                    options.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
                    options.TokenEndpoint = "https://github.com/login/oauth/access_token";
                    options.UserInformationEndpoint = "https://api.github.com/user";

                    options.SaveTokens = true;

                    options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                    options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
                    options.ClaimActions.MapJsonKey("urn:github:login", "login");
                    options.ClaimActions.MapJsonKey("urn:github:url", "html_url");
                    options.ClaimActions.MapJsonKey("urn:github:avatar", "avatar_url");

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

                                context.Identity.AddClaim(new Claim("urn:github:orgs", orgNames.ToString()));
                            }
                        }
                    };
                });

            services.AddAuthorization();
            services.AddSingleton<IConfigureOptions<AuthorizationOptions>, ConfigureOrganizationPolicy>();

            services.AddSingleton<IAuthorizationHandler, OrganizationRequirementHandler>();
            services.AddSingleton<IAuthorizationHandler, CommentOwnerRequirementHandler>();
            services.AddSingleton<IAuthorizationHandler, ReviewOwnerRequirementHandler>();
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
