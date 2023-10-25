// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#include "AstDumper.hpp"
#include "AstNode.hpp"

namespace clang { namespace comments {

  class Comment;
  enum CommandMarkerKind : int;
}} // namespace clang::comments

/** An AstDocumentation node represents a parsed comment. 
 * It is a base class which will be
 * specialized for different types of comments, loosely following the clang AST for comments.
 */
class AstDocumentation : public AstNode {
public:
  virtual bool IsInlineComment() const = 0;
  void AddChild(std::unique_ptr<AstDocumentation>&& line) { m_children.push_back(std::move(line)); }
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

/** Extract a comment from a comment node.
 * This function iterates over a clang::comments::Comment
 * node and retrieves all the information in the comment in a way which can later be dumped.
 */
std::unique_ptr<AstDocumentation> ExtractCommentForDeclaration(
    clang::ASTContext const& context,
    clang::Decl const* declaration);
