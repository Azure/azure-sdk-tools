// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.Services
{
    using MS.Az.Mgmt.CI.BuildTasks.Common.Logger;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Models.Nuget;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Models.REST;
    using NuGet.Versioning;
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;

    public class NugetServerClient : RestClient
    {
        const int DefaultSleepTimeout = 10;

        //https://api-v2v3search-0.nuget.org/autocomplete?q=storage&prerelease=true
        const string Nuget_Operation_WildSearch = @"https://api-v2v3search-0.nuget.org/autocomplete?q={0}&prerelease={1}";

        // GET https://api-v2v3search-0.nuget.org/autocomplete?id=nuget.protocol&prerelease=true
        const string Nuget_Operation_EnumPkgVersions = @"https://api-v2v3search-0.nuget.org/autocomplete?Id={0}&prerelease={1}";


        #region Constructor
        public NugetServerClient() : base() { }

        public NugetServerClient(Uri baseUri) : base(baseUri) { }

        #endregion


        /// <summary>
        /// Gets available version list of nuget packages using exact package Id
        /// TODO:
        ///     If you want to filter on pre-release and stable, provide the override for the function
        ///     the URL has two properties, package id and if it's a pre-release
        /// </summary>
        /// <param name="exactPackageId"></param>
        /// <returns></returns>
        public List<NuGetVersion> GetAvailablePackageVersion(string exactPackageId, bool includePreRelease = true)
        {
            List<string> responseVerList = null;
            //List<Version> availableVersionList = new List<Version>();
            List<NuGetVersion> availableVersionList = new List<NuGetVersion>();
            string endPointUrl = string.Format(Nuget_Operation_EnumPkgVersions, exactPackageId, includePreRelease);

            Task<RestClientResponse<EnumeratePkgVersionModel>> pkgVersionTask = Task.Run<RestClientResponse<EnumeratePkgVersionModel>>(async () => 
                                                                                await this.ExecuteRequest<EnumeratePkgVersionModel>(endPointUrl, HttpMethod.Get).ConfigureAwait(false));

            RestClientResponse<EnumeratePkgVersionModel> rcResponse = pkgVersionTask.GetAwaiter().GetResult();
            EnumeratePkgVersionModel enumVers = rcResponse.Body;
            responseVerList = enumVers.data;
            
            if (responseVerList == null)
            {
                //throw new ApplicationException("Unable to retrieve available package versions. Please try again");
                UtilLogger.LogWarning("Unable to retrieve available package versions for nuget pacakge '{0}'", exactPackageId);
            }

            foreach(string ver in responseVerList)
            {
                NuGetVersion nugVer = new NuGetVersion(ver);
                availableVersionList.Add(nugVer);

                //string[] splitVerString = ver.Split(new string[] { "-" }, StringSplitOptions.RemoveEmptyEntries);

                //Version pkgVersion = null;
                //if(Version.TryParse(splitVerString[0], out pkgVersion))
                //{
                //    availableVersionList.Add(pkgVersion);
                //}
            }

            UtilLogger.LogInfo(availableVersionList, "Available versions on nuget.org for Pacakge '{0}'", exactPackageId);

            return availableVersionList;
        }

        public NuGetVersion GetHighestVersion(string exactPackageId)
        {
            List<NuGetVersion> allVers = GetAvailablePackageVersion(exactPackageId);
            SortedList<Version, NuGetVersion> sortedVersion = new SortedList<Version, NuGetVersion>();            

            foreach(NuGetVersion nv in allVers)
            {
                sortedVersion.Add(nv.Version, nv);
            }

            int lastMemberIndex = sortedVersion.Count - 1;
            return sortedVersion.Values[lastMemberIndex];
        }

        /// <summary>
        /// Sorts the available version list and returns the highest version
        /// </summary>
        /// <param name="availableVersionList"></param>
        /// <returns></returns>
        public Version GetHighestVersion(List<Version> availableVersionList)
        {
            Version highestVersion = new Version("0.0.0.0");

            foreach(Version ver in availableVersionList)
            {
                if(ver > highestVersion)
                {
                    highestVersion = ver;
                }
            }

            return highestVersion;
        }
    }
}
