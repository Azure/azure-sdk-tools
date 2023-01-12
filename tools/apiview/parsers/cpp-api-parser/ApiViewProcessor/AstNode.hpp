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

class AstNode
{
protected:
  explicit AstNode(clang::Decl const* decl);

public:
  // AstNode's don't have namespaces or names, so return something that would make callers happy.
  virtual std::string_view const Namespace() { return ""; }
  virtual std::string_view const Name() { return ""; }

  virtual void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions) = 0;

  static std::string GetCommentForNode(clang::ASTContext& context, clang::Decl const* decl);
  static std::string GetCommentForNode(clang::ASTContext& context, clang::Decl const& decl);
  static std::unique_ptr<AstNode> Create(
      clang::Decl const* decl,
      AzureClassesDatabase* const azureClassesDatabase,
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode);
  static std::string GetNamespaceForDecl(clang::Decl const* decl);
};

class AstNamedNode : public AstNode {
  std::string m_namespace;
  std::string m_name;

protected:
  AzureClassesDatabase* const m_classDatabase;
  std::string m_navigationId;
  std::string m_nodeDocumentation;
  clang::AccessSpecifier m_nodeAccess;

  explicit AstNamedNode(
      clang::NamedDecl const* decl,
      AzureClassesDatabase* const classDatabase,
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode);

public:
  virtual void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions) override
  {
    assert(!"Pure virtual base - missing implementation of DumpNode in derived class.");
  };
  std::string_view const Namespace() override { return m_namespace; }
  std::string_view const Name() override { return m_name; }
};
