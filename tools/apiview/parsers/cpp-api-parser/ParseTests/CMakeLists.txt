cmake_minimum_required(VERSION 3.15)

set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

project(ApiViewTests)

# GTest
enable_testing()
find_package(GTest CONFIG REQUIRED)
include(GoogleTest)


# Unit Tests
add_executable(parseTests 
    tests.cpp 
    TestCases/SimpleTest.cpp
    TestCases/TemplateTests.cpp
    TestCases/ClassesWithInternalAndDetail.cpp 
    TestCases/ExpressionTests.cpp
    TestCases/UsingNamespace.cpp
    TestCases/DestructorTests.cpp
    TestCases/DocumentationTests.cpp)
add_dependencies(parseTests ApiViewProcessor)

target_include_directories(parseTests PRIVATE ${ApiViewProcessor_SOURCE_DIR})

# Link test executable against gtest & gtest_main
target_link_libraries(parseTests PRIVATE GTest::gtest_main ApiViewProcessor)
gtest_discover_tests( parseTests )
#add_test(parseTests parseTest)

add_custom_command(TARGET parseTests POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_directory ${CMAKE_CURRENT_SOURCE_DIR}/TestCases ${CMAKE_CURRENT_BINARY_DIR}/Tests
    COMMENT 'Copying test collateral files.'
  )



