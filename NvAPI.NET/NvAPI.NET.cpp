// This is the main DLL file.

#include "stdafx.h"
#include "NvAPI.NET.h"
#include <vcclr.h>


NvAPI::NvAPI()
{
}

void NvAPI::Initialize()
{
  NvAPI_Status r = NvAPI_Initialize();
  if (r == NVAPI_OK)
  {
  }
}

String^ NvAPI::GetErrorMessage(Status nr)
{
  NvAPI_ShortString desc;
  if (NvAPI_GetErrorMessage((NvAPI_Status) nr, desc) == NVAPI_OK)
  {
    return gcnew String(desc);
  }
  return String::Empty;
}

IntPtr NvAPI::GetPhysicalGPUFromUnAttachedDisplay(IntPtr hNvUnAttachedDisp)
{
  NvPhysicalGpuHandle h;
  if (NvAPI_GetPhysicalGPUFromUnAttachedDisplay((NvUnAttachedDisplayHandle)(void*) hNvUnAttachedDisp, &h) == NVAPI_OK)
  {
    return (IntPtr)(void*)h;
  }
  return IntPtr::Zero;
}

IntPtr NvAPI::EnumNvidiaUnAttachedDisplayHandle (Int32  thisEnum)
{
  NvUnAttachedDisplayHandle h;
	if (NvAPI_EnumNvidiaUnAttachedDisplayHandle(thisEnum, &h) == NVAPI_OK)
  {
    return (IntPtr)(void*)h;
  }
  return IntPtr::Zero;
}

IntPtr NvAPI::GetAssociatedNvidiaDisplayHandle  (String^ szDisplayName)
{
  NvDisplayHandle h;
  char* s = (char*)(void*) Marshal::StringToHGlobalAnsi(szDisplayName);
  NvAPI_Status r = NvAPI_GetAssociatedNvidiaDisplayHandle(s, &h);
  
  Marshal::FreeHGlobal(IntPtr((void*)s));

  if (r == NVAPI_OK)
  {
    return (IntPtr)(void*)h;
  }
  return IntPtr::Zero;
}