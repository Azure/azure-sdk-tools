// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#pragma once
#include "JsonDumper.hpp"
#include <algorithm>
#include <optional>
#include <stdexcept>
#include <unordered_map>
#include <unordered_set>
#include <vector>

class TreeJsonDumper : public JsonDumper {
  enum class TokenKind
  {
    Text = 0,
    Punctuation = 1,
    Keyword = 2,
    TypeName = 3,
    MemberName = 4,
    StringLiteral = 5,
    Literal = 6,
    Comment = 7,
    ExternalUrl = 8
  };

  struct Line
  {
    nlohmann::json Content = {{"Tokens", nlohmann::json::array()}};
    std::vector<size_t> Children;
  };
  struct Scope
  {
    size_t Parent;
    bool MergeOpeningBrace;
  };

  std::vector<Line> m_lines;
  std::vector<size_t> m_roots;
  std::vector<Scope> m_scopes;
  nlohmann::json m_line = {{"Tokens", nlohmann::json::array()}};
  std::vector<std::string> m_lineDefinitions;
  std::unordered_set<std::string> m_definitions;
  std::unordered_map<std::string, std::string> m_lineIds;
  std::unordered_map<std::string, size_t> m_namespaceCounts;
  std::string m_closedScopeId;
  std::optional<std::string> m_externalUrl;
  bool m_documentation{};
  bool m_deprecated{};
  bool m_skipDiff{};

  void AddToken(std::string_view value, TokenKind kind)
  {
    nlohmann::json token{{"Kind", kind}, {"Value", value}, {"HasSuffixSpace", false}};
    if (m_documentation)
    {
      token["IsDocumentation"] = true;
    }
    if (m_deprecated)
    {
      token["IsDeprecated"] = true;
    }
    if (m_skipDiff)
    {
      token["SkipDiff"] = true;
    }
    if (m_externalUrl)
    {
      token["Kind"] = TokenKind::ExternalUrl;
      token["NavigateToId"] = *m_externalUrl;
      token["RenderClasses"] = {"comment"};
    }
    m_line["Tokens"].push_back(std::move(token));
  }

  void AddDefinition(std::string_view id)
  {
    if (id.empty())
    {
      return;
    }
    if (!m_definitions.emplace(id).second)
    {
      throw std::runtime_error("Duplicate DefinitionId: " + std::string(id));
    }
    // Legacy rendering uses the last definition on a line as its comment ID.
    m_line["LineId"] = id;
    m_lineDefinitions.emplace_back(id);
  }

  void FinishLine()
  {
    if (m_line["Tokens"].empty() && !m_lines.empty() && !m_lines.back().Content["Tokens"].empty()
        && m_lines.back().Content["Tokens"][0].value("IsDocumentation", false))
    {
      m_line["Tokens"].push_back(
          {{"Kind", TokenKind::Comment},
           {"Value", ""},
           {"HasSuffixSpace", false},
           {"IsDocumentation", true}});
    }
    if (!m_scopes.empty() && m_scopes.back().MergeOpeningBrace && m_line["Tokens"].size() == 1
        && m_line["Tokens"][0]["Value"] == "{")
    {
      auto& heading = m_lines[m_scopes.back().Parent].Content["Tokens"];
      heading.push_back({{"Kind", TokenKind::Text}, {"Value", " "}, {"HasSuffixSpace", false}});
      heading.push_back(m_line["Tokens"][0]);
      m_scopes.back().MergeOpeningBrace = false;
    }
    else
    {
      if (m_line.contains("LineId"))
      {
        for (auto const& id : m_lineDefinitions)
        {
          m_lineIds.emplace(id, m_line["LineId"].get<std::string>());
        }
      }
      if (!m_closedScopeId.empty())
      {
        m_line["RelatedToLine"] = m_closedScopeId;
        m_line["IsContextEndLine"] = true;
        m_closedScopeId.clear();
      }
      auto index = m_lines.size();
      if (m_scopes.empty())
      {
        m_roots.push_back(index);
      }
      else
      {
        m_lines[m_scopes.back().Parent].Children.push_back(index);
      }
      m_lines.push_back({std::move(m_line), {}});
    }
    m_line = {{"Tokens", nlohmann::json::array()}};
    m_lineDefinitions.clear();
  }

  nlohmann::json BuildLine(size_t index) const
  {
    auto line = m_lines[index].Content;
    for (auto& token : line["Tokens"])
    {
      if (token.contains("NavigateToId") && token["Kind"] != TokenKind::ExternalUrl)
      {
        auto target = m_lineIds.find(token["NavigateToId"].get<std::string>());
        if (target != m_lineIds.end())
        {
          token["NavigateToId"] = target->second;
        }
      }
    }
    if (!m_lines[index].Children.empty())
    {
      line["Children"] = nlohmann::json::array();
      for (auto child : m_lines[index].Children)
      {
        line["Children"].push_back(BuildLine(child));
      }
    }
    return line;
  }

  static void SetRange(bool& state, bool value, std::string_view name)
  {
    if (state == value)
    {
      throw std::runtime_error("Unbalanced " + std::string(name) + " range");
    }
    state = value;
  }

  static void NormalizeNavigation(nlohmann::json& items)
  {
    for (auto& item : items)
    {
      if (!item.contains("ChildItems"))
      {
        item["ChildItems"] = nlohmann::json::array();
      }
      NormalizeNavigation(item["ChildItems"]);
    }
  }

public:
  using JsonDumper::JsonDumper;

  void DumpMessageNode(ApiViewMessage const& message) override
  {
    auto treeMessage = message;
    if (!message.CanonicalTargetId.empty())
    {
      treeMessage.TargetId = message.CanonicalTargetId;
    }
    JsonDumper::DumpMessageNode(treeMessage);
  }

  nlohmann::json GetJson()
  {
    if (!m_line["Tokens"].empty())
    {
      FinishLine();
    }
    if (!m_scopes.empty() || m_documentation || m_deprecated || m_skipDiff || m_externalUrl)
    {
      throw std::runtime_error("Unclosed tree scope or token range");
    }
    auto json = JsonDumper::GetJson();
    json.erase("Tokens");
    json["PackageVersion"] = json.value("PackageVersion", "");
    json["ParserVersion"] = "1.0.0";
    if (json.contains("Navigation"))
    {
      NormalizeNavigation(json["Navigation"]);
    }
    json["ReviewLines"] = nlohmann::json::array();
    for (auto index : m_roots)
    {
      json["ReviewLines"].push_back(BuildLine(index));
    }
    if (json.contains("Diagnostics"))
    {
      for (auto& diagnostic : json["Diagnostics"])
      {
        diagnostic["Level"] = diagnostic.value("Level", 1);
        auto target = m_lineIds.find(diagnostic["TargetId"].get<std::string>());
        if (target != m_lineIds.end())
        {
          diagnostic["TargetId"] = target->second;
        }
      }
    }
    return json;
  }

  void DumpToFile(std::ostream& outfile) { outfile << GetJson(); }

  void BeginChildScope(std::string_view const& id, bool mergeOpeningBrace) override
  {
    if (m_lines.empty() || !m_line["Tokens"].empty())
    {
      throw std::runtime_error("Tree scope must follow a completed heading line");
    }
    auto parent = m_lines.size() - 1;
    auto& heading = m_lines[parent].Content;
    if (!id.empty())
    {
      auto count = ++m_namespaceCounts[std::string(id)];
      auto lineId = std::string(id);
      if (count > 1)
      {
        lineId += " scope " + std::to_string(count);
      }
      if (!m_definitions.emplace(lineId).second)
      {
        throw std::runtime_error("Duplicate namespace LineId: " + lineId);
      }
      heading["LineId"] = lineId;
      m_lineIds.emplace(std::string(id), lineId);
      for (auto& token : heading["Tokens"])
      {
        if (token["Kind"] == TokenKind::TypeName)
        {
          token["NavigationDisplayName"] = token["Value"];
          token["RenderClasses"] = {"namespace"};
        }
      }
    }
    m_scopes.push_back({parent, mergeOpeningBrace});
  }

  void EndChildScope() override
  {
    if (m_scopes.empty() || !m_line["Tokens"].empty())
    {
      throw std::runtime_error("Unbalanced tree scope");
    }
    m_closedScopeId = m_lines[m_scopes.back().Parent].Content.value("LineId", "");
    m_scopes.pop_back();
  }

  void InsertWhitespace(int count) override
  {
    if (count < 0)
    {
      throw std::runtime_error("Negative whitespace count");
    }
    auto visibleCount = count;
    if (m_line["Tokens"].empty())
    {
      visibleCount -= std::min(count, static_cast<int>(m_scopes.size()) * 2);
    }
    if (visibleCount > 0)
    {
      AddToken(std::string(visibleCount, ' '), TokenKind::Text);
    }
    UpdateCursor(count);
  }
  void InsertNewline() override { FinishLine(); }
  void InsertKeyword(std::string_view const& value) override
  {
    AddToken(value, TokenKind::Keyword);
    UpdateCursor(value.size());
  }
  void InsertText(std::string_view const& value) override
  {
    AddToken(value, TokenKind::Text);
    UpdateCursor(value.size());
  }
  void InsertPunctuation(char value) override
  {
    AddToken(std::string(1, value), TokenKind::Punctuation);
    UpdateCursor(1);
  }
  void InsertLineIdMarker() override {}
  void InsertIdentifier(std::string_view const& value) override
  {
    AddToken(value, TokenKind::TypeName);
    UpdateCursor(value.size());
  }
  void InsertTypeName(std::string_view const& value, std::string_view const& id) override
  {
    AddToken(value, TokenKind::TypeName);
    AddDefinition(id);
    auto& token = m_line["Tokens"].back();
    token["NavigateToId"] = id;
    if (!id.empty())
    {
      std::string kind = "method";
      for (auto const& part : m_line["Tokens"])
      {
        if (part["Kind"] == TokenKind::Keyword)
        {
          if (part["Value"] == "enum")
          {
            kind = "enum";
            break;
          }
          if (part["Value"] == "class" || part["Value"] == "struct" || part["Value"] == "union")
          {
            kind = "class";
          }
        }
      }
      token["NavigationDisplayName"] = value;
      token["RenderClasses"] = {kind};
    }
    UpdateCursor(value.size());
  }
  void InsertMemberName(std::string_view const& value, std::string_view const& id) override
  {
    AddToken(value, TokenKind::MemberName);
    AddDefinition(id);
  }
  void InsertStringLiteral(std::string_view const& value) override
  {
    AddToken(value, TokenKind::StringLiteral);
    UpdateCursor(value.size());
  }
  void InsertLiteral(std::string_view const& value) override
  {
    AddToken(value, TokenKind::Literal);
    UpdateCursor(value.size());
  }
  void InsertComment(std::string_view const& value) override
  {
    AddToken(value, TokenKind::Comment);
    UpdateCursor(value.size());
  }
  void AddExternalLinkStart(std::string_view const& value) override
  {
    if (m_externalUrl)
    {
      throw std::runtime_error("Nested external link");
    }
    m_externalUrl = value;
  }
  void AddExternalLinkEnd() override
  {
    if (!m_externalUrl)
    {
      throw std::runtime_error("Unbalanced external link");
    }
    m_externalUrl.reset();
  }
  void AddDocumentRangeStart() override { SetRange(m_documentation, true, "documentation"); }
  void AddDocumentRangeEnd() override { SetRange(m_documentation, false, "documentation"); }
  void AddDeprecatedRangeStart() override { SetRange(m_deprecated, true, "deprecated"); }
  void AddDeprecatedRangeEnd() override { SetRange(m_deprecated, false, "deprecated"); }
  void AddSkipDiffRangeStart() override { SetRange(m_skipDiff, true, "skip-diff"); }
  void AddSkipDiffRangeEnd() override { SetRange(m_skipDiff, false, "skip-diff"); }
};
