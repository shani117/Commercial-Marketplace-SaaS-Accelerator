// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Azure.Identity;
using Azure.ResourceManager;
using Azure.Security.KeyVault.Secrets;
using Marketplace.SaaS.Accelerator.CustomerSite.Controllers;
using Marketplace.SaaS.Accelerator.CustomerSite.GraphOperations;
using Marketplace.SaaS.Accelerator.CustomerSite.WebHook;
using Marketplace.SaaS.Accelerator.DataAccess.Context;
using Marketplace.SaaS.Accelerator.DataAccess.Contracts;
using Marketplace.SaaS.Accelerator.DataAccess.Entities;
using Marketplace.SaaS.Accelerator.DataAccess.Services;
using Marketplace.SaaS.Accelerator.Services.Configurations;
using Marketplace.SaaS.Accelerator.Services.Contracts;
using Marketplace.SaaS.Accelerator.Services.Services;
using Marketplace.SaaS.Accelerator.Services.Utilities;
using Marketplace.SaaS.Accelerator.Services.WebHook;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.Marketplace.SaaS;
using System;

namespace Marketplace.SaaS.Accelerator.CustomerSite;

/// <summary>
/// Defines the <see cref="Startup" />.
/// </summary>
public class Startup
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="configuration">The configuration<see cref="IConfiguration"/>.</param>
    public Startup(IConfiguration configuration)
    {
        this.Configuration = configuration;
    }

    /// <summary>
    /// Gets the Configuration.
    /// </summary>
    public IConfiguration Configuration { get; }

    /// <summary>
    /// The ConfigureServices.
    /// </summary>
    /// <param name="services">The services<see cref="IServiceCollection"/>.</param>
    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<CookiePolicyOptions>(options =>
        {
            // This lambda determines whether user consent for non-essential cookies is needed for a given request.
            options.CheckConsentNeeded = context => true;
            options.MinimumSameSitePolicy = SameSiteMode.None;
        });

        var config = new SaaSApiClientConfiguration()
        {
            AdAuthenticationEndPoint = this.Configuration["SaaSApiConfiguration:AdAuthenticationEndPoint"],
            ClientId = this.Configuration["SaaSApiConfiguration:ClientId"],
            ClientSecret = this.Configuration["SaaSApiConfiguration:ClientSecret"],
            MTClientId = this.Configuration["SaaSApiConfiguration:MTClientId"],
            FulFillmentAPIBaseURL = this.Configuration["SaaSApiConfiguration:FulFillmentAPIBaseURL"],
            FulFillmentAPIVersion = this.Configuration["SaaSApiConfiguration:FulFillmentAPIVersion"],
            GrantType = this.Configuration["SaaSApiConfiguration:GrantType"],
            Resource = this.Configuration["SaaSApiConfiguration:Resource"],
            SaaSAppUrl = this.Configuration["SaaSApiConfiguration:SaaSAppUrl"],
            SignedOutRedirectUri = this.Configuration["SaaSApiConfiguration:SignedOutRedirectUri"],
            TenantId = this.Configuration["SaaSApiConfiguration:TenantId"],
            Environment = this.Configuration["SaaSApiConfiguration:Environment"]
        };
        var creds = new ClientSecretCredential(config.TenantId.ToString(), config.ClientId.ToString(), config.ClientSecret);

        services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApp(Configuration.GetSection("AzureAd"))
            .EnableTokenAcquisitionToCallDownstreamApi()
            .AddInMemoryTokenCaches();

        services.AddGraphService(this.Configuration);

        services
            .AddTransient<IClaimsTransformation, CustomClaimsTransformation>()
            .AddScoped<ExceptionHandlerAttribute>()
            .AddScoped<RequestLoggerActionFilter>();

        if (!Uri.TryCreate(config.FulFillmentAPIBaseURL, UriKind.Absolute, out var fulfillmentBaseApi)) 
        {
            fulfillmentBaseApi = new Uri("https://marketplaceapi.microsoft.com/api");
        }

        services
            .AddSingleton<IFulfillmentApiService>(new FulfillmentApiService(new MarketplaceSaaSClient(fulfillmentBaseApi, creds), config, new FulfillmentApiClientLogger()))
            .AddSingleton<SaaSApiClientConfiguration>(config);

        services
            .AddDbContext<SaasKitContext>(options => options.UseSqlServer(this.Configuration.GetConnectionString("DefaultConnection")));

        //make sure the app service is marked as a contributor on the subscription and has permissions to write to the AKV.
        ArmClient armClient = new ArmClient(new DefaultAzureCredential(), Configuration["AzureSubscriptionId"]);
        SecretClient secretClient = new SecretClient(vaultUri: new Uri(Configuration["VaultUrl"]), credential: new DefaultAzureCredential());

        services.AddScoped<IAzureSubService, AzureSubService>(provider =>
        {
            SaaSClientLogger<AzureSubService> subLogger = new SaaSClientLogger<AzureSubService>();
            return new AzureSubService(armClient, secretClient, subLogger);
        });

        InitializeRepositoryServices(services);

        services.AddControllersWithViews(options =>
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
            options.Filters.Add(new AuthorizeFilter(policy));
        }).AddMicrosoftIdentityUI();
        services.AddRazorPages();

        //services.AddMvc(option => option.EnableEndpointRouting = false);
    }

    /// <summary>
    /// The Configure.
    /// </summary>
    /// <param name="app">The app<see cref="IApplicationBuilder" />.</param>
    /// <param name="env">The env<see cref="IWebHostEnvironment" />.</param>
    /// <param name="loggerFactory">The loggerFactory<see cref="ILoggerFactory" />.</param>
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseCookiePolicy();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        //app.UseMvc(routes =>
        //{
        //    routes.MapRoute(
        //        name: "Index",
        //        template: "{controller=Home}/{action=Index}/{id?}");

        //    routes.MapRoute(
        //        name: "default",
        //        template: "{controller}/{action}/{id?}");
        //});

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllerRoute(
                name: "Index",
                pattern: "{controller=Home}/{action=Index}/{id?}");
            endpoints.MapRazorPages();

            endpoints.MapControllerRoute(
                name: "default",
                pattern: "{controller}/{action}/{id?}");
            endpoints.MapRazorPages();
        });
    }

    private static void InitializeRepositoryServices(IServiceCollection services)
    {
        services.AddScoped<ISubscriptionsRepository, SubscriptionsRepository>();
        services.AddScoped<IPlansRepository, PlansRepository>();
        services.AddScoped<IUsersRepository, UsersRepository>();
        services.AddScoped<ISubscriptionLogRepository, SubscriptionLogRepository>();
        services.AddScoped<IApplicationLogRepository, ApplicationLogRepository>();
        services.AddScoped<IWebhookProcessor, WebhookProcessor>();
        services.AddScoped<IWebhookHandler, WebHookHandler>();
        services.AddScoped<IApplicationConfigRepository, ApplicationConfigRepository>();
        services.AddScoped<IEmailTemplateRepository, EmailTemplateRepository>();
        services.AddScoped<IOffersRepository, OffersRepository>();
        services.AddScoped<IOfferAttributesRepository, OfferAttributesRepository>();
        services.AddScoped<IPlanEventsMappingRepository, PlanEventsMappingRepository>();
        services.AddScoped<IEventsRepository, EventsRepository>();
        services.AddScoped<IEmailService, SMTPEmailService>();
        services.AddScoped<SaaSClientLogger<HomeController>>();
        services.AddScoped<IWebNotificationService, WebNotificationService>();
    }
}