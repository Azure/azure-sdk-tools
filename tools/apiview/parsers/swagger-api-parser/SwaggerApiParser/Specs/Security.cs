using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using SwaggerApiParser.Converters;

namespace SwaggerApiParser.Specs
{
    public class SecurityDefinitions : Dictionary<string, JsonElement>
    {
    }

    public class Security : List<Dictionary<string, List<JsonElement>>>
    {

    }
}

