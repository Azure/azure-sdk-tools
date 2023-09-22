// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#include <chrono>
#include <map>
#include <string>
#include <vector>

namespace Test { namespace Inner {
  class Fred {};
}} // namespace Test::Inner

using namespace Test::Inner;

namespace A { namespace AB { namespace ABCD {
    char* GlobalFunctionInAABABCD(int character);
}}} // namespace A::AB::ABCD
