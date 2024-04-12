// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#include <iostream>

namespace MyTest {

class M1 {
  int intVal{};

  bool isIntValEqual(const M1& other) const { return intVal == other.intVal; }
};

namespace _detail {

  class DetailedClass {
    int intVal{};

    bool isIntValEqual(const DetailedClass& other) const { return intVal == other.intVal; }
  };
} // namespace _detail

class M2 {
  int intVal{};

  bool isIntValEqual(const M2& other) const { return intVal == other.intVal; }
  friend class M1;
  friend class _detail::DetailedClass;
  friend std::ostream& operator<<(std::ostream& os, const M2& m2);
};

std::ostream& operator<<(std::ostream& os, const M2& m2)
{
  os << m2.intVal;
  return os;
}
} // namespace MyTest
