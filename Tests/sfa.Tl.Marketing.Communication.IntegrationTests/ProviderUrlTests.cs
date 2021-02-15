using sfa.Tl.Marketing.Communication.Application.GeoLocations;
using sfa.Tl.Marketing.Communication.Application.Interfaces;
using sfa.Tl.Marketing.Communication.Application.Services;
using sfa.Tl.Marketing.Communication.Models.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using sfa.Tl.Marketing.Communication.Application.Repositories;
using sfa.Tl.Marketing.Communication.Models.Entities;
using Xunit;
using Xunit.Abstractions;

namespace sfa.Tl.Marketing.Communication.IntegrationTests
{
    public class ProviderUrlTests
    {
        private readonly IProviderSearchService _providerSearchService;
        private readonly ITestOutputHelper _outputHelper;

        public ProviderUrlTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;

            var configurationOptions = LoadConfiguration();

            var loggerFactory = new LoggerFactory();
            var providerDataServiceLogger = loggerFactory.CreateLogger<ProviderDataService>();

            var cache = new MemoryCache(new MemoryCacheOptions());

            var tableStorageService = CreateTableStorageService(
                configurationOptions.StorageConfiguration.TableStorageConnectionString,
                loggerFactory);

            IProviderDataService providerDataService = new ProviderDataService(tableStorageService, cache, providerDataServiceLogger);
            var locationService = new LocationService();
            var journeyService = new JourneyService();
            var providerLocationService = new ProviderLocationService(providerDataService);
            var distanceCalculationService = new DistanceCalculationService(new LocationApiClient(new HttpClient(), configurationOptions));
            _providerSearchService = new ProviderSearchService(providerDataService, journeyService, locationService, providerLocationService, distanceCalculationService);
        }

        [Fact]
        public async Task Check_If_a_Provider_Website_Is_Broken()
        {
            var distinctProviderUrls =
                (from r in _providerSearchService.GetAllProviderLocations()
                 group r by new { r.ProviderName, r.Website } into g
                 orderby g.Key.ProviderName
                 select (g.Key.ProviderName, g.Key.Website)
                ).ToList();

            var brokenProviderUrls = new List<(string ProviderName, string Website)>();

            foreach (var location in distinctProviderUrls)
            {
                Debug.WriteLine($"{location.ProviderName} - {location.Website}");

                var isUrlBroken = await IsUrlBroken(location.Website);
                if (isUrlBroken)
                {
                    brokenProviderUrls.Add(location);
                }
            }

            if (brokenProviderUrls.Any())
            {
                _outputHelper.WriteLine($"\n{brokenProviderUrls.Count} out of {distinctProviderUrls.Count} provider websites have broken urls, as shown below:\n");

                foreach (var (providerName, website) in brokenProviderUrls)
                {
                    _outputHelper.WriteLine($"\t{providerName} - {website}");
                }
            }
            else
            {
                var successMessage = $"All {distinctProviderUrls.Count} provider websites are working fine.";
                _outputHelper.WriteLine(successMessage);
            }

            brokenProviderUrls.Count.Should().Be(0,
                $"because there should be no broken urls. There are {brokenProviderUrls.Count} out of {distinctProviderUrls.Count} locations with broken websites.");
        }

        private static ConfigurationOptions LoadConfiguration()
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.test.json")
                .Build();

            return new ConfigurationOptions
            {
                PostcodeRetrieverBaseUrl = config["PostcodeRetrieverBaseUrl"],
                StorageConfiguration = new StorageSettings
                {
                    TableStorageConnectionString = config["TableStorageConnectionString"]
                }
            };
        }

        private static ITableStorageService CreateTableStorageService(
            string tableStorageConnectionString,
            ILoggerFactory loggerFactory)
        {
            var cloudStorageAccount = CloudStorageAccount.Parse(tableStorageConnectionString);

            var cloudTableClient = cloudStorageAccount.CreateCloudTableClient();

            var providerRepository = new GenericCloudTableRepository<ProviderEntity, long>(
                cloudTableClient,
                loggerFactory.CreateLogger<GenericCloudTableRepository<ProviderEntity, long>>());

            var qualificationRepository =  new GenericCloudTableRepository<QualificationEntity, int>(
                    cloudTableClient,
                    loggerFactory.CreateLogger<GenericCloudTableRepository<QualificationEntity, int>>());

            return new TableStorageService(
                providerRepository,
                qualificationRepository,
                loggerFactory.CreateLogger<TableStorageService>());
        }

        private async Task<bool> IsUrlBroken(string url)
        {
            var isUrlBroken = false;

            try
            {
                var clientHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        (sender, cert, chain, sslPolicyErrors)
                            => true
                };

                using var client = new HttpClient(clientHandler)
                {
                    Timeout = TimeSpan.FromSeconds(10)
                };

                var checkingResponse = await client.GetAsync(url);
                if (checkingResponse.StatusCode == HttpStatusCode.NotFound)
                {

                    isUrlBroken = true;
                }
            }
            catch (Exception ex)
            {
                _outputHelper.WriteLine($"\n\rWebsite: {url}\nError message: {ex.Message}\nStackTrace: {ex.StackTrace}\n");
            }

            return isUrlBroken;
        }
    }
}
