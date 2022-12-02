# CMakeList.txt : CMake project for ParseAzureSdkCpp, include source and define
# project specific logic here.
#
cmake_minimum_required (VERSION 3.15)
project(ApiViewDriver)

add_executable(ParseAzureSdkCpp ParseAzureSdkCpp.cpp )
add_dependencies(ParseAzureSdkCpp ApiViewProcessor)
target_include_directories(ParseAzureSdkCpp PRIVATE ${ApiViewProcessor_SOURCE_DIR})

target_link_libraries(ParseAzureSdkCpp
  PRIVATE
  ApiViewProcessor
  )
