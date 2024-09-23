// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using Azure.SDK.ChangelogGen.Utilities;

namespace Azure.SDK.ChangelogGen.Compare
{
    public class PropertyChange : Change
    {
        public PropertyInfo PropertyInfo { get; init; }

        public override string Description
        {
            get
            {
                return this.ChangeCatogory switch
                {
                    ChangeCatogory.Added => $"Added property '{this.PropertyInfo.ToFriendlyString()}' in type {this.PropertyInfo.DeclaringType!.ToFriendlyString(fullName: true)}",
                    ChangeCatogory.Removed => $"Removed property '{this.PropertyInfo.ToFriendlyString()}' in type {this.PropertyInfo.DeclaringType!.ToFriendlyString(fullName: true)}",
                    ChangeCatogory.Obsoleted => $"Obsoleted property '{this.PropertyInfo.ToFriendlyString()}' in type {this.PropertyInfo.DeclaringType!.ToFriendlyString(fullName: true)}",
                    _ => throw new InvalidOperationException("Unhandled ChangeCategory for PropertyInfo")
                };
            }
        }

        public PropertyChange(PropertyInfo info, ChangeCatogory category)
            : base(ChangeTarget.Property, category)
        {
            this.PropertyInfo = info;
        }

    }
}
