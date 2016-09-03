#include "stdafx.h"
#include "EDCBSupport.h"
#include "RecFolderDialog.h"
#include "../../Common/WritePlugInUtil.h"
#include "../../Common/ReNamePlugInUtil.h"
#include "resource.h"


namespace EDCBSupport
{


CRecFolderDialog::CRecFolderDialog(EDCBSettings &Settings,CSendCtrlCmd &SendCtrlCmd)
	: m_EDCBSettings(Settings)
	, m_SendCtrlCmd(SendCtrlCmd)
{
}


CRecFolderDialog::~CRecFolderDialog()
{
}


bool CRecFolderDialog::Show(HINSTANCE hinst,HWND hwndOwner,REC_FILE_SET_INFO *pRecFileSet)
{
	m_pRecFileSet=pRecFileSet;

	return ::DialogBoxParam(hinst,MAKEINTRESOURCE(IDD_FOLDER),
							hwndOwner,DlgProc,
							reinterpret_cast<LPARAM>(this))==IDOK;
}


static void SetPluginList(HWND hDlg,int ID,const vector<wstring> &PluginList,const wstring &CurPlugin)
{
	int Sel=-1;

	for (size_t i=0;i<PluginList.size();i++) {
		LRESULT Index=::SendDlgItemMessage(hDlg,ID,CB_ADDSTRING,0,
										   reinterpret_cast<LPARAM>(PluginList[i].c_str()));
		if (PluginList[i]==CurPlugin)
			Sel=(int)Index;
	}
	::SendDlgItemMessage(hDlg,ID,CB_SETCURSEL,Sel,0);
}

INT_PTR CALLBACK CRecFolderDialog::DlgProc(HWND hDlg,UINT uMsg,WPARAM wParam,LPARAM lParam)
{
	switch (uMsg) {
	case WM_INITDIALOG:
		{
			CRecFolderDialog *pThis=reinterpret_cast<CRecFolderDialog*>(lParam);

			::SetProp(hDlg,TEXT("This"),pThis);

			REC_FILE_SET_INFO &RecFileSet=*pThis->m_pRecFileSet;

			if (!RecFileSet.recFolder.empty())
				::SetDlgItemTextW(hDlg,IDC_FOLDER_FOLDER,RecFileSet.recFolder.c_str());

			vector<wstring> PluginList;
			if (pThis->m_SendCtrlCmd.SendEnumPlugIn(2,&PluginList)==CMD_SUCCESS)
				SetPluginList(hDlg,IDC_FOLDER_WRITEPLUGIN,PluginList,RecFileSet.writePlugIn);
			PluginList.clear();
			if (pThis->m_SendCtrlCmd.SendEnumPlugIn(1,&PluginList)==CMD_SUCCESS)
				SetPluginList(hDlg,IDC_FOLDER_FILENAMEPLUGIN,PluginList,RecFileSet.recNamePlugIn);
			::SendDlgItemMessage(hDlg,IDC_FOLDER_FILENAMEPLUGIN,CB_INSERTSTRING,0,
								 reinterpret_cast<LPARAM>(TEXT("なし")));
			if (RecFileSet.recNamePlugIn.empty())
				::SendDlgItemMessage(hDlg,IDC_FOLDER_FILENAMEPLUGIN,CB_SETCURSEL,0,0);

			bool fEDCBDirectory=
				!pThis->m_EDCBSettings.fUseNetwork
				&& !pThis->m_EDCBSettings.EDCBDirectory.empty();
			EnableDlgItem(hDlg,IDC_FOLDER_WRITEPLUGIN_SETTING,fEDCBDirectory);
			EnableDlgItem(hDlg,IDC_FOLDER_FILENAMEPLUGIN_SETTING,
						  fEDCBDirectory
						  && ::SendDlgItemMessage(hDlg,IDC_FOLDER_FILENAMEPLUGIN,CB_GETCURSEL,0,0)>0);
		}
		return TRUE;

	case WM_COMMAND:
		{
			CRecFolderDialog *pThis=static_cast<CRecFolderDialog*>(::GetProp(hDlg,TEXT("This")));

			if (pThis!=NULL)
				pThis->OnCommand(hDlg,LOWORD(wParam),HIWORD(wParam));
		}
		return TRUE;

	case WM_NCDESTROY:
		::RemoveProp(hDlg,TEXT("This"));
		return TRUE;
	}
	return FALSE;
}


void CRecFolderDialog::OnCommand(HWND hDlg,UINT Command,UINT NotifyCode)
{
	switch (Command) {
	case IDC_FOLDER_FOLDER_BROWSE:
		{
			wstring Folder;

			GetDlgItemString(hDlg,IDC_FOLDER_FOLDER,&Folder);
			if (BrowseFolderDialog(hDlg,&Folder,L"録画先フォルダ"))
				::SetDlgItemTextW(hDlg,IDC_FOLDER_FOLDER,Folder.c_str());
		}
		return;

	case IDC_FOLDER_WRITEPLUGIN_SETTING:
		{
			LRESULT Sel=::SendDlgItemMessage(hDlg,IDC_FOLDER_WRITEPLUGIN,CB_GETCURSEL,0,0);

			if (Sel>=0) {
				wstring PluginName;
				WCHAR szPluginPath[MAX_PATH];

				GetComboBoxString(hDlg,IDC_FOLDER_WRITEPLUGIN,(int)Sel,&PluginName);
				if (m_EDCBSettings.EDCBDirectory.length()+7+PluginName.length()>=MAX_PATH)
					return;
				::PathCombine(szPluginPath,m_EDCBSettings.EDCBDirectory.c_str(),L"Write");
				::PathAppend(szPluginPath,PluginName.c_str());

				CWritePlugInUtil WritePlugin;
				if (WritePlugin.Initialize(szPluginPath)) {
					WritePlugin.Setting(hDlg);
				} else {
					::MessageBox(hDlg,TEXT("プラグインを読み込めません。"),NULL,
								 MB_OK | MB_ICONEXCLAMATION);
				}
			}
		}
		return;

	case IDC_FOLDER_FILENAMEPLUGIN:
		if (NotifyCode==CBN_SELCHANGE
				&& !m_EDCBSettings.fUseNetwork
				&& !m_EDCBSettings.EDCBDirectory.empty()) {
			EnableDlgItem(hDlg,IDC_FOLDER_FILENAMEPLUGIN_SETTING,
						  ::SendDlgItemMessage(hDlg,IDC_FOLDER_FILENAMEPLUGIN,CB_GETCURSEL,0,0)>0);
		}
		return;

	case IDC_FOLDER_FILENAMEPLUGIN_SETTING:
		{
			LRESULT Sel=::SendDlgItemMessage(hDlg,IDC_FOLDER_FILENAMEPLUGIN,CB_GETCURSEL,0,0);

			if (Sel>0) {
				wstring PluginName;
				WCHAR szPluginPath[MAX_PATH];

				GetComboBoxString(hDlg,IDC_FOLDER_FILENAMEPLUGIN,(int)Sel,&PluginName);
				if (m_EDCBSettings.EDCBDirectory.length()+9+PluginName.length()>=MAX_PATH)
					return;
				::PathCombine(szPluginPath,m_EDCBSettings.EDCBDirectory.c_str(),L"RecName");
				//::PathAppend(szPluginPath,PluginName.c_str());

				if (!CReNamePlugInUtil::Setting(PluginName.c_str(), szPluginPath, hDlg)) {
					::MessageBox(hDlg,TEXT("プラグインを読み込めません。"),NULL,
								 MB_OK | MB_ICONEXCLAMATION);
				}
			}
		}
		return;

	case IDOK:
		if (!GetSettings(hDlg))
			return;
	case IDCANCEL:
		::EndDialog(hDlg,Command);
		return;
	}
}


bool CRecFolderDialog::GetSettings(HWND hDlg)
{
	wstring RecFolder,WritePlugin;

	if (GetDlgItemString(hDlg,IDC_FOLDER_FOLDER,&RecFolder)<1) {
		::MessageBox(hDlg,TEXT("フォルダを指定してください。"),NULL,MB_OK | MB_ICONEXCLAMATION);
		return false;
	}

	LRESULT Sel=::SendDlgItemMessage(hDlg,IDC_FOLDER_WRITEPLUGIN,CB_GETCURSEL,0,0);
	if (Sel<0) {
		::MessageBox(hDlg,TEXT("出力PlugInを選択してください。"),NULL,MB_OK | MB_ICONEXCLAMATION);
		return false;
	}
	GetComboBoxString(hDlg,IDC_FOLDER_WRITEPLUGIN,(int)Sel,&WritePlugin);

	m_pRecFileSet->recFolder=RecFolder;
	m_pRecFileSet->writePlugIn=WritePlugin;

	Sel=::SendDlgItemMessage(hDlg,IDC_FOLDER_FILENAMEPLUGIN,CB_GETCURSEL,0,0);
	if (Sel>0)
		GetComboBoxString(hDlg,IDC_FOLDER_FILENAMEPLUGIN,(int)Sel,&m_pRecFileSet->recNamePlugIn);
	else
		m_pRecFileSet->recNamePlugIn.clear();

	return true;
}


}	// namespace EDCBSupport
