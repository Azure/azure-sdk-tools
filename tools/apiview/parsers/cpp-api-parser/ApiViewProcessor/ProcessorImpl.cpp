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

class CurrentDirectorySetter {
  std::filesystem::path m_oldPath;

public:
  explicit CurrentDirectorySetter(std::filesystem::path const& newPath)
      : m_oldPath(std::filesystem::current_path())
  {
    std::filesystem::current_path(newPath);
  }
  ~CurrentDirectorySetter() { std::filesystem::current_path(m_oldPath); }
};

ApiViewProcessorImpl::ApiViewProcessorImpl(
    std::string_view directoryToProcess,
    nlohmann::json const& configurationJson)
    : m_currentSourceRoot{std::filesystem::absolute(directoryToProcess)},
      m_classDatabase{std::make_unique<AzureClassesDatabase>(this)}
{
  // CHDIR to the directory to process so relative paths in the configuration are properly resolved.
  CurrentDirectorySetter currentDirectory{directoryToProcess};
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
    if (configurationJson["filterNamespace"].is_string())
    {
      m_filterNamespaces.push_back(configurationJson["filterNamespace"].get<std::string>());
    }
    else if (configurationJson["filterNamespace"].is_array())
    {
      m_filterNamespaces = configurationJson["filterNamespace"];
    }
    else
    {
      throw std::runtime_error(
          "Configuration element `filterNamespace` is neither a string or an array of strings.");
    }
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
    //    m_additionalIncludeDirectories = configurationJson["additionalIncludeDirectories"];
    for (const auto& dir : configurationJson["additionalIncludeDirectories"])
    {
      auto includeDirectory{m_currentSourceRoot};
      includeDirectory /= std::filesystem::canonical(dir);
      m_additionalIncludeDirectories.push_back(std::filesystem::absolute(includeDirectory));
    }
  }
  if (configurationJson.contains("reviewName") && !configurationJson["reviewName"].is_null())
  {
    m_reviewName = configurationJson["reviewName"].get<std::string>();
  }
  if (configurationJson.contains("serviceName") && !configurationJson["serviceName"].is_null())
  {
    m_serviceName = configurationJson["serviceName"].get<std::string>();
  }
  if (configurationJson.contains("packageName") && !configurationJson["packageName"].is_null())
  {
    m_packageName = configurationJson["packageName"].get<std::string>();
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
    // The caller didn't specify any files to process. We'll process all files in the directory.
    // Note that if the caller didn't specify files to process, they MAY have specified files to
    // *skip*, so respect that.
    if (configurationJson.contains("sourceFilesToSkip")
        && configurationJson["sourceFilesToSkip"].is_array()
        && configurationJson["sourceFilesToSkip"].size() != 0)
    {
      for (const auto& file : configurationJson["sourceFilesToSkip"])
      {
        auto fileToSkip{m_currentSourceRoot};
        fileToSkip /= file;
        m_filesToIgnore.push_back(std::filesystem::absolute(fileToSkip));
      }
    }
    llvm::outs() << llvm::raw_ostream::Colors::CYAN << "No source files specified"
                 << llvm::raw_ostream::Colors::RESET << " collecting all files under "
                 << stringFromU8string(m_currentSourceRoot.u8string()) << "\n";
    auto subdirectories = GatherSubdirectories(m_currentSourceRoot);
    for (auto& subdirectory : subdirectories)
    {
      for (auto& entry : std::filesystem::directory_iterator(subdirectory))
      {
        if (entry.is_regular_file())
        {
          auto extension = entry.path().extension();
          auto filename = entry.path().filename();
          if (extension == ".hpp" || extension == ".h")
          {
            auto absoluteEntry = std::filesystem::absolute(entry.path());
            if (std::find(m_filesToIgnore.begin(), m_filesToIgnore.end(), absoluteEntry)
                == m_filesToIgnore.end())
            {
              m_filesToCompile.push_back(absoluteEntry);
            }
            else
            {
              llvm::outs() << llvm::raw_ostream::Colors::GREEN << "Skipping file "
                           << stringFromU8string(absoluteEntry.u8string())
                           << llvm::raw_ostream::Colors::RESET << "\n";
            }
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
  // We don't even want to consider any types which are a member of a class.
  if (AzureClassesDatabase::IsMemberOfObject(namedDecl))
  {
    return false;
  }
  bool shouldCollect = false;
  // By default, we consider any types within desired set of input files.
  auto fileId = namedDecl->getASTContext().getSourceManager().getFileID(namedDecl->getLocation());
  auto fileEntry = namedDecl->getASTContext().getSourceManager().getFileEntryForID(fileId);
  if (fileEntry)
  {
    if (fileEntry->getName().startswith_insensitive(
            stringFromU8string(m_processorImpl->CurrentSourceRoot().u8string())))
    {
      // If the file containing the type is within the source root, we want to consider the type.
      shouldCollect = true;
    }
  }
  if (shouldCollect)
  {
    // If we don't have a filter namespace, include all the types.
    if (m_processorImpl->FilterNamespaces().empty())
    {
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
      if ((typeName.find("::_detail") != std::string::npos) && !m_processorImpl->IncludeDetail())
      {
        shouldCollect = false;
      }
    }
    else
    {
      // We have a namespace filter set. Look at all types whose names start with the filter
      // namespace name. We don't want to consider namespaces, since they're not descrete entries in
      // our resulting output.
      const std::string typeName{namedDecl->getQualifiedNameAsString()};
      if (namedDecl->getKind() != Decl::Namespace)
      {
        if (std::find_if(
                std::begin(m_processorImpl->FilterNamespaces()),
                std::end(m_processorImpl->FilterNamespaces()),
                [&typeName](std::string const& ns) { return typeName.find(ns) == 0; })
            == std::end(m_processorImpl->FilterNamespaces()))
        {
          shouldCollect = true;
          bool generateError = true;
          // Don't flag "using" declarations or forward declarations.
          if ((namedDecl->getKind() == Decl::Using) || (namedDecl->getKind() == Decl::TypeAlias)
              || (namedDecl->getKind() == Decl::CXXRecord
                  && cast<CXXRecordDecl>(namedDecl)->getDefinition() != namedDecl))
          {
            generateError = false;
          }
          if (generateError)
          {
            m_processorImpl->GetClassesDatabase()->CreateApiViewMessage(
                ApiViewMessages::TypeDeclaredInNamespaceOutsideFilter, typeName);
          }
        }
      }
      if (shouldCollect)
      {
        // However if the type is in the _internal namespace, then we want to exclude it if we're
        // excluding internal types.
        if (typeName.find("::_internal") != std::string::npos)
        {
          if (!m_processorImpl->IncludeInternal())
          {
            shouldCollect = false;
          }
          // If this _internal type is not in a namespace starting with "Azure::Core", then
          // we want to flag it as an error and include the type.
          if (typeName.find("Azure::Core") != 0)
          {
            m_processorImpl->GetClassesDatabase()->CreateApiViewMessage(
                ApiViewMessages::InternalTypesInNonCorePackage, typeName);
            shouldCollect = true;
          }
        }
        // If the type is in the _detail namespace, then we want to exclude it if we're
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
      else
      {
        // We want to include any free types within the set of headers because they will result in
        // a diagnostic.
        PrintingPolicy pp{LangOptions{}};
        pp.adjustForCPlusPlus();
        std::string namespaceName;
        llvm::raw_string_ostream osNamespace(namespaceName);
        namedDecl->printQualifiedName(osNamespace, pp);
        std::string typeName;
        llvm::raw_string_ostream osType(typeName);
        namedDecl->printName(osType);
        if (typeName == namespaceName)
        {
          shouldCollect = true;
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

  // Note that this is *NOT* a real command line - instead it's the set of command line switches
  // handed to the clang tooling. And specifically the 1st entry tells clang that it should treat
  // the command line arguments as if they were arguments to clang (if it was "cl.exe", it would
  // treat the command line arguments as if they were from MSVC.
  std::vector<std::string>
      defaultCommandLine{"clang++.exe", "-DAZ_RTTI", "-fcxx-exceptions", "-c", "-std=c++14"};

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
          std::string includePath{static_cast<std::string>(
              stringFromU8string(std::filesystem::absolute(arg).u8string()))};
          commandLine.push_back("-I" + includePath);
          llvm::outs() << "Adding include directory: " << includePath << "\n";
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

int ApiViewProcessorImpl::ProcessApiView()
{
  // clang::tooling::ClangTool.run changes the current directory to the directory returned by the
  // compilation database. Use the CurrentDirectorySetter to preserve and restore the current
  // directory across calls into the clang tooling.
  CurrentDirectorySetter currentDirectory{std::filesystem::current_path()};
  // clang really likes all input paths to be absolute paths, so use the fiilesystem to
  // canonicalize the input filename and source location.
  std::filesystem::path tempFile = std::filesystem::temp_directory_path();
  tempFile /= "TempSourceFile.cpp";

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

  // Create a compilation database consisting of the source root and source file.
  auto absTemp = m_currentSourceRoot;

  ApiViewCompilationDatabase compileDb(
      {std::filesystem::absolute(tempFile)},
      m_currentSourceRoot,
      m_additionalIncludeDirectories,
      m_additionalCompilerArguments);

  std::vector<std::string> sourceFiles;
  sourceFiles.push_back(
      std::string(stringFromU8string(std::filesystem::absolute(tempFile).u8string())));

  ClangTool tool(compileDb, sourceFiles);

  auto frontEndActionFactory{std::make_unique<AstVisitorActionFactory>(this)};
  auto rv = tool.run(frontEndActionFactory.get());

  // Insert a terminal node into the classes database, which ensures that all opened namespaces
  // are closed.
  m_classDatabase->CreateAstNode();
  // Restore the current directory after processing (tool.run will change the current
  // directory).
  return rv;
}

void AzureClassesDatabase::DumpClassDatabase(AstDumper* dumper) const
{
  for (auto const& classNode : m_typeList)
  {
    classNode->DumpNode(dumper, {});
  }
  m_typeHierarchy.Dump(dumper);
  for (auto const& diagnostic : m_diagnostics)
  {
    dumper->DumpMessageNode(diagnostic);
  }
}

void TypeHierarchy::Dump(AstDumper* dumper) const
{
  for (auto const& [namespaceName, namespaceRoot] : m_namespaceRoots)
  {
    // Skip empty namespace nodes.
    if (!namespaceRoot->Children.empty())
    {
      dumper->DumpTypeHierarchyNode(namespaceRoot);
    }
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
        static_cast<std::string>(namespaceName), "", TypeHierarchyClass::Namespace);
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
  Children.emplace(static_cast<std::string>(name), rv);
  return rv;
}
