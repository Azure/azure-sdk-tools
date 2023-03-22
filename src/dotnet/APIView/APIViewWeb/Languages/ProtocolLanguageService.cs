// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class ProtocolLanguageService : LanguageProcessor
    {
        public override string Name { get; } = "Protocol";
        public override string[] Extensions { get; } = { ".yaml" };
        public override string VersionString { get; } = "0.1.0";

        private readonly string _protocolProcessor;
        public override string ProcessName => _protocolProcessor;

        public ProtocolLanguageService(IConfiguration configuration)
        {
            // protocolGen is located in python's scripts path e.g. <Pythonhome>/Scripts/protocolGen
            // Env variable PROTOCOLPARSERPATH is set to <pythonhome>/Scripts/protocolGen where parser is located
            _protocolProcessor = configuration["PROTOCOLPARSERPATH"] ?? string.Empty;
        }

        public override string GetProcessorArguments(string originalName, string tempDirectory, string jsonPath)
        {
            return $"--pkg-path {originalName} --temp-path {tempDirectory}" +
                $" --out-path {jsonPath} --hide-report";
        }

    }
}
