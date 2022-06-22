using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace APIViewTest.TestsHelpers
{
    public static class CosmosTestsHelpers
    {
        public static IConfigurationRoot GetIConfigurationRoot()
        {
            Assembly currAssembly = typeof(CosmosTestsHelpers).Assembly;
            return new ConfigurationBuilder()
                .AddUserSecrets(currAssembly)
                .Build();
        }
    }
}
