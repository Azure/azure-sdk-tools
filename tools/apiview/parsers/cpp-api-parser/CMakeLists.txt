# CMakeList.txt : Top-level CMake project file, do global configuration
# and include sub-projects here.
#
cmake_minimum_required (VERSION 3.13)
list(APPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/cmake-modules")

option(MSVC_USE_STATIC_CRT "(MSVC only) Set to ON to link SDK with static CRT (/MT or /MTd switch)." ON)

project (ApiViewCpp)

include(AzureVcpkg)

az_vcpkg_integrate()

enable_testing()

if(MSVC_USE_STATIC_CRT AND MSVC)
  # 1. More about static/shared CRT:
  # https://docs.microsoft.com/cpp/build/reference/md-mt-ld-use-run-time-library?view=msvc-160
  #
  # 2. MSVC_USE_STATIC_CRT build flag approach is used/inspired by libcurl
  # (https://github.com/curl/curl/blob/master/CMakeLists.txt) and some other projects.
  #
  # 3. GTest would emit the following warning:
  # warning LNK4098: defaultlib 'LIBCMTD' conflicts with use of other libs; use /NODEFAULTLIB:library
  # AddGoogleTest.cmake uses gtest_force_shared_crt
  # (see https://github.com/google/googletest/blob/master/googletest/README.md),
  # which respects linker settings that we set below, and our settings below are all in sync.
  #
  # 4. Sometimes, the following approach is recommended instead:
  # +-----------------------------------------------------------------------------------+
  # | # Use the static runtime libraries when building statically                       |
  # | # for consistency with the vcpkg arch-windows-static triplets:                    |
  # | cmake_policy(SET CMP0091 NEW)                                                     |
  # | # see https://cmake.org/cmake/help/v3.15/variable/CMAKE_MSVC_RUNTIME_LIBRARY.html |
  # | if (NOT DEFINED CMAKE_MSVC_RUNTIME_LIBRARY)                                       |
  # |   set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")         |
  # | endif()                                                                           |
  # +-----------------------------------------------------------------------------------+
  # However, it only works when cmake installed is 3.15+;
  # we have to require a minimum of 3.13.
  #
  # 5. We "replace with empty string" (i.e. remove) first, then add, so that '/MT'
  # will be present (and present once) even if '/MD' was not.
  message(STATUS "Configuring Static Runtime Library.")
  if(${CMAKE_CXX_FLAGS} MATCHES ".*/MD.*")
  string(REGEX REPLACE "/MD" "" CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS}")
  set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} /MT")
  endif()
  
  if(${CMAKE_CXX_FLAGS_RELEASE} MATCHES ".*/MD.*")
  string(REGEX REPLACE "/MD" "" CMAKE_CXX_FLAGS_RELEASE "${CMAKE_CXX_FLAGS_RELEASE}")
  set(CMAKE_CXX_FLAGS_RELEASE "${CMAKE_CXX_FLAGS_RELEASE} /MT")
  endif()
  
  if(${CMAKE_CXX_FLAGS_RELWITHDEBINFO} MATCHES ".*/MD.*")
  string(REGEX REPLACE "/MD" "" CMAKE_CXX_FLAGS_RELWITHDEBINFO "${CMAKE_CXX_FLAGS_RELWITHDEBINFO}")
  set(CMAKE_CXX_FLAGS_RELWITHDEBINFO "${CMAKE_CXX_FLAGS_RELWITHDEBINFO} /MT")
  endif()

  if(${CMAKE_CXX_FLAGS_MINSIZEREL} MATCHES ".*/MD.*")
  string(REGEX REPLACE "/MD" "" CMAKE_CXX_FLAGS_MINSIZEREL "${CMAKE_CXX_FLAGS_MINSIZEREL}")
  set(CMAKE_CXX_FLAGS_MINSIZEREL "${CMAKE_CXX_FLAGS_MINSIZEREL} /MT")
  endif()
  
  if(${CMAKE_CXX_FLAGS_DEBUG} MATCHES ".*/MD.*")
  string(REGEX REPLACE "/MDd" "" CMAKE_CXX_FLAGS_DEBUG "${CMAKE_CXX_FLAGS_DEBUG}")
  set(CMAKE_CXX_FLAGS_DEBUG "${CMAKE_CXX_FLAGS_DEBUG} /MTd")
  endif()
  message(STATUS "Configuring Static Runtime Library. Flags now ${CMAKE_CXX_FLAGS}/${CMAKE_CXX_FLAGS_DEBUG}/${CMAKE_CXX_FLAGS_RELEASE}")
endif()

message(STATUS "VCPKG Manifest Directory ${VCPKG_MANIFEST_DIR}, Installed_packages: ${VCPKG_INSTALLED_DIR}")


# Include sub-projects.
add_subdirectory(ApiViewProcessor)
add_subdirectory(ParseAzureSdkCpp)
add_subdirectory(ParseTests)
