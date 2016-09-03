#pragma once

#include "TVTestPlugin.h"
#include "..\..\Common\CommonDef.h"
#include "..\..\Common\SendCtrlCmd.h"
#include "EDCBSupportUtil.h"

#ifndef TVTEST_PLUGIN_CLASS_IMPLEMENT
extern HINSTANCE g_hinstDLL;
#endif

namespace EDCBSupport
{

	// 設定
	struct EDCBSettings
	{
		wstring EDCBDirectory;
		bool fUseNetwork;
		wstring NetworkAddress;
		WORD NetworkPort;
		wstring NetworkPassword;

		EDCBSettings()
			: fUseNetwork(false)
			, NetworkAddress(L"127.0.0.1")
			, NetworkPort(4510)
			, NetworkPassword(L"")
		{
		}
	};

	// コア機能
	class CEDCBSupportCore abstract
	{
	public:
		virtual void ShowCmdErrorMessage(HWND hwndOwner,LPCTSTR pszMessage,DWORD Err) const = 0;
		virtual bool ChangeReserve(HWND hwndOwner,const RESERVE_DATA &ReserveData) = 0;
		virtual bool ChangeReserve(HWND hwndOwner,std::vector<RESERVE_DATA> &ReserveList) = 0;
		virtual bool DeleteReserve(HWND hwndOwner,DWORD ReserveID) = 0;
		virtual bool DeleteReserve(HWND hwndOwner,std::vector<DWORD> &ReserveIDList) = 0;
		virtual bool ShowReserveDialog(HWND hwndOwner,RESERVE_DATA *pReserveData) = 0;
		virtual bool GetRecordedFileList(HWND hwndOwner,std::vector<REC_FILE_INFO> *pFileList) = 0;
		virtual bool DeleteRecordedFileInfo(HWND hwndOwner,DWORD ID) = 0;
		virtual bool DeleteRecordedFileInfo(HWND hwndOwner,std::vector<DWORD> &IDList) = 0;
		EDCBSettings &GetSettings() { return m_EDCBSettings; }
		const EDCBSettings &GetSettings() const { return m_EDCBSettings; }

	protected:
		EDCBSettings m_EDCBSettings;
		CSendCtrlCmd m_SendCtrlCmd;
	};

}	// EDCBSupport
