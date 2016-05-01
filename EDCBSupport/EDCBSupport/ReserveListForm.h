#pragma once


#include "Tab.h"
#include "StatusBar.h"
#include "ReserveListView.h"
#include "FileListView.h"


namespace EDCBSupport
{

	// 予約一覧ウィンドウ
	class CReserveListForm : public CWidget, public CListView::CEventHandler
	{
	public:
		enum {
			TAB_RESERVELIST,
			TAB_FILELIST,
			NUM_TABS
		};

		static bool Initialize(HINSTANCE hinst);

		CReserveListForm(CEDCBSupportCore *pCore);
		~CReserveListForm();

		bool Create(HWND hwndParent,INT_PTR ID=0) override;
		bool Show();
		void SetReserveList(std::vector<RESERVE_DATA> *pReserveList);
		bool NotifyReserveListChanged();
		bool NotifyRecordedListChanged();
		void LoadSettings(LPCTSTR pszFileName);
		void SaveSettings(LPCTSTR pszFileName) const;
		bool SetCurrentTab(int Tab);
		bool SetReserveListColor(int Type,COLORREF Color);
		bool SetFileListColor(int Type,COLORREF Color);

	private:
		struct PositionInfo
		{
			int Left,Top,Width,Height;
		};

		struct TabInfo
		{
			CWidget *pWidget;
			LPCTSTR pszText;
		};

		static const LPCTSTR m_pszWindowClass;
		static ATOM m_ClassAtom;

		CEDCBSupportCore *m_pCore;
		PositionInfo m_Position;
		CTab m_Tab;
		CStatusBar m_StatusBar;
		CReserveListView m_ReserveListView;
		CFileListView m_FileListView;
		TabInfo m_TabList[NUM_TABS];
		int m_CurrentTab;
		std::vector<RESERVE_DATA> *m_pReserveList;

		static LRESULT CALLBACK WndProc(HWND hwnd,UINT uMsg,WPARAM wParam,LPARAM lParam);

		LRESULT OnMessage(HWND hwnd,UINT uMsg,WPARAM wParam,LPARAM lParam);
		void OnCommand(int Command,int NotifyCode);
		void LoadListColumnSettings(CListView &ListView,LPCTSTR pszListName,LPCTSTR pszFileName);
		void SaveListColumnSettings(const CListView &ListView,LPCTSTR pszListName,LPCTSTR pszFileName) const;
		void UpdateStatusText();

	// CListView::CEventHandler
		void OnRClick(CListView *pListView) override;
		void OnDoubleClick(CListView *pListView,int Item) override;
		void OnKeyDown(CListView *pListView,UINT Key) override;
	};

}
