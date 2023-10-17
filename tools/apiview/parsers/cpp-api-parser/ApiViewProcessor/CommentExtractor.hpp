// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#include "AstDumper.hpp"
#include "AstNode.hpp"

namespace clang { namespace comments {

  class Comment;
  enum CommandMarkerKind : int;
}} // namespace clang::comments

class AstDocumentation : public AstNode {
public:
  size_t GetChildCount() const { return m_children.size(); }
  std::unique_ptr<AstDocumentation> const& GetChild(size_t index) const
  {
    return m_children[index];
  }
  std::string const& GetLine() const { return m_thisLine; }
  void AddChild(std::unique_ptr<AstDocumentation>&& line) { m_children.push_back(std::move(line)); }
  virtual bool IsInlineComment() const = 0;
  void DumpNode(AstDumper* dumper, DumpNodeOptions const& options) const override
  {
    if (options.NeedsLeadingNewline)
    {
      dumper->Newline();
    }
    if (options.NeedsLeftAlign)
    {
      dumper->LeftAlign();
    }
    dumper->InsertWhitespace();
    dumper->InsertComment("* ");
    dumper->InsertWhitespace();
    dumper->InsertComment(m_thisLine);

    for (auto const& child : m_children)
    {
      DumpNodeOptions innerOptions{options};
      innerOptions.NeedsLeftAlign = true;
      innerOptions.NeedsLeadingNewline = true;
      innerOptions.NeedsTrailingNewline = false;
      if (child)
      {
        child->DumpNode(dumper, options);
      }
    }

    if (options.NeedsTrailingNewline)
    {
      dumper->Newline();
    }
  }

  static std::unique_ptr<AstDocumentation> Create(clang::comments::Comment const* comment);

protected:
  AstDocumentation() : AstNode(){};
  std::string_view GetCommandMarker(clang::comments::CommandMarkerKind marker);

  std::vector<std::unique_ptr<AstDocumentation>> m_children;
  std::string m_thisLine{};
};

class CommentExtractor {
public:
  CommentExtractor(const clang::ASTContext& context) : m_context{context} {}

  std::unique_ptr<AstDocumentation> Extract(clang::comments::Comment* comment);

private:
  const clang::ASTContext& m_context;
};
