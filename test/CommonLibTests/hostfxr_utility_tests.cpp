// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"

//
//TEST(GetAbsolutePathToDotnetFromProgramFiles, BackupWorks)
//{
//    STRU struAbsolutePathToDotnet;
//    HRESULT hr = S_OK;
//    BOOL fDotnetInProgramFiles;
//    BOOL is64Bit;
//    BOOL fIsWow64 = FALSE;
//    SYSTEM_INFO systemInfo;
//    IsWow64Process(GetCurrentProcess(), &fIsWow64);
//    if (fIsWow64)
//    {
//        is64Bit = FALSE;
//    }
//    else
//    {
//        GetNativeSystemInfo(&systemInfo);
//        is64Bit = systemInfo.wProcessorArchitecture == PROCESSOR_ARCHITECTURE_AMD64;
//    }
//
//    if (is64Bit)
//    {
//        fDotnetInProgramFiles = UTILITY::CheckIfFileExists(L"C:/Program Files/dotnet/dotnet.exe");
//    }
//    else
//    {
//        fDotnetInProgramFiles = UTILITY::CheckIfFileExists(L"C:/Program Files (x86)/dotnet/dotnet.exe");
//    }
//
//    hr = HOSTFXR_UTILITY::GetAbsolutePathToDotnetFromProgramFiles(&struAbsolutePathToDotnet);
//    if (fDotnetInProgramFiles)
//    {
//        EXPECT_EQ(hr, S_OK);
//    }
//    else
//    {
//        EXPECT_NE(hr, S_OK);
//        EXPECT_TRUE(struAbsolutePathToDotnet.IsEmpty());
//    }
//}
//
//TEST(GetHostFxrArguments, InvalidParams)
//{
//    DWORD retVal = 0;
//    BSTR* bstrArray;
//    STRU  struHostFxrDllLocation;
//    STRU  struExeLocation;
//
//    HRESULT hr = HOSTFXR_UTILITY::GetHostFxrParameters(
//        INVALID_HANDLE_VALUE,
//        L"bogus", // processPath
//        L"",  // application physical path, ignored.
//        L"ignored",  //arguments
//        NULL, // event log
//        &struExeLocation,
//        &retVal, // arg count
//        &bstrArray); // args array.
//
//    EXPECT_EQ(E_INVALIDARG, hr);
//}