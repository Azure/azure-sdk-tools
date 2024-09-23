using System.Collections.Generic;
using System.Text.Json;

namespace SwaggerApiParser.Specs
{
    public class SecurityDefinitions : Dictionary<string, JsonElement>
    {
    }

    public class Security : List<Dictionary<string, List<JsonElement>>>
    {

    }
}

