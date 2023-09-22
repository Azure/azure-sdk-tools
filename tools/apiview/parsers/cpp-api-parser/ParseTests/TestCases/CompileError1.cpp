// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

#include <chrono>
#include <map>
#include <string>
#include <vector>

namespace Test {

void Function1(std::chrono::system_clock::time_point const&) {}
int Function2(std::chrono::system_clock::time_point const&) { return 0; }

namespace Inner {
  int Function3(std::vector<int> const& vint);
}

} // namespace Test

char* GlobalFunction4(int character)

namespace A { namespace AB {
  namespace ABC {
    std::string& FunctionABC(std::vector<std::map<int, std::string>> param1);
  }
  std::string& FunctionAB(void* vpa, const void* cvpa, void const* vcpa, void* const vpcb)
  namespace ABD { namespace ABE {
      std::string& FunctionABE(
          const std::string cs,
          const std::string& csr,
          volatile std::string volstring);
  }} // namespace ABD::ABE

}} // namespace A::AB
