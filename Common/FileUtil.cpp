#include "stdafx.h"

#include "PathUtil.h"
#include "FileUtil.h"

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

bool GetDirectoryExist(const wstring& path)
{
	DWORD attr = GetFileAttributes(path.c_str());
	return (attr != -1 && (attr & FILE_ATTRIBUTE_DIRECTORY) != 0);
}

HANDLE FileOpenRead(const wstring& filepath) {
	return CreateFile(filepath.c_str(), GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
}

void CDirectoryCache::CachePath(const wstring& filepath) {
	wstring folderPath;
	GetFileFolder(filepath, folderPath);
	directoryMap[folderPath];
}

void CDirectoryCache::UpdateDirectoryInfo() {
	// ディレクトリの更新を検知
#if _MSC_VER < 1700
	for (auto itr = directoryMap.begin(); itr != directoryMap.end(); itr++) {
		auto& entry = *itr;
#else
	for (auto& entry : directoryMap) {
#endif
		entry.second.exist = GetDirectoryExist(entry.first);
		if (entry.second.exist) {
			int64_t lastModified = GetFileLastModified(entry.first);
			if (entry.second.lastModified != lastModified) {
				const wstring& searchpath = entry.first + L"\\*";
				auto& files = entry.second.files;
				files.clear();

				WIN32_FIND_DATA fdata;
				HANDLE hFind = FindFirstFile(searchpath.c_str(), &fdata);
				if (hFind == INVALID_HANDLE_VALUE) {
					// 開けないので存在しないことにしちゃう
					entry.second.exist = false;
				}
				else {
					do {
						if (fdata.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) {
							// ディレクトリ
						}
						else {
							files.insert(fdata.cFileName);
						}
					} while (FindNextFile(hFind, &fdata));
					FindClose(hFind);
				}
				entry.second.lastModified = lastModified;
			}
		}
	}
}

HANDLE CDirectoryCache::Open(const wstring& filepath, bool withCache) {
	if (!withCache) {
		return FileOpenRead(filepath);
	}
	if (!Exist(filepath)) {
		return INVALID_HANDLE_VALUE;
	}
	return FileOpenRead(filepath);
}

bool CDirectoryCache::Exist(const wstring& filepath, bool withCache) {
	if (!withCache) {
		return GetFileExist(filepath);
	}
	wstring folderPath;
	GetFileFolder(filepath, folderPath);
	auto it = directoryMap.find(folderPath);
	if (it == directoryMap.end()) { // キャッシュにない
		return GetFileExist(filepath);
	}
	if (it->second.exist == false) { // ディレクトリがない
		return false;
	}
	wstring fileName;
	GetFileName(filepath, fileName);
	auto it2 = it->second.files.find(fileName);
	return (it2 != it->second.files.end());
}
