// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#include "ApiViewProcessor.hpp"
#include "AstDumper.hpp"
#include "AstNode.hpp"
#include "JsonDumper.hpp"
#include "TextDumper.hpp"
#include "gtest/gtest.h"
#include <clang/AST/ASTConsumer.h>
#include <clang/AST/Comment.h>
#include <clang/AST/CommentVisitor.h>
#include <clang/AST/RecursiveASTVisitor.h>
#include <clang/Frontend/CompilerInstance.h>
#include <clang/Frontend/FrontendAction.h>
#include <clang/Frontend/FrontendActions.h>
#include <clang/Tooling/CommonOptionsParser.h>
#include <clang/Tooling/CompilationDatabase.h>
#include <clang/Tooling/Tooling.h>
#include <filesystem>
#include <fstream>
#include <llvm/Support/CommandLine.h>
#include <nlohmann/json.hpp>
#include <ostream>
#include <string_view>

using namespace nlohmann::literals;
using namespace clang;
using namespace clang::tooling;

inline std::string_view stringFromU8string(std::u8string const& str)
{
  return std::string_view(reinterpret_cast<const char*>(str.data()), str.size());
}

class TestParser : public testing::Test {
protected:
  void SetUp() override {}

  void TearDown() override {}

private:
  void OutputClassDbToConsole(std::unique_ptr<AzureClassesDatabase> const& classDb)
  {
    TextDumper dumpToConsole(std::cout);
    classDb->DumpClassDatabase(&dumpToConsole);
  }

  void OutputClassDbToFile(
      std::unique_ptr<AzureClassesDatabase> const& classDb,
      std::string_view const& fileToDump,
      bool isAzureTest = false,
      bool isAzureCore = false)
  {
    std::ofstream outFile(static_cast<std::string>(fileToDump), std::ios::out);
    outFile << R"(#include <memory>)" << std::endl;
    outFile << R"(#include <string>)" << std::endl;
    outFile << R"(#include <string_view>)" << std::endl;
    outFile << R"(#include <chrono>)" << std::endl;
    outFile << R"(#include <map>)" << std::endl;
    outFile << R"(#include <set>)" << std::endl;
    outFile << R"(#include <functional>)" << std::endl;
    outFile << R"(#include <vector>)" << std::endl;
    outFile << R"(#include <exception>)" << std::endl;
    outFile << R"(#include <stdexcept>)" << std::endl;
    if (isAzureTest)
    {
      if (!isAzureCore)
      {
        outFile << R"(#include <azure/core/nullable.hpp>)" << std::endl;
        outFile << R"(#include <azure/core/datetime.hpp>)" << std::endl;
        outFile << R"(#include <azure/core/response.hpp>)" << std::endl;
        outFile << R"(#include <azure/core/context.hpp>)" << std::endl;
        outFile << R"(#include <azure/core/credentials/credentials.hpp>)" << std::endl;
        outFile << R"(#include <azure/core/internal/extendable_enumeration.hpp>)" << std::endl;
        outFile << R"(#include <azure/core/internal/client_options.hpp>)" << std::endl;
      }
      else
      {
        outFile << R"(typedef void *HINTERNET;)" << std::endl;
      }
    }

    outFile << std::endl;

    TextDumper dumpToFile(outFile);
    classDb->DumpClassDatabase(&dumpToFile);
  }
  class TestCompilationDatabase : public CompilationDatabase {
    std::vector<std::filesystem::path> m_filesToCompile;
    std::filesystem::path m_sourceLocation;
    std::vector<std::filesystem::path> m_additionalIncludePaths;
    std::vector<std::string> m_additionalArguments;

  public:
    TestCompilationDatabase(
        std::vector<std::filesystem::path> const& filesToCompile,
        std::filesystem::path const& sourceLocation,
        std::vector<std::filesystem::path> const& additionalIncludePaths,
        std::vector<std::string> const& additionalArguments)
        : CompilationDatabase(), m_filesToCompile(filesToCompile),
          m_sourceLocation(std::filesystem::absolute(sourceLocation)), m_additionalIncludePaths{
                                                                           additionalIncludePaths}
    {
      for (auto const& arg : additionalArguments)
      {
        m_additionalArguments.push_back(std::string(arg));
      }
    }
    std::vector<std::string>
        defaultCommandLine{"clang++.exe", "-DAZ_RTTI", "-fcxx-exceptions", "-c", "-std=c++14", "-D_ALLOW_COMPILER_AND_STL_VERSION_MISMATCH"};
    // Inherited via CompilationDatabase
    virtual std::vector<CompileCommand> getCompileCommands(llvm::StringRef FilePath) const override
    {
      for (auto const& file : m_filesToCompile)
      {
        if (file.compare(FilePath.str()) == 0)
        {
          std::vector<std::string> commandLine{defaultCommandLine};
          // Add the source location to the include paths.
          commandLine.push_back(
              "-I" + static_cast<std::string>(stringFromU8string(m_sourceLocation.u8string())));
          llvm::outs() << "Adding include directory: "
                       << static_cast<std::string>(stringFromU8string(m_sourceLocation.u8string()))
                       << "\n";
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

          std::vector<clang::tooling::CompileCommand> rv;
          std::string outputFile;
          rv.push_back(CompileCommand(
              static_cast<std::string>(stringFromU8string(m_sourceLocation.u8string())),
              static_cast<std::string>(stringFromU8string(file.u8string())),
              commandLine,
              ""));
          return rv;
        }
      }
      return std::vector<clang::tooling::CompileCommand>();
    }
  };

protected:
  // Note that the baseFIleName should be unique within a given test case because clang's
  // OptionCategory is global.
  bool SyntaxCheckClassDb(
      std::unique_ptr<AzureClassesDatabase> const& classDb,
      std::string const& baseFileName,
      bool isAzureTest = false,
      bool isAzureCore = false)
  {
    std::filesystem::path tempFileName{std::filesystem::temp_directory_path()};
    tempFileName.append(baseFileName);
    if (std::filesystem::exists(tempFileName))
    {
      std::filesystem::remove(tempFileName);
    }
    OutputClassDbToFile(
        classDb, stringFromU8string(tempFileName.u8string()), isAzureTest, isAzureCore);
    //    auto currentTestName{::testing::UnitTest::GetInstance()->current_test_info()->name()};

    std::vector<std::filesystem::path> additionalIncludeDirectories;
    if (isAzureTest && !isAzureCore)
    {
      additionalIncludeDirectories.push_back(
          std::filesystem::absolute(R"(.\Tests\core\azure-core\inc)"));
    }

    TestCompilationDatabase compileDb(
        {std::filesystem::absolute(tempFileName)}, ".", additionalIncludeDirectories, {});

    std::vector<std::string> sourceFiles;
    sourceFiles.push_back(
        std::string(stringFromU8string(std::filesystem::absolute(tempFileName).u8string())));

    clang::tooling::ClangTool tool(compileDb, sourceFiles);

    int result
        = tool.run(clang::tooling::newFrontendActionFactory<clang::SyntaxOnlyAction>().get());
    return result == 0;
  }
};

TEST_F(TestParser, Create)
{
  ApiViewProcessor processor(".", R"({})"_json);

  auto& db = processor.GetClassesDatabase();

  JsonDumper jsonDumper("My First Review", "Azure Core", "Azure.Core");
  processor.GetClassesDatabase()->DumpClassDatabase(&jsonDumper);
  EXPECT_EQ(0ul, db->GetAstNodeMap().size());
}

TEST_F(TestParser, CompileSimple)
{
  {
    ApiViewProcessor processor("tests", std::string_view("SimpleTest.json"));

    EXPECT_EQ(processor.ProcessApiView(), 0);
    auto& db = processor.GetClassesDatabase();
    EXPECT_EQ(8ul, db->GetAstNodeMap().size());

    EXPECT_TRUE(SyntaxCheckClassDb(db, "SimpleTestGenerated.cpp"));
  }
}

TEST_F(TestParser, CompileWithErrors)
{
  {
    ApiViewProcessor processor("tests", R"({
  "sourceFilesToProcess": [
    "CompileError1.cpp"
  ],
  "additionalIncludeDirectories": [
  ],
  "additionalCompilerSwitches": [],
  "allowInternal": false,
  "includeDetail": false,
  "includePrivate": false,
  "filterNamespace": null
  }
  )"_json);

    EXPECT_NE(processor.ProcessApiView(), 0);
  }
}

struct NsDumper : AstDumper
{
  // Inherited via AstDumper
  virtual void InsertNewline() override {}
  virtual void InsertWhitespace(int count) override {}
  virtual void InsertKeyword(std::string_view const& keyword) override {}
  virtual void InsertText(std::string_view const& text) override {}
  virtual void InsertPunctuation(char punctuation) override {}
  virtual void InsertLineIdMarker() override {}
  virtual void InsertTypeName(
      std::string_view const& type,
      std::string_view const& typeNavigationId) override
  {
  }
  virtual void InsertMemberName(std::string_view const& member, std::string_view const&) override {}
  virtual void InsertIdentifier(std::string_view const& identifier) override {}
  virtual void InsertStringLiteral(std::string_view const& str) override {}
  virtual void InsertLiteral(std::string_view const& str) override {}
  virtual void InsertComment(std::string_view const& comment) override {}
  virtual void AddDocumentRangeStart() override {}
  virtual void AddDocumentRangeEnd() override {}
  virtual void AddDeprecatedRangeStart() override {}
  virtual void AddDeprecatedRangeEnd() override {}
  virtual void AddDiffRangeStart() override {}
  virtual void AddDiffRangeEnd() override {}
  virtual void AddExternalLinkStart(std::string_view const& url) override {}
  virtual void AddExternalLinkEnd() override {}
  virtual void DumpTypeHierarchyNode(
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> const& node) override
  {
  }
  struct Message
  {
    std::string_view DiagnosticId;
    std::string_view FailingId;
  };
  std::vector<Message> Messages;
  virtual void DumpMessageNode(ApiViewMessage const& msg) override
  {
    Messages.push_back({msg.DiagnosticId, msg.TargetId});
  }
};

TEST_F(TestParser, NamespaceFilter1)
{
  ApiViewProcessor processor("tests", R"({
  "sourceFilesToProcess": [
    "SimpleTest.cpp"
  ],
  "additionalIncludeDirectories": [],
  "additionalCompilerSwitches": ["-Qunused-arguments"],
  "allowInternal": false,
  "includeDetail": false,
  "includePrivate": false,
  "filterNamespace": "Test"
}
)"_json);

  NsDumper dumper;
  EXPECT_EQ(processor.ProcessApiView(), 0);
  auto& db = processor.GetClassesDatabase();
  EXPECT_EQ(8ul, db->GetAstNodeMap().size());
  db->DumpClassDatabase(&dumper);
  EXPECT_EQ(5ul, dumper.Messages.size());
  EXPECT_EQ("CPA0003", dumper.Messages[0].DiagnosticId);
  EXPECT_EQ("GlobalFunction4", dumper.Messages[0].FailingId);
  EXPECT_EQ("CPA0002", dumper.Messages[1].DiagnosticId);
  EXPECT_EQ("char *GlobalFunction4(int character)", dumper.Messages[1].FailingId);
  EXPECT_EQ("CPA0003", dumper.Messages[2].DiagnosticId);
  EXPECT_EQ("A::AB::ABC::FunctionABC", dumper.Messages[2].FailingId);
  EXPECT_EQ("CPA0003", dumper.Messages[3].DiagnosticId);
  EXPECT_EQ("A::AB::FunctionAB", dumper.Messages[3].FailingId);
  EXPECT_EQ("CPA0003", dumper.Messages[4].DiagnosticId);
  EXPECT_EQ("A::AB::ABD::ABE::FunctionABE", dumper.Messages[4].FailingId);

  EXPECT_TRUE(SyntaxCheckClassDb(db, "SimpleTestGenerated1.cpp"));
}
TEST_F(TestParser, NamespaceFilter2)
{
  ApiViewProcessor processor("tests", R"({
  "sourceFilesToProcess": [
    "SimpleTest.cpp"
  ],
  "additionalIncludeDirectories": [],
  "additionalCompilerSwitches": ["-Qunused-arguments"],
  "allowInternal": false,
  "includeDetail": false,
  "includePrivate": false,
  "filterNamespace": "Test::Inner"
}
)"_json);

  EXPECT_EQ(processor.ProcessApiView(), 0);
  auto& db = processor.GetClassesDatabase();
  EXPECT_EQ(8ul, db->GetAstNodeMap().size());

  NsDumper dumper;
  db->DumpClassDatabase(&dumper);
  EXPECT_EQ(7ul, dumper.Messages.size());

  EXPECT_EQ("CPA0003", dumper.Messages[0].DiagnosticId);
  EXPECT_EQ("Test::Function1", dumper.Messages[0].FailingId);
  EXPECT_EQ("CPA0003", dumper.Messages[1].DiagnosticId);
  EXPECT_EQ("Test::Function2", dumper.Messages[1].FailingId);
  EXPECT_EQ("CPA0003", dumper.Messages[2].DiagnosticId);
  EXPECT_EQ("GlobalFunction4", dumper.Messages[2].FailingId);
  EXPECT_EQ("CPA0002", dumper.Messages[3].DiagnosticId);
  EXPECT_EQ("char *GlobalFunction4(int character)", dumper.Messages[3].FailingId);
  EXPECT_EQ("CPA0003", dumper.Messages[4].DiagnosticId);
  EXPECT_EQ("A::AB::ABC::FunctionABC", dumper.Messages[4].FailingId);
  EXPECT_EQ("CPA0003", dumper.Messages[5].DiagnosticId);
  EXPECT_EQ("A::AB::FunctionAB", dumper.Messages[5].FailingId);
  EXPECT_EQ("CPA0003", dumper.Messages[6].DiagnosticId);
  EXPECT_EQ("A::AB::ABD::ABE::FunctionABE", dumper.Messages[6].FailingId);

  EXPECT_TRUE(SyntaxCheckClassDb(db, "SimpleTestGenerated2.cpp"));
}
TEST_F(TestParser, NamespaceFilter3)
{
  ApiViewProcessor processor("tests", R"({
  "sourceFilesToProcess": [
    "SimpleTest.cpp"
  ],
  "additionalIncludeDirectories": [],
  "additionalCompilerSwitches": ["-Qunused-arguments"],
  "allowInternal": false,
  "includeDetail": false,
  "includePrivate": false,
  "filterNamespace": "Axxx"
}
)"_json);
  EXPECT_EQ(processor.ProcessApiView(), 0);

  auto& db = processor.GetClassesDatabase();
  EXPECT_EQ(8ul, db->GetAstNodeMap().size());

  NsDumper dumper;
  db->DumpClassDatabase(&dumper);
  EXPECT_EQ(8ul, dumper.Messages.size());

  EXPECT_EQ("CPA0003", dumper.Messages[0].DiagnosticId);
  EXPECT_EQ("Test::Function1", dumper.Messages[0].FailingId);
  EXPECT_EQ("CPA0003", dumper.Messages[1].DiagnosticId);
  EXPECT_EQ("Test::Function2", dumper.Messages[1].FailingId);
  EXPECT_EQ("CPA0003", dumper.Messages[2].DiagnosticId);
  EXPECT_EQ("Test::Inner::Function3", dumper.Messages[2].FailingId);
  EXPECT_EQ("CPA0003", dumper.Messages[3].DiagnosticId);
  EXPECT_EQ("GlobalFunction4", dumper.Messages[3].FailingId);
  EXPECT_EQ("CPA0002", dumper.Messages[4].DiagnosticId);
  EXPECT_EQ("char *GlobalFunction4(int character)", dumper.Messages[4].FailingId);
  EXPECT_EQ("CPA0003", dumper.Messages[5].DiagnosticId);
  EXPECT_EQ("A::AB::ABC::FunctionABC", dumper.Messages[5].FailingId);
  EXPECT_EQ("CPA0003", dumper.Messages[6].DiagnosticId);
  EXPECT_EQ("A::AB::FunctionAB", dumper.Messages[6].FailingId);
  EXPECT_EQ("CPA0003", dumper.Messages[7].DiagnosticId);
  EXPECT_EQ("A::AB::ABD::ABE::FunctionABE", dumper.Messages[7].FailingId);

  EXPECT_TRUE(SyntaxCheckClassDb(db, "SimpleTestGenerated3.cpp"));
}

TEST_F(TestParser, NamespaceFilter4)
{
  ApiViewProcessor processor("tests", R"({
  "sourceFilesToProcess": [
    "SimpleTest.cpp"
  ],
  "additionalIncludeDirectories": [],
  "additionalCompilerSwitches": null,
  "allowInternal": false,
  "includeDetail": false,
  "includePrivate": false,
  "filterNamespace": ["Test::Inner", "A::AB"]
}
)"_json);
  EXPECT_EQ(processor.ProcessApiView(), 0);

  auto& db = processor.GetClassesDatabase();
  EXPECT_EQ(8ul, db->GetAstNodeMap().size());

  NsDumper dumper;
  db->DumpClassDatabase(&dumper);
  EXPECT_EQ(4ul, dumper.Messages.size());

  EXPECT_EQ("CPA0003", dumper.Messages[0].DiagnosticId);
  EXPECT_EQ("Test::Function1", dumper.Messages[0].FailingId);
  EXPECT_EQ("CPA0003", dumper.Messages[1].DiagnosticId);
  EXPECT_EQ("Test::Function2", dumper.Messages[1].FailingId);
  EXPECT_EQ("CPA0003", dumper.Messages[2].DiagnosticId);
  EXPECT_EQ("GlobalFunction4", dumper.Messages[2].FailingId);
  EXPECT_EQ("CPA0002", dumper.Messages[3].DiagnosticId);
  EXPECT_EQ("char *GlobalFunction4(int character)", dumper.Messages[3].FailingId);

  EXPECT_TRUE(SyntaxCheckClassDb(db, "SimpleTestGenerated4.cpp"));
}

TEST_F(TestParser, Class1)
{
  ApiViewProcessor processor("tests", R"({
  "sourceFilesToProcess": [
    "ClassesWithInternalAndDetail.cpp"
  ],
  "additionalIncludeDirectories": [],
  "additionalCompilerSwitches": ["-Qunused-arguments"],
  "allowInternal": false,
  "includeDetail": false,
  "includePrivate": false,
  "filterNamespace": null
}
)"_json);
  processor.ProcessApiView();

  auto& db = processor.GetClassesDatabase();
  EXPECT_EQ(16ul, db->GetAstNodeMap().size());

  NsDumper dumper;
  db->DumpClassDatabase(&dumper);
  EXPECT_EQ(55ul, dumper.Messages.size());

  size_t internalTypes = 0;
  for (const auto& msg : dumper.Messages)
  {
    if (msg.DiagnosticId == "CPA0007")
    {
      internalTypes += 1;
    }
  }
  EXPECT_EQ(internalTypes, 8ul);

  EXPECT_TRUE(SyntaxCheckClassDb(db, "Classes1.cpp"));
}
TEST_F(TestParser, Class2)
{
  ApiViewProcessor processor("tests", R"({
  "sourceFilesToProcess": [
    "ClassesWithInternalAndDetail.cpp"
  ],
  "additionalIncludeDirectories": [],
  "additionalCompilerSwitches": ["-Qunused-arguments"],
  "allowInternal": true,
  "includeDetail": false,
  "includePrivate": false,
  "filterNamespace": null
}
)"_json);
  EXPECT_EQ(processor.ProcessApiView(), 0);

  auto& db = processor.GetClassesDatabase();
  EXPECT_EQ(16ul, db->GetAstNodeMap().size());
  EXPECT_TRUE(SyntaxCheckClassDb(db, "Classes1B.cpp"));
}

TEST_F(TestParser, Expressions)
{
  ApiViewProcessor processor("tests", R"({
  "sourceFilesToProcess": [
    "ExpressionTests.cpp"
  ],
  "additionalIncludeDirectories": [],
  "additionalCompilerSwitches": ["-Qunused-arguments"],
  "allowInternal": true,
  "includeDetail": false,
  "includePrivate": false,
  "filterNamespace": null
}
)"_json);

  EXPECT_EQ(processor.ProcessApiView(), 0);

  auto& db = processor.GetClassesDatabase();
  EXPECT_TRUE(SyntaxCheckClassDb(db, "Expression1.cpp"));
}

TEST_F(TestParser, Templates)
{
  ApiViewProcessor processor("tests", R"({
  "sourceFilesToProcess": [
    "TemplateTests.cpp"
  ],
  "additionalIncludeDirectories": [],
  "additionalCompilerSwitches": null,
  "allowInternal": true,
  "includeDetail": false,
  "includePrivate": false,
  "filterNamespace": null
}
)"_json);

  EXPECT_EQ(processor.ProcessApiView(), 0);

  auto& db = processor.GetClassesDatabase();
  // Until we get parsing types working correctly, we can't do the syntax check tests.
  //  EXPECT_TRUE(SyntaxCheckClassDb(db, "Template1.cpp"));
}

TEST_F(TestParser, UsingNamespace)
{
  ApiViewProcessor processor("tests", R"({
  "sourceFilesToProcess": [
    "UsingNamespace.cpp"
  ],
  "additionalIncludeDirectories": [],
  "additionalCompilerSwitches": null,
  "allowInternal": false,
  "includeDetail": false,
  "includePrivate": false,
  "filterNamespace": null
}
)"_json);

  EXPECT_EQ(processor.ProcessApiView(), 0);

  auto& db = processor.GetClassesDatabase();
  EXPECT_TRUE(SyntaxCheckClassDb(db, "UsingNamespace1.cpp"));

  NsDumper dumper;
  db->DumpClassDatabase(&dumper);
  EXPECT_EQ(1ul, dumper.Messages.size());

  size_t usingNamespaces = 0;
  for (const auto& msg : dumper.Messages)
  {
    if (msg.DiagnosticId == "CPA000A")
    {
      usingNamespaces += 1;
    }
  }
  EXPECT_EQ(usingNamespaces, 1ul);
}

TEST_F(TestParser, TestDtors)
{
  ApiViewProcessor processor("tests", R"({
  "sourceFilesToProcess": [
    "DestructorTests.cpp"
  ],
  "additionalIncludeDirectories": [],
  "additionalCompilerSwitches": null,
  "allowInternal": false,
  "includeDetail": false,
  "includePrivate": false,
  "filterNamespace": null
}
)"_json);

  EXPECT_EQ(processor.ProcessApiView(), 0);

  auto& db = processor.GetClassesDatabase();
  EXPECT_TRUE(SyntaxCheckClassDb(db, "DestructorTests1.cpp"));

  NsDumper dumper;
  db->DumpClassDatabase(&dumper);
  EXPECT_EQ(2ul, dumper.Messages.size());

  size_t nonVirtualDestructor= 0;
  for (const auto& msg : dumper.Messages)
  {
    if (msg.DiagnosticId == "CPA000B")
    {
      nonVirtualDestructor+= 1;
    }
  }
  EXPECT_EQ(nonVirtualDestructor, 2ul);
}

TEST_F(TestParser, TestDocuments)
{
  ApiViewProcessor processor("tests", R"({
  "sourceFilesToProcess": [
    "DocumentationTests.cpp"
  ],
  "additionalIncludeDirectories": [],
  "additionalCompilerSwitches": null,
  "allowInternal": false,
  "includeDetail": false,
  "includePrivate": false,
  "filterNamespace": null
}
)"_json);


  EXPECT_EQ(processor.ProcessApiView(), 0);

  auto& db = processor.GetClassesDatabase();
  EXPECT_TRUE(SyntaxCheckClassDb(db, "DocumentationTests1.cpp"));

  NsDumper dumper;
  db->DumpClassDatabase(&dumper);
}



#if 0
TEST_F(TestParser, AzureCore1)
{
  ApiViewProcessor processor("Tests", R"({
  "sourceFilesToProcess": [
    "core/azure-core/inc/azure/core.hpp"
  ],
  "additionalIncludeDirectories": ["core/azure-core/inc"],
  "additionalCompilerSwitches": ["-Qunused-arguments"],
  "allowInternal": true,
  "includeDetail": false,
  "includePrivate": false,
  "filterNamespace": "Azure::"
}
)"_json);

    EXPECT_EQ(processor.ProcessApiView(), 0);

  auto& db = processor.GetClassesDatabase();
  EXPECT_LT(1ul, db->GetAstNodeMap().size());
  EXPECT_TRUE(SyntaxCheckClassDb(db, "Core1.cpp", true, true));
}

TEST_F(TestParser, AzureCore2)
{
  ApiViewProcessor processor(R"(tests\\core\azure-core)");

    EXPECT_EQ(processor.ProcessApiView(), 0);

  auto& db = processor.GetClassesDatabase();
  EXPECT_LT(1ul, db->GetAstNodeMap().size());
  EXPECT_TRUE(SyntaxCheckClassDb(db, "Core2.cpp", true, true));
}

TEST_F(TestParser, AzureAttestation)
{
  ApiViewProcessor processor(R"(tests\attestation\azure-security-attestation)", R"({
  "sourceFilesToProcess": null,
  "additionalIncludeDirectories": ["../../core/azure-core/inc", "inc"],
  "additionalCompilerSwitches": [],
  "allowInternal": false,
  "includeDetail": false,
  "includePrivate": false,
  "filterNamespace": "Azure::Security::Attestation"
}
)"_json);
    EXPECT_EQ(processor.ProcessApiView(), 0);
  auto& db = processor.GetClassesDatabase();
  EXPECT_TRUE(SyntaxCheckClassDb(db, "Attestation1.cpp", true, false));
}
#endif
