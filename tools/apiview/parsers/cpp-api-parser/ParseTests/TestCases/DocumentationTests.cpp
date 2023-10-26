// Copyright (c) Microsoft Corporation. All rights reserved.
// SPDX-License-Identifier: MIT

/**
 * @file DoxygenDemo.hpp
 * @brief A class skeleton which demonstrates all the doxygen special commands.
 *
 * This file contains the class skeleton which demonstrates all the doxygen special commands.
 *
 * @author [Azure SDK Tools]
 *
 */

#ifndef DOXYGEN_DEMO_HPP
#define DOXYGEN_DEMO_HPP

#include <cerrno>
#include <map>
#include <string>

/**
 * Global integer value.
 */
extern int external;

// A class which demontrates all the doxygen documentation features.

/**
 * @brief A class that demonstrates all the doxygen special commands.
 *
 * This class demonstrates all the doxygen special commands.
 *
 */
class DoxygenDemo {
public:
  /**
   * @brief A constructor.
   *
   * A more elaborate description of the constructor.
   *
   */
  DoxygenDemo();

  /**
   * @brief A destructor.
   *
   * A more elaborate description of the destructor.
   *
   */
  ~DoxygenDemo();

  /**
   * @brief A normal member taking two arguments and returning an integer value.
   *
   * This function takes two arguments and returns an integer value.
   *
   * @param a An integer argument.
   * @param s A constant character pointer.
   * @see DoxygenDemo()
   * @see ~DoxygenDemo()
   * @see testMeToo()
   * @see publicVar()
   * @return The test results.
   */
  int testMe(int a, const char* s);

  /**
   * @brief A normal member taking no arguments and returning nothing.
   *
   * This function takes no arguments and returns nothing.
   *
   * @return Nothing.
   */
  void testMeToo();

  /**
   * @brief A public variable.
   *
   * This is a public variable.
   *
   */
  int publicVar;

  /**
   * @brief A normal member taking no arguments and returning nothing.
   *
   * This function takes no arguments and returns nothing.
   *
   * @return Nothing.
   * @verbatim
   * This is a verbatim directive.
   * @endverbatim
   * 
   * \cond
   * This is a set of documentation that should be excluded.
   * \endcond
   */
  void verbatimDirective();

  /**
   * \brief A normal member taking no arguments and returning nothing.
   *
   * This function takes no @a arguments @p and returns @b nothing.
   *
   * \return Nothing.
   * \code{.c}
   * int x = 5;
   * int y = 10;
   * int z = x + y;
   * \endcode
   */
  void codeDirective();

  
  /**
   * @brief A normal member taking no arguments and returning nothing.
   *
   * This function takes no arguments and returns nothing. It is a multi-line paragraph ending on a \a break.
   *
   * This is what happens when you have a hypertext link: <a href="https://www.example.com">An
   * example link.</a>
   *
   * @return \a Nothing.
   * 
   * @link https://www.example.com An example link. @endlink
   */
  void linkDirective();


private:
  /**
   * @brief A private variable.
   *
   * This is a private variable.
   *
   */
  int privateVar;

  /**
   * @brief \b A normal member taking no arguments and returning nothing.
   *
   * This function takes no arguments and returns nothing. It is a multi-line paragraph ending on a \a break.
   * 
   * This is what happens when you have a hypertext link: <a href=https://www.example.com'>An example link.</a>
   *
   * @return \a Nothing.
   */
  void privateFunction();
};

#endif /* DOXYGEN_DEMO_HPP */
