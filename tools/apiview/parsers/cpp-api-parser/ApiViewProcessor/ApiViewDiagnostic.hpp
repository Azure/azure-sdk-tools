// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#include "ApiViewProcessor.hpp"


struct ApiViewDiagnostic {
  enum class DiagnosticLevel
  {
      None=0,
      Info=1,
      Warning=2,
      Error=3
  };
  std::string DiagnosticId;
  std::string HelpLinkUri;
  std::string TargetId;
  DiagnosticLevel Level;
};

enum class ApiViewDiagnostics
{
  CPPA0001, // "Missing documentation for {0}"
  CPPA0002, // Type Declared in Global Namespace
  CPPA0003, // Type Declared in non-filtered namespace.
  CPPA0004, // Non enum class enums (https://isocpp.github.io/CppCoreGuidelines/CppCoreGuidelines#Renum-class)
  CPPA0005, // Non-const static fields
  
  
};

