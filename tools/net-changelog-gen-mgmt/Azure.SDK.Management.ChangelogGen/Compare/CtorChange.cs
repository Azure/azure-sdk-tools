// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using Azure.SDK.ChangelogGen.Utilities;

namespace Azure.SDK.ChangelogGen.Compare
{
    public class CtorChange : Change
    {
        public ConstructorInfo ConstructorInfo { get; init; }

        public override string Description
        {
            get
            {
                return this.ChangeCatogory switch
                {
                    ChangeCatogory.Added => $"Added constructor '{this.ConstructorInfo.ToFriendlyString()}' in type {this.ConstructorInfo.DeclaringType!.ToFriendlyString(fullName: true)}",
                    ChangeCatogory.Removed => $"Removed constrcutor '{this.ConstructorInfo.ToFriendlyString()}' in type {this.ConstructorInfo.DeclaringType!.ToFriendlyString(fullName: true)}",
                    ChangeCatogory.Obsoleted => $"Obsoleted constructor '{this.ConstructorInfo.ToFriendlyString()}' in type {this.ConstructorInfo.DeclaringType!.ToFriendlyString(fullName: true)}",
                    _ => throw new InvalidOperationException("Unhandled ChangeCategory for MethodInfo")
                };
            }
        }

        public CtorChange(ConstructorInfo info, ChangeCatogory category)
            : base(ChangeTarget.Constructor, category)
        {
            this.ConstructorInfo = info;
        }

    }
}
