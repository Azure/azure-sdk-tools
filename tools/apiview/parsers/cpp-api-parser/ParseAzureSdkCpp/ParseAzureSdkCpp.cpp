// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

// ParseAzureSdkCpp.cpp : Defines the entry point for the application.
//
// This file primarily parses the command line (using TCLAP
// (https://tclap.sourceforge.net/manual.html) for command line parsing).
//
// It then feeds those inputs into the ApiViewProcessor and dumps an output JSON file whose name is
// provided on the command line.
//

#include "ApiViewProcessor.hpp"
#include "JsonDumper.hpp"
#include "TextDumper.hpp"
#include <filesystem>
#include <fstream>
#include <iostream>
#include <ostream>
#include <tclap/CmdLine.h>
#include <vector>

int main(int argc, char** argv)
{
  try
  {
    TCLAP::CmdLine commandLine(
        R"(C++ ApiView Parser

Settings for an Api Review are contained in the file "ApiViewSettings.json".

The ApiViewSettings.json defines the following properties:

"sourceFilesToProcess" - if present and non-null, represents an array of source files which describes the 
    files which define the APIs included in the API Review. If this is not present, the entire source tree under the input directory
    is scanned for headers to include.
"sourceFilesToSkip" - If sourceFilesToProcess is not present or is null, this is a set of files which should be excluded from the
    source tree scan. 
"additionalIncludeDirectories" - if present and non-null, represents an array of directories which are added to the include path
    when compiling the source files.
"reviewName" - The name of the API review, used if the -r command line switch is not present.
"packageName" - the name of the package being reviewed.
"serviceName" - The name of the service being reviewed (preferably as represented in ARM).
"additionalCompilerSwitches" - if present and non-null, represents an array of additional compiler switches to pass to the compiler.
"allowInternal" - if present and true, types in the "_internal" namespace will not generate an error.
"includeDetail" - if present and true, includes types in the "_detail" namespace in the API Review.
"includePrivate" - if present and true, includes private APIs in the API Review.
"filterNamespace" - if present and non-null, represents a set of namespace prefixes which are expected in the package.
    Types which do not match the filter will generate a warning.

An example of an ApiViewSettings.json file is:
    {
      "sourceFilesToProcess": null,
      "additionalIncludeDirectories": [
        "../../../core/azure-core/inc"
      ],
      "sourceFilesToSkip": [
      ],
      "additionalCompilerSwitches": [],
      "allowInternal": true,
      "includeDetail": false,
      "includePrivate": false,
      "filterNamespace": "Azure::Storage",
      "reviewName": "Azure Storage Common API Review",
      "serviceName": "Azure Storage",
      "packageName": "azure-storage-common-cpp"
    }

)",
        ' ');

    TCLAP::UnlabeledValueArg<std::string> inputDirectory(
        "input", "Input Directory", true, ".", "string", commandLine);
    TCLAP::ValueArg<std::string> outputFileArg(
        "o", "output", "Output filename", true, "ApiReview.json", "string", commandLine);
    TCLAP::ValueArg<std::string> reviewName(
        "r", "review", "Review Name", false, "", "string", commandLine);
    TCLAP::ValueArg<std::string> packageVersion(
        "", "packageVersion", "Package Version", false, "", "string", commandLine);
    TCLAP::SwitchArg consoleOutput(
        "c", "console", "Dump output to console (diagnostic)", commandLine);

    commandLine.parse(argc, argv);
    std::filesystem::path outputFileName{std::filesystem::absolute(outputFileArg.getValue())};

    if (outputFileName.extension() != ".json")
    {
      std::cerr << "Output file name must have an extension of .json" << std::endl;
      return 1;
    }

    std::string directoryToParse{inputDirectory.getValue()};
    if (directoryToParse.back() == '\\')
    {
      directoryToParse.erase(directoryToParse.size() - 1);
    }
    ApiViewProcessor apiViewProcessor(directoryToParse);

    int rv = apiViewProcessor.ProcessApiView();
    if (rv == 0)
    {
      if (consoleOutput)
      {
        TextDumper textDumper(std::cout);
        apiViewProcessor.GetClassesDatabase()->DumpClassDatabase(&textDumper);
      }

      {
        JsonDumper jsonDumper(
            reviewName.getValue().empty() ? apiViewProcessor.ReviewName() : reviewName.getValue(),
            apiViewProcessor.ServiceName(),
            apiViewProcessor.PackageName(),
            packageVersion.getValue());
        apiViewProcessor.GetClassesDatabase()->DumpClassDatabase(&jsonDumper);

        std::cout << "Writing API Review JSON file to: " << outputFileName.string() << std::endl;
        std::ofstream outfile{outputFileName};
        jsonDumper.DumpToFile(outfile);
      }
    }
    return rv;
  }
  catch (std::exception& ex)
  {
    std::cerr << "Error: Exception thrown: " << ex.what() << std::endl;
  }
  return -1;
}

// Note for future:
// clang-tidy command line:
// clang-tidy -p .\out\build\x64-DebugWithTestsWinHttp\ --extra-arg-before=-Qunused-arguments
// -checks=cppcoreguidelines* -header-filter='.*.hpp'   .\sdk\core\azure-core\inc\azure\core.hpp
