#ifndef __UTIL_H__
#define __UTIL_H__

HANDLE _CreateDirectoryAndFile( LPCTSTR lpFileName, DWORD dwDesiredAccess, DWORD dwShareMode, LPSECURITY_ATTRIBUTES lpsa, DWORD dwCreationDisposition, DWORD dwFlagsAndAttributes, HANDLE hTemplateFile );
BOOL _CreateDirectory( LPCTSTR lpPathName );
//�{�����[���̃}�E���g���l�����Ď��h���C�u�̋󂫂��擾����
BOOL _GetDiskFreeSpaceEx(
  LPCTSTR lpDirectoryName,                 // �f�B���N�g����
  PULARGE_INTEGER lpFreeBytesAvailable,    // �Ăяo���������p�ł���o�C�g��
  PULARGE_INTEGER lpTotalNumberOfBytes,    // �f�B�X�N�S�̂̃o�C�g��
  PULARGE_INTEGER lpTotalNumberOfFreeBytes // �f�B�X�N�S�̂̋󂫃o�C�g��
);
void GetLastErrMsg(DWORD err, wstring& msg);
std::basic_string<TCHAR> GetPrivateProfileToString(LPCTSTR lpAppName, LPCTSTR lpKeyName, LPCTSTR lpDefault, LPCTSTR lpFileName);
BOOL WritePrivateProfileInt(LPCTSTR lpAppName, LPCTSTR lpKeyName, int value, LPCTSTR lpFileName);

#endif
