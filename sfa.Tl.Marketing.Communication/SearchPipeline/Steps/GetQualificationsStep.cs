﻿using Microsoft.AspNetCore.Mvc.Rendering;
using sfa.Tl.Marketing.Communication.Application.Interfaces;
using System.Linq;
using System.Threading.Tasks;

namespace sfa.Tl.Marketing.Communication.SearchPipeline.Steps
{
    public class GetQualificationsStep : ISearchStep
    {
        private readonly IProviderSearchService _providerSearchService;

        public GetQualificationsStep(IProviderSearchService providerSearchService)
        {
            _providerSearchService = providerSearchService;
        }

        public Task Execute(ISearchContext context)
        {
            context.ViewModel.SelectedQualificationId ??= 0;

            var qualifications = _providerSearchService.GetQualifications();
            context.ViewModel.Qualifications = qualifications
                .Select(q =>
                    new SelectListItem
                    {
                        Text = q.Name,
                        Value = q.Id.ToString(),
                        Selected = q.Id == context.ViewModel.SelectedQualificationId.Value
                    });

            return Task.CompletedTask;
        }
    }
}
