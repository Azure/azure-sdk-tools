// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using Azure.SDK.ChangelogGen.Utilities;

namespace Azure.SDK.ChangelogGen.Compare
{
    public enum PropertyMethodName
    {
        Set,
        Get,
    }

    public class PropertyMethodChange : Change
    {
        public PropertyInfo PropertyInfo { get; init; }
        public PropertyMethodName MethodName { get; init; }

        public override string Description
        {
            get
            {
                return this.ChangeCatogory switch
                {
                    ChangeCatogory.Added => $"Added property method '{this.MethodName}' for '{this.PropertyInfo.ToFriendlyString()}' in type {this.PropertyInfo.DeclaringType!.ToFriendlyString(fullName: true)}",
                    ChangeCatogory.Removed => $"Removed property method '{this.MethodName}' for '{this.PropertyInfo.ToFriendlyString()}' in type {this.PropertyInfo.DeclaringType!.ToFriendlyString(fullName: true)}",
                    ChangeCatogory.Obsoleted => $"Obsoleted property method '{this.MethodName}' for '{this.PropertyInfo.ToFriendlyString()}' in type {this.PropertyInfo.DeclaringType!.ToFriendlyString(fullName: true)}",
                    _ => throw new InvalidOperationException("Unhandled ChangeCategory for PropertyMethod")
                };
            }
        }

        public PropertyMethodChange(PropertyInfo info, PropertyMethodName methodName, ChangeCatogory category)
            : base(ChangeTarget.PropertyMethod, category)
        {
            this.PropertyInfo = info;
            this.MethodName = methodName;
        }

    }
}
