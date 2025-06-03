// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Identity;
using Microsoft.Graph;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface IUserHelper
    {
        public Task<string> GetUserEmail();
    }
    public class UserHelper: IUserHelper
    {
        private readonly string[]  scopes = new[] { "https://graph.microsoft.com/.default" };
        private readonly DefaultAzureCredential credential = new DefaultAzureCredential();

        public async Task<string> GetUserEmail()
        {

            var graphClient = new GraphServiceClient(credential, scopes);
            var user = await graphClient.Me.GetAsync();
            if (user == null)
            {
                throw new InvalidOperationException("User not found.");
            }
            return user.UserPrincipalName ?? user.Mail;
        }
    }
}
