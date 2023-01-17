#include "ApiViewProcessor.hpp"

void AzureClassesDatabase::CreateApiViewMessage(
    ApiViewMessages diagnostic,
    std::string_view const& targetId)
{
  ApiViewMessage newMessage;

  switch (diagnostic)
  {
    case ApiViewMessages::MissingDocumentation: {
      newMessage.DiagnosticId = "CPA0001";
      newMessage.DiagnosticText = "Missing Documentation for Node";
      newMessage.Level = ApiViewMessage::MessageLevel::Warning;
      break;
    }
    case ApiViewMessages::TypeDeclaredInGlobalNamespace: {
      newMessage.DiagnosticId = "CPA0002";
      newMessage.DiagnosticText = "Type declared in global namespace. This type will be "
                                  "visible to all other types in the "
                                  "application. Consider moving it to a namespace.",
      newMessage.Level = ApiViewMessage::MessageLevel::Error;
      break;
    }
    case ApiViewMessages::TypeDeclaredInNamespaceOutsideFilter: {
      newMessage.DiagnosticId = "CPA0003";
      newMessage.DiagnosticText
          = "Type declared in namespace which was not included in the ApiView filter.",
          newMessage.Level = ApiViewMessage::MessageLevel::Info;
      break;
    }
    case ApiViewMessages::UnscopedEnumeration: {
      newMessage.DiagnosticId = "CPA0004";
      newMessage.HelpLinkUri
          = "https://isocpp.github.io/CppCoreGuidelines/CppCoreGuidelines#Renum-class";
      newMessage.DiagnosticText
          = "Enumeration declared which was not a scoped enumeration. Consider using a scoped "
            "enumeration instead.",
          newMessage.Level = ApiViewMessage::MessageLevel::Error;
      break;
    }
    case ApiViewMessages::NonConstStaticFields: {
      newMessage.DiagnosticId = "CPA0005";
      newMessage.DiagnosticText
          = "Static field declared which is not 'const'. Consider making it 'const'.";
      newMessage.Level = ApiViewMessage::MessageLevel::Warning;
      break;
    }
    case ApiViewMessages::ProtectedFieldsInFinalClass: {
      newMessage.DiagnosticId = "CPA0006";
      newMessage.DiagnosticText = "Protected field declared in a class marked as 'final'. "
                                  "Consider making the field private.";
      newMessage.Level = ApiViewMessage::MessageLevel::Warning;
      break;
    }
    case ApiViewMessages::InternalTypesInNonCorePackage: {
      newMessage.DiagnosticId = "CPA0007";
      newMessage.DiagnosticText
          = "'internal' types declared in a non-common package. Consider putting the type in the '_detail' namespace.";
      newMessage.Level = ApiViewMessage::MessageLevel::Warning;
      break;
    }
    case ApiViewMessages::ImplicitConstructor: {
      newMessage.DiagnosticId = "CPA0008";
      newMessage.DiagnosticText
          = "Implicit Constructor is found. Constructors should be marked 'explicit'";
      newMessage.Level = ApiViewMessage::MessageLevel::Info;
      break;
    }
  }
  newMessage.TargetId = targetId;
  m_diagnostics.push_back(std::move(newMessage));
}
