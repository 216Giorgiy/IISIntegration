// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"

TEST(PassUnexpandedEnvString, ExpandsResult)
{
    HRESULT hr = S_OK;
    PCWSTR unexpandedString = L"ANCM_TEST_ENV_VAR";
    PCWSTR unexpandedStringValue = L"foobar";
    STRU   struExpandedString;
    SetEnvironmentVariable(L"ANCM_TEST_ENV_VAR", unexpandedStringValue);

    hr = struExpandedString.CopyAndExpandEnvironmentStrings(L"%ANCM_TEST_ENV_VAR%");
    EXPECT_EQ(hr, S_OK);
    EXPECT_STREQ(L"foobar", struExpandedString.QueryStr());
}

TEST(PassUnexpandedEnvString, LongStringExpandsResults)
{
    HRESULT hr = S_OK;
    PCWSTR unexpandedString = L"ANCM_TEST_ENV_VAR_LONG";
    STRU   struStringValue;
    STACK_STRU(struExpandedString, MAX_PATH);

    struStringValue.Append(L"TestValueThatIsLongerThan256CharactersLongToTriggerResize");
    struStringValue.Append(L"TestValueThatIsLongerThan256CharactersLongToTriggerResize");
    struStringValue.Append(L"TestValueThatIsLongerThan256CharactersLongToTriggerResize");
    struStringValue.Append(L"TestValueThatIsLongerThan256CharactersLongToTriggerResize");
    struStringValue.Append(L"TestValueThatIsLongerThan256CharactersLongToTriggerResize");
    struStringValue.Append(L"TestValueThatIsLongerThan256CharactersLongToTriggerResize");

    SetEnvironmentVariable(unexpandedString, struStringValue.QueryStr());

    hr = struExpandedString.CopyAndExpandEnvironmentStrings(L"%ANCM_TEST_ENV_VAR_LONG%");
    EXPECT_EQ(hr, S_OK);
    EXPECT_EQ(struStringValue.QueryCCH(), struExpandedString.QueryCCH());
    // The values are exactly the same, however EXPECT_EQ is returning false.
    //EXPECT_EQ(struStringValue.QueryStr(), struExpandedString.QueryStr());
    EXPECT_STREQ(struStringValue.QueryStr(), struExpandedString.QueryStr());
}

TEST(ParseHostFxrArguments, BasicHostFxrArguments)
{
    DWORD retVal = 0;
    BSTR* bstrArray;
    PCWSTR exeStr = L"C:/Program Files/dotnet.exe";
    HRESULT hr = UTILITY::ParseHostfxrArguments(
        L"exec \"test.dll\"", // args
        exeStr,  // exe path
        L"invalid",  // physical path to application
        NULL, // event log
        &retVal, // arg count
        &bstrArray); // args array.

    EXPECT_EQ(hr, S_OK);
    EXPECT_EQ(DWORD(3), retVal);
    ASSERT_STREQ(exeStr, bstrArray[0]);
    ASSERT_STREQ(L"exec", bstrArray[1]);
    ASSERT_STREQ(L"test.dll", bstrArray[2]);
}

TEST(ParseHostFxrArguments, NoExecProvided)
{
    DWORD retVal = 0;
    BSTR* bstrArray;
    PCWSTR exeStr = L"C:/Program Files/dotnet.exe";

    HRESULT hr = UTILITY::ParseHostfxrArguments(
        L"test.dll", // args
        exeStr,  // exe path
        L"ignored",  // physical path to application
        NULL, // event log
        &retVal, // arg count
        &bstrArray); // args array.

    EXPECT_EQ(hr, S_OK);
    EXPECT_EQ(DWORD(2), retVal);
    ASSERT_STREQ(exeStr, bstrArray[0]);
    ASSERT_STREQ(L"test.dll", bstrArray[1]);
}

TEST(ParseHostFxrArguments, ConvertDllToAbsolutePath)
{
    DWORD retVal = 0;
    BSTR* bstrArray;
    PCWSTR exeStr = L"C:/Program Files/dotnet.exe";

    HRESULT hr = UTILITY::ParseHostfxrArguments(
        L"exec \"test.dll\"", // args
        exeStr,  // exe path
        L"C:/test",  // physical path to application
        NULL, // event log
        &retVal, // arg count
        &bstrArray); // args array.

    EXPECT_EQ(hr, S_OK);
    EXPECT_EQ(DWORD(3), retVal);
    ASSERT_STREQ(exeStr, bstrArray[0]);
    ASSERT_STREQ(L"exec", bstrArray[1]);
    ASSERT_STREQ(L"C:\\test\\test.dll", bstrArray[2]);
}

TEST(ParseHostFxrArguments, ProvideNoArgs_InvalidArgs)
{
    DWORD retVal = 0;
    BSTR* bstrArray;
    PCWSTR exeStr = L"C:/Program Files/dotnet.exe";

    HRESULT hr = UTILITY::ParseHostfxrArguments(
        L"", // args
        exeStr,  // exe path
        L"ignored",  // physical path to application
        NULL, // event log
        &retVal, // arg count
        &bstrArray); // args array.

    EXPECT_EQ(E_INVALIDARG, hr);
}
