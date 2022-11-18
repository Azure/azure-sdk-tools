// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

// ParseAzureSdkCpp.cpp : Defines the entry point for the application.
//

#include "ParseAzureSdkCpp.h"
#include "ApiViewProcessor.hpp"
#include "AstNode.hpp"
#include "JsonDumper.hpp"
#include "TextDumper.hpp"
#include <filesystem>
#include <fstream>
#include <map>
#include <optional>
#include <ostream>
#include <vector>

inline std::string_view stringFromU8string(std::u8string const& str)
{
  return std::string_view(reinterpret_cast<const char*>(str.data()), str.size());
}
#define BUILD_AZURE_CORE 1

int main(int argc, char** argv)
{

  // clang-tidy command line:
  // clang-tidy -p .\out\build\x64-DebugWithTestsWinHttp\ --extra-arg-before=-Qunused-arguments
  // -checks=cppcoreguidelines* -header-filter='.*.hpp'   .\sdk\core\azure-core\inc\azure\core.hpp

  ApiViewProcessorOptions options
  {
    .IncludeInternal = true,
#if BUILD_AZURE_CORE
    .FilterNamespace = "Azure::"
#else
    .FilterNamespace = "Azure::Security::Attestation"
#endif
  };
  ApiViewProcessor apiViewProcessor(options);

#if BUILD_AZURE_CORE
  int rv = apiViewProcessor.ProcessApiView(
      R"(..\ParseTests\Tests\core\azure-core\inc)",
      {},
      {
          R"(..\ParseTests\Tests\core\azure-core\inc\azure\core.hpp)",
          R"(..\ParseTests\Tests\core\azure-core\inc\azure\core\internal\extendable_enumeration.hpp)",
          R"(..\ParseTests\Tests\core\azure-core\inc\azure\core\internal\contract.hpp)",
          R"(..\ParseTests\Tests\core\azure-core\inc\azure\core\internal\environment.hpp)",
          R"(..\ParseTests\Tests\core\azure-core\inc\azure\core\internal\unique_handle.hpp)",
          R"(..\ParseTests\Tests\core\azure-core\inc\azure\core\internal\tracing\service_tracing.hpp)",
          R"(..\ParseTests\Tests\core\azure-core\inc\azure\core\internal\tracing\tracing_impl.hpp)",
          // R"(..\ParseTests\Tests\core\azure-core\inc\azure\core\internal\json\json.hpp)",
          R"(..\ParseTests\Tests\core\azure-core\inc\azure\core\internal\http\http_sanitizer.hpp)",
          R"(..\ParseTests\Tests\core\azure-core\inc\azure\core\internal\http\pipeline.hpp)",
          R"(..\ParseTests\Tests\core\azure-core\inc\azure\core\internal\http\user_agent.hpp)",
          R"(..\ParseTests\Tests\core\azure-core\inc\azure\core\internal\diagnostics\log.hpp)",
          R"(..\ParseTests\Tests\core\azure-core\inc\azure\core\internal\diagnostics\global_exception.hpp)",
          R"(..\ParseTests\Tests\core\azure-core\inc\azure\core\internal\cryptography\sha_hash.hpp)",
      });
#else

  std::filesystem::path sourcePath(R"(..\ParseTests\Tests\core\azure-core\inc)");
  auto coreIncludeDirectory = std::filesystem::absolute(sourcePath);

  int rv = apiViewProcessor.ProcessApiView(
      R"(..\ParseTests\Tests\attestation\azure-security-attestation\inc)",
      {R"(-I)" + std::string(stringFromU8string(coreIncludeDirectory.u8string()))},
      {R"(..\ParseTests\Tests\attestation\azure-security-attestation\inc\azure\attestation.hpp)"});
#endif

  if (rv == 0)
  {
    // Dump to text file
    {
      TextDumper dumper(std::cout);
      apiViewProcessor.GetClassesDatabase()->DumpClassDatabase(&dumper);
    }

    {
      JsonDumper jsonDumper("My First Review", "Azure Core", "Azure.Core");
      apiViewProcessor.GetClassesDatabase()->DumpClassDatabase(&jsonDumper);
      std::ofstream outfile{"MyFirstReview.json"};
      jsonDumper.DumpToFile(outfile);
    }
  }

  return rv;
}
