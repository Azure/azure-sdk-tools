// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#pragma once

#include "ApiViewProcessor.hpp"
#include "AstDumper.hpp"
#include <cassert>
#include <memory>
#include <optional>
#include <tuple>
#include <vector>

namespace clang {
class Decl;
class ASTContext;
enum AccessSpecifier : int;
} // namespace clang

struct DumpNodeOptions
{
  bool DumpListInitializer{false};
  bool NeedsLeftAlign{true};
  bool NeedsLeadingNewline{true};
  bool NeedsTrailingNewline{true};
  bool NeedsTrailingSemi{true};
  bool NeedsNamespaceAdjustment{true};
  bool IncludeNamespace{false};
  bool IncludeContainingClass{false};
};

struct AstNode
{
  virtual void DumpNode(AstDumper* dumper, DumpNodeOptions dumpOptions) = 0;
  AstNode(clang::Decl const* decl);
  
  virtual std::string_view const Namespace() { return ""; }

  static std::string GetCommentForNode(clang::ASTContext& context, clang::Decl const* decl);
  static std::string GetCommentForNode(clang::ASTContext& context, clang::Decl const& decl);
  static std::unique_ptr<AstNode> Create(
      clang::Decl const* decl,
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode);
  static std::string GetNamespaceForDecl(clang::Decl const* decl);
};

class AstNamedNode : public AstNode {
public:
  std::string m_namespace;
  std::string m_name;
  std::string m_navigationId;
  std::string m_nodeDocumentation;
  clang::AccessSpecifier m_nodeAccess;

  AstNamedNode(
      clang::NamedDecl const* decl,
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode);
  virtual void DumpNode(AstDumper* dumper, DumpNodeOptions dumpOptions)
  {
    assert(!"Pure virtual base");
  };
  std::string_view const Namespace() override { return m_namespace; }
};
