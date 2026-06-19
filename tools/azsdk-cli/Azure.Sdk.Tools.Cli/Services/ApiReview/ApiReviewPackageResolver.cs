// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;

namespace Azure.Sdk.Tools.Cli.Services.ApiReview;

public interface IApiReviewPackageResolver
{
    ApiReviewPackageResult ResolvePackage(string packageName, string worktreeRoot);
}

public class ApiReviewPackageResolver : IApiReviewPackageResolver
{
    public ApiReviewPackageResult ResolvePackage(string packageName, string worktreeRoot)
    {
        if (string.IsNullOrWhiteSpace(packageName))
        {
            return ApiReviewPackageResult.CreateFailure("Package name is required.");
        }

        var codeownersPath = Path.Combine(worktreeRoot, Constants.AZURE_CODEOWNERS_PATH);
        if (!File.Exists(codeownersPath))
        {
            return ApiReviewPackageResult.CreateFailure($"CODEOWNERS file not found: {codeownersPath}");
        }

        var entries = CodeownersParser.ParseCodeownersFile(codeownersPath);
        var normalizedPackageName = NormalizePath(packageName);
        var matchingPackages = entries
            .Where(entry => entry.IsValid && !entry.ContainsWildcard)
            .Select(entry => new
            {
                Entry = entry,
                RelativePath = NormalizePath(entry.PathExpression)
            })
            .Where(candidate => MatchesPackage(candidate.RelativePath, normalizedPackageName))
            .Where(candidate => Directory.Exists(Path.Combine(worktreeRoot, candidate.RelativePath)))
            .ToList();

        if (matchingPackages.Count == 0)
        {
            return ApiReviewPackageResult.CreateFailure($"Unable to find package '{packageName}' in CODEOWNERS at {codeownersPath}.");
        }

        if (matchingPackages.Count > 1)
        {
            return ApiReviewPackageResult.CreateFailure($"Package '{packageName}' matched multiple CODEOWNERS entries: {string.Join(", ", matchingPackages.Select(package => package.Entry.PathExpression))}.");
        }

        var match = matchingPackages.Single();
        return ApiReviewPackageResult.CreateSuccess(new ApiReviewPackageInfo
        {
            PackagePath = Path.GetFullPath(Path.Combine(worktreeRoot, match.RelativePath.Replace('/', Path.DirectorySeparatorChar))),
            RelativePath = match.RelativePath,
            CodeownersPathExpression = match.Entry.PathExpression
        });
    }

    private static bool MatchesPackage(string relativePath, string packageName)
    {
        return string.Equals(relativePath, packageName, StringComparison.OrdinalIgnoreCase)
            || relativePath.EndsWith($"/{packageName}", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').Trim().Trim('/');
    }
}

public class ApiReviewPackageResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public ApiReviewPackageInfo? Package { get; set; }

    public static ApiReviewPackageResult CreateFailure(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };

    public static ApiReviewPackageResult CreateSuccess(ApiReviewPackageInfo package) => new()
    {
        Success = true,
        Package = package
    };
}

public class ApiReviewPackageInfo
{
    public required string PackagePath { get; set; }
    public required string RelativePath { get; set; }
    public required string CodeownersPathExpression { get; set; }
}
