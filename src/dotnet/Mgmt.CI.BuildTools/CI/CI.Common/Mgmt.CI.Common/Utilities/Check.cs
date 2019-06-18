// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

//namespace Microsoft.WindowsAzure.Build.Tasks.Utilities
namespace MS.Az.Mgmt.CI.BuildTasks.Common.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Utility to check Null/Empty and throw appropriate exceptions
    /// </summary>
    public static class Check
    {
        #region Non-Negative Number

        /// <summary>
        /// Checks if provided string value is greater than 0
        /// </summary>
        /// <param name="number"></param>
        /// <param name="argumentName"></param>
        public static void NonNegativeNumber(string number, string argumentName = "argument")
        {
            string exceptionString = string.Format("Provided value '{0}' for argument '{1}' should be greater than zero", number, argumentName);

            if (long.TryParse(number, out long num))
            {
                if(num <= 0)
                {
                    throw new ArgumentOutOfRangeException(exceptionString);
                }
            }
        }


        /// <summary>
        /// Checks if the provided int and long values are non-negative
        /// TODO:
        /// There are other ways to implement this, but currently this is our top scenarios
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="number"></param>
        /// <param name="argumentName"></param>
        static void NonNegativeNumber<T>(T number, string argumentName = "argument") 
        {
            string typeName = typeof(T).Name;
            bool throwException = false;

            string exceptionString = string.Format("Only int32 and int64 is supported. Provided type '{0}' is not a valid applicable type for argument '{1}'", typeof(T).Name, argumentName);

            if(!IsNumericType(number))
            {
                throw new ArgumentOutOfRangeException(exceptionString);
            }

            if (!(typeof(T).Equals(typeof(Int32))) || !(typeof(T).Equals(typeof(long))))
            {
                throwException = true;
            }
            else if ((typeof(T).Equals(typeof(Int32))) || (typeof(T).Equals(typeof(long))))
            {
                if(typeName.Equals(typeof(Int32).Name, StringComparison.OrdinalIgnoreCase))
                {
                    int intNum = Convert.ToInt32(number);
                    if(intNum < 0)
                    {
                        throwException = true;
                    }
                }
                else if (typeName.Equals(typeof(Int64).Name, StringComparison.OrdinalIgnoreCase))
                {
                    Int64 intNum = Convert.ToInt64(number);
                    if (intNum < 0)
                    {
                        throwException = true;
                    }
                }
            }

            if(throwException == true)
            {
                throw new ArgumentOutOfRangeException(exceptionString);
            }
        }

        static bool IsNumericType(object o)
        {
            switch (Type.GetTypeCode(o.GetType()))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }

            //return type.IsPrimitive && type!=typeof(char) && type!=typeof(bool); also ignore IntPtr and UIntPtr
        }

        #endregion


        /// <summary>
        /// Checks if the argument passed in is NotNull
        /// Throws NullReferenceException if the arugment is Null
        /// </summary>
        /// <param name="argument">arument info for logging purpose</param>
        /// <param name="argumentName">argument info for logging</param>
        public static void NotNull(object argument, string argumentName = "argument")
        {
            if (argument == null)
            {
                throw new NullReferenceException(string.Format("{0} provided is null", argumentName));
            }
        }

        /// <summary>
        /// Checks if the string argument passed in is NonEmpty and NotNull
        /// Throws NullReferenceException if the arugment is Null or Empty
        /// </summary>
        /// <param name="argument">arument info for logging purpose</param>
        /// <param name="argumentName">argument info for logging</param>
        public static void NotEmptyNotNull(string argument, string argumentName = "argument")
        {
            if (string.IsNullOrEmpty(argument))
            {
                throw new NullReferenceException(string.Format("{0} provided is either Null or Empty string", argumentName));
            }
        }

        /// <summary>
        /// Checks if the FilePath exists
        /// Throws FileNotFoundException if the filePath does not exists
        /// </summary>
        /// <param name="filePath">file path info for logging purpose</param>
        public static void FileExists(string filePath)
        {
            NotNull(filePath, "File Path");
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(string.Format("'{0}' does not exists. Please check the validity of the path", filePath));
            }
        }

        /// <summary>
        /// Checks if the DirectoryPath exists
        /// Throws DirectoryNotFoundException if the DirectoryPath does not exists
        /// </summary>
        /// <param name="dirPath">Directory Path</param>
        public static void DirectoryExists(string dirPath)
        {
            NotNull(dirPath, "Directory Path");
            if (!Directory.Exists(dirPath))
            {
                throw new DirectoryNotFoundException(string.Format("'{0}' does not exists. Please check the validity of the directory path", dirPath));
            }
        }

        public static void DirectoryExists(List<string> dirPaths, string argumentName = "argument")
        {
            foreach(string dp in dirPaths)
            {
                NotNull(dp, argumentName);
                DirectoryExists(dp);
            }
        }

        public static void Empty<T>(IEnumerable<T> collection, bool expectedResult, string exceptionMessage = "Collection items do not match with expectedResult" )
        {
            if (collection == null) throw new ArgumentNullException(exceptionMessage);

            if(collection.Any<T>() == expectedResult)
            {
                throw new ArgumentException(exceptionMessage);
            }
        }
    }
}
