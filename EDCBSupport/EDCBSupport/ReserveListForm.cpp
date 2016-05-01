#include "stdafx.h"
#include "EDCBSupport.h"
#include "ReserveListForm.h"
#include "resource.h"


namespace EDCBSupport
{

	enum {
		IDC_TAB=1000,
		IDC_STATUS,
		IDC_RESERVELIST,
		IDC_FILELIST
	};

	enum {
		WM_APP_RESERVELISTCHANGED=WM_APP,
		WM_APP_RECORDEDLISTCHANGED
	};


	const LPCTSTR CReserveListForm::m_pszWindowClass=TEXT("EDCBSupport Reserve List Form");
	ATOM CReserveListForm::m_ClassAtom=0;


	bool CReserveListForm::Initialize(HINSTANCE hinst)
	{
		if (m_ClassAtom==0) {
			WNDCLASS wc;

			wc.style=0;
			wc.lpfnWndProc=WndProc;
			wc.cbClsExtra=0;
			wc.cbWndExtra=0;
			wc.hInstance=hinst;
			wc.hIcon=::LoadIcon(hinst,MAKEINTRESOURCE(IDI_RESERVELIST));
			wc.hCursor=::LoadCursor(NULL,IDC_ARROW);
			wc.hbrBackground=reinterpret_cast<HBRUSH>(COLOR_3DFACE+1);
			wc.lpszMenuName=NULL;
			wc.lpszClassName=m_pszWindowClass;
			m_ClassAtom=::RegisterClass(&wc);
			if (m_ClassAtom==0)
				return false;
		}
		return true;
	}


	CReserveListForm::CReserveListForm(CEDCBSupportCore *pCore)
		: m_pCore(pCore)
		, m_pReserveList(NULL)
		, m_CurrentTab(TAB_RESERVELIST)
	{
		m_Position.Left=0;
		m_Position.Top=0;
		m_Position.Width=640;
		m_Position.Height=480;

		m_TabList[TAB_RESERVELIST].pWidget=&m_ReserveListView;
		m_TabList[TAB_RESERVELIST].pszText=TEXT("ó\ñÒàÍóó");
		m_TabList[TAB_FILELIST].pWidget=&m_FileListView;
		m_TabList[TAB_FILELIST].pszText=TEXT("ò^âÊçœÇ›àÍóó");
	}


	CReserveListForm::~CReserveListForm()
	{
		Destroy();
	}


	bool CReserveListForm::Create(HWND hwndParent,INT_PTR ID)
	{
		if (m_ClassAtom==0)
			return false;

		Destroy();

		m_hwnd=::CreateWindowEx(0,
								MAKEINTATOM(m_ClassAtom),
								TEXT("EpgTimeró\ñÒàÍóó"),
								WS_OVERLAPPEDWINDOW | WS_CLIPCHILDREN,
								0,0,m_Position.Width,m_Position.Height,
								hwndParent,
								NULL,
								g_hinstDLL,
								this);
		if (m_hwnd==NULL)
			return false;

		return true;
	}


	bool CReserveListForm::Show()
	{
		if (m_hwnd==NULL)
			return false;

		WINDOWPLACEMENT wp;
		wp.length=sizeof(wp);
		::GetWindowPlacement(m_hwnd,&wp);
		wp.showCmd=SW_SHOWNORMAL;
		wp.rcNormalPosition.left=m_Position.Left;
		wp.rcNormalPosition.top=m_Position.Top;
		wp.rcNormalPosition.right=m_Position.Left+m_Position.Width;
		wp.rcNormalPosition.bottom=m_Position.Top+m_Position.Height;
		::SetWindowPlacement(m_hwnd,&wp);

		::UpdateWindow(m_hwnd);

		return true;
	}


	void CReserveListForm::SetReserveList(std::vector<RESERVE_DATA> *pReserveList)
	{
		m_pReserveList=pReserveList;
		if (m_hwnd!=NULL) {
			m_ReserveListView.SetReserveList(*pReserveList);
			if (m_CurrentTab==TAB_RESERVELIST)
				UpdateStatusText();
		}
	}


	bool CReserveListForm::NotifyReserveListChanged()
	{
		if (m_hwnd==NULL)
			return false;
		::PostMessage(m_hwnd,WM_APP_RESERVELISTCHANGED,0,0);
		return true;
	}


	bool CReserveListForm::NotifyRecordedListChanged()
	{
		if (m_hwnd==NULL)
			return false;
		::PostMessage(m_hwnd,WM_APP_RECORDEDLISTCHANGED,0,0);
		return true;
	}


	void CReserveListForm::LoadSettings(LPCTSTR pszFileName)
	{
		m_Position.Left=GetPrivateProfileInt(TEXT("Settings"),TEXT("ReserveListForm.Left"),
											 m_Position.Left,pszFileName);
		m_Position.Top=GetPrivateProfileInt(TEXT("Settings"),TEXT("ReserveListForm.Top"),
											m_Position.Top,pszFileName);
		m_Position.Width=GetPrivateProfileInt(TEXT("Settings"),TEXT("ReserveListForm.Width"),
											  m_Position.Width,pszFileName);
		m_Position.Height=GetPrivateProfileInt(TEXT("Settings"),TEXT("ReserveListForm.Height"),
											   m_Position.Height,pszFileName);

		LoadListColumnSettings(m_ReserveListView,TEXT("ReserveList"),pszFileName);
		LoadListColumnSettings(m_FileListView,TEXT("FileList"),pszFileName);
	}


	void CReserveListForm::SaveSettings(LPCTSTR pszFileName) const
	{
		WritePrivateProfileInt(TEXT("Settings"),TEXT("ReserveListForm.Left"),
							   m_Position.Left,pszFileName);
		WritePrivateProfileInt(TEXT("Settings"),TEXT("ReserveListForm.Top"),
							   m_Position.Top,pszFileName);
		WritePrivateProfileInt(TEXT("Settings"),TEXT("ReserveListForm.Width"),
							   m_Position.Width,pszFileName);
		WritePrivateProfileInt(TEXT("Settings"),TEXT("ReserveListForm.Height"),
							   m_Position.Height,pszFileName);

		SaveListColumnSettings(m_ReserveListView,TEXT("ReserveList"),pszFileName);
		SaveListColumnSettings(m_FileListView,TEXT("FileList"),pszFileName);
	}


	void CReserveListForm::LoadListColumnSettings(CListView &ListView,LPCTSTR pszListName,LPCTSTR pszFileName)
	{
		TCHAR szName[256];

		for (int i=0;i<ListView.NumColumns();i++) {
			const CListView::CColumn *pColumn=ListView.GetColumnByIndex(i);

			::wsprintf(szName,TEXT("%s.%s.Width"),pszListName,pColumn->GetName());
			int Width=GetPrivateProfileInt(TEXT("Settings"),szName,-1,pszFileName);
			if (Width>=0)
				ListView.SetColumnWidth(pColumn->GetID(),Width);
			::wsprintf(szName,TEXT("%s.%s.Order"),pszListName,pColumn->GetName());
			int Order=GetPrivateProfileInt(TEXT("Settings"),szName,-1,pszFileName);
			if (Order>=0)
				ListView.SetColumnOrder(pColumn->GetID(),Order);
		}
	}


	void CReserveListForm::SaveListColumnSettings(const CListView &ListView,LPCTSTR pszListName,LPCTSTR pszFileName) const
	{
		TCHAR szName[256];

		for (int i=0;i<ListView.NumColumns();i++) {
			const CListView::CColumn *pColumn=ListView.GetColumnByIndex(i);

			::wsprintf(szName,TEXT("%s.%s.Width"),pszListName,pColumn->GetName());
			WritePrivateProfileInt(TEXT("Settings"),szName,
								   ListView.GetColumnWidth(pColumn->GetID()),
								   pszFileName);
			::wsprintf(szName,TEXT("%s.%s.Order"),pszListName,pColumn->GetName());
			WritePrivateProfileInt(TEXT("Settings"),szName,
								   ListView.GetColumnOrder(pColumn->GetID()),
								   pszFileName);
		}
	}


	bool CReserveListForm::SetCurrentTab(int Tab)
	{
		if (Tab!=m_CurrentTab) {
			if (Tab<0 || Tab>=NUM_TABS)
				return false;

			if (m_hwnd!=NULL) {
				RECT rc;
				m_TabList[m_CurrentTab].pWidget->GetPosition(&rc);
				m_TabList[Tab].pWidget->SetPosition(rc);
				m_TabList[Tab].pWidget->SetVisible(true);
				m_TabList[m_CurrentTab].pWidget->SetVisible(false);

				m_CurrentTab=Tab;

				UpdateStatusText();
				::UpdateWindow(m_hwnd);

				if (Tab==TAB_FILELIST)
					::PostMessage(m_hwnd,WM_APP_RECORDEDLISTCHANGED,0,0);
			} else {
				m_CurrentTab=Tab;
			}
		}

		return true;
	}


	bool CReserveListForm::SetReserveListColor(int Type,COLORREF Color)
	{
		return m_ReserveListView.SetColor(Type,Color);
	}


	bool CReserveListForm::SetFileListColor(int Type,COLORREF Color)
	{
		return m_FileListView.SetColor(Type,Color);
	}


	LRESULT CALLBACK CReserveListForm::WndProc(HWND hwnd,UINT uMsg,WPARAM wParam,LPARAM lParam)
	{
		CReserveListForm *pThis;

		if (uMsg==WM_CREATE) {
			LPCREATESTRUCT pcs=reinterpret_cast<LPCREATESTRUCT>(lParam);

			pThis=reinterpret_cast<CReserveListForm*>(pcs->lpCreateParams);
			::SetWindowLongPtr(hwnd,GWLP_USERDATA,reinterpret_cast<LONG_PTR>(pThis));
		} else {
			pThis=reinterpret_cast<CReserveListForm*>(::GetWindowLongPtr(hwnd,GWLP_USERDATA));
			if (pThis==NULL)
				return ::DefWindowProc(hwnd,uMsg,wParam,lParam);
			if (uMsg==WM_NCDESTROY) {
				pThis->m_hwnd=NULL;
				return 0;
			}
		}
		return pThis->OnMessage(hwnd,uMsg,wParam,lParam);
	}


	LRESULT CReserveListForm::OnMessage(HWND hwnd,UINT uMsg,WPARAM wParam,LPARAM lParam)
	{
		switch (uMsg) {
		case WM_CREATE:
			{
				m_Tab.Create(hwnd,IDC_TAB);
				m_Tab.SetImageList(::ImageList_LoadImage(g_hinstDLL,
														 MAKEINTRESOURCE(IDB_TABICONS),
														 16,1,CLR_NONE,IMAGE_BITMAP,
														 LR_CREATEDIBSECTION));
				for (size_t i=0;i<_countof(m_TabList);i++)
					m_Tab.AddItem(m_TabList[i].pszText,(int)i);
				m_Tab.SetSel(m_CurrentTab);
				m_Tab.SetVisible(true);

				m_StatusBar.Create(hwnd,IDC_STATUS);
				m_StatusBar.SetVisible(true);

				m_ReserveListView.Create(hwnd,IDC_RESERVELIST);
				m_ReserveListView.SetEventHandler(this);

				m_FileListView.Create(hwnd,IDC_FILELIST);
				m_FileListView.SetEventHandler(this);

				m_TabList[m_CurrentTab].pWidget->SetVisible(true);

				if (m_CurrentTab==TAB_FILELIST)
					::PostMessage(hwnd,WM_APP_RECORDEDLISTCHANGED,0,0);
			}
			return 0;

		case WM_SIZE:
			{
				int Width=LOWORD(lParam),Height=HIWORD(lParam);
				int TabHeight=m_Tab.GetTabHeight();
				int StatusHeight=m_StatusBar.GetHeight();
				int ListHeight=Height-(TabHeight+StatusHeight);

				m_Tab.SetPosition(0,0,Width,TabHeight);
				m_TabList[m_CurrentTab].pWidget->SetPosition(0,TabHeight,Width,max(ListHeight,0));
				m_StatusBar.SetPosition(0,TabHeight+ListHeight,Width,StatusHeight);
			}
			return 0;

		case WM_COMMAND:
			OnCommand(LOWORD(wParam),HIWORD(wParam));
			return 0;

		case WM_NOTIFY:
			{
				LPNMHDR pnmhdr=reinterpret_cast<LPNMHDR>(lParam);

				switch (pnmhdr->code) {
				case TCN_SELCHANGE:
					SetCurrentTab(m_Tab.GetSel());
					return 0;

				default:
					{
						LRESULT Result;

						if (m_ReserveListView.OnNotify(wParam,lParam,&Result))
							return Result;
						if (m_FileListView.OnNotify(wParam,lParam,&Result))
							return Result;
					}
				}
			}
			break;

		case WM_APP_RESERVELISTCHANGED:
			if (m_pReserveList!=NULL)
				m_ReserveListView.SetReserveList(*m_pReserveList);
			if (m_CurrentTab==TAB_RESERVELIST)
				UpdateStatusText();
			return 0;

		case WM_APP_RECORDEDLISTCHANGED:
			{
				std::vector<REC_FILE_INFO> RecFileList;

				if (m_pCore->GetRecordedFileList(m_hwnd,&RecFileList)) {
					m_FileListView.SetRecFileList(RecFileList);
					UpdateStatusText();
				}
			}
			return 0;

		case WM_DESTROY:
			{
				WINDOWPLACEMENT wp;
				wp.length=sizeof(wp);
				::GetWindowPlacement(m_hwnd,&wp);
				m_Position.Left=wp.rcNormalPosition.left;
				m_Position.Top=wp.rcNormalPosition.top;
				m_Position.Width=wp.rcNormalPosition.right-wp.rcNormalPosition.left;
				m_Position.Height=wp.rcNormalPosition.bottom-wp.rcNormalPosition.top;

				m_Tab.Destroy();
				m_StatusBar.Destroy();
				m_ReserveListView.Destroy();
				m_FileListView.Destroy();
			}
			return 0;
		}
		return ::DefWindowProc(hwnd,uMsg,wParam,lParam);
	}


	void CReserveListForm::OnCommand(int Command,int NotifyCode)
	{
		switch (Command) {
		case CM_RESERVE_SETTINGS:
			{
				const int Index=m_ReserveListView.GetSelectedItem();

				if (Index>=0) {
					const RESERVE_DATA *pReserveData=m_ReserveListView.GetItemReserveData(Index);

					if (pReserveData!=NULL) {
						RESERVE_DATA Reserve=*pReserveData;

						if (m_pCore->ShowReserveDialog(m_hwnd,&Reserve))
							m_pCore->ChangeReserve(m_hwnd,Reserve);
					}
				}
			}
			return;

		case CM_RESERVE_PRIORITY_1:
		case CM_RESERVE_PRIORITY_2:
		case CM_RESERVE_PRIORITY_3:
		case CM_RESERVE_PRIORITY_4:
		case CM_RESERVE_PRIORITY_5:
			{
				const int Priority=(Command-CM_RESERVE_PRIORITY_1)+1;
				std::vector<int> Items;

				if (m_ReserveListView.GetSelectedItems(&Items)) {
					std::vector<RESERVE_DATA> ReserveList;

					for (size_t i=0;i<Items.size();i++) {
						RESERVE_DATA *pReserveData=m_ReserveListView.GetItemReserveData(Items[i]);

						if (pReserveData!=NULL
								&& pReserveData->recSetting.priority!=Priority) {
							ReserveList.push_back(*pReserveData);
							ReserveList.back().recSetting.priority=Priority;
						}
					}
					if (ReserveList.size()>0) {
						m_pCore->ChangeReserve(m_hwnd,ReserveList);
					}
				}
			}
			return;

		case CM_RESERVE_DELETE:
			{
				std::vector<int> Items;

				if (m_ReserveListView.GetSelectedItems(&Items)) {
					std::vector<DWORD> ReserveIDList;
					wstring *pTitle=NULL;

					for (size_t i=0;i<Items.size();i++) {
						RESERVE_DATA *pReserveData=m_ReserveListView.GetItemReserveData(Items[i]);

						if (pReserveData!=NULL) {
							ReserveIDList.push_back(pReserveData->reserveID);
							if (pTitle==NULL)
								pTitle=&pReserveData->title;
						}
					}
					if (ReserveIDList.size()>0) {
						TCHAR szText[1024];
						if (ReserveIDList.size()==1 && !pTitle->empty())
							FormatString(szText,_countof(szText),
										 TEXT("Åu%sÅvÇÃó\ñÒÇçÌèúÇµÇ‹Ç∑Ç©?"),
										 pTitle->c_str());
						else
							FormatString(szText,_countof(szText),
										 TEXT("%uåèÇÃó\ñÒÇçÌèúÇµÇ‹Ç∑Ç©?"),
										 (unsigned int)ReserveIDList.size());
						if (::MessageBox(m_hwnd,szText,TEXT("ó\ñÒÇÃçÌèú"),
										 MB_OKCANCEL | MB_DEFBUTTON2 | MB_ICONQUESTION)==IDOK)
							m_pCore->DeleteReserve(m_hwnd,ReserveIDList);
					}
				}
			}
			return;

		case CM_RECORDEDFILE_OPEN:
			{
				std::vector<int> Items;

				if (m_FileListView.GetSelectedItems(&Items)) {
					for (size_t i=0;i<Items.size();i++) {
						REC_FILE_INFO *pRecFileInfo=m_FileListView.GetItemRecFileInfo(Items[i]);

						if (pRecFileInfo!=NULL) {
							::ShellExecuteW(m_hwnd,L"open",pRecFileInfo->recFilePath.c_str(),
											NULL,NULL,SW_SHOWNORMAL);
						}
					}
				}
			}
			return;

		case CM_RECORDEDFILE_OPENFOLDER:
			{
				std::vector<int> Items;

				if (m_FileListView.GetSelectedItems(&Items)) {
					std::vector<wstring> FolderList;

					for (size_t i=0;i<Items.size();i++) {
						REC_FILE_INFO *pRecFileInfo=m_FileListView.GetItemRecFileInfo(Items[i]);

						if (pRecFileInfo!=NULL) {
							wstring Folder(pRecFileInfo->recFilePath);
							wstring::size_type Pos=Folder.find_last_of(L'\\');
							if (Pos!=wstring::npos) {
								Folder[Pos]=L'\0';
								bool fFound=false;
								for (size_t i=0;i<FolderList.size();i++) {
									if (::lstrcmpiW(FolderList[i].c_str(),Folder.c_str())==0) {
										fFound=true;
										break;
									}
								}
								if (!fFound) {
									::ShellExecuteW(m_hwnd,L"explore",Folder.c_str(),
													NULL,NULL,SW_SHOWNORMAL);
									FolderList.push_back(Folder);
								}
							}
						}
					}
				}
			}
			return;

		case CM_RECORDEDFILE_REMOVE:
			{
				std::vector<int> Items;

				if (m_FileListView.GetSelectedItems(&Items)) {
					std::vector<DWORD> IDList;

					for (size_t i=0;i<Items.size();i++) {
						REC_FILE_INFO *pRecFileInfo=m_FileListView.GetItemRecFileInfo(Items[i]);

						if (pRecFileInfo!=NULL) {
							IDList.push_back(pRecFileInfo->id);
						}
					}
					if (IDList.size()>0) {
						m_pCore->DeleteRecordedFileInfo(m_hwnd,IDList);
					}
				}
			}
			return;

		default:
			if (Command>=CM_RESERVE_RECMODE_FIRST
					&& Command<=CM_RESERVE_RECMODE_LAST) {
				const BYTE RecMode=(BYTE)(Command-CM_RESERVE_RECMODE_FIRST);
				std::vector<int> Items;

				if (m_ReserveListView.GetSelectedItems(&Items)) {
					std::vector<RESERVE_DATA> ReserveList;

					for (size_t i=0;i<Items.size();i++) {
						RESERVE_DATA *pReserveData=m_ReserveListView.GetItemReserveData(Items[i]);

						if (pReserveData!=NULL
								&& pReserveData->recSetting.recMode!=RecMode) {
							ReserveList.push_back(*pReserveData);
							ReserveList.back().recSetting.recMode=RecMode;
						}
					}
					if (ReserveList.size()>0) {
						m_pCore->ChangeReserve(m_hwnd,ReserveList);
					}
				}
				return;
			}
		}
	}


	void CReserveListForm::UpdateStatusText()
	{
		TCHAR szText[256];

		szText[0]=_T('\0');

		switch (m_CurrentTab) {
		case TAB_RESERVELIST:
			if (m_pReserveList!=NULL) {
				unsigned int DisabledCount=0;

				for (size_t i=0;i<m_pReserveList->size();i++) {
					if ((*m_pReserveList)[i].recSetting.recMode==RECMODE_NO)
						DisabledCount++;
				}

				FormatString(szText,_countof(szText),
							 TEXT("%u åèÇÃó\ñÒ (Ç§Çø %u åèñ≥å¯)"),
							 (unsigned int)m_pReserveList->size(),
							 DisabledCount);
			}
			break;

		case TAB_FILELIST:
			{
				const int ItemCount=m_FileListView.GetItemCount();
				int ErrorCount=0;

				for (int i=0;i<ItemCount;i++) {
					const REC_FILE_INFO *pRecFileInfo=m_FileListView.GetItemRecFileInfo(i);

					if (pRecFileInfo!=NULL) {
						if ((pRecFileInfo->recStatus &
								(REC_STATUS_ERR_WAKE | REC_STATUS_ERR_OPEN | REC_STATUS_ERR_FIND))!=0)
							ErrorCount++;
					}
				}

				FormatString(szText,_countof(szText),
							 TEXT("%d åèÇÃò^âÊçœÇ›çÄñ⁄ (%d åèÇÃÉGÉâÅ[)"),
							 ItemCount,ErrorCount);
			}
			break;
		}

		m_StatusBar.SetText(szText);
	}


	void CReserveListForm::OnRClick(CListView *pListView)
	{
		if (pListView==&m_ReserveListView) {
			const int SelectedCount=m_ReserveListView.GetSelectedItemCount();

			if (SelectedCount>0) {
				HMENU hmenu=::LoadMenu(g_hinstDLL,MAKEINTRESOURCE(IDM_RESERVEITEM));
				const RESERVE_DATA *pReserveData=NULL;
				POINT pt;

				if (SelectedCount==1)
					pReserveData=m_ReserveListView.GetItemReserveData(m_ReserveListView.GetSelectedItem());

				::EnableMenuItem(hmenu,CM_RESERVE_SETTINGS,
								 MF_BYCOMMAND | (SelectedCount==1?MF_ENABLED:MF_GRAYED));
				if (pReserveData!=NULL) {
					::CheckMenuRadioItem(hmenu,CM_RESERVE_PRIORITY_1,CM_RESERVE_PRIORITY_5,
										 CM_RESERVE_PRIORITY_1+(pReserveData->recSetting.priority-1),
										 MF_BYCOMMAND);
					if (pReserveData->recSetting.recMode<=RECMODE_NO)
						::CheckMenuRadioItem(hmenu,CM_RESERVE_RECMODE_FIRST,CM_RESERVE_RECMODE_LAST,
											 CM_RESERVE_RECMODE_FIRST+pReserveData->recSetting.recMode,
											 MF_BYCOMMAND);
				}
				::GetCursorPos(&pt);
				::TrackPopupMenu(::GetSubMenu(hmenu,0),TPM_RIGHTBUTTON,
								 pt.x,pt.y,0,m_hwnd,NULL);
				::DestroyMenu(hmenu);
			}
		} else if (pListView==&m_FileListView) {
			const int SelectedCount=m_FileListView.GetSelectedItemCount();

			if (SelectedCount>0) {
				HMENU hmenu=::LoadMenu(g_hinstDLL,MAKEINTRESOURCE(IDM_RECORDEDFILE));
				const bool fNetwork=m_pCore->GetSettings().fUseNetwork;
				POINT pt;

				::EnableMenuItem(hmenu,CM_RECORDEDFILE_OPEN,
								 MF_BYCOMMAND | (fNetwork?MF_GRAYED:MF_ENABLED));
				::EnableMenuItem(hmenu,CM_RECORDEDFILE_OPENFOLDER,
								 MF_BYCOMMAND | (fNetwork?MF_GRAYED:MF_ENABLED));
				::GetCursorPos(&pt);
				::TrackPopupMenu(::GetSubMenu(hmenu,0),TPM_RIGHTBUTTON,
								 pt.x,pt.y,0,m_hwnd,NULL);
				::DestroyMenu(hmenu);
			}
		}
	}


	void CReserveListForm::OnDoubleClick(CListView *pListView,int Item)
	{
		if (Item<0)
			return;

		if (pListView==&m_ReserveListView) {
			RESERVE_DATA *pReserveData=m_ReserveListView.GetItemReserveData(Item);

			if (pReserveData!=NULL) {
				RESERVE_DATA Reserve=*pReserveData;

				if (m_pCore->ShowReserveDialog(m_hwnd,&Reserve))
					m_pCore->ChangeReserve(m_hwnd,Reserve);
			}
		}
	}


	void CReserveListForm::OnKeyDown(CListView *pListView,UINT Key)
	{
		if (pListView==&m_ReserveListView) {
			if (Key==VK_DELETE)
				::SendMessage(m_hwnd,WM_COMMAND,CM_RESERVE_DELETE,0);
		} else if (pListView==&m_FileListView) {
			if (Key==VK_DELETE)
				::SendMessage(m_hwnd,WM_COMMAND,CM_RECORDEDFILE_REMOVE,0);
		}
	}

}
