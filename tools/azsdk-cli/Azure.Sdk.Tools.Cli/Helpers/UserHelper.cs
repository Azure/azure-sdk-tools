// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Threading;
using Azure.Sdk.Tools.Cli.Services;
using Microsoft.Graph;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface IUserHelper
    {
        public Task<string> GetUserEmail(CancellationToken ct);
    }

    public class UserHelper(IAzureService azureService): IUserHelper
    {
        private readonly string[]  scopes = ["https://graph.microsoft.com/.default"];

        public async Task<string> GetUserEmail(CancellationToken ct)
        {
            var graphClient = new GraphServiceClient(azureService.GetCredential(), scopes);
            var user = await graphClient.Me.GetAsync(cancellationToken: ct);
            if (user == null)
            {
                throw new InvalidOperationException("User not found.");
            }
            return user.UserPrincipalName ?? user.Mail;
        }
    }
}
