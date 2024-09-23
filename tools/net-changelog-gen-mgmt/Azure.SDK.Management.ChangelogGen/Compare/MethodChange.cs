// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using Azure.SDK.ChangelogGen.Utilities;

namespace Azure.SDK.ChangelogGen.Compare
{
    public class MethodChange : Change
    {
        public MethodInfo MethodInfo { get; init; }

        public override string Description
        {
            get
            {
                return this.ChangeCatogory switch
                {
                    ChangeCatogory.Added => $"Added method '{this.MethodInfo.ToFriendlyString()}' in type {this.MethodInfo.DeclaringType!.ToFriendlyString(fullName: true)}",
                    ChangeCatogory.Removed => $"Removed method '{this.MethodInfo.ToFriendlyString()}' in type {this.MethodInfo.DeclaringType!.ToFriendlyString(fullName: true)}",
                    ChangeCatogory.Obsoleted => $"Obsoleted method '{this.MethodInfo.ToFriendlyString()}' in type {this.MethodInfo.DeclaringType!.ToFriendlyString(fullName: true)}",
                    _ => throw new InvalidOperationException("Unhandled ChangeCategory for MethodInfo")
                };
            }
        }

        public MethodChange(MethodInfo info, ChangeCatogory category)
            : base(ChangeTarget.Method, category)
        {
            this.MethodInfo = info;
        }
    }
}
