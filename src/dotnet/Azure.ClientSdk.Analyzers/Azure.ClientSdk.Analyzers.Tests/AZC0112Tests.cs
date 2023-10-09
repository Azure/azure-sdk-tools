// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.InternalsVisibleToAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0112Tests
    {
        [Fact]
        public async Task AZC0020WhenInheritingFromInternalInterface()
        {
            string code = @"
using System;
using TestReferenceWithInternalsVisibleTo;

namespace LibraryNamespace
{
    public class {|AZC0112:MyClass|} : IInternalInterface
    {
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, additionalReferences: new[] { typeof(TestReferenceWithInternalsVisibleTo.PublicClass) });
        }

        [Fact]
        public async Task NoAZC0020WhenInheritingFromInternalInterfaceWithFriendAttribute()
        {
            string code = @"
using System;
using TestReferenceWithInternalsVisibleTo;

namespace LibraryNamespace
{
    public class MyClass : IInternalInterfaceWithFriendAttribute
    {
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, additionalReferences: new[] { typeof(TestReferenceWithInternalsVisibleTo.PublicClass) });
        }

        [Fact]
        public async Task AZC0020WhenDerivingFromInternalClass()
        {
            string code = @"
using System;
using TestReferenceWithInternalsVisibleTo;

namespace LibraryNamespace
{
    internal class {|AZC0112:MyClass|} : InternalClass
    {
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, additionalReferences: new[] { typeof(TestReferenceWithInternalsVisibleTo.PublicClass) });
        }

        [Fact]
        public async Task NoAZC0020WhenInheritingFromInternalClassWithFriendAttribute()
        {
            string code = @"
using System;
using TestReferenceWithInternalsVisibleTo;

namespace LibraryNamespace
{
    internal class MyClass : InternalClassWithFriendAttribute
    {
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, additionalReferences: new[] { typeof(TestReferenceWithInternalsVisibleTo.PublicClass) });
        }

        [Fact]
        public async Task AZC0020WhenDeclaringInternalProperty()
        {
            string code = @"
using System;
using TestReferenceWithInternalsVisibleTo;

namespace LibraryNamespace
{
    public class MyClass
    {
        internal InternalClass {|AZC0112:PropReferencesInternalType|} { {|AZC0112:get|}; set;}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, additionalReferences: new[] { typeof(TestReferenceWithInternalsVisibleTo.PublicClass) });
        }

        [Fact]
        public async Task NoAZC0020WhenDeclaringInternalPropertyWithFriendAttribute()
        {
            string code = @"
using System;
using TestReferenceWithInternalsVisibleTo;

namespace LibraryNamespace
{
    public class MyClass
    {
        internal InternalClassWithFriendAttribute PropReferencesInternalType { get; set;}
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, additionalReferences: new[] { typeof(TestReferenceWithInternalsVisibleTo.PublicClass) });
        }

        [Fact]
        public async Task NoAZC0020WhenDeclaringInternalFieldWithFriendAttribute()
        {
            string code = @"
using System;
using TestReferenceWithInternalsVisibleTo;

namespace LibraryNamespace
{
    public class MyClass
    {
        internal InternalClassWithFriendAttribute fieldReferencesInternalType; 
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, additionalReferences: new[] { typeof(TestReferenceWithInternalsVisibleTo.PublicClass) });
        }

        [Fact]
        public async Task AZC0020WhenDeclaringInternalField()
        {
            string code = @"
using System;
using TestReferenceWithInternalsVisibleTo;

namespace LibraryNamespace
{
    public class MyClass
    {
        internal InternalClass {|AZC0112:fieldReferencesInternalType|};
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, additionalReferences: new[] { typeof(TestReferenceWithInternalsVisibleTo.PublicClass) });
        }

        [Fact]
        public async Task AZC0020WhenReferencingInternalProperty()
        {
            string code = @"
using System;
using TestReferenceWithInternalsVisibleTo;
using System.Reflection;

namespace LibraryNamespace
{
    public class MyClass
    {
        public void MyMethod()
        {
            var myClass = new PublicClass();
            var value = {|AZC0112:myClass.InternalProperty|};
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, additionalReferences: new[] { typeof(TestReferenceWithInternalsVisibleTo.PublicClass) });
        }

        [Fact]
        public async Task NoAZC0020WhenReferencingInternalPropertyWithFriendAttribute()
        {
            string code = @"
using System;
using TestReferenceWithInternalsVisibleTo;
using System.Reflection;

namespace LibraryNamespace
{
    public class MyClass
    {
        public void MyMethod()
        {
            var myClass = new PublicClass();
            var value = myClass.InternalPropertyWithFriendAttribute;
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, additionalReferences: new[] { typeof(TestReferenceWithInternalsVisibleTo.PublicClass) });
        }

        [Fact]
        public async Task AZC0020WhenReferencingInternalMethod()
        {
            string code = @"
using System;
using TestReferenceWithInternalsVisibleTo;
using System.Reflection;

namespace LibraryNamespace
{
    public class MyClass
    {
        public void MyMethod()
        {
            var myClass = new PublicClass();
            {|AZC0112:myClass.InternalMethod|}();
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, additionalReferences: new[] { typeof(TestReferenceWithInternalsVisibleTo.PublicClass) });
        }

        [Fact]
        public async Task NoAZC0020WhenReferencingInternalMethodWithFriendAttribute()
        {
            string code = @"
using System;
using TestReferenceWithInternalsVisibleTo;
using System.Reflection;

namespace LibraryNamespace
{
    public class MyClass
    {
        public void MyMethod()
        {
            var myClass = new PublicClass();
            myClass.InternalMethodWithFriendAttribute();
        }
    }
}";
            await Verifier.VerifyAnalyzerAsync(code, additionalReferences: new[] { typeof(TestReferenceWithInternalsVisibleTo.PublicClass) });
        }

    }
}
