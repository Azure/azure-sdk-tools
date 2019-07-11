﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Threading.Tasks;
using Xunit;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class AZC0010Tests
    {
        private readonly DiagnosticAnalyzerRunner _runner = new DiagnosticAnalyzerRunner(new ClientOptionsAnalyzer());

        [Fact]
        public async Task AZC0010ProducedForClientOptionsCtorWithNoServiceVersionDefault()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class SomeClientOptions { 

        public enum ServiceVersion
        {
            V2018_11_09 = 0
        }

        public SomeClientOptions(ServiceVersion /*MM*/version)
        {
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0010", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0010ProducedForClientOptionsCtorWithNonMaxVersionExplicit()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class SomeClientOptions { 

        public enum ServiceVersion
        {
            V2018_11_09 = 0,
            V2019_03_20 = 1
        }

        public SomeClientOptions(ServiceVersion /*MM*/version = ServiceVersion.V2018_11_09)
        {
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0010", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0010ProducedForClientOptionsCtorWithNonMaxVersionImplicit()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class SomeClientOptions { 

        public enum ServiceVersion
        {
            V2018_11_09,
            V2019_03_20
        }

        public SomeClientOptions(ServiceVersion /*MM*/version = ServiceVersion.V2018_11_09)
        {
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);

            var diagnostic = Assert.Single(diagnostics);

            Assert.Equal("AZC0010", diagnostic.Id);
            AnalyzerAssert.DiagnosticLocation(testSource.DefaultMarkerLocation, diagnostics[0].Location);
        }

        [Fact]
        public async Task AZC0010NotProducedForClientOptionsCtorWithMaxServiceVersionDefault()
        {
            var testSource = TestSource.Read(@"
namespace RandomNamespace
{
    public class SomeClientOptions { 

        public enum ServiceVersion
        {
            V2018_11_09 = 0,
            V2019_03_20 = 1
        }

        public SomeClientOptions(ServiceVersion version = ServiceVersion.V2019_03_20)
        {
        }
    }
}
");
            var diagnostics = await _runner.GetDiagnosticsAsync(testSource.Source);
            Assert.Empty(diagnostics);
        }
    }
}
