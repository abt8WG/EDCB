#pragma once

#include <stdint.h>

#include "Util.h"

__int64 GetFileLastModified(const wstring& path);
bool GetFileExist(const wstring& path);
bool GetDirectoryExist(const wstring& path);
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
