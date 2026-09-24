// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace APIViewWeb
{
    public class IdHelper
    {
        public static string GenerateId()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}