using Azure.Sdk.Tools.TestProxy.Common;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public static class TestHelpers
    {
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
            if(entry.Request.Body != null)
            {
                context.Request.Body = new BinaryData(entry.Request.Body).ToStream();
            }
            context.Request.Method = entry.RequestMethod.ToString();
            foreach (var header in entry.Request.Headers)
            {
                context.Request.Headers[header.Key] = header.Value;
            }

            context.Request.Headers["x-recording-upstream-base-uri"] = entry.RequestUri;

            var uri = new Uri(entry.RequestUri);
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
        /// <returns>The absolute path to the created folder.</returns>
        public static string DescribeTestFolder(string assetsJsonContent, string[] sampleFiles, bool ignoreEmptyAssetsJson = false)
        {
            // get a test folder root
            var tmpPath = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString());
            var testFolder = Directory.CreateDirectory(tmpPath);

            var assetsJsonPath = Path.Join(tmpPath, "assets.json");

            foreach (var sampleFile in sampleFiles)
            {
                var fullPath = Path.Join(tmpPath, sampleFile);

                if (Path.HasExtension(fullPath))
                {
                    if (fullPath.EndsWith("assets.json"))
                    {
                        assetsJsonPath = fullPath;
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

            // write a .git file into the root
            WriteTestFile(String.Empty, Path.Combine(tmpPath, ".git"));

            // write assets json if we were passed content
            if (!String.IsNullOrWhiteSpace(assetsJsonContent) || ignoreEmptyAssetsJson)
            {
                WriteTestFile(assetsJsonContent, assetsJsonPath);
            }

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
            {   string errorString = String.Format("FileName {0} does not exist", fullFileName);
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
                string errorString = String.Format("FileName {0} does not exist", fullFileName);
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
                string errorString = String.Format("FileName {0} already exists", fullFileName);
                throw new ArgumentException(errorString);
            }

            File.WriteAllText(fullFileName, "1");
        }
    }
}
