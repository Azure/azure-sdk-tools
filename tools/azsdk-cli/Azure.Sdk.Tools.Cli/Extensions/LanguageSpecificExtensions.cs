// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Extensions;

public class LanguageSpecificImplementations
{
    public Type? DotNet { get; set; }
    public Type? Go { get; set; }
    public Type? Java { get; set; }
    public Type? JavaScript { get; set; }
    public Type? Python { get; set; }
}

public static class LanguageSpecificExtensions
{
    public static void AddLanguageSpecific<T>(this IServiceCollection services, LanguageSpecificImplementations implementations) where T : class
    {
        services.AddScoped<ILanguageSpecificService<T>, LanguageSpecificService<T>>();
        if (implementations.DotNet != null)
        {
            services.AddKeyedScoped(typeof(T), SdkLanguage.DotNet, implementations.DotNet);
        }
        if (implementations.Go != null)
        {
            services.AddKeyedScoped(typeof(T), SdkLanguage.Go, implementations.Go);
        }
        if (implementations.Java != null)
        {
            services.AddKeyedScoped(typeof(T), SdkLanguage.Java, implementations.Java);
        }
        if (implementations.JavaScript != null)
        {
            services.AddKeyedScoped(typeof(T), SdkLanguage.JavaScript, implementations.JavaScript);
        }
        if (implementations.Python != null)
        {
            services.AddKeyedScoped(typeof(T), SdkLanguage.Python, implementations.Python);
        }
    }
}
