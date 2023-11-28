# CMakeList.txt : CMake project for ParseAzureSdkCpp, include source and define
# project specific logic here.
#
cmake_minimum_required (VERSION 3.15)
project(ApiViewProcessor)

find_package(llvm CONFIG REQUIRED)

list(APPEND CMAKE_MODULE_PATH "${LLVM_CMAKE_DIR}")

#get_cmake_property(_variableNames VARIABLES)
#foreach (_variableName ${_variableNames})
#message(STATUS "${_variableName}=${${_variableName}}")
#endforeach()

# Set your project compile flags.
# E.g. if using the C++ header files
# you will need to enable C++11 support
# for your compiler.

include(HandleLLVMOptions)
add_definitions(${LLVM_DEFINITIONS})

add_compile_options(/EHsc)
add_library(ApiViewProcessor STATIC AstNode.cpp ApiViewProcessor.cpp ProcessorImpl.cpp AstDumper.cpp  ApiViewMessage.cpp CommentExtractor.cpp)


target_include_directories(ApiViewProcessor PRIVATE ${LLVM_INCLUDE_DIRS})

# Find the libraries that correspond to the LLVM components that we wish to use
llvm_map_components_to_libnames(llvm_libs 
       Support
       Option
       WindowsDriver
       FrontEndOpenMP
)

set(cmakeLibs
clangBasic
clangAST
clangLex
clangParse
clangDriver
clangSema
clangSupport
clangFrontend
clangSerialization
clangTooling
clangEdit
clangAnalysis
)


set(clang_libs)
foreach (clangLib ${cmakeLibs})
find_library(clangLibPath_${clangLib} ${clangLib})
list(APPEND clang_libs ${clangLibPath_${clangLib}})
endforeach()

#message(STATUS "llvm_libs: ${llvm_libs}")
#message(STATUS "clang_libs: ${clang_libs}")

target_link_libraries(ApiViewProcessor
  PUBLIC
  ${llvm_libs}
  ${clang_libs}
  version
  )
