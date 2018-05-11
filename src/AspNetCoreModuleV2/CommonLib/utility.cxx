// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#include"stdafx.h"

// static
HRESULT
UTILITY::SplitUrl(
    PCWSTR pszDestinationUrl,
    BOOL *pfSecure,
    STRU *pstrDestination,
    STRU *pstrUrl
)
/*++

Routine Description:

    Split the URL specified for forwarding into its specific components
    The format of the URL looks like
    http[s]://destination[:port]/path
    when port is omitted, the default port for that specific protocol is used
    when host is omitted, it gets the same value as the destination

Arguments:

    pszDestinationUrl - the url to be split up
    pfSecure - SSL to be used in forwarding?
    pstrDestination - destination
    pDestinationPort - port
    pstrUrl - URL

Return Value:

    HRESULT

--*/
{
    HRESULT hr;

    //
    // First determine if the target is secure
    //
    if (_wcsnicmp(pszDestinationUrl, L"http://", 7) == 0)
    {
        *pfSecure = FALSE;
        pszDestinationUrl += 7;
    }
    else if (_wcsnicmp(pszDestinationUrl, L"https://", 8) == 0)
    {
        *pfSecure = TRUE;
        pszDestinationUrl += 8;
    }
    else
    {
        return HRESULT_FROM_WIN32(ERROR_INVALID_DATA);
    }

    if (*pszDestinationUrl == L'\0')
    {
        return HRESULT_FROM_WIN32(ERROR_INVALID_DATA);
    }

    //
    // Find the 3rd slash corresponding to the url
    //
    LPCWSTR pszSlash = wcschr(pszDestinationUrl, L'/');
    if (pszSlash == NULL)
    {
        if (FAILED(hr = pstrUrl->Copy(L"/", 1)) ||
            FAILED(hr = pstrDestination->Copy(pszDestinationUrl)))
        {
            return hr;
        }
    }
    else
    {
        if (FAILED(hr = pstrUrl->Copy(pszSlash)) ||
            FAILED(hr = pstrDestination->Copy(pszDestinationUrl,
            (DWORD)(pszSlash - pszDestinationUrl))))
        {
            return hr;
        }
    }

    return S_OK;
}

// Change a hexadecimal digit to its numerical equivalent
#define TOHEX( ch )                                     \
    ((ch) > L'9' ?                                      \
        (ch) >= L'a' ?                                  \
            (ch) - L'a' + 10 :                          \
            (ch) - L'A' + 10                            \
        : (ch) - L'0')

// static
HRESULT
UTILITY::UnEscapeUrl(
    PCWSTR      pszUrl,
    DWORD       cchUrl,
    bool        fCopyQuery,
    STRA *      pstrResult
)
{
    HRESULT hr;
    CHAR pch[2];
    pch[1] = '\0';
    DWORD cchStart = 0;
    DWORD index = 0;

    while (index < cchUrl &&
        (fCopyQuery || pszUrl[index] != L'?'))
    {
        switch (pszUrl[index])
        {
        case L'%':
            if (iswxdigit(pszUrl[index + 1]) && iswxdigit(pszUrl[index + 2]))
            {
                if (index > cchStart &&
                    FAILED(hr = pstrResult->AppendW(pszUrl + cchStart,
                        index - cchStart)))
                {
                    return hr;
                }
                cchStart = index + 3;

                pch[0] = static_cast<CHAR>(TOHEX(pszUrl[index + 1]) * 16 +
                    TOHEX(pszUrl[index + 2]));
                if (FAILED(hr = pstrResult->Append(pch, 1)))
                {
                    return hr;
                }
                index += 3;
                break;
            }

            __fallthrough;
        default:
            index++;
        }
    }

    if (index > cchStart)
    {
        return pstrResult->AppendW(pszUrl + cchStart,
            index - cchStart);
    }

    return S_OK;
}

// static
HRESULT
UTILITY::UnEscapeUrl(
    PCWSTR      pszUrl,
    DWORD       cchUrl,
    STRU *      pstrResult
)
{
    HRESULT hr;
    WCHAR pch[2];
    pch[1] = L'\0';
    DWORD cchStart = 0;
    DWORD index = 0;
    bool fInQuery = FALSE;

    while (index < cchUrl)
    {
        switch (pszUrl[index])
        {
        case L'%':
            if (iswxdigit(pszUrl[index + 1]) && iswxdigit(pszUrl[index + 2]))
            {
                if (index > cchStart &&
                    FAILED(hr = pstrResult->Append(pszUrl + cchStart,
                        index - cchStart)))
                {
                    return hr;
                }
                cchStart = index + 3;

                pch[0] = static_cast<WCHAR>(TOHEX(pszUrl[index + 1]) * 16 +
                    TOHEX(pszUrl[index + 2]));
                if (FAILED(hr = pstrResult->Append(pch, 1)))
                {
                    return hr;
                }
                index += 3;
                if (pch[0] == L'?')
                {
                    fInQuery = TRUE;
                }
                break;
            }

            index++;
            break;

        case L'/':
            if (fInQuery)
            {
                if (index > cchStart &&
                    FAILED(hr = pstrResult->Append(pszUrl + cchStart,
                        index - cchStart)))
                {
                    return hr;
                }
                cchStart = index + 1;

                if (FAILED(hr = pstrResult->Append(L"\\", 1)))
                {
                    return hr;
                }
                index += 1;
                break;
            }

            __fallthrough;
        default:
            index++;
        }
    }

    if (index > cchStart)
    {
        return pstrResult->Append(pszUrl + cchStart,
            index - cchStart);
    }

    return S_OK;
}

HRESULT
UTILITY::EscapeAbsPath(
    IHttpRequest * pRequest,
    STRU * strEscapedUrl
)
{
    HRESULT hr = S_OK;
    STRU    strAbsPath;
    LPCWSTR pszAbsPath = NULL;
    LPCWSTR pszFindStr = NULL;

    hr = strAbsPath.Copy(pRequest->GetRawHttpRequest()->CookedUrl.pAbsPath,
        pRequest->GetRawHttpRequest()->CookedUrl.AbsPathLength / sizeof(WCHAR));
    if (FAILED(hr))
    {
        goto Finished;
    }

    pszAbsPath = strAbsPath.QueryStr();
    pszFindStr = wcschr(pszAbsPath, L'?');

    while (pszFindStr != NULL)
    {
        strEscapedUrl->Append(pszAbsPath, pszFindStr - pszAbsPath);
        strEscapedUrl->Append(L"%3F");
        pszAbsPath = pszFindStr + 1;
        pszFindStr = wcschr(pszAbsPath, L'?');
    }

    strEscapedUrl->Append(pszAbsPath);
    strEscapedUrl->Append(pRequest->GetRawHttpRequest()->CookedUrl.pQueryString,
        pRequest->GetRawHttpRequest()->CookedUrl.QueryStringLength / sizeof(WCHAR));

Finished:
    return hr;
}

// static
bool
UTILITY::IsValidAttributeNameChar(
    WCHAR ch
)
{
    //
    // Values based on ASP.NET rendering for cookie names. RFC 2965 is not clear
    // what the non-special characters are.
    //
    return ch == L'\t' || (ch > 31 && ch < 127);
}

// static
bool
UTILITY::FindInMultiString(
    PCWSTR      pszMultiString,
    PCWSTR      pszStringToFind
)
{
    while (*pszMultiString != L'\0')
    {
        if (wcscmp(pszMultiString, pszStringToFind) == 0)
        {
            return TRUE;
        }
        pszMultiString += wcslen(pszMultiString) + 1;
    }

    return FALSE;
}

// static
bool
UTILITY::IsValidQueryStringName(
    PCWSTR  pszName
)
{
    while (*pszName != L'\0')
    {
        WCHAR c = *pszName;
        if (c != L'-' && c != L'_' && c != L'+' &&
            c != L'.' && c != L'*' && c != L'$' && c != L'%' && c != L',' &&
            !iswalnum(c))
        {
            return FALSE;
        }
        pszName++;
    }

    return TRUE;
}

// static
bool
UTILITY::IsValidHeaderName(
    PCWSTR  pszName
)
{
    while (*pszName != L'\0')
    {
        WCHAR c = *pszName;
        if (c != L'-' && c != L'_' && c != L'+' &&
            c != L'.' && c != L'*' && c != L'$' && c != L'%'
            && !iswalnum(c))
        {
            return FALSE;
        }
        pszName++;
    }

    return TRUE;
}

HRESULT
UTILITY::IsPathUnc(
    __in  LPCWSTR       pszPath,
    __out BOOL *        pfIsUnc
)
{
    HRESULT hr = S_OK;
    STRU strTempPath;

    if (pszPath == NULL || pfIsUnc == NULL)
    {
        hr = E_INVALIDARG;
        goto Finished;
    }

    hr = MakePathCanonicalizationProof((LPWSTR)pszPath, &strTempPath);
    if (FAILED(hr))
    {
        goto Finished;
    }

    //
    // MakePathCanonicalizationProof will map \\?\UNC, \\.\UNC and \\ to \\?\UNC
    //
    (*pfIsUnc) = (_wcsnicmp(strTempPath.QueryStr(), L"\\\\?\\UNC\\", 8 /* sizeof \\?\UNC\ */) == 0);

Finished:

    return hr;
}

HRESULT
UTILITY::ConvertPathToFullPath(
    _In_  LPCWSTR   pszPath,
    _In_  LPCWSTR   pszRootPath,
    _Out_ STRU*     pStruFullPath
)
{
    HRESULT hr = S_OK;
    STRU strFileFullPath;
    LPWSTR pszFullPath = NULL;

    // if relative path, prefix with root path and then convert to absolute path.
    if (PathIsRelative(pszPath))
    {
        hr = strFileFullPath.Copy(pszRootPath);
        if (FAILED(hr))
        {
            goto Finished;
        }

        if (!strFileFullPath.EndsWith(L"\\"))
        {
            hr = strFileFullPath.Append(L"\\");
            if (FAILED(hr))
            {
                goto Finished;
            }
        }
    }

    hr = strFileFullPath.Append(pszPath);
    if (FAILED(hr))
    {
        goto Finished;
    }

    pszFullPath = new WCHAR[strFileFullPath.QueryCCH() + 1];
    if (pszFullPath == NULL)
    {
        hr = E_OUTOFMEMORY;
        goto Finished;
    }

    if (_wfullpath(pszFullPath,
        strFileFullPath.QueryStr(),
        strFileFullPath.QueryCCH() + 1) == NULL)
    {
        hr = HRESULT_FROM_WIN32(ERROR_INVALID_PARAMETER);
        goto Finished;
    }

    // convert to canonical path
    hr = MakePathCanonicalizationProof(pszFullPath, pStruFullPath);
    if (FAILED(hr))
    {
        goto Finished;
    }

Finished:

    if (pszFullPath != NULL)
    {
        delete[] pszFullPath;
        pszFullPath = NULL;
    }

    return hr;
}

HRESULT
UTILITY::EnsureDirectoryPathExist(
    _In_  LPCWSTR pszPath
)
{
    HRESULT hr = S_OK;
    STRU    struPath;
    DWORD   dwPosition = 0;
    BOOL    fDone = FALSE;
    BOOL    fUnc = FALSE;

    struPath.Copy(pszPath);
    hr = IsPathUnc(pszPath, &fUnc);
    if (FAILED(hr))
    {
        goto Finished;
    }
    if (fUnc)
    {
        // "\\?\UNC\"
        dwPosition = 8;
    }
    else if (struPath.IndexOf(L'?', 0) != -1)
    {
        // sceanrio "\\?\"
        dwPosition = 4;
    }
    while (!fDone)
    {
        dwPosition = struPath.IndexOf(L'\\', dwPosition + 1);
        if (dwPosition == -1)
        {
            // not found '/'
            fDone = TRUE;
            goto Finished;
        }
        else if (dwPosition == 0)
        {
            hr = ERROR_INTERNAL_ERROR;
            goto Finished;
        }
        else if (struPath.QueryStr()[dwPosition - 1] == L':')
        {
            //  skip volume case
            continue;
        }
        else
        {
            struPath.QueryStr()[dwPosition] = L'\0';
        }

        if (!CreateDirectory(struPath.QueryStr(), NULL) &&
            ERROR_ALREADY_EXISTS != GetLastError())
        {
            hr = HRESULT_FROM_WIN32(GetLastError());
            fDone = TRUE;
            goto Finished;
        }
        struPath.QueryStr()[dwPosition] = L'\\';
    }

Finished:
    return hr;
}

BOOL
UTILITY::DirectoryExists(
    _In_ STRU *pstrPath
)
{
    WIN32_FILE_ATTRIBUTE_DATA data;

    if (pstrPath->IsEmpty())
    {
        return false;
    }

    return GetFileAttributesExW(pstrPath->QueryStr(), GetFileExInfoStandard, &data);
}

VOID
UTILITY::FindDotNetFolders(
    _In_ PCWSTR pszPath,
    _Out_ std::vector<std::wstring> *pvFolders
)
{
    HANDLE handle = NULL;
    WIN32_FIND_DATAW data = { 0 };

    handle = FindFirstFileExW(pszPath, FindExInfoStandard, &data, FindExSearchNameMatch, NULL, 0);
    if (handle == INVALID_HANDLE_VALUE)
    {
        return;
    }

    do
    {
        std::wstring folder(data.cFileName);
        pvFolders->push_back(folder);
    } while (FindNextFileW(handle, &data));

    FindClose(handle);
}

BOOL
UTILITY::CheckIfFileExists(
    _In_ PCWSTR pszFilePath
)
{
    HANDLE              hFileHandle = INVALID_HANDLE_VALUE;
    SECURITY_ATTRIBUTES saAttr;
    BOOL                fFileExists = FALSE;

    saAttr.nLength = sizeof(SECURITY_ATTRIBUTES);
    saAttr.bInheritHandle = TRUE;
    saAttr.lpSecurityDescriptor = NULL;

    hFileHandle = CreateFile(pszFilePath,
        GENERIC_READ,
        FILE_SHARE_READ,
        &saAttr,
        OPEN_EXISTING,
        FILE_ATTRIBUTE_NORMAL,
        NULL);

    fFileExists = hFileHandle != INVALID_HANDLE_VALUE || GetLastError() == ERROR_SHARING_VIOLATION;

    if (fFileExists)
    {
        CloseHandle(hFileHandle);
    }

    return fFileExists;
}

VOID
UTILITY::LogEvent(
    _In_ HANDLE  hEventLog,
    _In_ WORD    dwEventInfoType,
    _In_ DWORD   dwEventId,
    _In_ LPCWSTR pstrMsg
)
{
    if (hEventLog != NULL)
    {
        ReportEventW(hEventLog,
            dwEventInfoType,
            0,        // wCategory
            dwEventId,
            NULL,     // lpUserSid
            1,        // wNumStrings
            0,        // dwDataSize,
            &pstrMsg,
            NULL      // lpRawData
        );
    }

    if (dwEventInfoType == EVENTLOG_ERROR_TYPE)
    {
        fwprintf(stderr, L"ERROR: %s\n", pstrMsg);
    }
}

VOID
UTILITY::LogEventF(
    _In_ HANDLE  hEventLog,
    _In_ WORD    dwEventInfoType,
    _In_ DWORD   dwEventId,
    _In_ LPCWSTR pstrMsg,
    ...
)
{
    va_list argsList;
    va_start(argsList, pstrMsg);

    STACK_STRU(strEventMsg, 256);

    if (SUCCEEDED(strEventMsg.SafeVsnwprintf(
        pstrMsg,
        argsList)))
    {
        UTILITY::LogEvent(hEventLog,
            dwEventInfoType,
            dwEventId,
            strEventMsg.QueryStr());
    }

    va_end(argsList);
}

//
// Forms the argument list in HOSTFXR_PARAMETERS.
// Sets the ArgCount and Arguments.
// Arg structure:
// argv[0] = Path to exe activating hostfxr.
// argv[1] = L"exec"
// argv[2] = absolute path to dll.
//
HRESULT
UTILITY::ParseHostfxrArguments(
    PCWSTR              pwzArgumentsFromConfig,
    PCWSTR              pwzExePath,
    PCWSTR              pcwzApplicationPhysicalPath,
    HANDLE              hEventLog,
    _Out_ DWORD*        pdwArgCount,
    _Out_ BSTR**        pbstrArgv
)
{
    UNREFERENCED_PARAMETER(hEventLog); // TODO use event log to set errors.

    DBG_ASSERT(dwArgCount != NULL);
    DBG_ASSERT(pwzArgv != NULL);

    HRESULT     hr = S_OK;
    INT         argc = 0;
    BSTR*       argv = NULL;
    LPWSTR*     pwzArgs = NULL;
    STRU        struTempPath;
    INT         intArgsProcessed = 0;

    // If we call CommandLineToArgvW with an empty string, argc is 5 for some interesting reason.
    // Protectively guard against this by check if the string is null or empty.
    if (pwzArgumentsFromConfig == NULL || wcscmp(pwzArgumentsFromConfig, L"") == 0)
    {
        hr = E_INVALIDARG;
        goto Finished;
    }

    pwzArgs = CommandLineToArgvW(pwzArgumentsFromConfig, &argc);

    if (pwzArgs == NULL)
    {
        hr = HRESULT_FROM_WIN32(GetLastError());
        goto Failure;
    }

    argv = new BSTR[argc + 1];
    if (argv == NULL)
    {
        hr = E_OUTOFMEMORY;
        goto Failure;
    }

    argv[0] = SysAllocString(pwzExePath);

    if (argv[0] == NULL)
    {
        hr = E_OUTOFMEMORY;
        goto Failure;
    }

    // Try to convert the application dll from a relative to an absolute path
    // Don't record this failure as pwzArgs[0] may already be an absolute path to the dll.
    for (intArgsProcessed = 0; intArgsProcessed < argc; intArgsProcessed++)
    {
        struTempPath.Copy(pwzArgs[intArgsProcessed]);
        if (struTempPath.EndsWith(L".dll"))
        {
            if (SUCCEEDED(UTILITY::ConvertPathToFullPath(pwzArgs[intArgsProcessed], pcwzApplicationPhysicalPath, &struTempPath)))
            {
                argv[intArgsProcessed + 1] = SysAllocString(struTempPath.QueryStr());
            }
            else
            {
                argv[intArgsProcessed + 1] = SysAllocString(pwzArgs[intArgsProcessed]);
            }
            if (argv[intArgsProcessed + 1] == NULL)
            {
                hr = E_OUTOFMEMORY;
                goto Failure;
            }
        }
        else
        {
            argv[intArgsProcessed + 1] = SysAllocString(pwzArgs[intArgsProcessed]);
            if (argv[intArgsProcessed + 1] == NULL)
            {
                hr = E_OUTOFMEMORY;
                goto Failure;
            }
        }
    }

    *pbstrArgv = argv;
    *pdwArgCount = argc + 1;

    goto Finished;

Failure:
    if (argv != NULL)
    {
        // intArgsProcess - 1 here as if we fail to allocated the ith string
        // we don't want to free it.
        for (INT i = 0; i < intArgsProcessed - 1; i++)
        {
            SysFreeString(argv[i]);
        }
    }

    delete[] argv;

Finished:
    if (pwzArgs != NULL)
    {
        LocalFree(pwzArgs);
        DBG_ASSERT(pwzArgs == NULL);
    }
    return hr;
}

//
// Runs a standalone appliction.
// The folder structure looks like this:
// Application/
//   hostfxr.dll
//   Application.exe
//   Application.dll
//   etc.
// We get the full path to hostfxr.dll and Application.dll and run hostfxr_main,
// passing in Application.dll.
// Assuming we don't need Application.exe as the dll is the actual application.
//
HRESULT
UTILITY::GetStandaloneHostfxrParameters(
    BSTR                pwzExeAbsolutePath, // includes .exe file extension.
    PCWSTR				pcwzApplicationPhysicalPath,
    PCWSTR              pcwzArguments,
    HANDLE              hEventLog,
    _Inout_ STRU*		pStruHostFxrDllLocation,
    _Out_ DWORD*		pdwArgCount,
    _Out_ BSTR**		ppwzArgv
)
{
    HRESULT             hr = S_OK;
    STRU                struDllPath;
    STRU                struArguments;
    STRU                struHostFxrPath;
    STRU                struRuntimeConfigLocation;
    DWORD               dwPosition;

    // Obtain the app name from the processPath section.
    if (FAILED(hr = struDllPath.Copy(pwzExeAbsolutePath)))
    {
        goto Finished;
    }

    dwPosition = struDllPath.LastIndexOf(L'.', 0);
    if (dwPosition == -1)
    {
        hr = E_FAIL;
        goto Finished;
    }

    hr = UTILITY::ConvertPathToFullPath(L".\\hostfxr.dll", pcwzApplicationPhysicalPath, &struHostFxrPath);
    if (FAILED(hr))
    {
        goto Finished;
    }

    struDllPath.QueryStr()[dwPosition] = L'\0';
    if (FAILED(hr = struDllPath.SyncWithBuffer()))
    {
        goto Finished;
    }

    if (!UTILITY::CheckIfFileExists(struHostFxrPath.QueryStr()))
    {
        // Most likely a full framework app.
        // Check that the runtime config file doesn't exist in the folder as another heuristic.
        if (FAILED(hr = struRuntimeConfigLocation.Copy(struDllPath)) ||
            FAILED(hr = struRuntimeConfigLocation.Append(L".runtimeconfig.json")))
        {
            goto Finished;
        }
        if (!UTILITY::CheckIfFileExists(struRuntimeConfigLocation.QueryStr()))
        {

            hr = E_APPLICATION_ACTIVATION_EXEC_FAILURE;
            UTILITY::LogEventF(hEventLog,
                EVENTLOG_ERROR_TYPE,
                ASPNETCORE_EVENT_INPROCESS_FULL_FRAMEWORK_APP,
                ASPNETCORE_EVENT_INPROCESS_FULL_FRAMEWORK_APP_MSG,
                pcwzApplicationPhysicalPath,
                hr);
        }
        else
        {
            // If a runtime config file does exist, report a file not found on the app.exe
            hr = HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND);
            UTILITY::LogEventF(hEventLog,
                EVENTLOG_ERROR_TYPE,
                ASPNETCORE_EVENT_APPLICATION_EXE_NOT_FOUND,
                ASPNETCORE_EVENT_APPLICATION_EXE_NOT_FOUND_MSG,
                pcwzApplicationPhysicalPath,
                hr);
        }

        goto Finished;
    }

    if (FAILED(hr = pStruHostFxrDllLocation->Copy(struHostFxrPath)))
    {
        goto Finished;
    }


    if (FAILED(hr = struDllPath.Append(L".dll")))
    {
        goto Finished;
    }

    if (!UTILITY::CheckIfFileExists(struDllPath.QueryStr()))
    {
        // Treat access issue as File not found
        hr = HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND);
        goto Finished;
    }

    if (FAILED(hr = struArguments.Copy(struDllPath)) ||
        FAILED(hr = struArguments.Append(L" ")) ||
        FAILED(hr = struArguments.Append(pcwzArguments)))
    {
        goto Finished;
    }

    if (FAILED(hr = ParseHostfxrArguments(
        struArguments.QueryStr(),
        pwzExeAbsolutePath,
        pcwzApplicationPhysicalPath,
        hEventLog,
        pdwArgCount,
        ppwzArgv)))
    {
        goto Finished;
    }

Finished:

    return hr;
}

BOOL
UTILITY::IsDotnetExecutable(PCWSTR executablePath)
{
    STRU struDotnetExecutablePath;
    if (FAILED(struDotnetExecutablePath.Copy(executablePath)))
    {
        return FALSE;
    }
    return struDotnetExecutablePath.EndsWith(L"dotnet.exe") || struDotnetExecutablePath.EndsWith(L"dotnet");
}
