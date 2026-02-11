using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Helpers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.Managers
{
    public class CodeFileManager : ICodeFileManager
    {
        private readonly IEnumerable<LanguageService> _languageServices;
        private readonly IBlobCodeFileRepository _codeFileRepository;
        private readonly IBlobOriginalsRepository _originalsRepository;
        private readonly IDevopsArtifactRepository _devopsArtifactRepository;
        private readonly ILogger<CodeFileManager> _logger;

        public CodeFileManager(
            IEnumerable<LanguageService> languageServices, IBlobCodeFileRepository codeFileRepository,
            IBlobOriginalsRepository originalsRepository, IDevopsArtifactRepository devopsArtifactRepository,
            ILogger<CodeFileManager> logger)
        {
            _originalsRepository = originalsRepository;
            _codeFileRepository = codeFileRepository;
            _languageServices = languageServices;
            _devopsArtifactRepository = devopsArtifactRepository;
            _logger = logger;
        }

        /// <summary>
        /// Downloads and extracts a CodeFile from an Azure DevOps build artifact.
        /// For TypeSpec artifacts, also extracts metadata if a metadata file is provided.
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
        /// <param name="language"></param>
        /// <param name="metadataFileName">Optional TypeSpec metadata file name (e.g., "typespec-metadata.json").</param>
        /// <returns>A CodeFileResult containing the CodeFile and optionally TypeSpec metadata.</returns>
        public async Task<CodeFileResult> GetCodeFileAsync(string repoName,
            string buildId,
            string artifactName,
            string packageName,
            string originalFileName,
            string codeFileName,
            MemoryStream originalFileStream,
            string baselineCodeFileName = "",
            MemoryStream baselineStream = null,
            string project = "public",
            string language = null,
            string metadataFileName = null)
        {
            CodeFile codeFile = null;
            TypeSpecMetadata metadata = null;

            if (string.IsNullOrEmpty(codeFileName))
            {
                // backward compatibility until all languages moved to sandboxing of codefile to pipeline
                using var stream = await _devopsArtifactRepository.DownloadPackageArtifact(repoName, buildId, artifactName, originalFileName, format: "file", project: project);
                codeFile = await CreateCodeFileAsync(originalName: Path.GetFileName(originalFileName), fileStream: stream, runAnalysis: false, memoryStream: originalFileStream, language: language);
            }
            else
            {
                using var stream = await _devopsArtifactRepository.DownloadPackageArtifact(repoName, buildId, artifactName, packageName, format: "zip", project: project);
                using var archive = new ZipArchive(stream);

                if (!string.IsNullOrEmpty(originalFileName))
                {
                    var entry = archive.Entries.FirstOrDefault(e => Path.GetFileName(e.Name) == originalFileName);
                    if (entry != null)
                    {
                        using var entryStream = entry.Open();
                        await entryStream.CopyToAsync(originalFileStream);
                    }
                }
                    
                if (!string.IsNullOrEmpty(baselineCodeFileName))
                {
                    var entry = archive.Entries.FirstOrDefault(e => Path.GetFileName(e.Name) == baselineCodeFileName);
                    if (entry != null)
                    {
                        using var entryStream = entry.Open();
                        await entryStream.CopyToAsync(baselineStream);
                    }
                }

                if (!string.IsNullOrEmpty(codeFileName))
                {
                    var entry = archive.Entries.FirstOrDefault(e => Path.GetFileName(e.Name) == codeFileName);
                    if (entry != null)
                    {
                        using var entryStream = entry.Open();
                        codeFile = await CodeFile.DeserializeAsync(entryStream);
                    }
                }

                if (!string.IsNullOrEmpty(metadataFileName))
                {
                    var metadataEntry = archive.Entries.FirstOrDefault(e => Path.GetFileName(e.Name).Equals(metadataFileName, StringComparison.OrdinalIgnoreCase));
                    if (metadataEntry != null)
                    {
                        try
                        {
                            await using var metadataStream = metadataEntry.Open();
                            metadata = await JsonSerializer.DeserializeAsync<TypeSpecMetadata>(metadataStream, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to deserialize TypeSpec metadata file '{MetadataFileName}'. Continuing without metadata.", metadataFileName);
                        }
                    }
                }
            }

            return new CodeFileResult { CodeFile = codeFile, Metadata = metadata };
        }

        /// <summary>
        /// Create Code File
        /// </summary>
        /// <param name="apiRevisionId"></param>
        /// <param name="originalName"></param>
        /// <param name="runAnalysis"></param>
        /// <param name="fileStream"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public async Task<APICodeFileModel> CreateCodeFileAsync(
            string apiRevisionId,
            string originalName,
            bool runAnalysis,
            Stream fileStream = null,
            string language = null)
        {
            using var memoryStream = new MemoryStream();
            var codeFile = await CreateCodeFileAsync(originalName: originalName, runAnalysis: runAnalysis,
                memoryStream: memoryStream, fileStream: fileStream, language: language);
            var reviewCodeFileModel = await CreateReviewCodeFileModel(apiRevisionId, memoryStream, codeFile);
            reviewCodeFileModel.FileName = originalName;
            memoryStream.Dispose();
            return reviewCodeFileModel;
        }

        /// <summary>
        /// Create Code File
        /// </summary>
        /// <param name="originalName"></param>
        /// <param name="runAnalysis"></param>
        /// <param name="memoryStream"></param>
        /// <param name="fileStream"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        public async Task<CodeFile> CreateCodeFileAsync(
            string originalName,
            bool runAnalysis,
            MemoryStream memoryStream,
            Stream fileStream = null,
            string language = null)
        {
            var languageService = LanguageServiceHelpers.GetLanguageService(language, _languageServices) ?? _languageServices.FirstOrDefault(s => s.IsSupportedFile(originalName));
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
        /// <param name="apiRevisionId"></param>
        /// <param name="memoryStream"></param>
        /// <param name="codeFile"></param>
        /// <returns></returns>
        public async Task<APICodeFileModel> CreateReviewCodeFileModel(string apiRevisionId, MemoryStream memoryStream, CodeFile codeFile)
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
            await _codeFileRepository.UpsertCodeFileAsync(apiRevisionId, reviewCodeFileModel.FileId, codeFile);
            return reviewCodeFileModel;
        }

        /// <summary>
        /// Compare two CodeFiles
        /// </summary>
        /// <param name="codeFileA"></param>
        /// <param name="codeFileB"></param>
        /// <returns>bool</returns>
        public bool AreAPICodeFilesTheSame(RenderedCodeFile codeFileA, RenderedCodeFile codeFileB)
        {
            if (codeFileA.CodeFile.VersionString != codeFileA.CodeFile.VersionString)
            {
                return false;
            }

            var languageService = LanguageServiceHelpers.GetLanguageService(codeFileA.CodeFile.Language, _languageServices);
            if (languageService.UsesTreeStyleParser)
            {
                return CodeFileHelpers.AreCodeFilesSame(codeFileA.CodeFile, codeFileB.CodeFile);
            }
            else
            {
                var codeFileATextLines = codeFileA.RenderText(false, skipDiff: true);
                var codeFileBTextLines = codeFileB.RenderText(false, skipDiff: true);
                return codeFileATextLines.SequenceEqual(codeFileBTextLines);
            }
        }

        public bool AreCodeFilesTheSame(CodeFile codeFileA, CodeFile codeFileB)
        {
            if( codeFileA == null || codeFileB == null )
                return false;

            bool result = true;

            if (codeFileA.Tokens == null || codeFileB.Tokens == null || !codeFileA.Tokens.SequenceEqual(codeFileB.Tokens))
                result = false;

            if (codeFileA.LeafSections == null || codeFileB.LeafSections == null || !codeFileA.LeafSections.SequenceEqual(codeFileB.LeafSections))
                result = false;

            return result;
        }

        private static void InitializeFromCodeFile(APICodeFileModel file, CodeFile codeFile)
        {
            file.Language = codeFile.Language;
            file.LanguageVariant = codeFile.LanguageVariant;
            file.VersionString = codeFile.VersionString;
            file.Name = codeFile.Name;
            file.PackageName = codeFile.PackageName;
            file.PackageVersion = codeFile.PackageVersion;
            file.CrossLanguagePackageId = codeFile.CrossLanguageMetadata != null ? codeFile.CrossLanguageMetadata.CrossLanguagePackageId : codeFile.CrossLanguagePackageId;
            file.ParserStyle = (codeFile.ReviewLines.Count > 0) ? ParserStyle.Tree : ParserStyle.Flat;
        }
    }
}
