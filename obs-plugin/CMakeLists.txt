cmake_minimum_required(VERSION 3.28...3.30)

# Read plugin metadata from buildspec.json
file(READ "${CMAKE_CURRENT_SOURCE_DIR}/buildspec.json" _buildspec)
string(JSON _name    GET ${_buildspec} name)
string(JSON _version GET ${_buildspec} version)
string(JSON _author  GET ${_buildspec} author)
string(JSON _website GET ${_buildspec} website)

project(${_name} VERSION ${_version} LANGUAGES CXX C)
set(PLUGIN_NAME    ${_name})
set(PLUGIN_VERSION ${_version})
set(PLUGIN_AUTHOR  ${_author})
set(PLUGIN_WEBSITE ${_website})

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_C_STANDARD 11)

# ── OBS (required) ────────────────────────────────────────────────────────────
find_package(libobs REQUIRED)

# ── libsrt (required for SRT transport) ───────────────────────────────────────
find_package(PkgConfig QUIET)
if(PKG_CONFIG_FOUND)
  pkg_check_modules(SRT IMPORTED_TARGET libsrt>=1.4)
endif()
if(NOT SRT_FOUND)
  find_library(SRT_LIBRARIES NAMES srt REQUIRED)
  find_path(SRT_INCLUDE_DIRS NAMES srt/srt.h REQUIRED)
  message(STATUS "Found libsrt via find_library")
endif()

# ── libcurl (required for WHIP signaling and telemetry) ───────────────────────
find_package(CURL REQUIRED)

# ── libdatachannel (optional — enables native WebRTC WHIP) ────────────────────
find_package(LibDataChannel QUIET)
if(LibDataChannel_FOUND)
  message(STATUS "libdatachannel found — WHIP WebRTC transport ENABLED")
  add_compile_definitions(MONA_WHIP_DATACHANNEL=1)
else()
  message(STATUS "libdatachannel not found — WHIP uses SRT-over-HTTP signalling")
endif()

# ── Qt6 (optional — enables native settings dialog) ───────────────────────────
option(ENABLE_QT "Build Qt-based settings dialog" OFF)
if(ENABLE_QT)
  find_package(Qt6 COMPONENTS Widgets Core REQUIRED)
endif()

# ── Plugin target ─────────────────────────────────────────────────────────────
add_library(${CMAKE_PROJECT_NAME} MODULE)

target_compile_definitions(
  ${CMAKE_PROJECT_NAME}
  PRIVATE
    PLUGIN_NAME="${PLUGIN_NAME}"
    PLUGIN_VERSION="${PLUGIN_VERSION}"
    PLUGIN_AUTHOR="${PLUGIN_AUTHOR}"
)

target_sources(
  ${CMAKE_PROJECT_NAME}
  PRIVATE
    src/plugin-main.cpp
    src/mona-output.cpp
    src/mona-output.h
    src/muxer/mpegts.cpp
    src/muxer/mpegts.h
    src/transport/transport.h
    src/transport/srt-transport.cpp
    src/transport/srt-transport.h
    src/transport/rtmp-transport.cpp
    src/transport/rtmp-transport.h
    src/transport/whip-transport.cpp
    src/transport/whip-transport.h
    src/control/ws-client.cpp
    src/control/ws-client.h
    src/control/telemetry.cpp
    src/control/telemetry.h
    src/control/ptz-channel.cpp
    src/control/ptz-channel.h
)

target_include_directories(
  ${CMAKE_PROJECT_NAME}
  PRIVATE src ${SRT_INCLUDE_DIRS} ${CURL_INCLUDE_DIRS}
)

target_link_libraries(
  ${CMAKE_PROJECT_NAME}
  PRIVATE
    OBS::libobs
    ${SRT_LIBRARIES}
    CURL::libcurl
)

if(TARGET PkgConfig::SRT)
  target_link_libraries(${CMAKE_PROJECT_NAME} PRIVATE PkgConfig::SRT)
endif()

if(LibDataChannel_FOUND)
  target_link_libraries(${CMAKE_PROJECT_NAME} PRIVATE LibDataChannel::LibDataChannel)
endif()

if(ENABLE_QT)
  target_link_libraries(${CMAKE_PROJECT_NAME} PRIVATE Qt6::Core Qt6::Widgets)
  set_target_properties(${CMAKE_PROJECT_NAME} PROPERTIES AUTOMOC ON AUTOUIC ON)
endif()

# ── Platform specifics ────────────────────────────────────────────────────────
if(WIN32)
  target_link_libraries(${CMAKE_PROJECT_NAME} PRIVATE ws2_32)
  set_target_properties(${CMAKE_PROJECT_NAME} PROPERTIES
    PREFIX "" SUFFIX ".dll")
elseif(APPLE)
  set_target_properties(${CMAKE_PROJECT_NAME} PROPERTIES
    PREFIX "" SUFFIX ".plugin")
else()
  set_target_properties(${CMAKE_PROJECT_NAME} PROPERTIES
    PREFIX "" SUFFIX ".so")
endif()

# ── Data files ────────────────────────────────────────────────────────────────
target_sources(${CMAKE_PROJECT_NAME} PRIVATE data/locale/en-US.ini)

# ── Install ───────────────────────────────────────────────────────────────────
if(WIN32)
  set(OBS_PLUGIN_DIR "$ENV{ProgramFiles}/obs-studio/obs-plugins/64bit" CACHE PATH "OBS plugin directory")
  install(TARGETS ${CMAKE_PROJECT_NAME} DESTINATION "${OBS_PLUGIN_DIR}")
  install(DIRECTORY data/ DESTINATION "$ENV{ProgramFiles}/obs-studio/data/obs-plugins/${PLUGIN_NAME}")
elseif(APPLE)
  set(OBS_PLUGIN_DIR "$ENV{HOME}/Library/Application Support/obs-studio/plugins/${PLUGIN_NAME}/bin" CACHE PATH "OBS plugin directory")
  install(TARGETS ${CMAKE_PROJECT_NAME} DESTINATION "${OBS_PLUGIN_DIR}")
  install(DIRECTORY data/ DESTINATION "$ENV{HOME}/Library/Application Support/obs-studio/plugins/${PLUGIN_NAME}/data")
else()
  set(OBS_PLUGIN_DIR "$ENV{HOME}/.config/obs-studio/plugins/${PLUGIN_NAME}/bin/64bit" CACHE PATH "OBS plugin directory")
  install(TARGETS ${CMAKE_PROJECT_NAME} DESTINATION "${OBS_PLUGIN_DIR}")
  install(DIRECTORY data/ DESTINATION "$ENV{HOME}/.config/obs-studio/plugins/${PLUGIN_NAME}/data")
endif()
