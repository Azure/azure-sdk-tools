using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Octokit.Internal;

namespace Azure.Sdk.Tools.GitHubEventProcessor.Constants
{
    // This is temporary. Octkit's 5.x release on 03/09/2023 included some breaking changes with permissions.
    // GitHubClient.Repository.Collaborator.ReviewPermission now returns a CollaboratorPermissionResponse
    // which still includes the legacy Collaborator permissions on the Permissions attribute but the enum
    // was removed. New, Collaborator permissions (CollaboratorPermission in Octokit/Models/Common/Permission.cs)
    // are on the Collaborator attribute and are a completely different set of permissions.
    public class PermissionLevel
    {
        public const string Admin = "admin";
        public const string Write = "write";
        public const string Read = "read";
        public const string None = "none";
    }
}
