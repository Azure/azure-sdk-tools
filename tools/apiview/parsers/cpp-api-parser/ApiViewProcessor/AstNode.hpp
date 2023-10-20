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
class Attr;
enum AccessSpecifier : int;
} // namespace clang

class AstDocumentation;

struct DumpNodeOptions
{
  bool DumpListInitializer{false};
  bool NeedsDocumentation{true};
  bool NeedsSourceComment{true};
  bool NeedsLeftAlign{true};
  bool NeedsLeadingNewline{true};
  bool NeedsTrailingNewline{true};
  bool NeedsTrailingSemi{true};
  bool NeedsNamespaceAdjustment{true};
  bool IncludeNamespace{false};
  bool IncludeContainingClass{false};
  bool InlineBlockComment{false};
  size_t RightMargin{80};   // Soft right margin for dumper.
};

class AstNode {
protected:
  explicit AstNode();

public:
  // AstNode's don't have namespaces or names, so return something that would make callers happy.
  virtual std::string_view const Namespace() const { return ""; }
  virtual std::string_view const Name() const { return ""; }

  virtual void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const = 0;

  static std::unique_ptr<AstDocumentation> GetCommentForNode(clang::ASTContext& context, clang::Decl const* decl);
  static std::unique_ptr<AstNode> Create(
      clang::Decl const* decl,
      AzureClassesDatabase* const azureClassesDatabase,
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode);
  static std::string GetNamespaceForDecl(clang::Decl const* decl);
};


class AstNamedNode : public AstNode {
  std::string m_namespace;
  std::string m_name;
  std::string m_typeUrl;
  std::string m_typeLocation;
  std::vector<std::unique_ptr<AstNode>> m_nodeAttributes;

protected:
  AzureClassesDatabase* const m_classDatabase;
  std::string m_navigationId;
  std::unique_ptr<AstDocumentation> m_nodeDocumentation;
  clang::AccessSpecifier m_nodeAccess;

  explicit AstNamedNode(
      clang::NamedDecl const* decl,
      AzureClassesDatabase* const classDatabase,
      std::shared_ptr<TypeHierarchy::TypeHierarchyNode> parentNode);

public:
  void DumpAttributes(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const;
  void DumpDocumentation(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const;
  void DumpSourceComment(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const;
  virtual void DumpNode(AstDumper* dumper, DumpNodeOptions const& dumpOptions) const override
  {
    assert(!"Pure virtual base - missing implementation of DumpNode in derived class.");
  };
  std::string_view const Namespace() const override { return m_namespace; }
  std::string_view const Name() const override { return m_name; }
};
