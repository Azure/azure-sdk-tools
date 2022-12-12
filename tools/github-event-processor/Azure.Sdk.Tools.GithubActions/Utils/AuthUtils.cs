using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Octokit;

namespace Azure.Sdk.Tools.GithubEventProcessor.Utils
{
    public class AuthUtils
    {
        /// <summary>
        /// Check to see if a given user is a Collaborator
        /// </summary>
        /// <param name="gitHubClient"></param>
        /// <param name="repositoryId"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public static async Task<bool> IsUserCollaborator(GitHubClient gitHubClient, long repositoryId, string user)
        {
            return await gitHubClient.Repository.Collaborator.IsCollaborator(repositoryId, user);
        }

        /// <summary>
        /// Check to see if the user is a member of the given Org
        /// </summary>
        /// <param name="gitHubClient"></param>
        /// <param name="orgName">chances are this is going to only ever be "Azure"</param>
        /// <param name="user">the github login for the user</param>
        /// <returns></returns>
        public static async Task<bool> IsUserMemberOfOrg(GitHubClient gitHubClient, string orgName, string user)
        {
            // Changes are the orgname is only going to be "Azure"
            return await gitHubClient.Organization.Member.CheckMember(orgName, user);
        }

        public static async Task<bool> DoesUserHavePermissions(GitHubClient gitHubClient, long repositoryId, string user)
        {
            CollaboratorPermission collaboratorPermission = await gitHubClient.Repository.Collaborator.ReviewPermission(repositoryId, user);

            Console.WriteLine(collaboratorPermission.Permission);

            return false;
        }
    }
}
