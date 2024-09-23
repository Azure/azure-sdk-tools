// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

namespace Test {

class BaseClassWithVirtualDestructor {
  int* member{};

public:
  virtual ~BaseClassWithVirtualDestructor()
  {
    if (member)
    {
      delete member;
    }
  };
};

class FinalBaseClassWithVirtualDestructor final {
  int* member{};

public:
  virtual ~FinalBaseClassWithVirtualDestructor()
  {
    if (member)
    {
      delete member;
    }
  };
};

class BaseClassWithNonVirtualDestructor {
  int* member{};

public:
  ~BaseClassWithNonVirtualDestructor()
  {
    if (member)
    {
      delete member;
    }
  };
};

class FinalBaseClassWithNonVirtualDestructor final {
  int* member{};

public:
  ~FinalBaseClassWithNonVirtualDestructor()
  {
    if (member)
    {
      delete member;
    }
  };
};

class BaseClassWithProtectedDestructor {
  int* member{};

protected:
  ~BaseClassWithProtectedDestructor()
  {
    if (member)
    {
      delete member;
    }
  };
};
class FinalBaseClassWithProtectedDestructor final {
  int* member{};

protected:
  ~FinalBaseClassWithProtectedDestructor()
  {
    if (member)
    {
      delete member;
    }
  };
};

class DerivedClassWithVirtualDestructor : public BaseClassWithVirtualDestructor {
  int* member{};
  ~DerivedClassWithVirtualDestructor()
  {
    if (member)
    {
      delete member;
    }
  };
};

class DerivedClassWithNonVirtualDestructor : public BaseClassWithNonVirtualDestructor {
  int* member{};
  ~DerivedClassWithNonVirtualDestructor()
  {
    if (member)
    {
      delete member;
    }
  };
};

class DerivedClassWithProtectedDestructor : public BaseClassWithProtectedDestructor {
  int* member{};

protected:
  virtual ~DerivedClassWithProtectedDestructor()
  {
    if (member)
    {
      delete member;
    }
  };
};

class FinalClassWithPrivateDestructor final {
  int* member{};
  ~FinalClassWithPrivateDestructor()
  {
    if (member)
    {
      delete member;
    }
  };
};

class ClassWithPrivateDestructor {
  int* member{};
  ~ClassWithPrivateDestructor()
  {
    if (member)
    {
      delete member;
    }
  };
};

} // namespace Test
