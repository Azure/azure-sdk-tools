using System.Collections.Generic;
using System.Text.Json;

namespace SwaggerApiParser;

public class SecurityDefinitions : Dictionary<string, JsonElement>
{
}

public class Security : List<Dictionary<string, List<string>>>
{
    
}