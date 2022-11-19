// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#include <clang/Frontend/FrontendActions.h>
#include <clang/Tooling/CommonOptionsParser.h>
#include <clang/Tooling/Tooling.h>
#include <llvm/Support/CommandLine.h>
#include <nlohmann/json.hpp>

#include "ApiViewProcessor.hpp"
#include "AstDumper.hpp"
#include "AstNode.hpp"
#include "JsonDumper.hpp"
#include "TextDumper.hpp"
#include "gtest/gtest.h"
// #include <codecvt>
#include <filesystem>
#include <fstream>
// #include <locale>
#include <ostream>

using namespace nlohmann::literals;

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
      bool isAzureCore)
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

    outFile << std::endl;

    TextDumper dumpToFile(outFile);
    classDb->DumpClassDatabase(&dumpToFile);
  }

protected:
  // Note that the baseFIleName should be unique within a given test case because clang's
  // OptionCategory is global.
  bool SyntaxCheckClassDb(
      std::unique_ptr<AzureClassesDatabase> const& classDb,
      std::string const& baseFileName,
      bool isAzureCore)
  {
    std::filesystem::path tempFileName{std::filesystem::temp_directory_path()};
    tempFileName.append(baseFileName);
    if (std::filesystem::exists(tempFileName))
    {
      std::filesystem::remove(tempFileName);
    }
    OutputClassDbToFile(classDb, stringFromU8string(tempFileName.u8string()), isAzureCore);
    auto currentTestName{::testing::UnitTest::GetInstance()->current_test_info()->name()};

    std::string testCategory{"Test Tool"};
    testCategory += currentTestName;
    testCategory += baseFileName;

    llvm::cl::OptionCategory toolCategory(testCategory);
    std::vector<std::string> clangArgv;
    clangArgv.push_back("TestTool");
    clangArgv.push_back(static_cast<std::string>(stringFromU8string(tempFileName.u8string())));

    clangArgv.push_back("--extra-arg-before=-Wno-deprecated-volatile");
    clangArgv.push_back("--extra-arg-before=-Qunused-arguments");
    clangArgv.push_back("--extra-arg-before=-Wno-unused-command-line-argument");
    if (!isAzureCore)
    {
      clangArgv.push_back(R"(--extra-arg-before=-I.\Tests\core\azure-core\inc)");
    }

    // Now move the clang arguments into a vector of char* values so it can be passed to the
    // CommonOptionsParser.
    std::vector<const char*> argv;
    for (auto const& arg : clangArgv)
    {
      argv.push_back(arg.data());
    }

    int testArgc = static_cast<int>(argv.size());
    auto optionsParser
        = clang::tooling::CommonOptionsParser::create(testArgc, argv.data(), toolCategory);
    if (!optionsParser)
    {
      llvm::errs() << optionsParser.takeError();
      return false;
    }

    clang::tooling::ClangTool tool(
        optionsParser->getCompilations(), optionsParser->getSourcePathList());
    int result
        = tool.run(clang::tooling::newFrontendActionFactory<clang::SyntaxOnlyAction>().get());
    return result == 0;
  }
};

TEST_F(TestParser, Create)
{
  ApiViewProcessor processor;

  auto& db = processor.GetClassesDatabase();

  JsonDumper jsonDumper("My First Review", "Azure Core", "Azure.Core");
  processor.GetClassesDatabase()->DumpClassDatabase(&jsonDumper);
  EXPECT_EQ(0ul, db->GetAstNodeMap().size());
}

TEST_F(TestParser, CompileSimple)
{
  {
    ApiViewProcessor processor("tests", std::string_view("SimpleTest.json"));

    processor.ProcessApiView();
    auto& db = processor.GetClassesDatabase();
    EXPECT_EQ(8ul, db->GetAstNodeMap().size());

    EXPECT_TRUE(SyntaxCheckClassDb(db, "SimpleTestGenerated.cpp", false));
  }
}

TEST_F(TestParser, NamespaceFilter1)
{
  ApiViewProcessor processor("tests", R"({
  "sourceFilesToProcess": [
    "SimpleTest.cpp"
  ],
  "additionalIncludeDirectories": [],
  "additionalCompilerSwitches": ["-Qunused-arguments"],
  "includeInternal": false,
  "includeDetail": false,
  "includePrivate": false,
  "filterNamespace": "Test"
}
)"_json);

  processor.ProcessApiView();
  auto& db = processor.GetClassesDatabase();
  EXPECT_EQ(4ul, db->GetAstNodeMap().size());
  EXPECT_TRUE(SyntaxCheckClassDb(db, "SimpleTestGenerated1.cpp", false));
}
TEST_F(TestParser, NamespaceFilter2)
{
  ApiViewProcessor processor("tests", R"({
  "sourceFilesToProcess": [
    "SimpleTest.cpp"
  ],
  "additionalIncludeDirectories": [],
  "additionalCompilerSwitches": ["-Qunused-arguments"],
  "includeInternal": false,
  "includeDetail": false,
  "includePrivate": false,
  "filterNamespace": "Test::Inner"
}
)"_json);

  processor.ProcessApiView();
  auto& db = processor.GetClassesDatabase();
  EXPECT_EQ(2ul, db->GetAstNodeMap().size());
  EXPECT_TRUE(SyntaxCheckClassDb(db, "SimpleTestGenerated2.cpp", false));
}
TEST_F(TestParser, NamespaceFilter3)
{
  ApiViewProcessor processor("tests", R"({
  "sourceFilesToProcess": [
    "SimpleTest.cpp"
  ],
  "additionalIncludeDirectories": [],
  "additionalCompilerSwitches": ["-Qunused-arguments"],
  "includeInternal": false,
  "includeDetail": false,
  "includePrivate": false,
  "filterNamespace": "Axxx"
}
)"_json);
  processor.ProcessApiView();

  auto& db = processor.GetClassesDatabase();
  EXPECT_EQ(1ul, db->GetAstNodeMap().size());
  EXPECT_TRUE(SyntaxCheckClassDb(db, "SimpleTestGenerated3.cpp", false));
}

TEST_F(TestParser, Class1)
{
  ApiViewProcessor processor("tests", R"({
  "sourceFilesToProcess": [
    "ClassesWithInternalAndDetail.cpp"
  ],
  "additionalIncludeDirectories": [],
  "additionalCompilerSwitches": ["-Qunused-arguments"],
  "includeInternal": false,
  "includeDetail": false,
  "includePrivate": false,
  "filterNamespace": null
}
)"_json);
  processor.ProcessApiView();

  auto& db = processor.GetClassesDatabase();
  EXPECT_EQ(8ul, db->GetAstNodeMap().size());
  EXPECT_TRUE(SyntaxCheckClassDb(db, "Classes1.cpp", false));
}
TEST_F(TestParser, Class2)
{
  ApiViewProcessor processor("tests", R"({
  "sourceFilesToProcess": [
    "ClassesWithInternalAndDetail.cpp"
  ],
  "additionalIncludeDirectories": [],
  "additionalCompilerSwitches": ["-Qunused-arguments"],
  "includeInternal": true,
  "includeDetail": false,
  "includePrivate": false,
  "filterNamespace": null
}
)"_json);
  processor.ProcessApiView();

  auto& db = processor.GetClassesDatabase();
  EXPECT_EQ(14ul, db->GetAstNodeMap().size());
  EXPECT_TRUE(SyntaxCheckClassDb(db, "Classes1B.cpp", false));
}

TEST_F(TestParser, Expressions)
{
  ApiViewProcessor processor("tests", R"({
  "sourceFilesToProcess": [
    "ExpressionTests.cpp"
  ],
  "additionalIncludeDirectories": [],
  "additionalCompilerSwitches": ["-Qunused-arguments"],
  "includeInternal": true,
  "includeDetail": false,
  "includePrivate": false,
  "filterNamespace": null
}
)"_json);

  processor.ProcessApiView();

  auto& db = processor.GetClassesDatabase();
  EXPECT_TRUE(SyntaxCheckClassDb(db, "Expression1.cpp", false));
}

TEST_F(TestParser, AzureCore1)
{
  ApiViewProcessor processor("tests", R"({
  "sourceFilesToProcess": [
    "core/azure-core/inc/azure/core.hpp"
  ],
  "additionalIncludeDirectories": ["core/azure-core/inc/azure"],
  "additionalCompilerSwitches": ["-Qunused-arguments"],
  "includeInternal": true,
  "includeDetail": false,
  "includePrivate": false,
  "filterNamespace": "Azure::"
}
)"_json);

  processor.ProcessApiView();

  auto& db = processor.GetClassesDatabase();
  EXPECT_TRUE(SyntaxCheckClassDb(db, "Core1.cpp", true));
}
TEST_F(TestParser, AzureCore2)
{
  ApiViewProcessor processor(R"(tests\\core\azure-core)");

  processor.ProcessApiView();

  auto& db = processor.GetClassesDatabase();
  EXPECT_TRUE(SyntaxCheckClassDb(db, "Core2.cpp", true));
}

TEST_F(TestParser, AzureAttestation)
{
  ApiViewProcessor processor(R"(tests\attestation\azure-security-attestation\inc)", R"({
  "sourceFilesToProcess": [
    "azure/attestation.hpp"
  ],
  "additionalIncludeDirectories": ["core/azure-core/inc/azure", "attestation/azure-security-attestation/inc"],
  "additionalCompilerSwitches": ["-Qunused-arguments"],
  "includeInternal": false,
  "includeDetail": false,
  "includePrivate": false,
  "filterNamespace": "Azure::Security::Attestation"
}
)"_json);
  processor.ProcessApiView();
  //ApiViewProcessorOptions processorOptions{.FilterNamespace = "Azure::Security::Attestation"};
  //ApiViewProcessor processor(processorOptions);
  //std::filesystem::path sourcePath(R"(.\Tests\core\azure-core\inc)");
  //auto coreIncludeDirectory = std::filesystem::absolute(sourcePath);

  //int rv = processor.ProcessApiView(
  //    R"(.\Tests\attestation\azure-security-attestation\inc)",
  //    {std::string("-I") + std::string(stringFromU8string(coreIncludeDirectory.u8string()))},
  //    {R"(.\Tests\attestation\azure-security-attestation\inc\azure\attestation.hpp)"});

  auto& db = processor.GetClassesDatabase();
  EXPECT_TRUE(SyntaxCheckClassDb(db, "Attestation1.cpp", false));
}

TEST_F(TestParser, AzureStorageBlobs)
{
  ApiViewProcessorOptions processorOptions{.FilterNamespace = "Azure::Storage::Blobs"};
  ApiViewProcessor processor(processorOptions);

  processor.ProcessApiView(
      R"(G:\az\LarryO\azure-sdk-for-cpp\out\build\x64-DebugWithPerfTest)",
      {
          "-Qunused-arguments",
          R"(-IG:\Az\LarryO\azure-sdk-for-cpp\sdk\core\azure-core\inc)"
          R"(-IG:\Az\LarryO\azure-sdk-for-cpp\sdk\storage\azure-storage-common\inc)",
          R"(-IG:\Az\LarryO\azure-sdk-for-cpp\sdk\storage\azure-storage-blobs\inc\azure\storage\blobs)",
          R"(-IG:\Az\LarryO\azure-sdk-for-cpp\sdk\storage\azure-storage-blobs\inc)",
      },
      {R"(G:\az\larryo\azure-sdk-for-cpp\sdk\storage\azure-storage-blobs\inc\azure\storage\blobs.hpp)"});
  auto& db = processor.GetClassesDatabase();
  EXPECT_TRUE(SyntaxCheckClassDb(db, "StorageBlob1.cpp", false));
}
