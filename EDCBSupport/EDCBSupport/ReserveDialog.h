#pragma once

#include <deque>


namespace EDCBSupport
{

	// 予約設定ダイアログ
	class CReserveDialog
	{
	public:
		CReserveDialog(EDCBSettings &Settings,CSendCtrlCmd &SendCtrlCmd);
		~CReserveDialog();
		bool Show(HINSTANCE hinst,HWND hwndOwner,
				  RESERVE_DATA *pData,const TVTest::EpgEventInfo *pEventInfo=NULL);

		typedef std::deque<wstring> BatFileList;
		static bool AddBatFileList(LPCWSTR pszFile);
		static const BatFileList &GetBatFileList() { return m_BatFileList; }

	private:
		EDCBSettings &m_EDCBSettings;
		CSendCtrlCmd &m_SendCtrlCmd;
		HINSTANCE m_hinst;
		RESERVE_DATA *m_pReserveData;
		const TVTest::EpgEventInfo *m_pEventInfo;

		static BatFileList m_BatFileList;

		void InitDialog(HWND hDlg);
		bool GetSettings(HWND hDlg);

		static INT_PTR CALLBACK DlgProc(HWND hDlg,UINT uMsg,WPARAM wParam,LPARAM lParam);
	};

}
