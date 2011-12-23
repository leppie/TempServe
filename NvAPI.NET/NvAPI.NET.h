// NvAPI.NET.h

#pragma once

#include "nvapi.h"
#pragma comment(lib, "nvapi.lib")

using namespace System;
using namespace System::Runtime::InteropServices;

public enum class Status
{
  OK                                    =  0,      // Success
  ERROR                                 = -1,      // Generic error
  LIBRARY_NOT_FOUND                     = -2,      // nvapi.dll can not be loaded
  NO_IMPLEMENTATION                     = -3,      // not implemented in current driver installation
  API_NOT_INTIALIZED                    = -4,      // NvAPI_Initialize has not been called (successfully)
  INVALID_ARGUMENT                      = -5,      // invalid argument
  NVIDIA_DEVICE_NOT_FOUND               = -6,      // no NVIDIA display driver was found
  END_ENUMERATION                       = -7,      // no more to enum
  INVALID_HANDLE                        = -8,      // invalid handle
  INCOMPATIBLE_STRUCT_VERSION           = -9,      // an argument's structure version is not supported
  HANDLE_INVALIDATED                    = -10,     // handle is no longer valid (likely due to GPU or display re-configuration)
  OPENGL_CONTEXT_NOT_CURRENT            = -11,     // no NVIDIA OpenGL context is current (but needs to be)
  INVALID_POINTER                       = -14,     // An invalid pointer, usually NULL, was passed as a parameter
  NO_GL_EXPERT                          = -12,     // OpenGL Expert is not supported by the current drivers
  INSTRUMENTATION_DISABLED              = -13,     // OpenGL Expert is supported, but driver instrumentation is currently disabled
  EXPECTED_LOGICAL_GPU_HANDLE           = -100,    // expected a logical GPU handle for one or more parameters
  EXPECTED_PHYSICAL_GPU_HANDLE          = -101,    // expected a physical GPU handle for one or more parameters
  EXPECTED_DISPLAY_HANDLE               = -102,    // expected an NV display handle for one or more parameters
  INVALID_COMBINATION                   = -103,    // used in some commands to indicate that the combination of parameters is not valid
  NOT_SUPPORTED                         = -104,    // Requested feature not supported in the selected GPU
  PORTID_NOT_FOUND                      = -105,    // NO port ID found for I2C transaction
  EXPECTED_UNATTACHED_DISPLAY_HANDLE    = -106,    // expected an unattached display handle as one of the input param
  INVALID_PERF_LEVEL                    = -107,    // invalid perf level
  DEVICE_BUSY                           = -108,    // device is busy, request not fulfilled
  NV_PERSIST_FILE_NOT_FOUND             = -109,    // NV persist file is not found
  PERSIST_DATA_NOT_FOUND                = -110,    // NV persist data is not found
  EXPECTED_TV_DISPLAY                   = -111,    // expected TV output display
  EXPECTED_TV_DISPLAY_ON_DCONNECTOR     = -112,    // expected TV output on D Connector - HDTV_EIAJ4120.
  NO_ACTIVE_SLI_TOPOLOGY                = -113,    // SLI is not active on this device
  SLI_RENDERING_MODE_NOTALLOWED         = -114,    // setup of SLI rendering mode is not possible right now
  EXPECTED_DIGITAL_FLAT_PANEL           = -115,    // expected digital flat panel
  ARGUMENT_EXCEED_MAX_SIZE              = -116,    // argument exceeds expected size
  DEVICE_SWITCHING_NOT_ALLOWED          = -117,    // inhibit ON due to one of the flags in NV_GPU_DISPLAY_CHANGE_INHIBIT or SLI Active
  TESTING_CLOCKS_NOT_SUPPORTED          = -118,    // testing clocks not supported
  UNKNOWN_UNDERSCAN_CONFIG              = -119,    // the specified underscan config is from an unknown source (e.g. INF)
  TIMEOUT_RECONFIGURING_GPU_TOPO        = -120,    // timeout while reconfiguring GPUs
  DATA_NOT_FOUND                        = -121,    // Requested data was not found
  EXPECTED_ANALOG_DISPLAY               = -122,    // expected analog display
  NO_VIDLINK                            = -123,    // No SLI video bridge present
  REQUIRES_REBOOT                       = -124,    // NVAPI requires reboot for its settings to take effect
  INVALID_HYBRID_MODE                   = -125,    // the function is not supported with the current hybrid mode.
  MIXED_TARGET_TYPES                    = -126,    // The target types are not all the same
  SYSWOW64_NOT_SUPPORTED                = -127,    // the function is not supported from 32-bit on a 64-bit system
  IMPLICIT_SET_GPU_TOPOLOGY_CHANGE_NOT_ALLOWED = -128,    //there is any implicit GPU topo active. Use SetHybridMode to change topology.
  REQUEST_USER_TO_CLOSE_NON_MIGRATABLE_APPS = -129,      //Prompt the user to close all non-migratable apps.
  OUT_OF_MEMORY                         = -130,    // Could not allocate sufficient memory to complete the call
  WAS_STILL_DRAWING                     = -131,    // The previous operation that is transferring information to or from this surface is incomplete
  FILE_NOT_FOUND                        = -132,    // The file was not found
  TOO_MANY_UNIQUE_STATE_OBJECTS         = -133,    // There are too many unique instances of a particular type of state object
  INVALID_CALL                          = -134,    // The method call is invalid. For example, a method's parameter may not be a valid pointer
  D3D10_1_LIBRARY_NOT_FOUND             = -135,    // d3d10_1.dll can not be loaded
  FUNCTION_NOT_FOUND                    = -136,    // Couldn't find the function in loaded dll library
  INVALID_USER_PRIVILEDGE               = -137,    // Current User is not Admin 
  EXPECTED_NON_PRIMARY_DISPLAY_HANDLE   = -138,    // The handle corresponds to GDIPrimary
  EXPECTED_COMPUTE_GPU_HANDLE           = -139,    // Setting Physx GPU requires that the GPU is compute capable
  STEREO_NOT_INITIALIZED                = -140,    // Stereo part of NVAPI failed to initialize completely. Check if stereo driver is installed.
  STEREO_REGISTRY_ACCESS_FAILED         = -141,    // Access to stereo related registry keys or values failed.
  STEREO_REGISTRY_PROFILE_TYPE_NOT_SUPPORTED = -142, // Given registry profile type is not supported.
  STEREO_REGISTRY_VALUE_NOT_SUPPORTED   = -143,    // Given registry value is not supported.
  STEREO_NOT_ENABLED                    = -144,    // Stereo is not enabled and function needed it to execute completely.
  STEREO_NOT_TURNED_ON                  = -145,    // Stereo is not turned on and function needed it to execute completely.
  STEREO_INVALID_DEVICE_INTERFACE       = -146,    // Invalid device interface.
  STEREO_PARAMETER_OUT_OF_RANGE         = -147,    // Separation percentage or JPEG image capture quality out of [0-100] range.
  STEREO_FRUSTUM_ADJUST_MODE_NOT_SUPPORTED = -148, // Given frustum adjust mode is not supported.
  TOPO_NOT_POSSIBLE                     = -149,    // The mosaic topo is not possible given the current state of HW
  MODE_CHANGE_FAILED                    = -150,    // An attempt to do a display resolution mode change has failed
  D3D11_LIBRARY_NOT_FOUND               = -151,    // d3d11.dll/d3d11_beta.dll cannot be loaded.
  INVALID_ADDRESS                       = -152,    // Address outside of valid range.
  MATCHING_DEVICE_NOT_FOUND             = -153,    // The input does not match any of the available devices
};


public ref class NvAPI
{
public:

  static void Initialize();
  static String^ GetErrorMessage(Status nr);
  static IntPtr GetPhysicalGPUFromUnAttachedDisplay(IntPtr hNvUnAttachedDisp);
  static IntPtr	EnumNvidiaUnAttachedDisplayHandle (Int32  thisEnum);
  static IntPtr GetAssociatedNvidiaDisplayHandle  (String^ szDisplayName);

private:
  NvAPI();
};
