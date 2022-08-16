using System;
using System.Collections.Generic;
using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace SwaggerApiParser;


public class SecurityDefinitions : Dictionary<string, JsonElement>
{
}

public class Security : List<Dictionary<string, List<string>>>
{
    
}
