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


