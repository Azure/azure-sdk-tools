// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Azure.Sdk.Tools.SnippetGenerator.Tests
{
    public class ProgramTests
    {
        [Test]
        public void OnExecuteAsync_NonExistentBasePath_ThrowsDirectoryNotFoundException()
        {
            var program = new Program
            {
                BasePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
            };

            var ex = Assert.ThrowsAsync<DirectoryNotFoundException>(async () => await program.OnExecuteAsync());
            StringAssert.Contains("base path", ex.Message);
        }

        [Test]
        public void OnExecuteAsync_NonExistentTargetPath_ThrowsDirectoryNotFoundException()
        {
            var basePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(basePath);

            try
            {
                var program = new Program
                {
                    BasePath = basePath,
                    TargetPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
                };

                var ex = Assert.ThrowsAsync<DirectoryNotFoundException>(async () => await program.OnExecuteAsync());
                StringAssert.Contains("target path", ex.Message);
            }
            finally
            {
                Directory.Delete(basePath, true);
            }
        }

        [Test]
        public void OnExecuteAsync_ValidPaths_DoesNotThrow()
        {
            var basePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(basePath);

            try
            {
                var program = new Program
                {
                    BasePath = basePath
                };

                Assert.DoesNotThrowAsync(async () => await program.OnExecuteAsync());
            }
            finally
            {
                Directory.Delete(basePath, true);
            }
        }

        [Test]
        public void OnExecuteAsync_ValidBaseAndTargetPaths_DoesNotThrow()
        {
            var basePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var targetPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(basePath);
            Directory.CreateDirectory(targetPath);

            try
            {
                var program = new Program
                {
                    BasePath = basePath,
                    TargetPath = targetPath
                };

                Assert.DoesNotThrowAsync(async () => await program.OnExecuteAsync());
            }
            finally
            {
                Directory.Delete(basePath, true);
                Directory.Delete(targetPath, true);
            }
        }

        [Test]
        public void OnExecuteAsync_ThrowsWhenBasePathDoesNotExist()
        {
            var program = new Program
            {
                BasePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
                TargetPath = Path.GetTempPath()
            };

            Assert.ThrowsAsync<DirectoryNotFoundException>(async () => await program.OnExecuteAsync());
        }

        [Test]
        public void OnExecuteAsync_ThrowsWhenTargetPathDoesNotExist()
        {
            var basePath = Directory.CreateDirectory(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;

            try
            {
                var program = new Program
                {
                    BasePath = basePath,
                    TargetPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
                };

                Assert.ThrowsAsync<DirectoryNotFoundException>(program.OnExecuteAsync);
            }
            finally
            {
                Directory.Delete(basePath, recursive: true);
            }
        }

        [Test]
        public async Task OnExecuteAsync_TargetPathDefaultsToBasePathWhenNull()
        {
            var basePath = Directory.CreateDirectory(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;

            try
            {
                var program = new Program
                {
                    BasePath = basePath,
                    TargetPath = null
                };

                // Should not throw the directory-not-found exception since target falls back to BasePath.
                await program.OnExecuteAsync();
            }
            finally
            {
                Directory.Delete(basePath, recursive: true);
            }
        }

        [Test]
        public async Task OnExecuteAsync_ProcessesEachSubdirectoryWhenBaseDirectoryIsSdk()
        {
            var root = Directory.CreateDirectory(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;
            var sdkDir = Directory.CreateDirectory(Path.Combine(root, "sdk")).FullName;
            Directory.CreateDirectory(Path.Combine(sdkDir, "serviceA"));
            Directory.CreateDirectory(Path.Combine(sdkDir, "serviceB"));

            try
            {
                var program = new Program
                {
                    BasePath = sdkDir,
                    TargetPath = sdkDir
                };

                await program.OnExecuteAsync();
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public async Task OnExecuteAsync_ProcessesSingleDirectoryWhenBaseDirectoryIsNotSdk()
        {
            var basePath = Directory.CreateDirectory(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "notSdk")).FullName;

            try
            {
                var program = new Program
                {
                    BasePath = basePath,
                    TargetPath = basePath
                };

                await program.OnExecuteAsync();
            }
            finally
            {
                Directory.Delete(Directory.GetParent(basePath)!.FullName, recursive: true);
            }
        }
    }
}
