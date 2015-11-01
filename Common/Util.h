#ifndef __UTIL_H__
#define __UTIL_H__

#include <string>
#include <map>
#include <set>
#include <vector>
#include <algorithm>
using std::string;
using std::wstring;
using std::pair;
using std::map;
using std::multimap;
using std::vector;

#include <TCHAR.h>
#include <windows.h>


template<class T> inline void SAFE_DELETE(T*& p) { delete p; p = NULL; }
template<class T> inline void SAFE_DELETE_ARRAY(T*& p) { delete[] p; p = NULL; }

HANDLE _CreateDirectoryAndFile( LPCTSTR lpFileName, DWORD dwDesiredAccess, DWORD dwShareMode, LPSECURITY_ATTRIBUTES lpsa, DWORD dwCreationDisposition, DWORD dwFlagsAndAttributes, HANDLE hTemplateFile );
BOOL _CreateDirectory( LPCTSTR lpPathName );
//�{�����[���̃}�E���g���l�����Ď��h���C�u�̋󂫂��擾����
BOOL _GetDiskFreeSpaceEx(
  LPCTSTR lpDirectoryName,                 // �f�B���N�g����
  PULARGE_INTEGER lpFreeBytesAvailable,    // �Ăяo���������p�ł���o�C�g��
  PULARGE_INTEGER lpTotalNumberOfBytes,    // �f�B�X�N�S�̂̃o�C�g��
  PULARGE_INTEGER lpTotalNumberOfFreeBytes // �f�B�X�N�S�̂̋󂫃o�C�g��
);
__int64 GetFileLastModified(const wstring& path);
bool GetFileExist(const wstring& path);
bool GetDirectoryExist(const wstring& path);

void _OutputDebugString(const TCHAR *pOutputString, ...);
void GetLastErrMsg(DWORD err, wstring& msg);

LONG FilterException(struct _EXCEPTION_POINTERS * ExceptionInfo);

HANDLE FileOpenRead(const wstring& filepath);

// �f�B���N�g���̃t�@�C�����X�g���L���b�V�����đ��݊m�F��t�@�C���I�[�v�������������
// �g����
//  1. �J���\��̃t�@�C����CachePath()�œo�^�i����͂����܂ŃL���b�V�����邽�߂̃q���g�Ȃ̂Ŏ��ۂɊJ���̂ƈ���Ă��悢�j
//  2. UpdateDirectoryInfo()�Ńf�B���N�g�������X�V
//  3. Open() or Exist() �Ńt�@�C���ɃA�N�Z�X
class CDirectoryCache {
public:
	void CachePath(const wstring& filepath);
	void UpdateDirectoryInfo();
	HANDLE Open(const wstring& filepath, bool withCache = true);
	bool Exist(const wstring& filepath, bool withCache = true);
private:
	struct DirectoryInfo {
		int64_t lastModified;
		bool exist;
		std::set<wstring> files;

		DirectoryInfo()
			: lastModified(0)
			, exist(false)
		{ }
	};
	std::map<wstring, DirectoryInfo> directoryMap;
};


#endif
