// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT
#pragma once
#include <string>

/** 
* Represents a Message reported in an ApiView.
*/
struct ApiViewMessage
{
  enum class MessageLevel
  {
    None = 0,
    Info = 1,
    Warning = 2,
    Error = 3
  };
  std::string_view DiagnosticId;
  std::string_view HelpLinkUri;
  std::string TargetId;
  std::string_view DiagnosticText;
  MessageLevel Level;
};

enum class ApiViewMessages
{
  MissingDocumentation, // "Missing documentation for {0}"
  TypeDeclaredInGlobalNamespace, // Type Declared in Global Namespace
  TypeDeclaredInNamespaceOutsideFilter, // Type Declared in non-filtered namespace.
  UnscopedEnumeration, // Non enum class enums
                       // (https://isocpp.github.io/CppCoreGuidelines/CppCoreGuidelines#Renum-class)
  NonConstStaticFields, // Non-const static fields
  ProtectedFieldsInFinalClass, // Protected fields in final class
  InternalTypesInNonCorePackage, // Internal types in a non-core package
  ImplicitConstructor, // Constructor for a type is not marked "explicit".
  UsingDirectiveFound, // "using namespace" directive found.
  ImplicitOverride, // Implicit override of virtual method.
  NonVirtualDestructor, // Destructor of non-final class is not virtual.
  TypedefInGlobalNamespace, // A type contains a non-builtin value in the global namespace.
};
