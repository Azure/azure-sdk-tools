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
    TCLAP::CmdLine commandLine("C++ ApiView Parser", ' ');

    TCLAP::UnlabeledValueArg<std::string> inputDirectory(
        "input", "Input Directory", true, ".", "string", commandLine);
    TCLAP::ValueArg<std::string> outputFileArg(
        "o", "output", "Output filename", true, "ApiReview.json", "string", commandLine);
    TCLAP::ValueArg<std::string> reviewName(
        "r", "review", "Review Name", false, "", "string", commandLine);
    TCLAP::ValueArg<std::string> packageVersion(
        "", "packageVersion", "Package Version", false, "", "string", commandLine);
    TCLAP::SwitchArg consoleOutput("c", "console", "Dump output to console (diagnostic)", commandLine);

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
