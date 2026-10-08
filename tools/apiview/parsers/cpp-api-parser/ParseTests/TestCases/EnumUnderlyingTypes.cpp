// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

using Byte = unsigned char;
typedef unsigned short Word;
#define ENUM_BASE unsigned char

enum Plain
{
};
enum PlainInt : int
{
};
enum PlainByte : unsigned char
{
};
enum class ImplicitClass
{
};
enum struct ImplicitStruct
{
};
enum class ExplicitInt : int
{
};
enum struct ExplicitByte : unsigned char
{
};
enum class AliasBase : Byte
{
};
enum class TypedefBase : Word
{
};
enum class MacroBase : ENUM_BASE
{
};
enum class OpaqueClass;
enum struct OpaqueStruct;
enum class OpaqueInt : int;
enum struct OpaqueByte : unsigned char;
enum OpaquePlain : unsigned short;
enum class RedeclaredImplicit : int;
enum class RedeclaredImplicit
{
};
enum class RedeclaredExplicit;
enum class RedeclaredExplicit : int
{
};
enum struct RedeclaredStruct : int;
enum struct RedeclaredStruct
{
};
enum RedeclaredPlain : unsigned char;
enum RedeclaredPlain : unsigned char
{
};
