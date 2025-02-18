﻿using System;
using sfa.Tl.Marketing.Communication.Models.Configuration;

// ReSharper disable UnusedMember.Global

namespace sfa.Tl.Marketing.Communication.UnitTests.Builders
{
    internal class SettingsBuilder
    {
        public const string FindCourseApiBaseAbsoluteUri = "https://test.com/findacourse/api";
        public static readonly Uri FindCourseApiBaseUri = new Uri(FindCourseApiBaseAbsoluteUri);
        public const string FindCourseApiKey = "0f608e5d437f4baabc04a0bc2dabbc1b";

        internal CourseDirectoryApiSettings BuildApiSettings(
            string findCourseApiBaseUri = FindCourseApiBaseAbsoluteUri,
            string findCourseApiKey = FindCourseApiKey) =>
            new CourseDirectoryApiSettings
            {
                ApiBaseUri = findCourseApiBaseUri,
                ApiKey = findCourseApiKey
            };

        internal StorageSettings BuildStorageSettings(
            string tableStorageConnectionString = "TestConnection") =>
            new StorageSettings
            {
                TableStorageConnectionString = tableStorageConnectionString
            };
    }
}
