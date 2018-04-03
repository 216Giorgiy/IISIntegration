// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

#ifndef _MACROS_H
#define _MACROS_H

//
// The DIFF macro should be used around an expression involving pointer
// subtraction. The expression passed to DIFF is cast to a size_t type,
// allowing the result to be easily assigned to any 32-bit variable or
// passed to a function expecting a 32-bit argument.
//

#define DIFF(x)     ((size_t)(x))

// Change a hexadecimal digit to its numerical equivalent
#define TOHEX( ch )                                     \
    ((ch) > L'9' ?                                      \
        (ch) >= L'a' ?                                  \
            (ch) - L'a' + 10 :                          \
            (ch) - L'A' + 10                            \
        : (ch) - L'0')


// Change a number to its Hexadecimal equivalent

#define TODIGIT( nDigit )                               \
     (CHAR)((nDigit) > 9 ?                              \
          (nDigit) - 10 + 'A'                           \
        : (nDigit) + '0')


inline int
SAFEIsSpace(UCHAR c)
{
    return isspace( c );
}

inline int
SAFEIsAlNum(UCHAR c)
{
    return isalnum( c );
}

inline int
SAFEIsAlpha(UCHAR c)
{
    return isalpha( c );
}

inline int
SAFEIsXDigit(UCHAR c)
{
    return isxdigit( c );
}

inline int
SAFEIsDigit(UCHAR c)
{
    return isdigit( c );
}

#define RINOK(x) { HRESULT __result_ = (x); if(__result_ != S_OK) { hr = __result_; goto Finished; }; }

template< typename T >
void ThrowIfFailed( HRESULT hr, T&& msg )
{
    if( FAILED( hr ) )
        throw std::system_error{ hr, std::system_category(), std::forward<T>( msg ) };
}

template< typename T >
HANDLE ThrowIfInvalid( HANDLE handle, T&& msg )
{
    if( handle == INVALID_HANDLE_VALUE )
        throw std::system_error{ HRESULT_FROM_WIN32(GetLastError()), std::system_category(), std::forward<T>( msg ) };

    return handle;
}

template< typename T >
HANDLE ThrowIfFailed( BOOL succeeded, T&& msg )
{
    if( !succeeded )
        throw std::system_error{ HRESULT_FROM_WIN32(GetLastError()), std::system_category(), std::forward<T>( msg ) };
}

#endif // _MACROS_H
