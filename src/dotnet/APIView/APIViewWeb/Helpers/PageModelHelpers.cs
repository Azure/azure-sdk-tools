using APIViewWeb.Models;
using APIViewWeb.Repositories;

namespace APIViewWeb.Helpers
{
    public static class PageModelHelpers
    {
        public static UserPreferenceModel GetUserPreference(UserPreferenceCache preferenceCache, string userName)
        {
            return preferenceCache.GetUserPreferences(userName);
        }
    }
}
