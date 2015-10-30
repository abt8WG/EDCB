#include "stdafx.h"
#include "Util.h"

BOOL _CreateDirectory( LPCTSTR lpPathName )
{
	BOOL bRet = FALSE;
	if( _tcslen(lpPathName) > 2 ){
		vector<TCHAR> createPath(lpPathName, lpPathName + _tcslen(lpPathName) + 1);
		
		for (int i = 2; createPath[i] != _T('\0'); i++) {
			if (createPath[i] == _T('\\') || createPath[i+1] == _T('\0')) {
				TCHAR c = createPath[i+1];
				createPath[i+1] = _T('\0');
				if ( GetFileAttributes(&createPath.front()) == 0xFFFFFFFF ) {
					bRet = ::CreateDirectory( &createPath.front(), NULL );
				}
				createPath[i+1] = c;
			}
		}
	}

	return bRet;
}

HANDLE _CreateDirectoryAndFile( LPCTSTR lpFileName, DWORD dwDesiredAccess, DWORD dwShareMode, LPSECURITY_ATTRIBUTES lpsa, DWORD dwCreationDisposition, DWORD dwFlagsAndAttributes, HANDLE hTemplateFile )
{
	HANDLE hFile =  ::CreateFile( lpFileName, dwDesiredAccess, dwShareMode, lpsa, dwCreationDisposition, dwFlagsAndAttributes, hTemplateFile );
	if( hFile == INVALID_HANDLE_VALUE ){
		const TCHAR* p = _tcsrchr(lpFileName, _T('\\'));
		if( p != NULL ){
			vector<TCHAR> dirPath(lpFileName, p + 1);
			dirPath.back() = _T('\0');
			_CreateDirectory(&dirPath.front());
			hFile =  ::CreateFile( lpFileName, dwDesiredAccess, dwShareMode, lpsa, dwCreationDisposition, dwFlagsAndAttributes, hTemplateFile );
		}
	}
	return hFile;
}

BOOL _GetDiskFreeSpaceEx(
  LPCTSTR lpDirectoryName,                 // ディレクトリ名
  PULARGE_INTEGER lpFreeBytesAvailable,    // 呼び出し側が利用できるバイト数
  PULARGE_INTEGER lpTotalNumberOfBytes,    // ディスク全体のバイト数
  PULARGE_INTEGER lpTotalNumberOfFreeBytes // ディスク全体の空きバイト数
)
{
	TCHAR szVolumePathName[MAX_PATH] = _T("");
	if( GetVolumePathName( lpDirectoryName, szVolumePathName, MAX_PATH) == FALSE ){
		return GetDiskFreeSpaceEx( lpDirectoryName, lpFreeBytesAvailable, lpTotalNumberOfBytes, lpTotalNumberOfFreeBytes );
	}
	TCHAR szMount[MAX_PATH] = _T("");
	if( GetVolumeNameForVolumeMountPoint(szVolumePathName, szMount, MAX_PATH) == FALSE ){
		return GetDiskFreeSpaceEx( szVolumePathName, lpFreeBytesAvailable, lpTotalNumberOfBytes, lpTotalNumberOfFreeBytes );
	}
	return GetDiskFreeSpaceEx( szMount, lpFreeBytesAvailable, lpTotalNumberOfBytes, lpTotalNumberOfFreeBytes );
}

__int64 GetFileLastModified(const wstring& path)
{
	WIN32_FIND_DATA data;
	HANDLE hfind = FindFirstFile(path.c_str(), &data);
	if (hfind == INVALID_HANDLE_VALUE) {
		return 0;
	}
	FindClose(hfind);
	return (__int64(data.ftLastWriteTime.dwHighDateTime) << 32) | data.ftLastWriteTime.dwLowDateTime;
}

bool GetFileExist(const wstring& path)
{
	DWORD attr = GetFileAttributes(path.c_str());
	return (attr != -1 && (attr & FILE_ATTRIBUTE_DIRECTORY) == 0);
}

void _OutputDebugString(const TCHAR *format, ...)
{
	va_list params;

	va_start(params, format);
	try{
		int length = _vsctprintf(format, params);
		if( length >= 0 ){
			vector<TCHAR> buff(length + 1);
			_vstprintf_s(&buff.front(), buff.size(), format, params);
			OutputDebugString(&buff.front());
		}
	}catch(...){
		va_end(params);
		throw;
	}

	va_end(params);
}

void GetLastErrMsg(DWORD err, wstring& msg)
{
	LPVOID lpMsgBuf;
	if( FormatMessageW(
		FORMAT_MESSAGE_ALLOCATE_BUFFER|FORMAT_MESSAGE_FROM_SYSTEM|FORMAT_MESSAGE_IGNORE_INSERTS,
		NULL,
		err,
		MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),
		(LPWSTR)&lpMsgBuf,
		0,
		NULL) == 0 ){
		msg.clear();
		return;
	}
	msg = (LPWSTR)lpMsgBuf;
	LocalFree( lpMsgBuf );
}

LONG FilterException(struct _EXCEPTION_POINTERS * ExceptionInfo) {
	if (ExceptionInfo->ExceptionRecord->ExceptionCode == 0xe06d7363) {
		// C++例外の場合は、意図的に出した例外なので、そのまま上に持っていく
		return EXCEPTION_CONTINUE_SEARCH;
	}
	// それ以外の場合は原因不明なので、エラー出力させる
	return UnhandledExceptionFilter(ExceptionInfo);
}
