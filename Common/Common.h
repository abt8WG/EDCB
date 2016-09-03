#pragma once

// すべてのプロジェクトに適用される追加ヘッダおよび定義

#include <string>
#include <utility>
#include <map>
#include <set>
#include <vector>
#include <memory>
#include <algorithm>
#include <tchar.h>
#include <windows.h>
#include <stdarg.h>

using std::string;
using std::wstring;
using std::pair;
using std::map;
using std::multimap;
using std::vector;

// 'identifier': unreferenced formal parameter
#pragma warning(disable : 4100)

#if defined(_MSC_VER) && _MSC_VER < 1900
// 'class': assignment operator was implicitly defined as deleted
#pragma warning(disable : 4512)
#endif

// 適切でないNULLの検出用
//#undef NULL
//#define NULL nullptr

template<class T> inline void SAFE_DELETE(T*& p) { delete p; p = NULL; }
template<class T> inline void SAFE_DELETE_ARRAY(T*& p) { delete[] p; p = NULL; }

inline void _OutputDebugString(const TCHAR* format, ...)
{
	// TODO: この関数名は予約名違反の上に紛らわしいので変更すべき
	va_list params;
	va_start(params, format);
	int length = _vsctprintf(format, params);
	va_end(params);
	if( length >= 0 ){
		TCHAR* buff = new TCHAR[length + 1];
		va_start(params, format);
		_vstprintf_s(buff, length + 1, format, params);
		va_end(params);
		OutputDebugString(buff);
		delete[] buff;
	}
}

inline LONG FilterException(struct _EXCEPTION_POINTERS * ExceptionInfo) {
	if (ExceptionInfo->ExceptionRecord->ExceptionCode == 0xe06d7363) {
		// C++例外の場合は、意図的に出した例外なので、そのまま上に持っていく
		return EXCEPTION_CONTINUE_SEARCH;
	}
	// それ以外の場合は原因不明なので、エラー出力させる
	return UnhandledExceptionFilter(ExceptionInfo);
}
