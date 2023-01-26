using System;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Azure.Sdk.Tools.NotificationConfiguration
{
    static class YamlHelper
    {
        private static readonly ISerializer serializer = 
            new SerializerBuilder().WithNamingConvention(new CamelCaseNamingConvention()).Build();

        private static readonly IDeserializer deserializer =
            new DeserializerBuilder().WithNamingConvention(new CamelCaseNamingConvention()).Build();


        public static T Deserialize<T>(string input, bool swallowExceptions = false)
            where T : class
        {
            T result; 
            try
            {
                result = deserializer.Deserialize<T>(input);
            }
            catch
            {
                if (!swallowExceptions)
                {
                    throw;
                }

                result = null;
            }

            return result;
        }

        public static string Serialize<T>(T input)
        {
            return serializer.Serialize(input);
        }
    }
}
