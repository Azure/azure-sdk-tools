// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.CodeownersUtils.Caches;
using Azure.Sdk.Tools.CodeownersUtils.Utils;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

// Keep CodeownersUtils types behind a factory so startup DI validation doesn't load the assembly.
internal class CodeownersCaches(HttpClient httpClient)
{
    private readonly Lazy<RepoLabelCache> _repoLabels = new(() => new RepoLabelCache());
    private readonly Lazy<ITeamUserCache> _teamUsers = new(() => new TeamUserCache());
    private readonly Lazy<UserOrgVisibilityCache> _userOrgVisibility = new(() => new UserOrgVisibilityCache());
    private readonly Lazy<ICacheValidator> _validator = new(() => new CacheValidator(httpClient));

    public RepoLabelCache RepoLabels => _repoLabels.Value;
    public ITeamUserCache TeamUsers => _teamUsers.Value;
    public UserOrgVisibilityCache UserOrgVisibility => _userOrgVisibility.Value;
    public ICacheValidator Validator => _validator.Value;
}
