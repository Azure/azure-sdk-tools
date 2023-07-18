using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CodeOwnersParser
{
    public class TeamUserHolder
    {
        private string TeamUserStorageURI { get; set; } = DefaultStorageConstants.DefaultStorageURI;
        private Dictionary<string, List<string>>? _teamUserDict = null;

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

        public TeamUserHolder(string? teamUserStorageURI)
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
                    return list.ToDictionary((keyItem) => keyItem.Key, (valueItem) => valueItem.Value);
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
            // does not being with @org/
            string teamWithoutOrg = teamName.Trim();
            if (teamWithoutOrg.Contains('/'))
            {
                teamWithoutOrg = teamWithoutOrg.Split("/")[1];
            }
            if (TeamUserDict != null)
            {
                if (TeamUserDict.ContainsKey(teamWithoutOrg))
                {
                    Console.WriteLine($"Found team entry for {teamWithoutOrg}");
                    return TeamUserDict[teamWithoutOrg];
                }
                Console.WriteLine($"Warning: TeamUserDictionary did not contain a team entry for {teamWithoutOrg}");
            }
            return new List<string>();
        }
    }
}
