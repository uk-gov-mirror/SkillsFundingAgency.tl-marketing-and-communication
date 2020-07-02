﻿using sfa.Tl.Marketing.Communication.Application.GeoLocations;
using sfa.Tl.Marketing.Communication.Application.Interfaces;
using sfa.Tl.Marketing.Communication.Models.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace sfa.Tl.Marketing.Communication.Application.Services
{
    public class DistanceCalculationService : IDistanceCalculationService
    {
        private readonly ILocationApiClient _locationApiClient;
        private readonly IDistanceService _distanceService;

        public DistanceCalculationService(ILocationApiClient locationApiClient, IDistanceService distanceService)
        {
            _locationApiClient = locationApiClient;
            _distanceService = distanceService;
        }

        public async Task<List<ProviderLocation>> CalculateProviderLocationDistanceInMiles(string originPostCode, IQueryable<ProviderLocation> providerLocations)
        {
            var originGeoLocation = await _locationApiClient.GetGeoLocationDataAsync(originPostCode);
            var results = new List<ProviderLocation>();
            foreach (var providerLocation in providerLocations)
            {
                var distanceInMiles = _distanceService.CalculateInMiles(Convert.ToDouble(originGeoLocation.Latitude)
                    , Convert.ToDouble(originGeoLocation.Longitude), providerLocation.Latitude, providerLocation.Longitude);
                providerLocation.DistanceInMiles = (int)Math.Floor(distanceInMiles);
                results.Add(providerLocation);
            }

            return results;
        }

        public async Task<(bool IsValid, string Postcode)> IsPostcodeValid(string postcode)
        {
            var location = await _locationApiClient.GetGeoLocationDataAsync(postcode);
            return (location != null, location?.Postcode);
        }
    }
}
