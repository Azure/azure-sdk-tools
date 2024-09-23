// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Xunit;
using Verifier = Azure.ClientSdk.Analyzers.Tests.AzureAnalyzerVerifier<Azure.ClientSdk.Analyzers.ClientMethodsAnalyzer>;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0018Tests
    {
        [Fact]
        public async Task AZC0018NotProducedForCorrectReturnType()
        {
            const string code = @"
using Azure;
using Azure.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task<Response<bool>> GetHeadAsBooleanAsync(string s, RequestContext context)
        {
            return null;
        }

        public virtual Response<bool> GetHeadAsBoolean(string s, RequestContext context)
        {
            return null;
        }

        public virtual Task<Response> GetResponseAsync(string s, RequestContext context)
        {
            return null;
        }

        public virtual Response GetResponse(string s, RequestContext context)
        {
            return null;
        }

        public virtual AsyncPageable<BinaryData> GetPageableAsync(string s, RequestContext context)
        {
            return null;
        }

        public virtual Pageable<BinaryData> GetPageable(string s, RequestContext context)
        {
            return null;
        }

        public virtual Task<Operation> GetOperationAsync(string s, RequestContext context)
        {
            return null;
        }

        public virtual Operation GetOperation(string s, RequestContext context)
        {
            return null;
        }

        public virtual Task<Operation<BinaryData>> GetOperationOfTAsync(string s, RequestContext context)
        {
            return null;
        }

        public virtual Operation<BinaryData> GetOperationOfT(string s, RequestContext context)
        {
            return null;
        }

        public virtual Task<Operation<AsyncPageable<BinaryData>>> GetOperationOfPageableAsync(string s, RequestContext context)
        {
            return null;
        }

        public virtual Operation<Pageable<BinaryData>> GetOperationOfPageable(string s, RequestContext context)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0018NotProducedForExtendedReturnType()
        {
            const string code = @"
using Azure;
using Azure.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Arbitrary
{
    public class ExtendedBinaryData : BinaryData
    {
        public ExtendedBinaryData(string data): base(data){}
    }
}

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual AsyncPageable<Arbitrary.ExtendedBinaryData> GetPageableAsync(string s, RequestContext context)
        {
            return null;
        }

        public virtual Pageable<Arbitrary.ExtendedBinaryData> GetPageable(string s, RequestContext context)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0018ProducedForCustomTypeWithSystemName()
        {
            const string code = @"
using Azure;
using Azure.Core;
using System.Threading;
using System.Threading.Tasks;

namespace Custom
{
    namespace System
    {
        public class BinaryData {}
    }
}

namespace RandomNamespace
{
    using Custom.System;
    public class SomeClient
    {
        public virtual AsyncPageable<BinaryData> {|AZC0018:GetPageableAsync|}(string s, RequestContext context)
        {
            return null;
        }

        public virtual Pageable<BinaryData> {|AZC0018:GetPageable|}(string s, RequestContext context)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0018ProducedForMethodsWithGenericResponseOfPrimitive()
        {
            const string code = @"
using Azure;
using Azure.Core;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task<Response<string>> {|AZC0018:GetAsync|}(string s, RequestContext context)
        {
            return null;
        }

        public virtual Response<string> {|AZC0018:Get|}(string s, RequestContext context)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0018ProducedForMethodsWithGenericResponseOfModel()
        {
            const string code = @"
using Azure;
using Azure.Core;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class Model
    {
        string a;
    }
    public class SomeClient
    {
        public virtual Task<Response<Model>> {|AZC0018:GetAsync|}(string s, RequestContext context)
        {
            return null;
        }

        public virtual Response<Model> {|AZC0018:Get|}(string s, RequestContext context)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0018ProducedForMethodsWithPageableOfModel()
        {
            const string code = @"
using Azure;
using Azure.Core;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class Model
    {
        string a;
    }
    public class SomeClient
    {
        public virtual AsyncPageable<Model> {|AZC0018:GetAsync|}(string s, RequestContext context)
        {
            return null;
        }

        public virtual Pageable<Model> {|AZC0018:Get|}(string s, RequestContext context)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0018ProducedForMethodsWithOperationOfModel()
        {
            const string code = @"
using Azure;
using Azure.Core;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class Model
    {
        string a;
    }
    public class SomeClient
    {
        public virtual Task<Operation<Model>> {|AZC0018:GetAsync|}(string s, RequestContext context)
        {
            return null;
        }

        public virtual Operation<Model> {|AZC0018:Get|}(string s, RequestContext context)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0018ProducedForMethodsWithOperationOfPageableModel()
        {
            const string code = @"
using Azure;
using Azure.Core;
using System.Threading;
using System.Threading.Tasks;

namespace RandomNamespace
{
    public class Model
    {
        string a;
    }
    public class SomeClient
    {
        public virtual Task<Operation<AsyncPageable<Model>>> {|AZC0018:GetAsync|}(string s, RequestContext context)
        {
            return null;
        }

        public virtual Operation<Pageable<Model>> {|AZC0018:Get|}(string s, RequestContext context)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0018ProducedForMethodsWithParameterModel()
        {
            const string code = @"
using Azure;
using Azure.Core;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace RandomNamespace
{
    public struct Model
    {
        string a;
    }
    public class SomeClient
    {
        public virtual Task<Response> {|AZC0018:GetAsync|}(Model model, Azure.RequestContext context)
        {
            return null;
        }

        public virtual Response {|AZC0018:Get|}(Model model, Azure.RequestContext context)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0018NotProducedForMethodsWithNoRequestContentAndRequiredContext()
        {
            const string code = @"
using Azure;
using Azure.Core;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task<Response> GetAsync(string a, Azure.RequestContext context)
        {
            return null;
        }

        public virtual Response Get(string a, Azure.RequestContext context)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0018NotProducedForMethodsWithNoRequestContentButOnlyProtocol()
        {
            const string code = @"
using Azure;
using Azure.Core;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task<Response> GetAsync(string a, Azure.RequestContext context = null)
        {
            return null;
        }

        public virtual Response Get(string a, Azure.RequestContext context = null)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .RunAsync();
        }

        [Fact]
        public async Task AZC0018NotProducedForMethodsWithRequestContentAndOptionalRequestContext()
        {
            const string code = @"
using Azure;
using Azure.Core;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace RandomNamespace
{
    public class SomeClient
    {
        public virtual Task<Response> GetAsync(RequestContent content, RequestContext context = null)
        {
            return null;
        }

        public virtual Response Get(RequestContent content, RequestContext context = null)
        {
            return null;
        }
    }
}";
            await Verifier.CreateAnalyzer(code)
                .RunAsync();
        }
    }
}
