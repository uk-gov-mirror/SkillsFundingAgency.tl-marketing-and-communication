﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using sfa.Tl.Marketing.Communication.Application.Interfaces;
using sfa.Tl.Marketing.Communication.Models.Dto;

namespace sfa.Tl.Marketing.Communication.Application.Services
{
    public class CourseDirectoryDataService : ICourseDirectoryDataService
    {
        public const string CourseDirectoryHttpClientName = "CourseDirectoryAutoCompressClient";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ITableStorageService _tableStorageService;
        private readonly ILogger<CourseDirectoryDataService> _logger;

        public CourseDirectoryDataService(
            IHttpClientFactory httpClientFactory,
            ITableStorageService tableStorageService,
            ILogger<CourseDirectoryDataService> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _tableStorageService = tableStorageService ?? throw new ArgumentNullException(nameof(tableStorageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<int> ImportFromCourseDirectoryApi()
        {
            _logger.LogInformation("ImportFromCourseDirectoryApi called");

            var httpClient = _httpClientFactory.CreateClient(CourseDirectoryHttpClientName);

            //https://dev.api.nationalcareersservice.org.uk/coursedirectory/findacourse/tleveldetail[?TLevelId]
            // ReSharper disable once StringLiteralTypo
            var response = await httpClient.GetAsync("tleveldetail");
            if (response.StatusCode != HttpStatusCode.OK)
            {
                //TODO: Add logger
                Console.WriteLine($"Response failed with {response.StatusCode} - {response.ReasonPhrase}");
            }

            //var content = await response.Content.ReadAsStringAsync();
            var jsonDoc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var root = jsonDoc.RootElement;

            //Should always check "offeringType": "TLevel"
            string offeringType = null;
            if (root.TryGetProperty("offeringType", out var offeringTypeElement))
            {
                offeringType = offeringTypeElement.GetString();
            }

            Console.WriteLine($"offeringType: {offeringType}");

            //For the initial version we just need to confirm 1 record was found. This will change before go-live
            //Should count json records, or records saved
            var count = offeringType == "TLevel" ? 1 : 0;

            _logger.LogInformation($"ImportFromCourseDirectoryApi saved {count} records");

            return count;
        }

        public async Task<IList<Provider>> GetProviders()
        {
            return (await _tableStorageService.RetrieveProviders())
                .OrderBy(p => p.Id).ToList();
        }

        public async Task<IList<Qualification>> GetQualifications()
        {
            return (await _tableStorageService
                .RetrieveQualifications())
                .OrderBy(q => q.Id).ToList();
        }
    }
}