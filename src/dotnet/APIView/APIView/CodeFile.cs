// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using APIView;
using System.Text.Json;
using System.Collections.ObjectModel;

namespace ApiView
{
    public class CodeFile
    {
        public const int CurrentVersion = 2;

        public int Version { get; set; }

        public CodeFileToken[] Tokens { get; set; } = Array.Empty<CodeFileToken>();

        //[System.Text.Json.Serialization.JsonIgnore]
        public List<NavigationItem> Navigation { get; set; } = new List<NavigationItem>();

        public override string ToString()
        {
            return new CodeFileRenderer().Render(this).ToString();
        }
    }

    public class NavigationItem
    {
        public string Text { get; set; }
        public string[] Children { get; set; } = Array.Empty<string>();

        public void Add(string child)
        {
            var list = new List<string>(Children);
            list.Add(child);
            Children = list.ToArray();
        }

        public override string ToString() => Text.ToString();
    }
}