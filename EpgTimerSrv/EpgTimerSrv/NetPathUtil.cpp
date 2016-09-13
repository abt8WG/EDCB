//
// PathUtil.cpp & .h に追加すると、全モジュールに netapi32.dll と shlwapi.dll の依存が出てくるので
// EpgTimerSrv 専用に分けておく。optimize すれば大丈夫な気はするけど。
//
#include "stdafx.h"
#include <wchar.h>
#include <shlwapi.h>
#include <Lm.h>
#include "../../Common/StringUtil.h"

#pragma comment(lib, "netapi32.lib")
#pragma comment(lib, "shlwapi.lib")

BOOL GetNetworkPath(const wstring strPath, wstring& strNetPath)
{
	// UNCパスはそのまま返す
	if (strPath.compare(0, 2, L"\\\\") == 0)
	{
		strNetPath = strPath;
		return TRUE;
	}

	TCHAR computername[MAX_COMPUTERNAME_LENGTH + 1];
	DWORD len = MAX_COMPUTERNAME_LENGTH + 1;
	if (!GetComputerName(computername, &len)) return FALSE;

	wstring relative;
	wstring netname;
	NET_API_STATUS res;
	do
	{
		PSHARE_INFO_502 BufPtr, p;
		DWORD er = 0, tr = 0, resume = 0;
		res = NetShareEnum(NULL, 502, (LPBYTE *)&BufPtr, MAX_PREFERRED_LENGTH, &er, &tr, &resume);
		if (res == ERROR_SUCCESS || res == ERROR_MORE_DATA)
		{
			p = BufPtr;
			for (DWORD i = 1; i <= er; i++)
			{
				// 共有名が$で終わるのは隠し共有
				if (p->shi502_netname[_tcslen(p->shi502_netname)-1] != _T('$'))
				{
					if (PathIsDirectory(p->shi502_path))
					{
						TCHAR tmp[MAX_PATH];
						if (CompareNoCase(p->shi502_path, strPath) == 0)
						{
							// shi502_pathとstrPath が同じ時に PathRelativePathTo が "." の代わりに "..\\<folder>" を返すので別処理をする
							relative = _T(".");
							netname = p->shi502_netname;
						}
						else if (PathRelativePathTo(tmp, p->shi502_path, FILE_ATTRIBUTE_DIRECTORY, strPath.c_str(), 0))
						{
							if (wcsncmp(tmp, _T("..\\"), 3) != 0 && (relative.empty() || relative.length() > _tcslen(tmp)))
							{
								relative = tmp;
								netname = p->shi502_netname;
							}
						}
					}
				}
				p++;
			}
			NetApiBufferFree(BufPtr);
		}
	} while (res==ERROR_MORE_DATA);

	if (relative.length() < 1) return FALSE;

	strNetPath = _T("\\\\") + wstring(computername) + _T("\\") + netname + relative.substr(1);

	return TRUE;
}
