﻿using sfa.Tl.Marketing.Communication.Models.Dto;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace sfa.Tl.Marketing.Communication.Application.Interfaces
{
    public interface IProviderSearchService
    {
        Task<(int totalCount, IEnumerable<ProviderLocation> searchResults)> Search(SearchRequest searchRequest);
        IEnumerable<Qualification> GetQualifications();
        Qualification GetQualificationById(int id);
        Task<(bool IsValid, PostcodeLocation PostcodeLocation)> IsSearchPostcodeValid(string postcode);
    }
}
