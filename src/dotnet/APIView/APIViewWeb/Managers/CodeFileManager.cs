using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Repositories;
using Microsoft.CodeAnalysis.Host;

namespace APIViewWeb.Managers
{
    public class CodeFileManager : ICodeFileManager
    {
        private readonly IEnumerable<LanguageService> _languageServices;
        private readonly IBlobCodeFileRepository _codeFileRepository;
        private readonly IBlobOriginalsRepository _originalsRepository;
        private readonly IDevopsArtifactRepository _devopsArtifactRepository;

        public CodeFileManager(
            IEnumerable<LanguageService> languageServices, IBlobCodeFileRepository codeFileRepository,
            IBlobOriginalsRepository originalsRepository, IDevopsArtifactRepository devopsArtifactRepository)
        {
            _originalsRepository = originalsRepository;
            _codeFileRepository = codeFileRepository;
            _languageServices = languageServices;
            _devopsArtifactRepository = devopsArtifactRepository;
        }

        /// <summary>
        /// Get CodeFile
        /// </summary>
        /// <param name="repoName"></param>
        /// <param name="buildId"></param>
        /// <param name="artifactName"></param>
        /// <param name="packageName"></param>
        /// <param name="originalFileName"></param>
        /// <param name="codeFileName"></param>
        /// <param name="originalFileStream"></param>
        /// <param name="baselineCodeFileName"></param>
        /// <param name="baselineStream"></param>
        /// <param name="project"></param>
        /// <returns></returns>
        public async Task<CodeFile> GetCodeFileAsync(string repoName,
            string buildId,
            string artifactName,
            string packageName,
            string originalFileName,
            string codeFileName,
            MemoryStream originalFileStream,
            string baselineCodeFileName = "",
            MemoryStream baselineStream = null,
            string project = "public"
            )
        {
            Stream stream = null;
            CodeFile codeFile = null;
            if (string.IsNullOrEmpty(codeFileName))
            {
                // backward compatibility until all languages moved to sandboxing of codefile to pipeline
                stream = await _devopsArtifactRepository.DownloadPackageArtifact(repoName, buildId, artifactName, originalFileName, format: "file", project: project);
                codeFile = await CreateCodeFileAsync(Path.GetFileName(originalFileName), stream, false, originalFileStream);
            }
            else
            {
                stream = await _devopsArtifactRepository.DownloadPackageArtifact(repoName, buildId, artifactName, packageName, format: "zip", project: project);
                var archive = new ZipArchive(stream);
                foreach (var entry in archive.Entries)
                {
                    var fileName = Path.GetFileName(entry.Name);
                    if (fileName == originalFileName)
                    {
                        await entry.Open().CopyToAsync(originalFileStream);
                    }

                    if (fileName == codeFileName)
                    {
                        codeFile = await CodeFile.DeserializeAsync(entry.Open());
                    }
                    else if (fileName == baselineCodeFileName)
                    {
                        await entry.Open().CopyToAsync(baselineStream);
                    }
                }
            }

            return codeFile;
        }

        /// <summary>
        /// Create Code File
        /// </summary>
        /// <param name="revisionId"></param>
        /// <param name="originalName"></param>
        /// <param name="fileStream"></param>
        /// <param name="runAnalysis"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public async Task<APICodeFileModel> CreateCodeFileAsync(
            string revisionId,
            string originalName,
            Stream fileStream,
            bool runAnalysis,
            string language)
        {
            using var memoryStream = new MemoryStream();
            var codeFile = await CreateCodeFileAsync(originalName, fileStream, runAnalysis, memoryStream, language);
            var reviewCodeFileModel = await CreateReviewCodeFileModel(revisionId, memoryStream, codeFile);
            reviewCodeFileModel.FileName = originalName;
            return reviewCodeFileModel;
        }

        /// <summary>
        /// Create Code File
        /// </summary>
        /// <param name="originalName"></param>
        /// <param name="fileStream"></param>
        /// <param name="runAnalysis"></param>
        /// <param name="memoryStream"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public async Task<CodeFile> CreateCodeFileAsync(
            string originalName,
            Stream fileStream,
            bool runAnalysis,
            MemoryStream memoryStream,
            string language = null)
        {
            var languageService = _languageServices.FirstOrDefault(s => (language != null ? s.Name == language : s.IsSupportedFile(originalName)));
            if (fileStream != null)
            {
                await fileStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
            }
            CodeFile codeFile = null;
            if (languageService.IsReviewGenByPipeline)
            {
                codeFile = languageService.GetReviewGenPendingCodeFile(originalName);
            }
            else
            {
                codeFile = await languageService.GetCodeFileAsync(
                originalName,
                memoryStream,
                runAnalysis);
            }
            return codeFile;
        }

        /// <summary>
        /// Create Code File
        /// </summary>
        /// <param name="revisionId"></param>
        /// <param name="memoryStream"></param>
        /// <param name="codeFile"></param>
        /// <returns></returns>
        public async Task<APICodeFileModel> CreateReviewCodeFileModel(string revisionId, MemoryStream memoryStream, CodeFile codeFile)
        {
            var reviewCodeFileModel = new APICodeFileModel
            {
                HasOriginal = true,
            };

            InitializeFromCodeFile(reviewCodeFileModel, codeFile);
            if (memoryStream != null)
            {
                memoryStream.Position = 0;
                await _originalsRepository.UploadOriginalAsync(reviewCodeFileModel.FileId, memoryStream);
            }
            await _codeFileRepository.UpsertCodeFileAsync(revisionId, reviewCodeFileModel.FileId, codeFile);
            return reviewCodeFileModel;
        }

        private void InitializeFromCodeFile(APICodeFileModel file, CodeFile codeFile)
        {
            file.Language = codeFile.Language;
            file.LanguageVariant = codeFile.LanguageVariant;
            file.VersionString = codeFile.VersionString;
            file.Name = codeFile.Name;
            file.PackageName = codeFile.PackageName;
        }
    }
}
