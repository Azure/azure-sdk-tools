// Co pyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#include "CommentExtractor.hpp"
#include <clang/AST/ASTContext.h>
#include <clang/AST/Comment.h>
#include <clang/AST/CommentVisitor.h>
#include <iostream>
#include <iterator>
#include <list>
#include <string>
#include <vector>

using namespace clang;

struct AstComment : public AstDocumentation
{
  AstComment(comments::FullComment const* const comment) : AstDocumentation() {}

  bool IsInlineComment() const override { return false; }
  void DumpNode(AstDumper* dumper, DumpNodeOptions const& options) const override
  {
    for (auto& child : m_children)
    {
      if (child)
      {
        child->DumpNode(dumper, options);
      }
    }
  }
};

struct AstBlockCommandComment : public AstDocumentation
{
  AstBlockCommandComment(comments::BlockCommandComment const* const comment) : AstDocumentation()
  {
    std::string value;
    value += GetCommandMarker(comment->getCommandMarker());
    auto commandInfo{
        clang::comments::CommandTraits::getBuiltinCommandInfo(comment->getCommandID())};
    if (commandInfo->IsBriefCommand)
    {
      value += "brief";
    }
    else if (commandInfo->IsReturnsCommand)
    {
      value += "returns";
    }
    else if (commandInfo->IsThrowsCommand)
    {
      value += "throws";
    }
    else if (commandInfo->IsParamCommand)
    {
      throw std::runtime_error("Block command comment should never have a param command.");
    }
    else if (commandInfo->IsTParamCommand)
    {
      throw std::runtime_error("Block command comment should never have a tparam command.");
    }
    else if (commandInfo->IsVerbatimBlockCommand)
    {
      throw std::runtime_error("Block command comment should never have a verbatim command.");
    }
    else if (commandInfo->IsVerbatimLineCommand)
    {
      throw std::runtime_error("Block command comment should never have a verbatim line command.");
    }
    else
    {
      value += commandInfo->Name;
    }
    m_thisLine = value;
  }

  bool IsInlineComment() const override { return false; }
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
    dumper->InsertPunctuation('*');
    dumper->InsertWhitespace();
    dumper->InsertComment(m_thisLine);

    // The first child will be the first line of the brief description, it should be joined with the
    // current line.
    auto child{m_children.begin()};
    if (child != m_children.end() && *child)
    {
      DumpNodeOptions innerOptions{options};
      innerOptions.NeedsLeftAlign = false;
      innerOptions.NeedsLeadingNewline = false;
      innerOptions.NeedsTrailingNewline = true;
      innerOptions.InlineBlockComment = true;
      (*child)->DumpNode(dumper, innerOptions);
      child++;
    }

    for (; child != m_children.end(); child++)
    {
      DumpNodeOptions innerOptions{options};
      innerOptions.NeedsLeftAlign = true;
      innerOptions.NeedsLeadingNewline = true;
      innerOptions.NeedsTrailingNewline = false;
      if (*child)
      {
        (*child)->DumpNode(dumper, options);
      }
    }

    if (options.NeedsTrailingNewline)
    {
      dumper->Newline();
    }
  }
};

struct AstParamComment : public AstDocumentation
{
  AstParamComment(comments::ParamCommandComment const* comment) : AstDocumentation()
  {
    std::string thisLine;
    thisLine += GetCommandMarker(comment->getCommandMarker());
    auto commandInfo{
        clang::comments::CommandTraits::getBuiltinCommandInfo(comment->getCommandID())};
    if (commandInfo->IsParamCommand)
    {
      thisLine += "param";
    }
    else
    {
      thisLine += commandInfo->Name;
    }
    thisLine += " ";
    // If the caller explicitly listed the direction, include that in the description.
    if (comment->isDirectionExplicit())
    {
      thisLine += comment->getDirectionAsString(comment->getDirection());
      thisLine += " ";
    }
    if (comment->hasParamName())
    {
      thisLine += comment->getParamNameAsWritten();
      thisLine += " ";
    }
    m_thisLine = thisLine;
  }

  bool IsInlineComment() const override { return false; }
  void DumpNode(AstDumper* dumper, DumpNodeOptions const& options) const override
  {
    if (m_thisLine == "@param format")
    {
      std::cout << "@param[in] format";
    }
    if (options.NeedsLeadingNewline)
    {
      dumper->Newline();
    }
    if (options.NeedsLeftAlign)
    {
      dumper->LeftAlign();
    }
    dumper->InsertWhitespace();
    dumper->InsertPunctuation('*');
    dumper->InsertWhitespace();
    dumper->InsertComment(m_thisLine);

    // The first child will be the first line of the parameter documentation, it should be joined
    // with the current line.
    auto child{m_children.begin()};
    if (child != m_children.end() && *child)
    {
      DumpNodeOptions innerOptions{options};
      innerOptions.NeedsLeftAlign = false;
      innerOptions.NeedsLeadingNewline = false;
      innerOptions.NeedsTrailingNewline = true;
      innerOptions.InlineBlockComment = true;
      (*child)->DumpNode(dumper, innerOptions);
      child++;
    }

    for (; child != m_children.end(); child++)
    {
      DumpNodeOptions innerOptions{options};
      innerOptions.NeedsLeftAlign = true;
      innerOptions.NeedsLeadingNewline = false;
      innerOptions.NeedsTrailingNewline = true;
      if (*child)
      {
        (*child)->DumpNode(dumper, options);
      }
    }

    if (options.NeedsTrailingNewline)
    {
      dumper->Newline();
    }
  }
};

struct AstTParamComment : public AstDocumentation
{
  AstTParamComment(comments::TParamCommandComment const* comment) : AstDocumentation()
  {
    std::string thisLine;
    thisLine += GetCommandMarker(comment->getCommandMarker());
    auto commandInfo{
        clang::comments::CommandTraits::getBuiltinCommandInfo(comment->getCommandID())};
    if (commandInfo->IsTParamCommand)
    {
      thisLine += "tparam";
    }
    else
    {
      thisLine += commandInfo->Name;
    }
    thisLine += " ";

    if (comment->hasParamName())
    {
      thisLine += comment->getParamNameAsWritten();
      thisLine += " ";
    }
    m_thisLine = thisLine;
  }
  bool IsInlineComment() const override { return false; }

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
    dumper->InsertPunctuation('*');
    dumper->InsertWhitespace();
    dumper->InsertComment(m_thisLine);

    // The first child will be the first line of the parameter documentation, it should be joined
    // with the current line.
    auto child{m_children.begin()};
    if (child != m_children.end() && *child)
    {
      DumpNodeOptions innerOptions{options};
      innerOptions.NeedsLeftAlign = false;
      innerOptions.NeedsLeadingNewline = false;
      innerOptions.NeedsTrailingNewline = true;
      innerOptions.InlineBlockComment = true;
      (*child)->DumpNode(dumper, innerOptions);
      child++;
    }

    for (; child != m_children.end(); child++)
    {
      DumpNodeOptions innerOptions{options};
      innerOptions.NeedsLeftAlign = true;
      innerOptions.NeedsLeadingNewline = true;
      innerOptions.NeedsTrailingNewline = false;
      if (*child)
      {
        (*child)->DumpNode(dumper, options);
      }
    }

    if (options.NeedsTrailingNewline)
    {
      dumper->Newline();
    }
  }
};

struct AstVerbatimBlockComment : public AstDocumentation
{
  AstVerbatimBlockComment(comments::VerbatimBlockComment const* comment) : AstDocumentation()
  {
    std::string thisLine;
    auto commandInfo{
        clang::comments::CommandTraits::getBuiltinCommandInfo(comment->getCommandID())};

    thisLine += GetCommandMarker(comment->getCommandMarker());
    thisLine += commandInfo->Name;

    auto it = comment->child_begin();
    auto childLineComment = clang::dyn_cast<clang::comments::VerbatimBlockLineComment>(*it);
    if (childLineComment)
    {
      std::string childText{childLineComment->getText()};
      // If the first character of the 0th argument is a '{', then this is a code block. Append
      // it to the name.
      if ((childText[0] == '{') && (childText[(childText.size() - 1)] = '}'))
      {
        m_hasLanguageTag = true;
      }
    }
    m_thisLine = thisLine;
    m_endMarker += GetCommandMarker(comment->getCommandMarker());
    m_endMarker += commandInfo->EndCommandName;
  }

  bool IsInlineComment() const override { return false; }
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

    // The first child will be the first line of the parameter documentation, it should be joined
    // with the current line.
    auto child{m_children.begin()};
    if (child != m_children.end() && *child)
    {
      DumpNodeOptions innerOptions{options};
      if (m_hasLanguageTag)
      {
        innerOptions.NeedsLeftAlign = false;
        innerOptions.NeedsLeadingNewline = false;
        innerOptions.NeedsTrailingNewline = false;
        innerOptions.InlineBlockComment = true;
      }
      (*child)->DumpNode(dumper, innerOptions);
      child++;
    }

    for (; child != m_children.end(); child++)
    {
      DumpNodeOptions innerOptions{options};
      innerOptions.NeedsLeftAlign = true;
      innerOptions.NeedsLeadingNewline = true;
      innerOptions.NeedsTrailingNewline = false;
      if (*child)
      {
        (*child)->DumpNode(dumper, options);
      }
    }

    if (!m_endMarker.empty())
    {
      dumper->Newline();

      dumper->LeftAlign();
      dumper->InsertWhitespace();
      dumper->InsertComment("* ");
      dumper->InsertWhitespace();
      dumper->InsertComment(m_endMarker);
    }

    if (options.NeedsTrailingNewline)
    {
      dumper->Newline();
    }
  }

private:
  bool m_hasLanguageTag{false};
  std::string m_endMarker;
};

// Represents an inline command marker. Examples include the \c in \c foo, or the \a in \a foo.
// The marker is the \c or \a.
//
// \p or \c should be rendered in a fixed width font
// \a or \e or \em should be rendered in a italic font
// \b should be rendered in a bold font
// \emoji should be rendered as an emoji (if possible - see
// https://gist.github.com/rxaviers/7360908).
struct AstInlineCommand : AstDocumentation
{
  AstInlineCommand(const comments::InlineCommandComment* comment) : AstDocumentation()
  {
    std::string thisLine;
    std::string commandRenderMarkdownStart;
    std::string commandRenderMarkdownEnd;
    switch (comment->getRenderKind())
    {
      case clang::comments::InlineCommandComment::RenderKind::RenderNormal:
        break;
      case clang::comments::InlineCommandComment::RenderKind::RenderBold:
        commandRenderMarkdownEnd = "**";
        commandRenderMarkdownStart = "**";
        break;
      case clang::comments::InlineCommandComment::RenderKind::RenderEmphasized:
        commandRenderMarkdownEnd = "*";
        commandRenderMarkdownStart = "*";
        break;
      case clang::comments::InlineCommandComment::RenderKind::RenderMonospaced:
        commandRenderMarkdownEnd = "`";
        commandRenderMarkdownStart = "`";
        break;
      default:
        throw std::runtime_error("Unknown inline command render kind.");
    }
    // Include the arguments to the command.
    thisLine += commandRenderMarkdownStart;
    for (unsigned i = 0u; i < comment->getNumArgs(); ++i)
    {
      thisLine += comment->getArgText(i);
    }
    thisLine += commandRenderMarkdownEnd;
    m_thisLine = thisLine;
  }
  bool IsInlineComment() const override { return true; }

  void DumpNode(AstDumper* dumper, DumpNodeOptions const& options) const override
  {
    dumper->InsertComment(m_thisLine);
    for (auto& child : m_children)
    {
      child->DumpNode(dumper, options);
    }
  }
};

// A paragraph represents a block of text. Typically this is a paragraph of text. The children of
// the line are typically AstTextComment nodes, but they may also be AstInlineCommand nodes. If they
// are AstInlineCommand nodes, we should just insert them with no separation, if they are
// AstTextComment nodes, we should insert them with a new line and comment leader between them.
struct AstParagraphComment : AstDocumentation
{
  AstParagraphComment(comments::ParagraphComment const* comment) : AstDocumentation() {}
  bool IsInlineComment() const override { return false; }

  void DumpNode(AstDumper* dumper, DumpNodeOptions const& options) const override
  {
    if (!options.InlineBlockComment)
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
      dumper->InsertPunctuation('*');
      dumper->InsertWhitespace();

      // Insert a blank line before the paragraph if the previous line was not an inline comment.
      dumper->Newline();
      dumper->LeftAlign();
      dumper->InsertWhitespace();
      dumper->InsertPunctuation('*');
      dumper->InsertWhitespace();
    }
    bool insertLineBreak = false;
    for (auto& child : m_children)
    {
      if (child)
      {
        if (insertLineBreak && !child->IsInlineComment())
        {
          dumper->Newline();
          dumper->LeftAlign();
          dumper->InsertWhitespace();
          dumper->InsertPunctuation('*');
        }
        child->DumpNode(dumper, options);
        if (child->IsInlineComment())
        {
          insertLineBreak = false;
        }
        else
        {
          insertLineBreak = true;
        }
      }
    }
  }
};

struct AstVerbatimBlockLineComment : AstDocumentation
{
  AstVerbatimBlockLineComment(comments::VerbatimBlockLineComment const* comment)
      : AstDocumentation()
  {
    m_thisLine = comment->getText();
  }

  bool IsInlineComment() const override { return false; }
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
    if (!options.InlineBlockComment)
    {
      dumper->InsertWhitespace();
      dumper->InsertPunctuation('*');
      dumper->InsertWhitespace();
    }
    dumper->InsertComment(m_thisLine);
    for (auto& child : m_children)
    {
      child->DumpNode(dumper, options);
    }
    if (options.NeedsTrailingNewline)
    {
      dumper->Newline();
    }
  }
};

struct AstTextComment : AstDocumentation
{
  AstTextComment(comments::TextComment const* comment) : AstDocumentation()
  {
    m_thisLine = comment->getText();
  }
  bool IsInlineComment() const override { return false; }
  void DumpNode(AstDumper* dumper, DumpNodeOptions const& options) const override
  {
    dumper->InsertComment(m_thisLine);
  }
};

struct AstVerbatimLineComment : AstDocumentation
{
  AstVerbatimLineComment(comments::VerbatimLineComment const* comment) : AstDocumentation()
  {
    auto commandInfo{
        clang::comments::CommandTraits::getBuiltinCommandInfo(comment->getCommandID())};
    m_thisLine += GetCommandMarker(comment->getCommandMarker());
    m_thisLine += commandInfo->Name;
    m_endMarker = commandInfo->EndCommandName;
  }
  bool IsInlineComment() const override { return true; }
  void DumpNode(AstDumper* dumper, DumpNodeOptions const& options) const override
  {
    dumper->InsertComment(m_thisLine);
  }

private:
  std::string m_endMarker;
};

struct AstHtmlStartTagComment : AstDocumentation
{
  AstHtmlStartTagComment(comments::HTMLStartTagComment const* comment) : AstDocumentation()
  {
    if (comment->getTagName() == "a")
    {
      m_isLinkHref = true;
      auto argCount = comment->getNumAttrs();
      for (size_t i = 0; i < argCount; i += 1)
      {
        if (comment->getAttr(i).Name == "href")
        {
          m_linkTarget = comment->getAttr(i).Value;
        }
      }
    }
  }
  bool IsInlineComment() const override { return true; }
  void DumpNode(AstDumper* dumper, DumpNodeOptions const& options) const override
  {
    // We only serialize the first link argument.
    if (m_isLinkHref)
    {
      dumper->AddExternalLinkStart(m_linkTarget);
    }
  }

private:
  std::string m_linkTarget;
  bool m_isLinkHref{false};
};

struct AstHtmlEndTagComment : AstDocumentation
{
  AstHtmlEndTagComment(comments::HTMLEndTagComment const* comment) : AstDocumentation()
  {
    if (comment->getTagName() == "a")
    {
      m_isLinkHref = true;
    }
  }
  bool IsInlineComment() const override { return true; }
  void DumpNode(AstDumper* dumper, DumpNodeOptions const& options) const override
  {
    if (m_isLinkHref)
    {
      dumper->AddExternalLinkEnd();
    }
  }

private:
  bool m_isLinkHref{false};
};

std::unique_ptr<AstDocumentation> AstDocumentation::Create(const comments::Comment* comment)
{
  switch (comment->getCommentKind())
  {
    case comments::Comment::CommentKind::FullCommentKind:
      return std::make_unique<AstComment>(cast<const comments::FullComment>(comment));
    case comments::Comment::CommentKind::BlockCommandCommentKind:
      return std::make_unique<AstBlockCommandComment>(
          cast<const comments::BlockCommandComment>(comment));
    case comments::Comment::CommentKind::ParamCommandCommentKind:
      return std::make_unique<AstParamComment>(cast<const comments::ParamCommandComment>(comment));
    case comments::Comment::CommentKind::TParamCommandCommentKind:
      return std::make_unique<AstTParamComment>(
          cast<const comments::TParamCommandComment>(comment));
    case comments::Comment::CommentKind::VerbatimBlockCommentKind:
      return std::make_unique<AstVerbatimBlockComment>(
          cast<const comments::VerbatimBlockComment>(comment));
    case comments::Comment::CommentKind::InlineCommandCommentKind:
      return std::make_unique<AstInlineCommand>(
          cast<const comments::InlineCommandComment>(comment));
    case comments::Comment::ParagraphCommentKind:
      return std::make_unique<AstParagraphComment>(cast<const comments::ParagraphComment>(comment));
    case comments::Comment::TextCommentKind:
      return std::make_unique<AstTextComment>(cast<const comments::TextComment>(comment));
    case comments::Comment::VerbatimBlockLineCommentKind:
      return std::make_unique<AstVerbatimBlockLineComment>(
          cast<const comments::VerbatimBlockLineComment>(comment));
    case comments::Comment::VerbatimLineCommentKind:
      return std::make_unique<AstVerbatimLineComment>(
          cast<const comments::VerbatimLineComment>(comment));

    case comments::Comment::HTMLStartTagCommentKind:
      return std::make_unique<AstHtmlStartTagComment>(
          cast<const comments::HTMLStartTagComment>(comment));
    case comments::Comment::HTMLEndTagCommentKind:
      return std::make_unique<AstHtmlEndTagComment>(
          cast<const comments::HTMLEndTagComment>(comment));

    default:
      llvm::errs() << "Unknown comment kind: " << comment->getCommentKindName() << "\n";
      return nullptr;
  }
}

// clang visitor to extract comments from the AST.
//
// clang comment visitors look for methods named "visit<type>Comment". If the method is found, it is
// called, otherwise the comment visitor tries the parent type of the comment. This allows us to
// specialize the visitor for different types of comments but leaves processing for most comments
// inside the visitComment method.
class CommentVisitor
    : public clang::comments::CommentVisitor<CommentVisitor, std::unique_ptr<AstDocumentation>> {
public:
  CommentVisitor()
      : clang::comments::CommentVisitor<CommentVisitor, std::unique_ptr<AstDocumentation>>()
  {
  }

  // Primary processor for comments. This method is called for all comments which do not have a
  // specialized visitor.
  std::unique_ptr<AstDocumentation> visitComment(const clang::comments::Comment* comment)
  {
    std::unique_ptr<AstDocumentation> rv{AstDocumentation::Create(comment)};
    for (auto child = comment->child_begin(); child != comment->child_end(); child++)
    {
      auto childNode = visit(*child);
      if (childNode)
      {
        rv->AddChild(std::move(childNode));
      }
    }
    return rv;
  };

  // Process a full comment. This is the top level comment type.
  std::unique_ptr<AstDocumentation> visitFullComment(const clang::comments::FullComment* decl)
  {
    //    decl->dump(llvm::outs(), m_context);
    std::unique_ptr<AstDocumentation> rv{AstDocumentation::Create(decl)};
    for (auto child = decl->child_begin(); child != decl->child_end(); child++)
    {
      auto childNode = visit(*child);
      if (childNode)
      {
        rv->AddChild(std::move(childNode));
      }
    }
    return rv;
  };

  // We want to ignore empty paragraph comments, so we need to specialize the visitor for paragraph
  // comments.
  std::unique_ptr<AstDocumentation> visitParagraphComment(
      const clang::comments::ParagraphComment* decl)
  {
    // Ignore empty paragraph clang::comments.
    if (decl->isWhitespace())
    {
      return nullptr;
    }
    std::unique_ptr<AstDocumentation> node{AstDocumentation::Create(decl)};
    for (auto child = decl->child_begin(); child != decl->child_end(); child++)
    {
      auto childNode = visit(*child);
      if (childNode)
      {
        node->AddChild(std::move(childNode));
      }
    }

    return node;
  };

  // We want to ignore empty text comments, so we need to specialize the visitor for text
  // comments.
  std::unique_ptr<AstDocumentation> visitTextComment(const clang::comments::TextComment* tc)
  {
    // Ignore text clang::comments which are whitespace.
    if (tc->isWhitespace())
    {
      return nullptr;
    }
    std::unique_ptr<AstDocumentation> node{AstDocumentation::Create(tc)};
    return node;
  };
};

// Use a commentVisitor to extract all the comments from a comment node.
std::unique_ptr<AstDocumentation> ExtractCommentForDeclaration(
    clang::ASTContext const& context,
    clang::Decl const* decl)
{
  auto comment = context.getCommentForDecl(decl, nullptr);
  if (comment != nullptr)
  {
    CommentVisitor visitor;
    std::unique_ptr<AstDocumentation> doc{visitor.visit(comment)};
    return doc;
  }
  return nullptr;
}

std::string_view AstDocumentation::GetCommandMarker(clang::comments::CommandMarkerKind marker)
{
  switch (marker)
  {
    case clang::comments::CommandMarkerKind::CMK_At:
      return "@";
    case clang::comments::CommandMarkerKind::CMK_Backslash:
      return "\\";
  }
  throw std::runtime_error("Unknown command marker kind.");
}
