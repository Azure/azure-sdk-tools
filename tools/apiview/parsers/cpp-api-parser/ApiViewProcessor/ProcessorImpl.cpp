// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#include "ProcessorImpl.hpp"
#include "ApiViewProcessor.hpp"
#include <clang/Tooling/CompilationDatabase.h>
#include <fstream>
#include <memory>
#include <nlohmann/json.hpp>
#include <ostream>

using namespace clang;
using namespace clang::tooling;

using namespace nlohmann;

inline std::string_view stringFromU8string(std::u8string const& str)
{
  return std::string_view(reinterpret_cast<const char*>(str.data()), str.size());
}

ApiViewProcessorImpl::ApiViewProcessorImpl(ApiViewProcessorOptions const& options)
    : m_classDatabase{std::make_unique<AzureClassesDatabase>(this)},
      m_includeDetail{options.IncludeDetail}, m_includePrivate{options.IncludePrivate},
      m_filterNamespace{options.FilterNamespace}, m_includeInternal{options.IncludeDetail}
{
}

std::vector<std::filesystem::path> GatherSubdirectories(std::filesystem::path const& path)
{
  std::vector<std::filesystem::path> subdirectories;
  for (auto& entry : std::filesystem::directory_iterator(path))
  {
    if (entry.is_directory())
    {
      subdirectories.push_back(entry.path());
      auto inner = GatherSubdirectories(entry.path());
      subdirectories.insert(subdirectories.end(), inner.begin(), inner.end());
    }
  }
  return subdirectories;
}

const nlohmann::json JsonFromConfigurationPath(
    std::string_view configurationRoot,
    std::string_view const& configurationFileName)
{
  std::filesystem::path configurationFilePath{configurationRoot};
  configurationFilePath /= configurationFileName;
  std::ifstream configurationFile{configurationFilePath};
  if (!configurationFile.is_open())
  {
    throw std::runtime_error(
        "Unable to open configuration file: " + configurationFilePath.string());
  }
  nlohmann::json configurationJson;
  configurationFile >> configurationJson;
  return configurationJson;
}

ApiViewProcessorImpl::ApiViewProcessorImpl(
    std::string_view directoryToProcess,
    std::string_view const& configurationFileName)
    : ApiViewProcessorImpl(
        directoryToProcess,
        JsonFromConfigurationPath(directoryToProcess, configurationFileName))
{
}

ApiViewProcessorImpl::ApiViewProcessorImpl(
    std::string_view directoryToProcess,
    nlohmann::json const& configurationJson)
    : m_currentSourceRoot{std::filesystem::absolute(directoryToProcess)}, m_classDatabase{
                                                   std::make_unique<AzureClassesDatabase>(this)}
{
  if (configurationJson.contains("includeInternal"))
  {
    m_includeInternal = configurationJson["includeInternal"];
  }
  if (configurationJson.contains("includeDetail"))
  {
    m_includeDetail = configurationJson["includeDetail"];
  }
  if (configurationJson.contains("includePrivate"))
  {
    m_includePrivate = configurationJson["includePrivate"];
  }
  if (configurationJson.contains("filterNamespace")
      && !configurationJson["filterNamespace"].is_null())
  {
    m_filterNamespace = configurationJson["filterNamespace"].get<std::string>();
  }
  if (configurationJson.contains("additionalCompilerSwitches")
      && configurationJson["additionalIncludeDirectories"].is_array()
      && configurationJson["additionalIncludeDirectories"].size() != 0)
  {
    m_additionalCompilerArguments = configurationJson["additionalCompilerSwitches"];
  }
  if (configurationJson.contains("additionalIncludeDirectories")
      && configurationJson["additionalIncludeDirectories"].is_array())
  {
    m_additionalIncludeDirectories = configurationJson["additionalIncludeDirectories"];
  }
  if (configurationJson.contains("sourceFilesToProcess")
      && configurationJson["sourceFilesToProcess"].is_array()
      && configurationJson["sourceFilesToProcess"].size() != 0)
  {
    for (const auto& file : configurationJson["sourceFilesToProcess"])
    {
      auto fileToAdd{m_currentSourceRoot};
      fileToAdd /= file;
      m_filesToCompile.push_back(std::filesystem::absolute(fileToAdd));
    }
  }
  else
  {
    llvm::outs() << llvm::raw_ostream::Colors::CYAN << "No source files specified"
                 << llvm::raw_ostream::Colors::RESET << "collecting all files under "
                 << stringFromU8string(m_currentSourceRoot.u8string()) << "\n";
    auto subdirectories = GatherSubdirectories(directoryToProcess);
    for (auto& subdirectory : subdirectories)
    {
      for (auto& entry : std::filesystem::directory_iterator(subdirectory))
      {
        if (entry.is_regular_file())
        {
          auto extension = entry.path().extension();
          if (extension == ".hpp" || extension == ".h")
          {
            m_filesToCompile.push_back(entry.path().string());
          }
        }
      }
    }
  }
}

AzureClassesDatabase::AzureClassesDatabase(ApiViewProcessorImpl* processor) : m_processor{processor}
{
}
AzureClassesDatabase::~AzureClassesDatabase() {}

std::unique_ptr<clang::ASTConsumer> ApiViewProcessorImpl::AstVisitorAction::CreateASTConsumer(
    clang::CompilerInstance& compiler,
    llvm::StringRef /* inFile*/)
{
  return std::make_unique<ExtractCppClassConsumer>(m_processorImpl);
}

ApiViewProcessorImpl::AstVisitorAction::AstVisitorAction(ApiViewProcessorImpl* processorImpl)
    : clang::ASTFrontendAction(), m_processorImpl{processorImpl}
{
}

bool ApiViewProcessorImpl::CollectCppClassesVisitor::ShouldCollectNamedDecl(
    clang::NamedDecl* namedDecl)
{
  bool shouldCollect = false;
  if (m_processorImpl->FilterNamespace().empty())
  {
    // Figure out the source file for this type.
    auto fileId = namedDecl->getASTContext().getSourceManager().getFileID(namedDecl->getLocation());
    auto fileEntry = namedDecl->getASTContext().getSourceManager().getFileEntryForID(fileId);
    if (fileEntry && !fileEntry->getName().startswith("C:\\Program"))
    {
      for (auto const& fileToProcess : m_processorImpl->GetFilesToCompile())
      {
        if (fileToProcess.compare(std::string(fileEntry->getName())) == 0)
        {
          shouldCollect = true;
          // There's no type name filter, so we're going by a source file filter. But we still
          // want to do the type name exclusions.
          std::string typeName{namedDecl->getQualifiedNameAsString()};
          // However if the type is in the _internal namespace, then we want to exclude it if
          // we're excluding internal types.
          if ((typeName.find("::_internal") != std::string::npos)
              && !m_processorImpl->IncludeInternal())
          {
            shouldCollect = false;
          }
          // However if the type is in the _detail namespace, then we want to exclude it if we're
          // excluding detail types.
          if ((typeName.find("::_detail") != std::string::npos)
              && !m_processorImpl->IncludeDetail())
          {
            shouldCollect = false;
          }
        }
      }
    }
  }
  else
  {
    std::string typeName{namedDecl->getQualifiedNameAsString()};
    if (typeName.find(m_processorImpl->FilterNamespace()) == 0)
    {
      // Assume we're going to process this type.
      shouldCollect = true;

      // However if the type is in the _internal namespace, then we want to exclude it if we're
      // excluding internal types.
      if ((typeName.find("::_internal") != std::string::npos)
          && !m_processorImpl->IncludeInternal())
      {
        shouldCollect = false;
      }
      // However if the type is in the _detail namespace, then we want to exclude it if we're
      // excluding detail types.
      if ((typeName.find("::_detail") != std::string::npos) && !m_processorImpl->IncludeDetail())
      {
        // There is an exception for Azure::_detail::Clock to the "exclude _detail" rule.
        if (typeName.find("Azure::_detail::Clock") != 0)
        {
          shouldCollect = false;
        }
      }
    }
  }
  return shouldCollect;
}

class ApiViewCompilationDatabase : public CompilationDatabase {
  std::vector<std::filesystem::path> m_filesToCompile;
  std::filesystem::path m_sourceLocation;
  std::vector<std::filesystem::path> m_additionalIncludePaths;
  std::vector<std::string> m_additionalArguments;

public:
  ApiViewCompilationDatabase(
      std::vector<std::filesystem::path> const& filesToCompile,
      std::filesystem::path const& sourceLocation,
      std::vector<std::filesystem::path> const& additionalIncludePaths,
      std::vector<std::string> const& additionalArguments)
      : CompilationDatabase(), m_filesToCompile(filesToCompile),
        m_sourceLocation(sourceLocation), m_additionalIncludePaths{additionalIncludePaths}
  {
    for (auto const& arg : additionalArguments)
    {
      m_additionalArguments.push_back(std::string(arg));
    }
  }
  std::vector<std::string> defaultCommandLine{
      "cl.exe",
      "--driver-mode=cl",
      "/nologo",
      //      "/TP",
      "-DAZ_RTTI",
      "-DBUILD_CURL_HTTP_TRANSPORT_ADAPTER",
      "-DBUILD_TRANSPORT_WINHTTP_ADAPTER",
      "-DCURL_STATICLIB",
      //"-DTESTING_BUILD",
      //      "-IG:\\Az\\LarryO\\azure-sdk-for-cpp\\sdk\\core\\azure-core\\inc"
      //      "-external:IG:\\Az\\LarryO\\azure-sdk-for-cpp\\out\\build\\x64-DebugWithTests\\vcpkg_installed\\x64-windows-static\\include",
      //      "-external:W0",
      "/DWIN32",
      "/D_WINDOWS",
      "/W4",
      "/GR",
      "/EHsc",
      "/Zi",
      "/Ob0",
      "/Od",
      "/RTC1",
      "/MTd",
      "/permissive-",
      "/W4",
      "/WX",
      "/wd5031",
      "/wd4668",
      "/wd4820",
      "/wd4255",
      "/wd4710",
      "-std:c++14",
      "/FdTARGET_COMPILE_PDB",
      "/FS",
      "-c",
      "/std:c++14",
      //      "-IG:\\Az\\LarryO\\azure-sdk-for-cpp\\sdk\\core\\azure-core\\inc",
      //"--",
      //      "<Source file>"
  };
  // Inherited via CompilationDatabase
  virtual std::vector<CompileCommand> getCompileCommands(StringRef FilePath) const override
  {
    for (auto const& file : m_filesToCompile)
    {
      if (file.compare(static_cast<std::string_view>(FilePath)) == 0)
      {
        std::vector<std::string> commandLine{defaultCommandLine};
        // Add the source location to the include paths.
        commandLine.push_back("-I" + std::string(stringFromU8string(m_sourceLocation.u8string())));
        // Add any additional include directories (as absolute paths).
        for (auto const& arg : m_additionalIncludePaths)
        {
          commandLine.push_back(
              "-I"
              + static_cast<std::string>(
                  stringFromU8string(std::filesystem::absolute(arg).u8string())));
        }
        // And finally, include any additional command line arguments.
        for (auto const& arg : m_additionalArguments)
        {
          commandLine.push_back(arg);
        }
        commandLine.push_back(std::string(stringFromU8string(file.u8string())));

        std::vector<CompileCommand> rv;
        rv.push_back(CompileCommand(
            stringFromU8string(m_sourceLocation.u8string()),
            stringFromU8string(file.u8string()),
            commandLine,
            ""));
        return rv;
      }
    }
    return std::vector<CompileCommand>();
  }
};

std::string replaceAll(
    std::string_view const& source,
    std::string_view const& oldValue,
    std::string_view const& newValue)
{
  std::string newString;
  newString.reserve(source.size());
  size_t findPos{};
  size_t lastPos{};

  while (std::string::npos != (findPos = source.find(oldValue, lastPos)))
  {
    newString.append(source, lastPos, findPos - lastPos);
    newString += newValue;
    lastPos = findPos + oldValue.length();
  }
  newString += source.substr(lastPos);
  return newString;
}

int ApiViewProcessorImpl::ProcessApiView()
{
  // clang really likes all input paths to be absolute paths, so use the fiilesystem to canonicalize
  // the input filename and source location.
  std::filesystem::path tempFile("TempSourceFile.cpp");
  if (m_filesToCompile.size() == 1)
  {
    tempFile = m_filesToCompile.front();
  }
  else
  {
    {
      std::ofstream sourceFileAggregate(
          static_cast<std::string>(stringFromU8string(tempFile.u8string())),
          std::ios::out | std::ios::trunc);
      for (const auto& file : m_filesToCompile)
      {
        assert(file.u8string().find(m_currentSourceRoot.u8string()) == 0);
        auto relativeFile = static_cast<std::string>(stringFromU8string(
            file.u8string().erase(0, m_currentSourceRoot.u8string().size() + 1)));
        std::string quotedFile = replaceAll(relativeFile, "\\", "\\\\");
        sourceFileAggregate << "#include \"" << quotedFile << "\"" << std::endl;
      }
    }
  }

  // Create a compilation database consisting of the source root and source file.
  auto absTemp = m_currentSourceRoot;
  //  absTemp /= tempFile;
  ApiViewCompilationDatabase compileDb(
      /* m_filesToCompile*/ {std::filesystem::absolute(tempFile)},
      m_currentSourceRoot,
      m_additionalIncludeDirectories,
      m_additionalCompilerArguments);

  std::vector<std::string> sourceFiles;
  sourceFiles.push_back(
      std::string(stringFromU8string(std::filesystem::absolute(tempFile).u8string())));

  ClangTool tool(compileDb, sourceFiles);

  auto frontEndActionFactory{std::make_unique<AstVisitorActionFactory>(this)};
  auto rv = tool.run(frontEndActionFactory.get());

  // Insert a terminal node into the classes database, which ensures that all opened namespaces are
  // closed.
  m_classDatabase->CreateAstNode();
  return rv;
}

int ApiViewProcessorImpl::ProcessApiView(
    std::string_view const& sourceLocation,
    std::vector<std::string> const& additionalCompilerArguments,
    std::vector<std::string_view> const& filesToProcess)
{
  // clang really likes all input paths to be absolute paths, so use the fiilesystem to canonicalize
  // the input filename and source location.
  for (const auto file : filesToProcess)
  {
    std::filesystem::path sourcePath(file);
    m_filesToCompile.push_back(std::filesystem::absolute(sourcePath));
  }

  std::filesystem::path rootPath(sourceLocation);
  m_currentSourceRoot = std::filesystem::absolute(rootPath);

  std::filesystem::path tempFile("TempSourceFile.cpp");
  {
    std::ofstream sourceFileAggregate(
        static_cast<std::string>(stringFromU8string(tempFile.u8string())),
        std::ios::out | std::ios::trunc);
    for (const auto& file : m_filesToCompile)
    {
      assert(file.u8string().find(m_currentSourceRoot.u8string()) == 0);
      auto relativeFile = static_cast<std::string>(
          stringFromU8string(file.u8string().erase(0, m_currentSourceRoot.u8string().size() + 1)));
      std::string quotedFile = replaceAll(relativeFile, "\\", "\\\\");
      sourceFileAggregate << "#include \"" << quotedFile << "\"" << std::endl;
    }
  }

  // Create a compilation database consisting of the source root and source file.
  auto absTemp = m_currentSourceRoot;
  //  absTemp /= tempFile;
  ApiViewCompilationDatabase compileDb(
      /* m_filesToCompile*/ {std::filesystem::absolute(tempFile)},
      m_currentSourceRoot,
      {},
      additionalCompilerArguments);

  std::vector<std::string> sourceFiles;
  //  for (auto const& file : m_filesToCompile)
  //  {
  //    sourceFiles.push_back(std::string(stringFromU8string(file.u8string())));
  //  }
  sourceFiles.push_back(
      std::string(stringFromU8string(std::filesystem::absolute(tempFile).u8string())));

  ClangTool tool(compileDb, sourceFiles);

  auto frontEndActionFactory{std::make_unique<AstVisitorActionFactory>(this)};
  auto rv = tool.run(frontEndActionFactory.get());

  // Insert a terminal node into the classes database, which ensures that all opened namespaces are
  // closed.
  m_classDatabase->CreateAstNode();
  return rv;
}

void AzureClassesDatabase::DumpClassDatabase(AstDumper* dumper) const
{
  for (auto const& classNode : m_typeList)
  {
    classNode->DumpNode(dumper, {});
  }
  m_typeHierarchy.Dump(dumper);
}

void TypeHierarchy::Dump(AstDumper* dumper) const
{
  for (auto const& [namespaceName, namespaceRoot] : m_namespaceRoots)
  {
    dumper->DumpTypeHierarchyNode(namespaceRoot);
  }
}

std::shared_ptr<TypeHierarchy::TypeHierarchyNode> TypeHierarchy::GetNamespaceRoot(
    std::string_view const& namespaceName)
{
  std::shared_ptr<TypeHierarchyNode> rv;
  auto result = m_namespaceRoots.find(static_cast<std::string>(namespaceName));
  if (result == m_namespaceRoots.end())
  {
    rv = std::make_shared<TypeHierarchyNode>(
        static_cast<std::string>(namespaceName), "", TypeHierarchyClass::Assembly);
    m_namespaceRoots.emplace(std::make_pair(namespaceName, rv));
    return rv;
  }
  else
  {
    return result->second;
  }
}

std::shared_ptr<TypeHierarchy::TypeHierarchyNode> TypeHierarchy::TypeHierarchyNode::InsertChildNode(
    std::string_view const& name,
    std::string_view const& navigationId,
    TypeHierarchy::TypeHierarchyClass nodeClass)
{
  std::shared_ptr<TypeHierarchyNode> rv = std::make_shared<TypeHierarchyNode>(
      static_cast<std::string>(name), static_cast<std::string>(navigationId), nodeClass);
  Children.push_back(rv);
  return rv;
}
