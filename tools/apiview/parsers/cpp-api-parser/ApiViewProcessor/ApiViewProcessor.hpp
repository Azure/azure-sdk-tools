// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#pragma once

// #include "AstNode.hpp"
#include <map>
#include <memory>
#include <nlohmann/json.hpp>
#include <optional>
#include <set>
#include <string>
#include <string_view>
#include <vector>

class ApiViewProcessorImpl;
struct AstNode;

namespace clang {
class Decl;
class NamedDecl;
} // namespace clang

class AstDumper;

class TypeHierarchy {

public:
  enum class TypeHierarchyClass
  {
    Unknown,
    Class,
    Interface,
    Struct,
    Enum,
    Delegate,
    Assembly,
    Namespace,
  };

  struct TypeHierarchyNode
  {
  public:
    TypeHierarchyNode(
        std::string const& name,
        std::string const& navigationId,
        TypeHierarchyClass typeClass)
        : NodeName{name}, NavigationId{navigationId}, NodeClass{typeClass}
    {
    }

    std::string NodeName;
    std::string NavigationId;
    TypeHierarchyClass NodeClass;
    std::map<std::string, std::shared_ptr<TypeHierarchyNode>> Children;
    std::shared_ptr<TypeHierarchyNode> InsertChildNode(
        std::string_view const& nodeName,
        std::string_view const& navigationId,
        TypeHierarchyClass nodeClass);
  };

  TypeHierarchy() = default;
  std::shared_ptr<TypeHierarchyNode> GetNamespaceRoot(std::string_view const& ns);
  void Dump(AstDumper* dumper) const;

private:
  std::map<std::string, std::shared_ptr<TypeHierarchyNode>> m_namespaceRoots;
};

class ApiViewProcessorImpl;
struct ApiViewDiagnostic;

class AzureClassesDatabase {
  std::vector<std::unique_ptr<AstNode>> m_typeList;
  std::vector<std::unique_ptr<ApiViewDiagnostic>> m_diagnostics;
  TypeHierarchy m_typeHierarchy;
  ApiViewProcessorImpl* m_processor;

  bool IsMemberOfObject(clang::NamedDecl const* decl);

public:
  AzureClassesDatabase(ApiViewProcessorImpl* processor);
  ~AzureClassesDatabase();

  TypeHierarchy* GetTypeHierarchy() { return &m_typeHierarchy; }

  void CreateAstNode(clang::NamedDecl* namedNode);
  void CreateAstNode(); // Create a terminal AstNode which is used to close out all outstanding
                        // namespaces.

  void DumpClassDatabase(AstDumper* dumper) const;
  const std::vector<std::unique_ptr<AstNode>>& GetAstNodeMap() const { return m_typeList; }
};


// Isolation class to isolate clang implemetnation and headers from consumers of the
// ApiViewProcessor. Forwards all methods to ApiViewProcessorImpl class.
class ApiViewProcessor {

  std::unique_ptr<ApiViewProcessorImpl> m_processorImpl;

public:
  ApiViewProcessor(
      std::string_view const& pathToProcessor,
      std::string_view const apiViewSettings = "ApiViewSettings.json");
  ApiViewProcessor(std::string_view const& pathToProcessor, nlohmann::json const& apiViewSettings);

  ~ApiViewProcessor();

  int ProcessApiView();

  std::unique_ptr<AzureClassesDatabase> const& GetClassesDatabase();
  std::string_view const ReviewName();
  std::string_view const ServiceName();
  std::string_view const PackageName();
};
