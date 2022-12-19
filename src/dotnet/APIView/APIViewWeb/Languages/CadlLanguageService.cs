// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Models;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace APIViewWeb
{
    public class CadlLanguageService : LanguageProcessor
    {
        public override string Name { get; } = "Cadl";

        public override string Extension { get; } = ".cadl";

        public override string VersionString { get; } = "0";

        public override string ProcessName => throw new NotImplementedException();

        private string _cadlSpecificPathPrefix;

        public CadlLanguageService(IConfiguration configuration)
        {
            IsReviewGenByPipeline = true;
            _cadlSpecificPathPrefix = configuration["CADL-Specification-path-prefix"] ?? "/specification";
        }
        public override async Task<CodeFile> GetCodeFileAsync(string originalName, Stream stream, bool runAnalysis)
        {
            return await CodeFile.DeserializeAsync(stream, true);
        }

        public override string GetProcessorArguments(string originalName, string tempDirectory, string jsonPath)
        {
            throw new NotImplementedException();
        }
        public override bool CanUpdate(string versionString)
        {
            return false;
        }

        public override bool GeneratePipelineRunParams(ReviewGenPipelineParamModel param)
        {
            var filePath = param.FileName;
            // Verify cadl source file path is a GitHub URL to cadl package root 
            if (filePath == null || !filePath.StartsWith("https://github.com/"))
                return false;
          
            if (!filePath.Contains("/tree/") || !filePath.Contains(_cadlSpecificPathPrefix))
                return false;

            filePath = filePath.Replace("https://github.com/", "");
            var sourceUrlparts = filePath.Split("/tree/", 2);
            param.SourceRepoName = sourceUrlparts[0];
            sourceUrlparts = sourceUrlparts[1].Split(_cadlSpecificPathPrefix, 2);
            param.SourceBranchName = sourceUrlparts[0];
            param.FileName = $"{_cadlSpecificPathPrefix}{sourceUrlparts[1]}";
            _telemetryClient.TrackTrace($"Pipeline parameters to run CADL API rview gen pipeline: '{JsonConvert.SerializeObject(param)}'");

            return true;
        }
    }
}
