#pragma once

#include <stdint.h>

#include "Util.h"

__int64 GetFileLastModified(const wstring& path);
bool GetFileExist(const wstring& path);
bool GetDirectoryExist(const wstring& path);
HANDLE FileOpenRead(const wstring& filepath);

// ディレクトリのファイルリストをキャッシュして存在確認やファイルオープン操作を高速化
// 使い方
//  1. 開く予定のファイルをCachePath()で登録（これはあくまでキャッシュするためのヒントなので実際に開くのと違ってもよい）
//  2. UpdateDirectoryInfo()でディレクトリ情報を更新
//  3. Open() or Exist() でファイルにアクセス
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
