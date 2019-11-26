﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using System;
using System.Diagnostics;
using System.Reflection;

namespace Roslyn.Utilities
{
    internal static class EnumUtilities
    {
        /// <summary>
        /// Convert a boxed primitive (generally of the backing type of an enum) into a ulong.
        /// </summary>
        /// <remarks>
        /// </remarks>
        internal static ulong ConvertEnumUnderlyingTypeToUInt64(object value, SpecialType specialType)
        {
            Debug.Assert(value != null);
            Debug.Assert(value.GetType().GetTypeInfo().IsPrimitive);

            unchecked
            {
                return specialType switch
                {
                    SpecialType.System_SByte => (ulong)(sbyte)value,
                    SpecialType.System_Int16 => (ulong)(short)value,
                    SpecialType.System_Int32 => (ulong)(int)value,
                    SpecialType.System_Int64 => (ulong)(long)value,
                    SpecialType.System_Byte => (byte)value,
                    SpecialType.System_UInt16 => (ushort)value,
                    SpecialType.System_UInt32 => (uint)value,
                    SpecialType.System_UInt64 => (ulong)value,

                    // not using ExceptionUtilities.UnexpectedValue() because this is used by the Services layer
                    // which doesn't have those utilities.
                    _ => throw new InvalidOperationException(string.Format("{0} is not a valid underlying type for an enum", specialType)),
                };
            }
        }

        internal static T[] GetValues<T>() where T : struct
        {
            return (T[])Enum.GetValues(typeof(T));
        }

#if DEBUG
        internal static bool ContainsAllValues<T>(int mask) where T : struct, Enum, IConvertible
        {
            foreach (T value in GetValues<T>())
            {
                int val = value.ToInt32(null);
                if ((val & mask) != val)
                {
                    return false;
                }
            }
            return true;
        }
#endif
    }
}
