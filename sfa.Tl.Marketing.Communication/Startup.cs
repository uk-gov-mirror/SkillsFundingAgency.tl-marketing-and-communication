﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using sfa.Tl.Marketing.Communication.Application.GeoLocations;
using sfa.Tl.Marketing.Communication.Application.Interfaces;
using sfa.Tl.Marketing.Communication.Application.Services;
using sfa.Tl.Marketing.Communication.Models.Configuration;
using sfa.Tl.Marketing.Communication.SearchPipeline;
using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos.Table;
using Notify.Client;
using Notify.Interfaces;
using sfa.Tl.Marketing.Communication.Application.Repositories;

namespace sfa.Tl.Marketing.Communication
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        protected ConfigurationOptions SiteConfiguration;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public Startup(IConfiguration configuration, IWebHostEnvironment webHostEnvironment)
        {
            Configuration = configuration;
            _webHostEnvironment = webHostEnvironment;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            if(!int.TryParse(Configuration["CacheExpiryInSeconds"], out var cacheExpiryInSeconds))
            {
                cacheExpiryInSeconds = 60;
            }

            SiteConfiguration = new ConfigurationOptions
            {
                CacheExpiryInSeconds = cacheExpiryInSeconds,
                PostcodeRetrieverBaseUrl = Configuration["PostcodeRetrieverBaseUrl"],
                EmployerContactEmailTemplateId = Configuration["EmployerContactEmailTemplateId"],
                SupportEmailInboxAddress = Configuration["SupportEmailInboxAddress"],
                StorageConfiguration = new StorageSettings
                {
                    TableStorageConnectionString = Configuration[ConfigurationKeys.TableStorageConnectionStringConfigKey]
                }
            };

            services.AddApplicationInsightsTelemetry();

            services.AddSingleton(SiteConfiguration);

            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = _ => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddAntiforgery(options =>
            {
                options.Cookie.Name = "tlevels-mc-x-csrf";
                options.FormFieldName = "_csrfToken";
                options.HeaderName = "X-XSRF-TOKEN";
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            });

            RegisterHttpClients(services);
            RegisterServices(services);

            services.AddMemoryCache();

            var mvcBuilder = services.AddControllersWithViews()
                .AddMvcOptions(config =>
                {
                    config.Filters.Add<AutoValidateAntiforgeryTokenAttribute>();
                });

            if (_webHostEnvironment.IsDevelopment())
            {
                mvcBuilder.AddRazorRuntimeCompilation();
            }
            else
            {
                services.AddHsts(options =>
                {
                    options.MaxAge = TimeSpan.FromDays(365);
                });
            }
            services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        // ReSharper disable once UnusedMember.Global
        public void Configure(IApplicationBuilder app, 
            IWebHostEnvironment env)
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

            app.UseXContentTypeOptions();
            app.UseReferrerPolicy(opts => opts.NoReferrer());
            app.UseXXssProtection(opts => opts.EnabledWithBlockMode());
            app.UseXfo(xfo => xfo.Deny());

            app.UseCsp(options => options.ScriptSources(s => s
                        //.StrictDynamic()
                        .CustomSources("https:",
                            "https://www.google-analytics.com/analytics.js",
                            "https://www.googletagmanager.com/",
                            "https://tagmanager.google.com/",
                            "https://www.youtube.com/iframe_api")
                        .UnsafeInline()
                )
                .ObjectSources(s => s.None()));

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        protected virtual void RegisterHttpClients(IServiceCollection services)
        {
            services.AddHttpClient<ILocationApiClient, LocationApiClient>();
        }

        protected virtual void RegisterServices(IServiceCollection services)
        {
            services.AddTransient<IFileReader, FileReader>();
            services.AddSingleton<IProviderDataService, ProviderDataService>();
            services.AddTransient<IEmailService, EmailService>();
            services.AddTransient<IDistanceCalculationService, DistanceCalculationService>();
            services.AddTransient<IJourneyService, JourneyService>();
            services.AddTransient<IProviderSearchService, ProviderSearchService>();
            services.AddTransient<ISearchPipelineFactory, SearchPipelineFactory>();
            services.AddTransient<IProviderSearchEngine, ProviderSearchEngine>();

            var cloudStorageAccount =
                CloudStorageAccount.Parse(SiteConfiguration.StorageConfiguration.TableStorageConnectionString);
            services.AddSingleton(cloudStorageAccount);
            var cloudTableClient = cloudStorageAccount.CreateCloudTableClient();
            services.AddSingleton(cloudTableClient);

            services.AddTransient(typeof(ICloudTableRepository<>), typeof(GenericCloudTableRepository<>));

            services.AddTransient<ITableStorageService, TableStorageService>();
            
            var govNotifyApiKey = Configuration["GovNotifyApiKey"];
            services.AddTransient<IAsyncNotificationClient, NotificationClient>(
                _ => new NotificationClient(govNotifyApiKey));
        }
    }
}
