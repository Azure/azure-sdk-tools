// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace ApiView
{
    public readonly struct DependencyInfo
    {
        public string Version { get; }

        public string Name { get; }

        public DependencyInfo(string name, string version)
        {
            Name = name;
            Version = version;
        }
    }
}