# CMakeList.txt : CMake project for ParseAzureSdkCpp, include source and define
# project specific logic here.
#
cmake_minimum_required (VERSION 3.15)
project(ApiViewDriver)

add_executable(ParseAzureSdkCpp ParseAzureSdkCpp.cpp )
add_dependencies(ParseAzureSdkCpp ApiViewProcessor)
target_include_directories(ParseAzureSdkCpp PRIVATE ${ApiViewProcessor_SOURCE_DIR})

# Debug builds of azure keyvault administration require a larger stack size to avoid stack overflow
#if(MSVC)
target_link_options(ParseAzureSdkCpp PRIVATE /STACK:2000000)
#endif()

target_link_libraries(ParseAzureSdkCpp
  PRIVATE
  ApiViewProcessor
  )
