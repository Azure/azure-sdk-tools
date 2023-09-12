using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeOwnersParser;
using Azure.Sdk.Tools.CodeOwnersParser.Constants;
using Azure.Sdk.Tools.CodeownersLinter;
using Azure.Sdk.Tools.CodeownersLinter.Constants;
using Azure.Sdk.Tools.CodeownersLinter.Holders;

namespace Azure.Sdk.Tools.CodeownersLinter.Utils
{
    /// <summary>
    /// The OwnerData contains the team/user and user org visibility data as well as methods used for owner (user or team) verification.
    /// </summary>
    public class OwnerDataUtils
    {
        private TeamUserHolder _teamUserHolder = null;
        private UserOrgVisibilityHolder _userOrgVisibilityHolder = null;

        public OwnerDataUtils()
        {
        }

        public OwnerDataUtils(string teamUserBlobStorageUri,
                              string userOrgVisibilityBlobStorageUri)
        {
            _teamUserHolder = new TeamUserHolder(teamUserBlobStorageUri);
            _userOrgVisibilityHolder = new UserOrgVisibilityHolder(userOrgVisibilityBlobStorageUri);
        }

        // This constructor is for testing purposes only.
        public OwnerDataUtils(TeamUserHolder teamUserHolder,
                              UserOrgVisibilityHolder userOrgVisibilityHolder)
        {
            _teamUserHolder = teamUserHolder;
            _userOrgVisibilityHolder = userOrgVisibilityHolder;
        }

        /// <summary>
        /// Check whether or not the owner has write permissions.
        /// </summary>
        /// <param name="owner">The login of the owner to check</param>
        /// <returns>True if the owner has write permissions, false otherwise.</returns>
        public bool IsWriteOwner(string owner)
        {
            return _userOrgVisibilityHolder.UserOrgVisibilityDict.ContainsKey(owner);
        }

        /// <summary>
        /// Check whether or not the team has write permissions.
        /// </summary>
        /// <param name="team">The name of the team to check</param>
        /// <returns>True if the team has write permissions, false otherwise.</returns>
        public bool IsWriteTeam(string team)
        {
            var teamWithoutOrg = team.Trim();
            if (teamWithoutOrg.Contains(SeparatorConstants.Team))
            {
                teamWithoutOrg = teamWithoutOrg.Split(SeparatorConstants.Team, StringSplitOptions.TrimEntries)[1];
            }
            return _teamUserHolder.TeamUserDict.ContainsKey(teamWithoutOrg);
        }

        /// <summary>
        /// Check whether or not the user login is a member of of the Azure org
        /// </summary>
        /// <param name="login">The user login to check.</param>
        /// <returns>True, if the user is a member of the Azure org. False, if the user is not a member or not a public member</returns>
        public bool IsPublicAzureMember(string login)
        {
            // If the user isn't in the dictionary then call to get it
            if (_userOrgVisibilityHolder.UserOrgVisibilityDict.ContainsKey(login))
            {
                return _userOrgVisibilityHolder.UserOrgVisibilityDict[login];
            }
            return false;
        }
    }
}
