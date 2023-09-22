using System.Security.Claims;
using APIViewWeb.Models;
using APIViewWeb.Repositories;

namespace APIViewWeb.Helpers
{
    public static class PageModelHelpers
    {
        public static UserPreferenceModel GetUserPreference(UserPreferenceCache preferenceCache, ClaimsPrincipal User)
        {
            return preferenceCache.GetUserPreferences(User).Result;
        }

        public static string GetHiddenApiClass(UserPreferenceModel userPreference)
        {
            var hiddenApiClass = " hidden-api hidden-api-toggleable";
            if (userPreference.ShowHiddenApis != true)
            {
                hiddenApiClass += " d-none";
            }
            return hiddenApiClass;
        }
    }
}
