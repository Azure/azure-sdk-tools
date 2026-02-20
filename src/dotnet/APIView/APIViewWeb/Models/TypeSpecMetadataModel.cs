using System;
using System.Collections.Generic;

namespace APIViewWeb.Models;

public class TypeSpecMetadata
{
    public string EmitterVersion { get; set; }
    public DateTime GeneratedAt { get; set; }
    public TypeSpecInfo TypeSpec { get; set; }
    public Dictionary<string, LanguageConfig> Languages { get; set; }
    public string SourceConfigPath { get; set; }
}

public class TypeSpecInfo
{
    public string Namespace { get; set; }
    public string Documentation { get; set; }
    public string Type { get; set; }
}

public class LanguageConfig
{
    public string EmitterName { get; set; }
    public string PackageName { get; set; }
    public string Namespace { get; set; }
    public string OutputDir { get; set; }
    public string Flavor { get; set; }
    public string ServiceDir { get; set; }
}
