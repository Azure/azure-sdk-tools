// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

internal class FriendAttribute : Attribute
{
    public string FriendAssembly { get; }

    public FriendAttribute(string friendAssembly)
    {
        FriendAssembly = friendAssembly;
    }
}
