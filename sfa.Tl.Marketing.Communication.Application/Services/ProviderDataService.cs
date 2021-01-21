﻿using System;
using sfa.Tl.Marketing.Communication.Application.Interfaces;
using sfa.Tl.Marketing.Communication.Models.Configuration;
using sfa.Tl.Marketing.Communication.Models.Dto;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace sfa.Tl.Marketing.Communication.Application.Services
{
    public class ProviderDataService : IProviderDataService
    {
        private readonly IFileReader _fileReader;
        private readonly ConfigurationOptions _configurationOptions;
        private readonly JsonDocument _providersData;
        private readonly JsonDocument _qualificationsData;
        private readonly IList<Provider> _providerTableData;
        private readonly IList<Qualification> _qualificationTableData;

        private readonly ITableStorageService _tableStorageService;
        private readonly ILogger<ProviderDataService> _logger;

        public ProviderDataService(
            IFileReader fileReader,
            ConfigurationOptions configurationOptions,
            ITableStorageService tableStorageService,
            ILogger<ProviderDataService> logger)
        {
            _fileReader = fileReader ?? throw new ArgumentNullException(nameof(fileReader));
            _configurationOptions = configurationOptions ?? throw new ArgumentNullException(nameof(configurationOptions));
            _tableStorageService = tableStorageService ?? throw new ArgumentNullException(nameof(tableStorageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _providersData = GetProvidersData();
            _qualificationsData = GetQualificationsData();
            _providerTableData = LoadProviderTableData().GetAwaiter().GetResult();
            _qualificationTableData = LoadQualificationTableData().GetAwaiter().GetResult();
        }

        public IQueryable<Provider> GetProviders()
        {
            return GetAllProviders();
        }

        public IEnumerable<Qualification> GetQualifications(int[] qualificationIds)
        {
            var qualifications = GetAllQualifications();
            return qualifications
                .Where(q => qualificationIds.Contains(q.Id))
                .OrderBy(q => q.Name);
        }

        public Qualification GetQualification(int qualificationId)
        {
            var qualifications = GetAllQualifications();
            return qualifications.SingleOrDefault(q => q.Id == qualificationId);
        }

        public IEnumerable<Qualification> GetQualifications()
        {
            var qualifications = GetAllQualifications().ToList();
            qualifications.Add(new Qualification { Id = 0, Name = "All T Level courses" });
            return qualifications;
        }

        private JsonDocument GetProvidersData()
        {
            var json = _fileReader.ReadAllText(_configurationOptions.ProvidersDataFilePath);
            return JsonDocument.Parse(json);
        }

        private JsonDocument GetQualificationsData()
        {
            var json = _fileReader.ReadAllText(_configurationOptions.QualificationsDataFilePath);
            return JsonDocument.Parse(json);
        }

        private async Task<IList<Provider>> LoadProviderTableData()
        {
            try
            {
                 _logger.LogInformation("Looking for providers in table storage");
                var providersFromTable = await _tableStorageService.RetrieveProviders();
                _logger.LogInformation($"Found {providersFromTable?.Count ?? 0} providers in table storage");
                
                return providersFromTable;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve providers from table storage");
            	return null;
            }
        }

        private async Task<IList<Qualification>> LoadQualificationTableData()
        {
            try
            {
                _logger.LogInformation("Looking for qualifications in table storage");
                var qualificationsFromTable = await _tableStorageService.RetrieveQualifications();
                _logger.LogInformation($"Found {qualificationsFromTable?.Count ?? 0} qualifications in table storage");

                return qualificationsFromTable;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve qualifications from table storage");
                return null;
            }
        }

        public IEnumerable<string> GetWebsiteUrls()
        {
            var urlList = new List<string>();

            foreach (var provider in GetAllProviders())
            {
                foreach (var location in provider.Locations.Where(l => !string.IsNullOrWhiteSpace(l.Website)))
                {
                    if (!urlList.Contains(location.Website))
                    {
                        urlList.Add(location.Website);
                    }
                }
            }

            return urlList;
        }

        private IQueryable<Qualification> GetAllQualifications()
        {
            var qualifications = _qualificationTableData != null && _qualificationTableData.Any()
                ? _qualificationTableData
                : GetAllQualificationsFromJsonDocument();

            return qualifications.AsQueryable();
        }

        private IList<Qualification> GetAllQualificationsFromJsonDocument()
        {
            return _qualificationsData
                .RootElement
                .GetProperty("qualifications")
                .EnumerateObject()
                .Select(q =>
                    new Qualification
                    {
                        Id = int.Parse(q.Name),
                        Name = q.Value.GetString()
                    })
                .ToList();
        }

        private IQueryable<Provider> GetAllProviders()
        {
            var providers = _providerTableData != null && _providerTableData.Any()
                ? _providerTableData
                : GetAllProvidersFromJsonDocument();

            return providers.AsQueryable();
        }

        private IList<Provider> GetAllProvidersFromJsonDocument()
        {
            return _providersData.RootElement
                .GetProperty("providers")
                .EnumerateArray()
                .Select(p =>
                    new Provider
                    {
                        Id = p.GetProperty("id").GetInt32(),
                        Name = p.GetProperty("name").GetString(),
                        Locations = p.GetProperty("locations")
                            .EnumerateArray()
                            .Select(l =>
                                new Location
                                {
                                    Postcode = l.GetProperty("postcode").GetString(),
                                    Name = l.GetProperty("name").GetString(),
                                    Town = l.GetProperty("town").GetString(),
                                    Latitude = l.GetProperty("latitude").GetDouble(),
                                    Longitude = l.GetProperty("longitude").GetDouble(),
                                    Website = l.GetProperty("website").GetString(),
                                    DeliveryYears = l.TryGetProperty("deliveryYears", out var deliveryYears)
                                        ? deliveryYears.EnumerateArray()
                                            .Select(d =>
                                                new DeliveryYearDto
                                                {
                                                    Year = d.GetProperty("year").GetInt16(),
                                                    Qualifications = d.GetProperty("qualifications")
                                                        .EnumerateArray()
                                                        .Select(q => q.GetInt32())
                                                        .ToList()
                                                })
                                            .ToList()
                                        : new List<DeliveryYearDto>()
                                }).ToList()
                    })
                .ToList();
        }
    }
}
