using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;

namespace Azure.Sdk.Tools.TestProxy.Store
{

    /// <summary>
    /// Used to store and retrieve values from the breadcrumb file.
    /// </summary>
    public class BreadcrumbLine
    {
        public string PathToAssetsJson;
        public string ShortHash;
        public string Tag;

        /// <summary>
        /// Creates a breadcrumb (with relevant details) from an existing breadcrumb line.
        /// </summary>
        /// <param name="line"></param>
        public BreadcrumbLine(string line)
        {
            // split the line here. assign values
            var values = line.Split(";").Select(x => x.Trim()).ToList();

            if (values.Count() != 3)
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"Unable to parse the line {line}");
            }

            PathToAssetsJson = values[0];
            ShortHash = values[1];
            Tag = values[2];
        }

        /// <summary>
        /// Converts a configuration to the breadcrumb presentation.
        /// </summary>
        /// <param name="config"></param>
        public BreadcrumbLine(GitAssetsConfiguration config)
        {
            PathToAssetsJson = config.AssetsJsonRelativeLocation.ToString();
            ShortHash = config.AssetRepoShortHash;
            Tag = config.Tag;
        }

        /// <summary>
        /// Contract is AssetsJsonRelative;ShortHash;TargetTag
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"{PathToAssetsJson.Replace("\\", "/")};{ShortHash};{Tag}";
        }
    }

    /// <summary>
    /// The breadcrumb store is shared across any asset repo within a given asset store. Given that, we need to control access
    /// to one update at any given time. This ensures that parallel updates can't accidentally flatten the breadcrumb lines from
    /// each other.
    /// 
    /// This simple class merely abstracts the enqueuing of that work so that users don't need to worry about the above.
    ///
    /// NOTE: TaskQueue serializes updates within a single process only. Multiple test-proxy instances running concurrently
    /// against the same --storage-location (e.g. `go test ./...` across SDK subpackages on a developer box or CI) are peers
    /// that share the breadcrumb file with no cross-process coordination. To avoid spurious ERROR_SHARING_VIOLATION failures
    /// on Windows in that scenario, all reads and writes below open the file with FileShare.ReadWrite | FileShare.Delete,
    /// which matches the default sharing semantics of POSIX open(2). This intentionally adopts POSIX behavior on Windows
    /// rather than introducing atomic-rename or cross-process locking: the underlying race exists on Linux/macOS today and
    /// has not been observed to cause test failures in practice (writes are small and fast; same-SHA writers produce identical
    /// output, so a lost update is invisible). A fully race-free implementation would require temp-file-plus-rename for writes
    /// and a named cross-process mutex around the read-modify-write in Update; both are deferred until observed harm justifies
    /// the added complexity.
    /// </summary>
    public class GitStoreBreadcrumb
    {
        // Sharing flags that mirror POSIX open(2) defaults: any peer may open, read, write,
        // or unlink/rename the file while we hold a handle.
        private const FileShare PosixLikeShare = FileShare.ReadWrite | FileShare.Delete;

        TaskQueue BreadCrumbWorker = new TaskQueue();

        public GitStoreBreadcrumb() { }

        // ReadAllLinesShared / ReadAllLinesSharedAsync / WriteAllLinesShared mirror the
        // File.ReadAllLines / File.WriteAllLines helpers but open the underlying FileStream
        // with FileShare.ReadWrite | FileShare.Delete. See class remarks for rationale.
        private static string[] ReadAllLinesShared(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, PosixLikeShare);
            using var sr = new StreamReader(fs, Encoding.UTF8);
            var lines = new List<string>();
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                lines.Add(line);
            }
            return lines.ToArray();
        }

        private static async Task<string[]> ReadAllLinesSharedAsync(string path)
        {
            // Open with FileOptions.Asynchronous so ReadLineAsync uses true async I/O
            // rather than dispatching sync reads to the thread pool.
            var opts = new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = PosixLikeShare,
                Options = FileOptions.Asynchronous,
            };
            using var fs = new FileStream(path, opts);
            using var sr = new StreamReader(fs, Encoding.UTF8);
            var lines = new List<string>();
            string line;
            while ((line = await sr.ReadLineAsync()) != null)
            {
                lines.Add(line);
            }
            return lines.ToArray();
        }

        private static void WriteAllLinesShared(string path, IEnumerable<string> lines)
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, PosixLikeShare);
            using var sw = new StreamWriter(fs, Encoding.UTF8);
            foreach (var line in lines)
            {
                sw.WriteLine(line);
            }
        }

        public string GetBreadCrumbLocation(GitAssetsConfiguration configuration)
        {
            var breadCrumbFolder = Path.Combine(configuration.ResolveAssetsStoreLocation().ToString(), "breadcrumb");

            if (!Directory.Exists(breadCrumbFolder))
            {
                Directory.CreateDirectory(breadCrumbFolder);
            }

            return Path.Join(breadCrumbFolder, $"{configuration.AssetRepoShortHash}.breadcrumb");
        }

        /// <summary>
        /// Updates an existing breadcrumb file with an assets configuration. Add/Update only. Should never remove.
        /// </summary>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public async Task Update(GitAssetsConfiguration configuration)
        {
            var breadcrumbFile = GetBreadCrumbLocation(configuration);
            var targetKey = configuration.AssetsJsonRelativeLocation.ToString();

            await BreadCrumbWorker.EnqueueAsync(async () =>
            {
                IEnumerable<string> linesForWriting = null;

                if (!File.Exists(breadcrumbFile))
                {
                    linesForWriting = new List<string>() { new BreadcrumbLine(configuration).ToString() };
                }
                else
                {
                    var readLines = await ReadAllLinesSharedAsync(breadcrumbFile);
                    var lines = readLines.Select(x => new BreadcrumbLine(x)).ToDictionary(x => x.PathToAssetsJson, x => x);
                    lines[targetKey] = new BreadcrumbLine(configuration);
                    linesForWriting = lines.Values.Select(x => x.ToString());
                }
                
                WriteAllLinesShared(breadcrumbFile, linesForWriting);
            });
        }

        public void RefreshLocalCache(ConcurrentDictionary<string, string> localCache, GitAssetsConfiguration config)
        {
            var breadLocation = GetBreadCrumbLocation(config);

            if (File.Exists(breadLocation))
            {
                var readLines = ReadAllLinesShared(breadLocation);
                var lines = readLines.Select(x => new BreadcrumbLine(x)).ToDictionary(x => x.PathToAssetsJson, x => x);

                foreach (var line in lines)
                {
                    localCache.AddOrUpdate(line.Value.PathToAssetsJson, line.Value.Tag, (key, oldValue) => line.Value.Tag);
                }
            }
        }
    }

}
