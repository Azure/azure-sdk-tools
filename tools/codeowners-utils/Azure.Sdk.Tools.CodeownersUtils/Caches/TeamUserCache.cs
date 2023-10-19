using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersUtils.Constants;
using Azure.Sdk.Tools.CodeownersUtils.Utils;

namespace Azure.Sdk.Tools.CodeownersUtils.Caches
{
    /// <summary>
    /// Holds the team/user information which is used for both verification and team expansion during parsing. Note that
    /// Teams are case insensitive but case preserving which means the dictionary needs to be able to case insensitive lookups.
    /// </summary>
    public class TeamUserCache
    {
        private string TeamUserStorageURI { get; set; } = DefaultStorageConstants.TeamUserBlobUri;
        private Dictionary<string, List<string>> _teamUserDict = null;
        public Dictionary<string, List<string>> TeamUserDict
        {
            get
            {
                if (_teamUserDict == null)
                {
                    _teamUserDict = GetTeamUserData();
                }
                return _teamUserDict;
            }
            set
            {
                _teamUserDict = value;
            }
        }

        public TeamUserCache(string teamUserStorageURI)
        {
            if (!string.IsNullOrWhiteSpace(teamUserStorageURI))
            {
                TeamUserStorageURI = teamUserStorageURI;
            }
        }

        private Dictionary<string, List<string>> GetTeamUserData()
        {
            if (null == _teamUserDict)
            {
                string rawJson = FileHelpers.GetFileOrUrlContents(TeamUserStorageURI);
                var list = JsonSerializer.Deserialize<List<KeyValuePair<string, List<string>>>>(rawJson);
                if (null != list)
                {
                    // The StringComparer needs to be set in order to do an case insensitive lookup. GitHub's teams
                    // and users are case insensitive but case preserving. This means that a team can be @Azure/SomeTeam
                    // but, in a CODEOWNERS file, it can be @azure/someteam and queries to get users for the team need to
                    // succeed regardless of casing.
                    return list.ToDictionary((keyItem) => keyItem.Key, (valueItem) => valueItem.Value, StringComparer.InvariantCultureIgnoreCase);
                }
                Console.WriteLine($"Error! Unable to deserialize json team/user data from {TeamUserStorageURI}. rawJson={rawJson}");
                return new Dictionary<string, List<string>>();
            }
            return _teamUserDict;
        }

        public List<string> GetUsersForTeam(string teamName)
        {
            // The teamName in the codeowners file should be in the form <org>/<team>.
            // The dictionary's team names do not contain the org so the org needs to
            // be stripped off. Handle the case where the teamName passed in does and
            // does not begin with @org/
            string teamWithoutOrg = teamName.Trim();
            if (teamWithoutOrg.Contains(SeparatorConstants.Team))
            {
                teamWithoutOrg = teamWithoutOrg.Split(SeparatorConstants.Team, StringSplitOptions.TrimEntries)[1];
            }
            if (TeamUserDict != null)
            {
                if (TeamUserDict.ContainsKey(teamWithoutOrg))
                {
                    return TeamUserDict[teamWithoutOrg];
                }
                Console.WriteLine($"Warning: TeamUserDictionary did not contain a team entry for {teamWithoutOrg}");
            }
            return new List<string>();
        }
    }
}
