// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;

namespace Azure.Sdk.Tools.Cli.Models
{
    public class GenericResponse
    {
        public string Status {get; set; } = string.Empty;
        public List<string> Details { get; set; } = [];
        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
