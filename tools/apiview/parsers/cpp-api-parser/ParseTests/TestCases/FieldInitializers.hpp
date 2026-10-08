// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#pragma once

#define FIELD_DEFAULT 42

namespace Azure { namespace FieldDefaults {
  enum class Mode
  {
    Ready,
    Done
  };

  struct Value
  {
    explicit Value(int number = 0);
  };

  struct Pair
  {
    int First;
    int Second;
  };

  int MakeDefault(int input);

  struct AA
  {
    int field = 10;
    int Uninitialized;
    int Negative = -3;
    bool Enabled = true;
    double Ratio = 1.5;
    const char* Label = "hello";
    const char* Escaped = "a\n\"b";
    int* Pointer = nullptr;
    Mode State = Mode::Ready;
    int Expression = (1 + 2) * 3;
    int Conditional = true ? 4 : 5;
    int Macro = FIELD_DEFAULT;
    int Braced{10};
    int Empty{};
    int CopyList = {20};
    Value Constructed{7};
    Value ConstructedEmpty{};
    Value CopyConstructed = Value(8);
    Pair Aggregate{1, 2};
    Pair PartialAggregate{3};
    Pair EmptyAggregate{};
    int Call = MakeDefault(9);
    int (*Factory)(int) = MakeDefault;
    int IndirectCall = Factory(2);

    struct
    {
      int Nested = 11;
    } Anonymous = {12};

    struct
    {
      int Field;
    } BracedAnonymous{13};
  };

  template <typename T> struct Generic
  {
    T Default{};
    T Copy = T();
  };
}} // namespace Azure::FieldDefaults

#undef FIELD_DEFAULT
