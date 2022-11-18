// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#pragma once
#include "AstDumper.hpp"
#include <iostream>

class TextDumper : public AstDumper {
  std::ostream& m_stream;

public:
  TextDumper(std::ostream& stream) : m_stream(stream) {}

  virtual void InsertWhitespace(int count) override { m_stream << std::string(count, ' '); }
  virtual void InsertNewline() override { m_stream << std::endl; }
  virtual void InsertKeyword(std::string_view const& keyword) override { m_stream << keyword; }
  virtual void InsertText(std::string_view const& text) override { m_stream << text; }
  virtual void InsertPunctuation(const char punctuation) override { m_stream << punctuation; }
  virtual void InsertLineIdMarker() override { m_stream << "// "; }
  virtual void InsertTypeName(std::string_view const& type, std::string_view const&) override
  {
    m_stream << type;
  }
  virtual void InsertMemberName(std::string_view const& member) override { m_stream << member; }
  virtual void InsertStringLiteral(std::string_view const& str) override { m_stream << str; }
  virtual void InsertLiteral(std::string_view const& str) override { m_stream << str; }
  virtual void InsertComment(std::string_view const& comment) override { m_stream << comment; }
  virtual void AddDocumentRangeStart() override { m_stream << "/*"; }
  virtual void AddDocumentRangeEnd() override { m_stream << "*/"; }
  virtual void AddDeprecatedRangeStart() override { m_stream << "/* ** DEPRECATED **"; }
  virtual void AddDeprecatedRangeEnd() override { m_stream << "/* ** DEPRECATED ** */"; }
  virtual void AddDiffRangeStart() override { m_stream << "/* ** DIFF **"; }
  virtual void AddDiffRangeEnd() override { m_stream << " ** DIFF ** */"; }
  virtual void AddInheritanceInfoStart() override { m_stream << "/* ** INHERITANCE **"; }
  virtual void AddInheritanceInfoEnd() override { m_stream << " ** INHERITANCE ** */"; }
  virtual void DumpTypeHierarchyNode(std::shared_ptr<TypeHierarchy::TypeHierarchyNode> const& node)
  {
    InsertNewline();
  }
};