#include "stdafx.h"
#include "EDCBSupport.h"
#include "ReserveDialog.h"
#include "RecFolderDialog.h"
#include "resource.h"


namespace EDCBSupport
{


CReserveDialog::BatFileList CReserveDialog::m_BatFileList;


CReserveDialog::CReserveDialog(EDCBSettings &Settings,CSendCtrlCmd &SendCtrlCmd)
	: m_EDCBSettings(Settings)
	, m_SendCtrlCmd(SendCtrlCmd)
{
}


CReserveDialog::~CReserveDialog()
{
}


bool CReserveDialog::Show(HINSTANCE hinst,HWND hwndOwner,
						  RESERVE_DATA *pData,const TVTest::EpgEventInfo *pEventInfo)
{
	m_hinst=hinst;
	m_pReserveData=pData;
	m_pEventInfo=pEventInfo;

	return ::DialogBoxParam(hinst,MAKEINTRESOURCE(IDD_RESERVE),
							hwndOwner,DlgProc,reinterpret_cast<LPARAM>(this))==IDOK;
}


bool CReserveDialog::AddBatFileList(LPCWSTR pszFile)
{
	if (pszFile==NULL || pszFile[0]==L'\0')
		return false;
	for (std::deque<wstring>::iterator itr=m_BatFileList.begin();
			itr!=m_BatFileList.end();itr++) {
		if (::lstrcmpiW(itr->c_str(),pszFile)==0) {
			if (itr==m_BatFileList.begin())
				return true;
			m_BatFileList.erase(itr);
			break;
		}
	}
	if (m_BatFileList.size()>=20)
		m_BatFileList.pop_back();
	m_BatFileList.push_front(wstring(pszFile));
	return true;
}


static LRESULT FolderListInsertItem(HWND hDlg,LPARAM Index,REC_FILE_SET_INFO *pInfo)
{
	TCHAR szText[1024];

	::wsprintf(szText,TEXT("%s (出力PlugIn : %s / ファイル名PlugIn : %s)"),
			   pInfo->recFolder.c_str(),
			   pInfo->writePlugIn.c_str(),
			   pInfo->recNamePlugIn.empty()?TEXT("なし"):pInfo->recNamePlugIn.c_str());
	Index=::SendDlgItemMessage(hDlg,IDC_RESERVE_FOLDERLIST,LB_INSERTSTRING,Index,
							   reinterpret_cast<LPARAM>(szText));
	::SendDlgItemMessage(hDlg,IDC_RESERVE_FOLDERLIST,LB_SETITEMDATA,Index,
						 reinterpret_cast<LPARAM>(pInfo));
	return Index;
}

static LRESULT FolderListAddItem(HWND hDlg,REC_FILE_SET_INFO *pInfo)
{
	return FolderListInsertItem(hDlg,-1,pInfo);
}

INT_PTR CALLBACK CReserveDialog::DlgProc(HWND hDlg,UINT uMsg,WPARAM wParam,LPARAM lParam)
{
	switch (uMsg) {
	case WM_INITDIALOG:
		{
			CReserveDialog *pThis=reinterpret_cast<CReserveDialog*>(lParam);

			::SetProp(hDlg,TEXT("This"),pThis);

			pThis->InitDialog(hDlg);
		}
		return TRUE;

	case WM_CTLCOLORSTATIC:
		{
			HWND hwnd=reinterpret_cast<HWND>(lParam);

			if (hwnd==::GetDlgItem(hDlg,IDC_RESERVE_INFO)) {
				HDC hdc=reinterpret_cast<HDC>(wParam);

				::SetTextColor(hdc,::GetSysColor(COLOR_WINDOWTEXT));
				::SetBkColor(hdc,::GetSysColor(COLOR_WINDOW));
				return reinterpret_cast<INT_PTR>(::GetSysColorBrush(COLOR_WINDOW));
			}
		}
		break;

	case WM_COMMAND:
		switch (LOWORD(wParam)) {
		case IDC_RESERVE_PROGRAM:
			{
				bool fProgram=::IsDlgButtonChecked(hDlg,IDC_RESERVE_PROGRAM)==BST_CHECKED;

				EnableDlgItems(hDlg,IDC_RESERVE_STARTTIME_LABEL,IDC_RESERVE_ENDTIME,fProgram);
				EnableDlgItems(hDlg,IDC_RESERVE_FOLLOW,IDC_RESERVE_JUST,!fProgram);
			}
			return TRUE;

		case IDC_RESERVE_FOLDERLIST:
			if (HIWORD(wParam)==LBN_SELCHANGE) {
				LRESULT Sel=::SendDlgItemMessage(hDlg,IDC_RESERVE_FOLDERLIST,LB_GETCURSEL,0,0);

				EnableDlgItems(hDlg,IDC_RESERVE_FOLDERLIST_EDIT,IDC_RESERVE_FOLDERLIST_REMOVE,Sel>=0);
			}
			return TRUE;

		case IDC_RESERVE_FOLDERLIST_ADD:
			{
				CReserveDialog *pThis=static_cast<CReserveDialog*>(::GetProp(hDlg,TEXT("This")));

				REC_FILE_SET_INFO *pRecFileSet=new REC_FILE_SET_INFO;
				pRecFileSet->writePlugIn=L"Write_Default.dll";

				CRecFolderDialog FolderDialog(pThis->m_EDCBSettings,pThis->m_SendCtrlCmd);
				if (FolderDialog.Show(pThis->m_hinst,hDlg,pRecFileSet)) {
					LRESULT Index=FolderListAddItem(hDlg,pRecFileSet);
					::SendDlgItemMessage(hDlg,IDC_RESERVE_FOLDERLIST,LB_SETCURSEL,Index,0);
					::SendMessage(hDlg,WM_COMMAND,MAKEWPARAM(IDC_RESERVE_FOLDERLIST,LBN_SELCHANGE),0);
					SetListBoxHorzExtent(hDlg,IDC_RESERVE_FOLDERLIST);
				} else {
					delete pRecFileSet;
				}
			}
			return TRUE;

		case IDC_RESERVE_FOLDERLIST_EDIT:
			{
				LRESULT Sel=::SendDlgItemMessage(hDlg,IDC_RESERVE_FOLDERLIST,LB_GETCURSEL,0,0);

				if (Sel>=0) {
					REC_FILE_SET_INFO *pRecFileSet=reinterpret_cast<REC_FILE_SET_INFO*>(
						::SendDlgItemMessage(hDlg,IDC_RESERVE_FOLDERLIST,LB_GETITEMDATA,Sel,0));
					CReserveDialog *pThis=static_cast<CReserveDialog*>(::GetProp(hDlg,TEXT("This")));
					CRecFolderDialog FolderDialog(pThis->m_EDCBSettings,pThis->m_SendCtrlCmd);

					if (FolderDialog.Show(pThis->m_hinst,hDlg,pRecFileSet)) {
						::SendDlgItemMessage(hDlg,IDC_RESERVE_FOLDERLIST,LB_DELETESTRING,Sel,0);
						FolderListInsertItem(hDlg,Sel,pRecFileSet);
						::SendDlgItemMessage(hDlg,IDC_RESERVE_FOLDERLIST,LB_SETCURSEL,Sel,0);
						SetListBoxHorzExtent(hDlg,IDC_RESERVE_FOLDERLIST);
					}
				}
			}
			return TRUE;

		case IDC_RESERVE_FOLDERLIST_REMOVE:
			{
				LRESULT Sel=::SendDlgItemMessage(hDlg,IDC_RESERVE_FOLDERLIST,LB_GETCURSEL,0,0);

				if (Sel>=0) {
					REC_FILE_SET_INFO *pRecFileSet=reinterpret_cast<REC_FILE_SET_INFO*>(
						::SendDlgItemMessage(hDlg,IDC_RESERVE_FOLDERLIST,LB_GETITEMDATA,Sel,0));
					::SendDlgItemMessage(hDlg,IDC_RESERVE_FOLDERLIST,LB_DELETESTRING,Sel,0);
					delete pRecFileSet;
					::SendMessage(hDlg,WM_COMMAND,MAKEWPARAM(IDC_RESERVE_FOLDERLIST,LBN_SELCHANGE),0);
					SetListBoxHorzExtent(hDlg,IDC_RESERVE_FOLDERLIST);
				}
			}
			return TRUE;

		case IDC_RESERVE_SUSPENDMODE:
			if (HIWORD(wParam)==CBN_SELCHANGE) {
				EnableDlgItem(hDlg,IDC_RESERVE_REBOOT,
							  ::SendDlgItemMessage(hDlg,IDC_RESERVE_SUSPENDMODE,CB_GETCURSEL,0,0)>0);
			}
			return TRUE;

		case IDC_RESERVE_BAT_BROWSE:
			{
				wstring BatFile;

				GetDlgItemString(hDlg,IDC_RESERVE_BAT,&BatFile);
				if (FileOpenDialog(hDlg,&BatFile,L"録画後実行BAT",
								   L"BATファイル(*.bat)\0*.bat\0全てのファイル\0*.*\0",
								   L".bat"))
					::SetDlgItemTextW(hDlg,IDC_RESERVE_BAT,BatFile.c_str());
			}
			return TRUE;

		case IDC_RESERVE_USEMARGIN:
			EnableDlgItems(hDlg,IDC_RESERVE_STARTMARGIN_LABEL,IDC_RESERVE_ENDMARGIN_UNIT,
						   ::IsDlgButtonChecked(hDlg,IDC_RESERVE_USEMARGIN)==BST_CHECKED);
			return TRUE;

		case IDC_RESERVE_SERVICEMODE_CUSTOM:
			EnableDlgItems(hDlg,IDC_RESERVE_SERVICEMODE_CAPTION,IDC_RESERVE_SERVICEMODE_DATA,
						   ::IsDlgButtonChecked(hDlg,IDC_RESERVE_SERVICEMODE_CUSTOM)==BST_CHECKED);
			return TRUE;

		case IDOK:
			{
				CReserveDialog *pThis=static_cast<CReserveDialog*>(::GetProp(hDlg,TEXT("This")));

				if (!pThis->GetSettings(hDlg))
					return TRUE;

				if (!pThis->m_pReserveData->recSetting.batFilePath.empty())
					AddBatFileList(pThis->m_pReserveData->recSetting.batFilePath.c_str());
			}
		case IDCANCEL:
			::EndDialog(hDlg,LOWORD(wParam));
			return TRUE;
		}
		return TRUE;

	case WM_DESTROY:
		{
			int FolderCount=(int)::SendDlgItemMessage(hDlg,IDC_RESERVE_FOLDERLIST,LB_GETCOUNT,0,0);
			for (int i=0;i<FolderCount;i++) {
				REC_FILE_SET_INFO *pRecFileSet=reinterpret_cast<REC_FILE_SET_INFO*>(
					::SendDlgItemMessage(hDlg,IDC_RESERVE_FOLDERLIST,LB_GETITEMDATA,i,0));
				delete pRecFileSet;
			}
		}
		return TRUE;

	case WM_NCDESTROY:
		::RemoveProp(hDlg,TEXT("This"));
		return TRUE;
	}
	return FALSE;
}


void CReserveDialog::InitDialog(HWND hDlg)
{
	REC_SETTING_DATA &RecSetting=m_pReserveData->recSetting;
	TCHAR szText[1024];
	SYSTEMTIME EndTime;

	RECT rc;
	::SendDlgItemMessage(hDlg,IDC_RESERVE_INFO,EM_GETRECT,0,reinterpret_cast<LPARAM>(&rc));
	::InflateRect(&rc,-8,-4);
	::SendDlgItemMessage(hDlg,IDC_RESERVE_INFO,EM_SETRECT,0,reinterpret_cast<LPARAM>(&rc));
	if (m_pEventInfo!=NULL) {
		GetEndTime(m_pEventInfo->StartTime,m_pEventInfo->Duration,&EndTime);
		::wsprintf(szText,TEXT("%d/%02d/%02d(%s) %02d:%02d ～ %02d:%02d\r\n%s"),
				   m_pEventInfo->StartTime.wYear,
				   m_pEventInfo->StartTime.wMonth,
				   m_pEventInfo->StartTime.wDay,
				   GetDayOfWeekText(m_pEventInfo->StartTime.wDayOfWeek),
				   m_pEventInfo->StartTime.wHour,
				   m_pEventInfo->StartTime.wMinute,
				   EndTime.wHour,
				   EndTime.wMinute,
				   m_pEventInfo->pszEventName!=NULL?
				   m_pEventInfo->pszEventName:TEXT(""));
		::SetDlgItemText(hDlg,IDC_RESERVE_INFO,szText);
	}

	::SetDlgItemTextW(hDlg,IDC_RESERVE_TITLE,m_pReserveData->title.c_str());

	const bool fProgram=m_pReserveData->eventID==0xFFFF;
	::CheckDlgButton(hDlg,IDC_RESERVE_PROGRAM,fProgram?BST_CHECKED:BST_UNCHECKED);
	EnableDlgItem(hDlg,IDC_RESERVE_PROGRAM,!fProgram);

	HWND hwndStartTime=::GetDlgItem(hDlg,IDC_RESERVE_STARTTIME);
	HWND hwndEndTime=::GetDlgItem(hDlg,IDC_RESERVE_ENDTIME);
	DateTime_SetFormat(hwndStartTime,TEXT("yyyy'/'MM'/'dd' 'HH':'mm':'ss"));
	DateTime_SetFormat(hwndEndTime,TEXT("yyyy'/'MM'/'dd' 'HH':'mm':'ss"));
	DateTime_SetSystemtime(hwndStartTime,GDT_VALID,&m_pReserveData->startTime);
	GetEndTime(m_pReserveData->startTime,m_pReserveData->durationSecond,&EndTime);
	DateTime_SetSystemtime(hwndEndTime,GDT_VALID,&EndTime);
	EnableDlgItems(hDlg,IDC_RESERVE_STARTTIME_LABEL,IDC_RESERVE_ENDTIME,fProgram);

	if (!fProgram) {
		::CheckDlgButton(hDlg,IDC_RESERVE_FOLLOW,
						 RecSetting.tuijyuuFlag?BST_CHECKED:BST_UNCHECKED);
		::CheckDlgButton(hDlg,IDC_RESERVE_JUST,
						 RecSetting.pittariFlag?BST_CHECKED:BST_UNCHECKED);
	}
	EnableDlgItems(hDlg,IDC_RESERVE_FOLLOW,IDC_RESERVE_JUST,!fProgram);

	static const LPCTSTR RecModeList[] = {
		TEXT("全サービス"),
		TEXT("指定サービス"),
		TEXT("全サービス (スクランブル解除なし)"),
		TEXT("指定サービス (スクランブル解除なし)"),
		TEXT("視聴"),
		TEXT("無効"),
	};
	SetComboBoxList(hDlg,IDC_RESERVE_RECMODE,RecModeList,_countof(RecModeList));
	::SendDlgItemMessage(hDlg,IDC_RESERVE_RECMODE,CB_SETCURSEL,RecSetting.recMode,0);

	static const LPCTSTR PriorityList[] = {
		TEXT("1 (最低)"),
		TEXT("2 (低)"),
		TEXT("3 (中)"),
		TEXT("4 (高)"),
		TEXT("5 (最高)"),
	};
	SetComboBoxList(hDlg,IDC_RESERVE_PRIORITY,PriorityList,_countof(PriorityList));
	::SendDlgItemMessage(hDlg,IDC_RESERVE_PRIORITY,CB_SETCURSEL,RecSetting.priority-1,0);

	for (size_t i=0;i<RecSetting.recFolderList.size();i++)
		FolderListAddItem(hDlg,new REC_FILE_SET_INFO(RecSetting.recFolderList[i]));
	SetListBoxHorzExtent(hDlg,IDC_RESERVE_FOLDERLIST);
	EnableDlgItems(hDlg,IDC_RESERVE_FOLDERLIST_EDIT,IDC_RESERVE_FOLDERLIST_REMOVE,false);

	static const LPCTSTR SuspendModeList[] = {
		TEXT("デフォルト"),
		TEXT("スタンバイ"),
		TEXT("休止"),
		TEXT("シャットダウン"),
		TEXT("何もしない"),
	};
	SetComboBoxList(hDlg,IDC_RESERVE_SUSPENDMODE,SuspendModeList,_countof(SuspendModeList));
	::SendDlgItemMessage(hDlg,IDC_RESERVE_SUSPENDMODE,CB_SETCURSEL,RecSetting.suspendMode,0);
	bool fDefault=RecSetting.suspendMode==0;
	if (!fDefault)
		::CheckDlgButton(hDlg,IDC_RESERVE_REBOOT,RecSetting.rebootFlag?BST_CHECKED:BST_UNCHECKED);
	EnableDlgItem(hDlg,IDC_RESERVE_REBOOT,!fDefault);

	if (m_BatFileList.size()>0) {
		for (size_t i=0;i<m_BatFileList.size();i++)
			::SendDlgItemMessage(hDlg,IDC_RESERVE_BAT,CB_ADDSTRING,0,
								 reinterpret_cast<LPARAM>(m_BatFileList[i].c_str()));
		SetComboBoxDroppedWidth(hDlg,IDC_RESERVE_BAT);
	}
	::SetDlgItemTextW(hDlg,IDC_RESERVE_BAT,RecSetting.batFilePath.c_str());
	::SetDlgItemTextW(hDlg,IDC_RESERVE_REC_TAG,RecSetting.recTag.c_str());

	::CheckDlgButton(hDlg,IDC_RESERVE_USEMARGIN,
					 RecSetting.useMargineFlag?BST_CHECKED:BST_UNCHECKED);
	::SetDlgItemInt(hDlg,IDC_RESERVE_STARTMARGIN,RecSetting.startMargine,TRUE);
	::SetDlgItemInt(hDlg,IDC_RESERVE_ENDMARGIN,RecSetting.endMargine,TRUE);
	EnableDlgItems(hDlg,IDC_RESERVE_STARTMARGIN_LABEL,IDC_RESERVE_ENDMARGIN_UNIT,
				   RecSetting.useMargineFlag!=0);

	fDefault=(RecSetting.serviceMode&RECSERVICEMODE_SET)==0;
	::CheckDlgButton(hDlg,IDC_RESERVE_SERVICEMODE_CUSTOM,
					 !fDefault?BST_CHECKED:BST_UNCHECKED);
	if (!fDefault) {
		::CheckDlgButton(hDlg,IDC_RESERVE_SERVICEMODE_CAPTION,
						 (RecSetting.serviceMode&RECSERVICEMODE_CAP)!=0);
		::CheckDlgButton(hDlg,IDC_RESERVE_SERVICEMODE_DATA,
						 (RecSetting.serviceMode&RECSERVICEMODE_DATA)!=0);
	}
	EnableDlgItems(hDlg,IDC_RESERVE_SERVICEMODE_CAPTION,IDC_RESERVE_SERVICEMODE_DATA,!fDefault);

	::CheckDlgButton(hDlg,IDC_RESERVE_PARTIALREC,
					 RecSetting.partialRecFlag?BST_CHECKED:BST_UNCHECKED);

	::CheckDlgButton(hDlg,IDC_RESERVE_CONTINUEREC,
					 RecSetting.continueRecFlag?BST_CHECKED:BST_UNCHECKED);

	vector<TUNER_RESERVE_INFO> TunerList;
	int Sel=0;
	::SendDlgItemMessage(hDlg,IDC_RESERVE_TUNER,CB_ADDSTRING,0,
						 reinterpret_cast<LPARAM>(TEXT("自動")));
	if (m_SendCtrlCmd.SendEnumTunerReserve(&TunerList)==CMD_SUCCESS) {
		for (size_t i=0;i<TunerList.size();i++) {
			if (TunerList[i].tunerID!=0xFFFFFFFFUL) {
				::wsprintf(szText,TEXT("%s (ID: %lu)"),
						   TunerList[i].tunerName.c_str(),
						   TunerList[i].tunerID);
				LRESULT Index=::SendDlgItemMessage(hDlg,IDC_RESERVE_TUNER,CB_ADDSTRING,0,
												   reinterpret_cast<LPARAM>(szText));
				::SendDlgItemMessage(hDlg,IDC_RESERVE_TUNER,CB_SETITEMDATA,Index,
									 TunerList[i].tunerID);
				if (TunerList[i].tunerID==RecSetting.tunerID)
					Sel=(int)Index;
			}
		}
	}
	::SendDlgItemMessage(hDlg,IDC_RESERVE_TUNER,CB_SETCURSEL,Sel,0);
}


bool CReserveDialog::GetSettings(HWND hDlg)
{
	REC_SETTING_DATA &RecSetting=m_pReserveData->recSetting;

	GetDlgItemString(hDlg,IDC_RESERVE_TITLE,&m_pReserveData->title);
	bool fProgram=m_pReserveData->eventID==0xFFFF
		|| ::IsDlgButtonChecked(hDlg,IDC_RESERVE_PROGRAM)==BST_CHECKED;
	RecSetting.recMode=(BYTE)::SendDlgItemMessage(hDlg,IDC_RESERVE_RECMODE,CB_GETCURSEL,0,0);
	RecSetting.priority=(BYTE)(::SendDlgItemMessage(hDlg,IDC_RESERVE_PRIORITY,CB_GETCURSEL,0,0)+1);
	if (fProgram) {
		SYSTEMTIME StartTime,EndTime;
		FILETIME ftStart,ftEnd;

		if (DateTime_GetSystemtime(::GetDlgItem(hDlg,IDC_RESERVE_STARTTIME),&StartTime)!=GDT_VALID
				|| DateTime_GetSystemtime(::GetDlgItem(hDlg,IDC_RESERVE_ENDTIME),&EndTime)!=GDT_VALID) {
			::MessageBox(hDlg,TEXT("録画日時が正しくありません。"),NULL,MB_OK | MB_ICONEXCLAMATION);
			return false;
		}
		::SystemTimeToFileTime(&StartTime,&ftStart);
		::SystemTimeToFileTime(&EndTime,&ftEnd);
		LONGLONG Duration=((((LONGLONG)ftEnd.dwHighDateTime<<32) | (LONGLONG)ftEnd.dwLowDateTime)-
						  (((LONGLONG)ftStart.dwHighDateTime<<32) | (LONGLONG)ftStart.dwLowDateTime))/10000000LL;
		if (Duration<=0) {
			::MessageBox(hDlg,TEXT("録画日時の範囲が正しくありません。"),NULL,MB_OK | MB_ICONEXCLAMATION);
			return false;
		}
		if (Duration>0xFFFFFFFFUL) {
			::MessageBox(hDlg,TEXT("録画時間が長すぎます。"),NULL,MB_OK | MB_ICONEXCLAMATION);
			return false;
		}
		m_pReserveData->startTime=StartTime;
		m_pReserveData->durationSecond=(DWORD)Duration;
		m_pReserveData->eventID = 0xFFFF;
		RecSetting.tuijyuuFlag=0;
		RecSetting.pittariFlag=0;
	} else {
		RecSetting.tuijyuuFlag=::IsDlgButtonChecked(hDlg,IDC_RESERVE_FOLLOW)==BST_CHECKED;
		RecSetting.pittariFlag=::IsDlgButtonChecked(hDlg,IDC_RESERVE_JUST)==BST_CHECKED;
	}
	int FolderCount=(int)::SendDlgItemMessage(hDlg,IDC_RESERVE_FOLDERLIST,LB_GETCOUNT,0,0);
	RecSetting.recFolderList.resize(FolderCount);
	for (int i=0;i<FolderCount;i++) {
		REC_FILE_SET_INFO *pRecFileSet=reinterpret_cast<REC_FILE_SET_INFO*>(
			::SendDlgItemMessage(hDlg,IDC_RESERVE_FOLDERLIST,LB_GETITEMDATA,i,0));
		RecSetting.recFolderList[i]=*pRecFileSet;
	}
	RecSetting.suspendMode=(BYTE)::SendDlgItemMessage(hDlg,IDC_RESERVE_SUSPENDMODE,CB_GETCURSEL,0,0);
	if (RecSetting.suspendMode!=0)
		RecSetting.rebootFlag=::IsDlgButtonChecked(hDlg,IDC_RESERVE_REBOOT)==BST_CHECKED;
	else
		RecSetting.rebootFlag=0;
	GetDlgItemString(hDlg,IDC_RESERVE_BAT,&RecSetting.batFilePath);
	GetDlgItemString(hDlg,IDC_RESERVE_REC_TAG,&RecSetting.recTag);
	RecSetting.useMargineFlag=::IsDlgButtonChecked(hDlg,IDC_RESERVE_USEMARGIN)==BST_CHECKED;
	if (RecSetting.useMargineFlag) {
		RecSetting.startMargine=::GetDlgItemInt(hDlg,IDC_RESERVE_STARTMARGIN,NULL,TRUE);
		RecSetting.endMargine=::GetDlgItemInt(hDlg,IDC_RESERVE_ENDMARGIN,NULL,TRUE);
	} else {
		RecSetting.startMargine=0;
		RecSetting.endMargine=0;
	}
	RecSetting.serviceMode=
		::IsDlgButtonChecked(hDlg,IDC_RESERVE_SERVICEMODE_CUSTOM)?RECSERVICEMODE_SET:RECSERVICEMODE_DEF;
	if (RecSetting.serviceMode==RECSERVICEMODE_SET) {
		if (::IsDlgButtonChecked(hDlg,IDC_RESERVE_SERVICEMODE_CAPTION))
			RecSetting.serviceMode|=RECSERVICEMODE_CAP;
		if (::IsDlgButtonChecked(hDlg,IDC_RESERVE_SERVICEMODE_DATA))
			RecSetting.serviceMode|=RECSERVICEMODE_DATA;
	}
	RecSetting.partialRecFlag=::IsDlgButtonChecked(hDlg,IDC_RESERVE_PARTIALREC)==BST_CHECKED;
	RecSetting.continueRecFlag=::IsDlgButtonChecked(hDlg,IDC_RESERVE_CONTINUEREC)==BST_CHECKED;

	LRESULT Tuner=::SendDlgItemMessage(hDlg,IDC_RESERVE_TUNER,CB_GETCURSEL,0,0);
	if (Tuner>0)
		RecSetting.tunerID=(DWORD)::SendDlgItemMessage(hDlg,IDC_RESERVE_TUNER,CB_GETITEMDATA,Tuner,0);
	else
		RecSetting.tunerID=0;

	return true;
}


}	// namespace EDCBSupport
