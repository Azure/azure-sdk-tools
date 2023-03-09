using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Azure.Sdk.Tools.PerfAutomation.Tests
{
    public class CppTest
    {
        [Test]
        public async Task SetupDebugWindows()
        {
            var cpp = new Cpp();
            cpp.IsTest = true;
            cpp.UtilMethodCall += Cpp_UtilMethodCallDebugWindows;
            cpp.WorkingDirectory = "workingFolder";
            cpp.IsWindows = true;
            cpp.ProcessorCount = 16;
            string setupOutput = null;
            string setupError = null;
            object context = null;

            (setupOutput, setupError, context) = await cpp.SetupAsync("project", "languageVersion", "primaryPackage", null, true);
            Assert.AreEqual(setupOutput, "output");
            Assert.AreEqual(setupError, "error");
            Assert.AreEqual(context, "exe");
        }

        private void Cpp_UtilMethodCallDebugWindows(object sender, Cpp.UtilEventArgs e)
        {
            if (e.MethodName == "DeleteIfExists")
            {
                Assert.AreEqual(e.Params[0], Path.Combine("workingFolder", "build"));
            }
            else if (e.MethodName == "CreateDirectory")
            {
                Assert.AreEqual(e.Params[0], Path.Combine("workingFolder", "build"));
            }
            else if (e.MethodName == "UpdatePackageVersions")
            {
                Assert.AreEqual(e.Params[0], "packageVersions");
            }
            else if (e.MethodName == "RunAsync1")
            {
                Assert.AreEqual(e.Params[0], "cmake");
                Assert.AreEqual(e.Params[1], "-DBUILD_TESTING=ON -DBUILD_PERFORMANCE_TESTS=ON -DDISABLE_AZURE_CORE_OPENTELEMETRY=ON ..");
                Assert.AreEqual(e.Params[2], Path.Combine("workingFolder", "build"));
                Assert.AreEqual(e.Params[3], "output");
                Assert.AreEqual(e.Params[4], "error");
            }
            else if (e.MethodName == "RunAsync2")
            {
                Assert.AreEqual(e.Params[0], "cmake");
                Assert.AreEqual(e.Params[1], "--build . --parallel 16 --config Debug --target project");
                Assert.AreEqual(e.Params[2], Path.Combine("workingFolder", "build"));
                Assert.AreEqual(e.Params[3], "output");
                Assert.AreEqual(e.Params[4], "error");
            }
            else
            {
                Assert.Fail("Unknown method");
            }
        }

        [Test]
        public async Task SetupRetailWindows()
        {
            var cpp = new Cpp();
            cpp.IsTest = true;
            cpp.UtilMethodCall += Cpp_UtilMethodCallRetailWindows;
            cpp.WorkingDirectory = "workingFolder";
            cpp.IsWindows = true;
            cpp.ProcessorCount = 16;
            string setupOutput = null;
            string setupError = null;
            object context = null;

            (setupOutput, setupError, context) = await cpp.SetupAsync("project", "languageVersion", "primaryPackage", null, false);
            Assert.AreEqual(setupOutput, "output");
            Assert.AreEqual(setupError, "error");
            Assert.AreEqual(context, "exe");
        }

        private void Cpp_UtilMethodCallRetailWindows(object sender, Cpp.UtilEventArgs e)
        {
            if (e.MethodName == "DeleteIfExists")
            {
                Assert.AreEqual(e.Params[0], Path.Combine("workingFolder", "build"));
            }
            else if (e.MethodName == "CreateDirectory")
            {
                Assert.AreEqual(e.Params[0], Path.Combine("workingFolder", "build"));
            }
            else if (e.MethodName == "UpdatePackageVersions")
            {
                Assert.AreEqual(e.Params[0], "packageVersions");
            }
            else if (e.MethodName == "RunAsync1")
            {
                Assert.AreEqual(e.Params[0], "cmake");
                Assert.AreEqual(e.Params[1], "-DBUILD_TESTING=ON -DBUILD_PERFORMANCE_TESTS=ON -DDISABLE_AZURE_CORE_OPENTELEMETRY=ON ..");
                Assert.AreEqual(e.Params[2], Path.Combine("workingFolder", "build"));
                Assert.AreEqual(e.Params[3], "output");
                Assert.AreEqual(e.Params[4], "error");
            }
            else if (e.MethodName == "RunAsync2")
            {
                Assert.AreEqual(e.Params[0], "cmake");
                Assert.AreEqual(e.Params[1], "--build . --parallel 16 --config MinSizeRel --target project");
                Assert.AreEqual(e.Params[2], Path.Combine("workingFolder", "build"));
                Assert.AreEqual(e.Params[3], "output");
                Assert.AreEqual(e.Params[4], "error");
            }
            else
            {
                Assert.Fail("Unknown method");
            }
        }

        [Test]
        public async Task SetupDebugLinux()
        {
            var cpp = new Cpp();
            cpp.IsTest = true;
            cpp.UtilMethodCall += Cpp_UtilMethodCallDebugLinux;
            cpp.WorkingDirectory = "workingFolder";
            cpp.IsWindows = false;
            cpp.ProcessorCount = 16;
            string setupOutput = null;
            string setupError = null;
            object context = null;

            (setupOutput, setupError, context) = await cpp.SetupAsync("project", "languageVersion", "primaryPackage", null, true);
            Assert.AreEqual(setupOutput, "output");
            Assert.AreEqual(setupError, "error");
            Assert.AreEqual(context, "exe");
        }

        private void Cpp_UtilMethodCallDebugLinux(object sender, Cpp.UtilEventArgs e)
        {
            if (e.MethodName == "DeleteIfExists")
            {
                Assert.AreEqual(e.Params[0], Path.Combine("workingFolder", "build"));
            }
            else if (e.MethodName == "CreateDirectory")
            {
                Assert.AreEqual(e.Params[0], Path.Combine("workingFolder", "build"));
            }
            else if (e.MethodName == "UpdatePackageVersions")
            {
                Assert.AreEqual(e.Params[0], "packageVersions");
            }
            else if (e.MethodName == "RunAsync1")
            {
                Assert.AreEqual(e.Params[0], "cmake");
                Assert.AreEqual(e.Params[1], "-DBUILD_TESTING=ON -DBUILD_PERFORMANCE_TESTS=ON -DCMAKE_BUILD_TYPE=Debug ..");
                Assert.AreEqual(e.Params[2], Path.Combine("workingFolder", "build"));
                Assert.AreEqual(e.Params[3], "output");
                Assert.AreEqual(e.Params[4], "error");
            }
            else if (e.MethodName == "RunAsync2")
            {
                Assert.AreEqual(e.Params[0], "cmake");
                Assert.AreEqual(e.Params[1], "--build . --parallel 16  --target project");
                Assert.AreEqual(e.Params[2], Path.Combine("workingFolder", "build"));
                Assert.AreEqual(e.Params[3], "output");
                Assert.AreEqual(e.Params[4], "error");
            }
            else
            {
                Assert.Fail("Unknown method");
            }
        }

        [Test]
        public async Task SetupRetailLinux()
        {
            var cpp = new Cpp();
            cpp.IsTest = true;
            cpp.UtilMethodCall += Cpp_UtilMethodCallRetailLinux;
            cpp.WorkingDirectory = "workingFolder";
            cpp.IsWindows = false;
            cpp.ProcessorCount = 16;
            string setupOutput = null;
            string setupError = null;
            object context = null;

            (setupOutput, setupError, context) = await cpp.SetupAsync("project", "languageVersion", "primaryPackage", null, false);
            Assert.AreEqual(setupOutput, "output");
            Assert.AreEqual(setupError, "error");
            Assert.AreEqual(context, "exe");
        }

        private void Cpp_UtilMethodCallRetailLinux(object sender, Cpp.UtilEventArgs e)
        {
            if (e.MethodName == "DeleteIfExists")
            {
                Assert.AreEqual(e.Params[0], Path.Combine("workingFolder", "build"));
            }
            else if (e.MethodName == "CreateDirectory")
            {
                Assert.AreEqual(e.Params[0], Path.Combine("workingFolder", "build"));
            }
            else if (e.MethodName == "UpdatePackageVersions")
            {
                Assert.AreEqual(e.Params[0], "packageVersions");
            }
            else if (e.MethodName == "RunAsync1")
            {
                Assert.AreEqual(e.Params[0], "cmake");
                Assert.AreEqual(e.Params[1], "-DBUILD_TESTING=ON -DBUILD_PERFORMANCE_TESTS=ON -DCMAKE_BUILD_TYPE=Release ..");
                Assert.AreEqual(e.Params[2], Path.Combine("workingFolder", "build"));
                Assert.AreEqual(e.Params[3], "output");
                Assert.AreEqual(e.Params[4], "error");
            }
            else if (e.MethodName == "RunAsync2")
            {
                Assert.AreEqual(e.Params[0], "cmake");
                Assert.AreEqual(e.Params[1], "--build . --parallel 16  --target project");
                Assert.AreEqual(e.Params[2], Path.Combine("workingFolder", "build"));
                Assert.AreEqual(e.Params[3], "output");
                Assert.AreEqual(e.Params[4], "error");
            }
            else
            {
                Assert.Fail("Unknown method");
            }
        }

        [Test]
        public async Task RunProfileWindows()
        {
            var cpp = new Cpp();
            cpp.IsTest = true;
            cpp.WorkingDirectory = "workingFolder";
            cpp.IsWindows = true;
            cpp.ProcessorCount = 16;

            try
            {
                await cpp.RunAsync(
                    "project",
                    "languageVersion",
                    "primaryPackage",
                new Dictionary<string, string>(),
                    "testName",
                    "arguments",
                    true,
                    "profilerOptions",
                    "exe");
                Assert.Fail();
            }
            catch (InvalidOperationException)
            {
                Assert.Pass();
            }
        }

        [Test]
        public async Task RunNoProfileWindows()
        {
            var cpp = new Cpp();
            cpp.IsTest = true;
            cpp.UtilMethodCall += Cpp_RunUtilMethodCallNoProfileWindows;
            cpp.WorkingDirectory = "workingFolder";
            cpp.IsWindows = true;
            cpp.ProcessorCount = 16;

            var result = await cpp.RunAsync(
                "project",
                "languageVersion",
                "primaryPackage",
                new Dictionary<string, string>(),
                "testName",
                "arguments",
                false,
                "profilerOptions",
                "exe");

            Assert.AreEqual(result.StandardOutput, "output (2.0 ops/s, 1.0 s/op)");
            Assert.AreEqual(result.StandardError, "error");
            Assert.AreEqual(result.OperationsPerSecond, 2);
        }
        private void Cpp_RunUtilMethodCallNoProfileWindows(object sender, Cpp.UtilEventArgs e)
        {
            if (e.MethodName == "RunAsync")
            {
                Assert.AreEqual(e.Params[0], "exe");
                Assert.AreEqual(e.Params[1], "testName arguments");
                Assert.AreEqual(e.Params[2], "workingFolder");
            }
            else
            {
                Assert.Fail("Unknown method");
            }
        }

        [Test]
        public async Task RunProfileLinux()
        {
            var cpp = new Cpp();
            cpp.IsTest = true;
            cpp.UtilMethodCall += Cpp_RunUtilMethodCallProfileLinux;
            cpp.WorkingDirectory = "workingFolder";
            cpp.IsWindows = false;
            cpp.ProcessorCount = 16;

            var result = await cpp.RunAsync(
                "project",
                "languageVersion",
                "primaryPackage",
                new Dictionary<string, string>(),
                "testName",
                "arguments",
                true,
                "profilerOptions",
                "exe");

            Assert.AreEqual(result.StandardOutput, "output (2.0 ops/s, 1.0 s/op)");
            Assert.AreEqual(result.StandardError, "error");
            Assert.AreEqual(result.OperationsPerSecond, 2);
        }
        private void Cpp_RunUtilMethodCallProfileLinux(object sender, Cpp.UtilEventArgs e)
        {
            if (e.MethodName == "RunAsync")
            {
                Assert.AreEqual(e.Params[0], "valgrind");
                Assert.AreEqual(e.Params[1], "profilerOptions exe testName arguments");
                Assert.AreEqual(e.Params[2], "workingFolder");
            }
            else
            {
                Assert.Fail("Unknown method");
            }

        }

        [Test]
        public async Task RunNoProfileLinux()
        {
            var cpp = new Cpp();
            cpp.IsTest = true;
            cpp.UtilMethodCall += Cpp_RunUtilMethodCallNoProfileLinux;
            cpp.WorkingDirectory = "workingFolder";
            cpp.IsWindows = false;
            cpp.ProcessorCount = 16;

            var result = await cpp.RunAsync(
                "project",
                "languageVersion",
                "primaryPackage",
                new Dictionary<string, string>(),
                "testName",
                "arguments",
                false,
                "profilerOptions",
                "exe");

            Assert.AreEqual(result.StandardOutput, "output (2.0 ops/s, 1.0 s/op)");
            Assert.AreEqual(result.StandardError, "error");
            Assert.AreEqual(result.OperationsPerSecond, 2);
        }
        private void Cpp_RunUtilMethodCallNoProfileLinux(object sender, Cpp.UtilEventArgs e)
        {
            if (e.MethodName == "RunAsync")
            {
                Assert.AreEqual(e.Params[0], "exe");
                Assert.AreEqual(e.Params[1], "testName arguments");
                Assert.AreEqual(e.Params[2], "workingFolder");
            }
            else
            {
                Assert.Fail("Unknown method");
            }

        }
    }
}

