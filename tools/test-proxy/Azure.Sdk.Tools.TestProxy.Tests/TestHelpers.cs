using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Azure.Sdk.Tools.TestProxy.Store;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    /// <summary>
    /// Assets is the class representation of assets.json. When setting up for the push tests we're going to end up
    /// creating a branch of the original TagPrefix so we can automatically push to it. This is done at setup
    /// time and then the AssetsReproBranch will have to be changed to use this new branch. This class will be used
    /// to deserialize from string representation of assets.json, set the TagPrefix to the generated testing
    /// branch and serialize back into a string. This is only used for testing purposes.
    /// </summary>
    public class Assets
    {
        public string AssetsRepo { get; set; }
        public string AssetsRepoPrefixPath { get; set; }
        public string AssetsRepoId { get; set; }
        public string TagPrefix { get; set; }
        public string Tag { get; set; }
    }

    public static class TestHelpers
    {
        public static readonly string DisableBranchCleanupEnvVar = "DISABLE_INTEGRATION_BRANCH_CLEANUP";

        public static string GetValueFromCertificateFile(string certName)
        {
            var path = Path.Join(Directory.GetCurrentDirectory(), "Test.Certificates", certName);

            return File.ReadAllText(path);
        }

        public static Stream GenerateStreamRequestBody(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        public static string GenerateRandomFile(double sizeInMb, string destinationFolder)
        {
            if (!Directory.Exists(destinationFolder))
            {
                throw new Exception($"To generate a new test file, the destination folder {destinationFolder} must exist.");
            }

            var fileName = Path.Join(destinationFolder, $"{Guid.NewGuid()}.txt");

            const int blockSize = 1024 * 8;
            const int blocksPerMb = (1024 * 1024) / blockSize;

            byte[] data = new byte[blockSize];
            Random rng = new Random();

            using (FileStream stream = File.OpenWrite(fileName))
            {
                for (int i = 0; i < sizeInMb * blocksPerMb; i++)
                {
                    rng.NextBytes(data);
                    stream.Write(data, 0, data.Length);
                }
            }

            return fileName;
        }

        public static ModifiableRecordSession LoadRecordSession(string path)
        {
            using var stream = System.IO.File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream);

            return new ModifiableRecordSession(RecordSession.Deserialize(doc.RootElement));
        }

        public static RecordingHandler LoadRecordSessionIntoInMemoryStore(string path)
        {
            using var stream = System.IO.File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream);
            var guid = Guid.NewGuid().ToString();
            var session = new ModifiableRecordSession(RecordSession.Deserialize(doc.RootElement));

            RecordingHandler handler = new RecordingHandler(Directory.GetCurrentDirectory());
            handler.InMemorySessions.TryAdd(guid, session);

            return handler;
        }

        public static string GenerateStringFromStream(Stream s)
        {
            s.Position = 0;
            using StreamReader reader = new StreamReader(s);

            return reader.ReadToEnd();
        }

        public static byte[] GenerateByteRequestBody(string s)
        {
            return Encoding.UTF8.GetBytes(s);
        }

        public static HttpRequest CreateRequestFromEntry(RecordEntry entry)
        {
            var context = new DefaultHttpContext();
            if (entry.Request.Body != null)
            {
                context.Request.Body = new BinaryData(entry.Request.Body).ToStream();
            }
            context.Request.Method = entry.RequestMethod.ToString();
            foreach (var header in entry.Request.Headers)
            {
                context.Request.Headers[header.Key] = header.Value;
            }

            var uri = new Uri(entry.RequestUri);

            context.Request.Headers["x-recording-upstream-base-uri"] = new UriBuilder(uri.Scheme, uri.Host, uri.Port).Uri.ToString();
            context.Request.Host = new HostString(uri.Authority);
            context.Request.QueryString = new QueryString(uri.Query);
            context.Request.Path = uri.AbsolutePath;
            context.Features.Get<IHttpRequestFeature>().RawTarget = context.Request.Path + context.Request.QueryString;
            return context.Request;
        }

        public static void WriteTestFile(string content, string path)
        {

            var directoryName = Path.GetDirectoryName(path);
            if (!Directory.Exists(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }

            File.WriteAllText(path, content);
        }

        /// <summary>
        /// Used to define any set of file constructs we want. This enables us to roll a target environment to point various GitStore functionalities at.
        ///
        /// Creates folder under the temp directory.
        /// </summary>
        /// <param name="assetsJsonContent">The content of the assets json, if any.</param>
        /// <param name="sampleFiles">A set of relative paths defining what the folder structure of the test folder. Paths should be relative to the root of the newly created temp folder.
        /// If one of the paths ends with assets.json, that path will receive the assetsJsonContent string, instead of defaulting to the root of the temp folder.</param>
        /// <param name="ignoreEmptyAssetsJson">Normally passing string.Empty to assetsJsonContent argument will result in no assets.json being written.
        /// Passing true to this argument will ensure that the file is still created without content.</param>
        /// <param name="isPushTest">Whether or not the scenario being run is a push test</param>
        /// <returns>The absolute path to the created folder.</returns>
        public static string DescribeTestFolder(Assets assets, string[] sampleFiles, string malformedJson = null, bool ignoreEmptyAssetsJson = false, bool isPushTest = false)
        {
            string localAssetsJsonContent = JsonSerializer.Serialize(assets);
            if (null != malformedJson)
            {
                localAssetsJsonContent = malformedJson;
            }
            // the guid will be used to create a unique test folder root and, if this is a push test,
            // it'll be used as part of the generated branch name
            string testGuid = Guid.NewGuid().ToString();
            // generate a test folder root
            var tmpPath = Path.Join(Path.GetTempPath(), testGuid);

            // Push tests need some special setup for automation
            // 1. The AssetsReproBranch
            if (isPushTest)
            {
                string adjustedAssetsRepoTag = string.Format("test_{0}_{1}", testGuid, assets.TagPrefix);
                // Call InitIntegrationTag
                InitIntegrationTag(assets, adjustedAssetsRepoTag);

                // set the TagPrefix to the adjusted test branch
                assets.Tag = adjustedAssetsRepoTag;
                localAssetsJsonContent = JsonSerializer.Serialize(assets);
            }

            var testFolder = Directory.CreateDirectory(tmpPath);
            var assetsJsonPath = Path.Join(tmpPath, "assets.json");

            foreach (var sampleFile in sampleFiles)
            {
                var fullPath = Path.Join(tmpPath, sampleFile);

                if (Path.HasExtension(fullPath))
                {
                    if (fullPath.EndsWith("assets.json"))
                    {
                        // write assets json if we were passed content
                        if (!String.IsNullOrWhiteSpace(localAssetsJsonContent) || ignoreEmptyAssetsJson)
                        {
                            WriteTestFile(localAssetsJsonContent, fullPath);
                        }
                    }
                    else
                    {
                        var ext = Path.GetExtension(fullPath);
                        if (ext == ".json")
                        {
                            var sampleJson = @"
                                {
                                    ""hello"": ""world""
                                }
                            ";

                            WriteTestFile(sampleJson, fullPath);
                        }
                        else
                        {
                            throw new NotImplementedException("Files not ending in .json are not supported by this function currently.");
                        }
                    }
                }
                else
                {
                    Directory.CreateDirectory(fullPath);
                }
            }

            // initialize git repository into root
            GitProcessHandler GitHandler = new GitProcessHandler();
            GitHandler.Run($"init -q", tmpPath);
            // set a dummy git remote, used for protocol detection
            string gitCloneUrl = GitStore.GetCloneUrl("testrepo", Directory.GetCurrentDirectory());
            GitHandler.Run($"remote add test {gitCloneUrl}", tmpPath);

            return testFolder.ToString();
        }


        /// <summary>
        /// Remove the test folder created in the call to DescribeTestFolder
        /// </summary>
        /// <param name="testFolder">The temporary test folder created by TestHelpers.DescribeTestFolder</param>
        public static void RemoveTestFolder(string testFolder)
        {
            // We can't Directory.Delete(path, true) to recursiverly delete the directory
            // because the git files under .git\objects\pack have attributes on them that
            // cause an UnauthorizedAccessException when trying to delete them. Fortunately,
            // setting the attributes to normal allows them to be deleted.
            File.SetAttributes(testFolder, FileAttributes.Normal);

            string[] files = Directory.GetFiles(testFolder);
            string[] dirs = Directory.GetDirectories(testFolder);

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs)
            {
                RemoveTestFolder(dir);
            }

            Directory.Delete(testFolder, false);
        }

        /// <summary>
        /// Verify the version, inside the file, for a given file inside of a test folder.
        /// </summary>
        /// <param name="testFolder">The temporary test folder created by TestHelpers.DescribeTestFolder</param>
        /// <param name="fileName">The fileName whose version needs verification</param>
        /// <param name="expectedVersion">The expected version in the file</param>
        public static bool VerifyFileVersion(string testFolder, string fileName, int expectedVersion)
        {
            string fullFileName = Path.Combine(testFolder, fileName);
            string stringVersion = "";
            int intVersion = -1;

            if (!File.Exists(fullFileName))
            {
                string errorString = String.Format("AssetsJsonFileName {0} does not exist", fullFileName);
                throw new ArgumentException(errorString);
            }

            using (StreamReader reader = new StreamReader(fullFileName))
            {
                stringVersion = reader.ReadLine() ?? "";
            }

            if (Int32.TryParse(stringVersion, out intVersion))
            {
                if (expectedVersion == intVersion)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Verify the version, inside the file, for a given file inside of a test folder.
        /// </summary>
        /// <param name="testFolder">The temporary test folder created by TestHelpers.DescribeTestFolder</param>
        /// <param name="fileName">The file whose version needs to be incremented</param>
        public static void IncrementFileVersion(string testFolder, string fileName)
        {
            string fullFileName = Path.Combine(testFolder, fileName);
            string stringVersion = "";
            int intVersion = -1;

            if (!File.Exists(fullFileName))
            {
                string errorString = String.Format("AssetsJsonFileName {0} does not exist", fullFileName);
                throw new ArgumentException(errorString);
            }

            using (StreamReader reader = new StreamReader(fullFileName))
            {
                stringVersion = reader.ReadLine() ?? "";
            }

            if (Int32.TryParse(stringVersion, out intVersion))
            {
                File.WriteAllText(fullFileName, (++intVersion).ToString());
            }
        }

        /// <summary>
        /// Create a new file with an initial version of 1
        /// </summary>
        /// <param name="testFolder">The temporary test folder created by TestHelpers.DescribeTestFolder</param>
        /// <param name="fileName">The file to be created</param>
        public static void CreateFileWithInitialVersion(string testFolder, string fileName)
        {
            string fullFileName = Path.Combine(testFolder, fileName);

            if (File.Exists(fullFileName))
            {
                string errorString = String.Format("AssetsJsonFileName {0} already exists", fullFileName);
                throw new ArgumentException(errorString);
            }

            File.WriteAllText(fullFileName, "1");
        }

        /// <summary>
        /// This function is used to confirm that the .breadcrumb file under the assets store contains the appropriate
        /// information.
        /// </summary>
        /// <param name="configuration"></param>
        public static void CheckBreadcrumbAgainstAssetsConfig(GitAssetsConfiguration configuration)
        {
            var assetsStorePath = configuration.ResolveAssetsStoreLocation();
            var breadCrumbFile = Path.Join(assetsStorePath.ToString(), "breadcrumb", $"{configuration.AssetRepoShortHash}.breadcrumb");
            var targetKey = configuration.AssetsJsonRelativeLocation.ToString();

            Assert.True(File.Exists(breadCrumbFile));

            var contents = File.ReadAllLines(breadCrumbFile).Select(x => new BreadcrumbLine(x)).ToDictionary(x => x.PathToAssetsJson, x => x);

            Assert.True(contents.ContainsKey(targetKey));

            Assert.Equal(configuration.Tag, contents[targetKey].Tag);
            Assert.Equal(targetKey, contents[targetKey].PathToAssetsJson);
            Assert.Equal(configuration.AssetRepoShortHash, contents[targetKey].ShortHash);
        }

        /// <summary>
        /// This function is used to confirm that the .breadcrumb file under the assets store contains the appropriate
        /// information.
        /// </summary>
        /// <param name="configuration"></param>
        public static void CheckBreadcrumbAgainstAssetsConfigs(IEnumerable<GitAssetsConfiguration> configuration)
        {
            foreach (var config in configuration)
            {
                CheckBreadcrumbAgainstAssetsConfig(config);
            }
        }

        /// <summary>
        /// This function is used to confirm that the .breadcrumb file under the assets store contains the appropriate
        /// information.
        /// </summary>
        /// <param name="configuration"></param>
        public static async Task CheckBreadcrumbAgainstAssetsJsons(IEnumerable<string> jsonFileLocations)
        {
            GitStore store = new GitStore();

            foreach (var jsonFile in jsonFileLocations)
            {
                var config = await store.ParseConfigurationFile(jsonFile);
                CheckBreadcrumbAgainstAssetsConfig(config);
            }
        }

        /// <summary>
        /// This function is only used by the Push scenarios. It'll clone the assets repository
        /// </summary>
        /// <param name="assets"></param>
        /// <param name="adjustedAssetsRepoTag"></param>
        public static void InitIntegrationTag(Assets assets, string adjustedAssetsRepoTag)
        {
            // generate a test folder root
            string tmpPath = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString());

            // What needs to get done here is as follows:
            // 1. Clone the original assets repo
            // 2. <response> = git ls-remote --heads <gitCloneUrl> <assets.TagPrefix>
            // if the <response> is empty then just return, if not then create a test branch
            try
            {
                Directory.CreateDirectory(tmpPath);
                GitProcessHandler GitHandler = new GitProcessHandler();
                var gitCloneUrl = GitStore.GetCloneUrl(assets.AssetsRepo, tmpPath);

                // Clone the original assets repo
                GitHandler.Run($"clone --filter=blob:none {gitCloneUrl} .", tmpPath);

                // Check to see if the tag already exists
                CommandResult commandResult = GitHandler.Run($"ls-remote --tags {gitCloneUrl} {assets.Tag}", tmpPath);

                // If the commandResult response is empty, there's nothing to do and we can return
                if (!String.IsNullOrWhiteSpace(commandResult.StdOut))
                {
                    // If the commandResult response is not empty, the command result will have something
                    // similar to the following:
                    // e4a4949a2b6cc2ff75afd0fe0d97cbcabf7b67b7	refs/heads/scenario_clean_push
                    GitHandler.Run($"checkout {assets.Tag}", tmpPath);
                }

                // Create the adjustedAssetsRepoTag from the original branch. The reason being is that pushing
                // to a branch of a branch is automatic
                GitHandler.Run($"tag {adjustedAssetsRepoTag}", tmpPath);
                // Push the contents of the TagPrefix into the adjustedAssetsRepoTag
                GitHandler.Run($"push origin {adjustedAssetsRepoTag}", tmpPath);
            }
            finally
            {
                // After creating the test branch, there's nothing that needs to remain around.
                RemoveTestFolder(tmpPath);
            }
        }

        /// <summary>
        /// This function is only called by Push tests to cleanup the integration test tag.
        /// </summary>
        /// <param name="assets">The updated assets.json content which contains the tag to delete</param>
        public static void CleanupIntegrationTestTag(Assets assets)
        {
            var skipBranchCleanup = Environment.GetEnvironmentVariable(DisableBranchCleanupEnvVar);
            if (!String.IsNullOrWhiteSpace(skipBranchCleanup))
            {
                return;
            }

            // Assets can be null of something in the push testcase happens (throw or assert) before
            // the push completes and updates the assets.json
            if (assets == null)
            {
                return;
            }

            // generate a test folder root
            string tmpPath = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                Directory.CreateDirectory(tmpPath);
                GitProcessHandler GitHandler = new GitProcessHandler();
                string gitCloneUrl = GitStore.GetCloneUrl(assets.AssetsRepo, Directory.GetCurrentDirectory());
                GitHandler.Run($"clone --filter=blob:none {gitCloneUrl} .", tmpPath);
                GitHandler.Run($"push origin --delete {assets.Tag}", tmpPath);
            }
            finally
            {
                RemoveTestFolder(tmpPath);
            }

        }

        /// <summary>
        /// Create an Assets from a assets.json file on disk
        /// </summary>
        /// <param name="jsonFileLocation">locaion of the assets.json on disk</param>
        /// <returns></returns>
        public static Assets LoadAssetsFromFile(string jsonFileLocation)
        {
            return JsonSerializer.Deserialize<Assets>(File.ReadAllText(jsonFileLocation));
        }

        /// <summary>
        /// Update the assets.json from the input Assets
        /// </summary>
        /// <param name="jsonFileLocation">locaion of the assets.json on disk</param>
        /// <returns></returns>
        public static void UpdateAssetsFile(Assets assets, string jsonFileLocation)
        {
            string localAssetsJsonContent = JsonSerializer.Serialize(assets);
            WriteTestFile(localAssetsJsonContent, jsonFileLocation);
        }

        /// <summary>
        /// Given a test assets config, check to see if the tag exists on the assets repo.
        /// </summary>
        /// <param name="assets"></param>
        /// <param name="workingDirectory"></param>
        /// <returns></returns>
        public static bool CheckExistenceOfTag(Assets assets, string workingDirectory)
        {
            GitProcessHandler GitHandler = new GitProcessHandler();
            var cloneUrl = GitStore.GetCloneUrl(assets.AssetsRepo, Directory.GetCurrentDirectory());
            CommandResult result = GitHandler.Run($"ls-remote {cloneUrl} --tags {assets.Tag}", workingDirectory);
            return result.StdOut.Trim().Length > 0;
        }
    }
}
