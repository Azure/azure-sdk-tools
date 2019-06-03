// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.Common.Utilities
{
    using System;
    using System.ComponentModel;
    using System.Reflection;

    public static class ExtensionMethods
    {
        /// <summary>
        /// Converts log verbosity to message importance level
        /// 
        /// LOGGING GUIDELINES FOR EACH VERBOSITY LEVEL:
        /// 1) Quiet -- only display a summary at the end of build
        /// 2) Minimal -- only display errors, warnings, high importance events and a build summary
        /// 3) Normal -- display errors, warnings, high importance events, some status events, and a build summary
        /// 4) Detailed -- display all errors, warnings, high and normal importance events, all status events, and a build summary
        /// 5) Diagnostic -- display all events, and a build summary
        /// 
        /// MessageImportance.Low ==> Detailed and above
        /// MessageImportance.Normal ==> Normal and above
        /// MessageImportance.High ==> Minimal and above
        /// 
        /// </summary>
        /// <param name="verboseLevel"></param>
        /// <returns></returns>
        //public static MessageImportance GetMessageImportance(this NetSdkTaskVerbosityLevel verboseLevel)
        //{
        //    MessageImportance impLevel = MessageImportance.Normal;

        //    if(verboseLevel > NetSdkTaskVerbosityLevel.Normal)
        //    {
        //        impLevel = MessageImportance.Low;
        //    }
        //    else if (verboseLevel > NetSdkTaskVerbosityLevel.Minimal)
        //    {
        //        impLevel = MessageImportance.Normal;
        //    }
        //    else if (verboseLevel == NetSdkTaskVerbosityLevel.Minimal)
        //    {
        //        impLevel = MessageImportance.High;
        //    }

        //    return impLevel;
        //}
      
        //public static string GetEsrpOperationCode(this SignCertEnum EsrpOperationType)
        //{
        //    //MemberInfo mi = EsrpOperationType.GetType().GetField(EsrpOperationType.ToString());
        //    //DescriptionAttribute foo = Attribute.GetCustomAttribute(mi, typeof(DescriptionAttribute)) as DescriptionAttribute;
        //    return GetAttributeForField<SignCertificateTypeAttribute>(EsrpOperationType).SignCertOperationCode;
        //}

        //public static string GetAttributeInfo(this SignCertEnum esrpOperationType, Func<SignCertificateTypeAttribute, string> attributeDelegate)
        //{
        //    SignCertificateTypeAttribute at = GetAttributeForField<SignCertificateTypeAttribute>(esrpOperationType);
        //    return attributeDelegate(at);
        //}

        private static T GetAttributeForField<T>(Enum enumMember) where T : Attribute
        {   
            MemberInfo mi = enumMember.GetType().GetField(enumMember.ToString());
            T attrib = Attribute.GetCustomAttribute(mi, typeof(T)) as T;
            return attrib;
        }

        //public static T GetAttributeForField<T>(Enum enumMember) where T : Attribute
        //{
        //    MemberInfo mi = enumMember.GetType().GetField(enumMember.ToString());
        //    T attrib = Attribute.GetCustomAttribute(mi, typeof(T)) as T;
        //    return attrib;
        //}
    }
}
