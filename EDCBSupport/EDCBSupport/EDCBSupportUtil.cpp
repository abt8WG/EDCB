#include "stdafx.h"
#include "EDCBSupport.h"

#define STRSAFE_NO_DEPRECATE
#include <strsafe.h>

#pragma comment(lib,"strsafe.lib")


namespace EDCBSupport
{


// エラーのメッセージを取得する
LPCWSTR GetCmdErrorMessage(DWORD Err)
{
	switch (Err) {
	case CMD_NON_SUPPORT:
		return L"EpgTimerサービスがこの機能に対応していません。";
	case CMD_ERR_INVALID_ARG:
		return L"EpgTimerサービスにパラメータが受け付けられません。";
	case CMD_ERR_CONNECT:
		return L"EpgTimerサービスに接続できません";
	case CMD_ERR_DISCONNECT:
		return L"EpgTimerサービスへの接続が切断されました。";
	case CMD_ERR_TIMEOUT:
		return L"EpgTimerサービスから応答がありません。";
	case CMD_ERR_BUSY:
		return L"EpgTimerサービスがビジー状態のため処理できません。";
	}
	return NULL;
}


LPCWSTR GetDayOfWeekText(UINT DayOfWeek)
{
	if (DayOfWeek>=7)
		return L"";
	return L"日\0月\0火\0水\0木\0金\0土"+DayOfWeek*2;
}


int FormatString(LPTSTR pszDest,int DestLength,LPCTSTR pszFormat, ...)
{
	if (DestLength<=0)
		return 0;

	va_list Args;

	va_start(Args,pszFormat);
	LPTSTR pEnd=pszDest;
	StringCchVPrintfEx(pszDest,DestLength,&pEnd,NULL,STRSAFE_IGNORE_NULLS,pszFormat,Args);
	va_end(Args);
	return (int)(pEnd-pszDest);
}


int FormatStringV(LPTSTR pszDest,int DestLength,LPCTSTR pszFormat,va_list Args)
{
	if (DestLength<=0)
		return 0;

	LPTSTR pEnd=pszDest;
	StringCchVPrintfEx(pszDest,DestLength,&pEnd,NULL,STRSAFE_IGNORE_NULLS,pszFormat,Args);
	return (int)(pEnd-pszDest);
}


static int FormatNumberString(LPCTSTR pszValue,LPTSTR pszNumber,int MaxLength)
{
	TCHAR szDecimal[4];
	LPTSTR p;

	if (::GetNumberFormat(LOCALE_USER_DEFAULT,0,pszValue,NULL,pszNumber,MaxLength)<=0) {
		pszNumber[0]=_T('\0');
		return 0;
	}
	::GetLocaleInfo(LOCALE_USER_DEFAULT,LOCALE_SDECIMAL,szDecimal,_countof(szDecimal));
	for (p=pszNumber;*p!=_T('\0');p++) {
		if (*p==szDecimal[0]) {
			*p=_T('\0');
			break;
		}
	}
	return (int)(p-pszNumber);
}


int FormatInt(int Value,LPTSTR pszText,int MaxLength)
{
	TCHAR szValue[24];

	::wsprintf(szValue,TEXT("%d"),Value);
	return FormatNumberString(szValue,pszText,MaxLength);
}


int FormatSystemTime(const SYSTEMTIME &Time,LPTSTR pszText,int MaxLength,unsigned int Flags)
{
	if (MaxLength<=0)
		return 0;

	int Length=0;

	if ((Flags&SYSTEMTIME_FORMAT_TIMEONLY)==0) {
		Length=::GetDateFormat(LOCALE_USER_DEFAULT,
							   (Flags&SYSTEMTIME_FORMAT_LONG)!=0?DATE_LONGDATE:DATE_SHORTDATE,
							   &Time,NULL,pszText,MaxLength);
		if (Length>0)
			Length--;
	}
	if ((Flags&(SYSTEMTIME_FORMAT_TIME | SYSTEMTIME_FORMAT_TIMEONLY))!=0) {
		if (Length<MaxLength-2) {
			pszText[Length++]=_T(' ');
			Length+=::GetTimeFormat(LOCALE_USER_DEFAULT,
									TIME_FORCE24HOURFORMAT |
										((Flags&SYSTEMTIME_FORMAT_SECONDS)!=0?0:TIME_NOSECONDS),
									&Time,TEXT("HH':'mm':'ss"),pszText+Length,MaxLength-Length);
		}
	}
	pszText[Length]=_T('\0');
	return Length;
}


int CompareSystemTime(const SYSTEMTIME &Time1,const SYSTEMTIME &Time2)
{
	DWORD Date1,Date2;

	Date1=((DWORD)Time1.wYear<<16) | ((DWORD)Time1.wMonth<<8) | Time1.wDay;
	Date2=((DWORD)Time2.wYear<<16) | ((DWORD)Time2.wMonth<<8) | Time2.wDay;
	if (Date1==Date2) {
		Date1=((DWORD)Time1.wHour<<24) | ((DWORD)Time1.wMinute<<16) |
			  ((DWORD)Time1.wSecond<<10) | Time1.wMilliseconds;
		Date2=((DWORD)Time2.wHour<<24) | ((DWORD)Time2.wMinute<<16) |
			  ((DWORD)Time2.wSecond<<10) | Time2.wMilliseconds;
	}
	if (Date1<Date2)
		return -1;
	if (Date1>Date2)
		return 1;
	return 0;
}


void GetEndTime(const SYSTEMTIME &StartTime,DWORD Duration,SYSTEMTIME *pEndTime)
{
	FILETIME ft;
	ULARGE_INTEGER uli;

	::SystemTimeToFileTime(&StartTime,&ft);
	uli.LowPart=ft.dwLowDateTime;
	uli.HighPart=ft.dwHighDateTime;
	uli.QuadPart+=(ULONGLONG)Duration*10000000ULL;
	ft.dwLowDateTime=uli.LowPart;
	ft.dwHighDateTime=uli.HighPart;
	::FileTimeToSystemTime(&ft,pEndTime);
}


BOOL WritePrivateProfileInt(LPCTSTR pszSection,LPCTSTR pszKey,int Value,LPCTSTR pszFileName)
{
	TCHAR szValue[32];

	::wsprintf(szValue,TEXT("%d"),Value);
	return ::WritePrivateProfileString(pszSection,pszKey,szValue,pszFileName);
}


void EnableDlgItem(HWND hDlg,int ID,bool fEnable)
{
	::EnableWindow(::GetDlgItem(hDlg,ID),fEnable);
}


void EnableDlgItems(HWND hDlg,int FirstID,int LastID,bool fEnable)
{
	for (int i=FirstID;i<=LastID;i++)
		EnableDlgItem(hDlg,i,fEnable);
}


void SetComboBoxList(HWND hDlg,int ID,LPCTSTR const *pList,int Length)
{
	for (int i=0;i<Length;i++)
		::SendDlgItemMessage(hDlg,ID,CB_ADDSTRING,0,reinterpret_cast<LPARAM>(pList[i]));
}


int GetDlgItemString(HWND hDlg,int ID,wstring *pString)
{
	int Length=::GetWindowTextLengthW(::GetDlgItem(hDlg,ID));

	if (Length<1) {
		pString->clear();
	} else {
		LPWSTR pszBuffer=new WCHAR[Length+1];

		::GetDlgItemTextW(hDlg,ID,pszBuffer,Length+1);
		*pString=pszBuffer;
		delete [] pszBuffer;
	}
	return Length;
}


int GetComboBoxString(HWND hDlg,int ID,int Index,wstring *pString)
{
	int Length=(int)::SendDlgItemMessage(hDlg,ID,CB_GETLBTEXTLEN,Index,0);

	if (Length<1) {
		pString->clear();
	} else {
		LPWSTR pszBuffer=new WCHAR[Length+1];

		::SendDlgItemMessage(hDlg,ID,CB_GETLBTEXT,Index,
							 reinterpret_cast<LPARAM>(pszBuffer));
		*pString=pszBuffer;
		delete [] pszBuffer;
	}
	return Length;
}


int SetListBoxHorzExtent(HWND hDlg,int ID)
{
	HWND hwnd=GetDlgItem(hDlg,ID);
	int Count,MaxWidth=0;

	Count=(int)SendMessage(hwnd,LB_GETCOUNT,0,0);
	if (Count>=1) {
		HFONT hfont,hfontOld;
		HDC hdc;

		hfont=reinterpret_cast<HFONT>(SendMessage(hwnd,WM_GETFONT,0,0));
		if (hfont==NULL || (hdc=GetDC(hwnd))==NULL)
			return false;
		hfontOld=static_cast<HFONT>(SelectObject(hdc,hfont));
		LPTSTR pszText=NULL;
		size_t MaxLength=0;
		for (int i=0;i<Count;i++) {
			LRESULT Length=SendMessage(hwnd,LB_GETTEXTLEN,i,0);
			if (Length>0) {
				if ((size_t)Length>MaxLength) {
					delete [] pszText;
					MaxLength=Length+32;
					pszText=new TCHAR[MaxLength];
				}
				SIZE sz;
				SendMessage(hwnd,LB_GETTEXT,i,reinterpret_cast<LPARAM>(pszText));
				GetTextExtentPoint32(hdc,pszText,(int)Length,&sz);
				if (sz.cx>MaxWidth)
					MaxWidth=sz.cx;
			}
		}
		delete [] pszText;
		SelectObject(hdc,hfontOld);
		ReleaseDC(hwnd,hdc);
		MaxWidth+=4;
	}
	SendMessage(hwnd,LB_SETHORIZONTALEXTENT,MaxWidth,0);
	return MaxWidth;
}


int SetComboBoxDroppedWidth(HWND hDlg,int ID)
{
	HWND hwnd=GetDlgItem(hDlg,ID);
	int Count,MaxWidth=0;

	Count=(int)SendMessage(hwnd,CB_GETCOUNT,0,0);
	if (Count>=1) {
		HFONT hfont,hfontOld;
		HDC hdc;

		hfont=reinterpret_cast<HFONT>(SendMessage(hwnd,WM_GETFONT,0,0));
		if (hfont==NULL || (hdc=GetDC(hwnd))==NULL)
			return false;
		hfontOld=static_cast<HFONT>(SelectObject(hdc,hfont));
		LPTSTR pszText=NULL;
		size_t MaxLength=0;
		for (int i=0;i<Count;i++) {
			LRESULT Length=SendMessage(hwnd,CB_GETLBTEXTLEN,i,0);
			if (Length>0) {
				if ((size_t)Length>MaxLength) {
					delete [] pszText;
					MaxLength=Length+32;
					pszText=new TCHAR[MaxLength];
				}
				SIZE sz;
				SendMessage(hwnd,CB_GETLBTEXT,i,reinterpret_cast<LPARAM>(pszText));
				GetTextExtentPoint32(hdc,pszText,(int)Length,&sz);
				if (sz.cx>MaxWidth)
					MaxWidth=sz.cx;
			}
		}
		delete [] pszText;
		SelectObject(hdc,hfontOld);
		ReleaseDC(hwnd,hdc);
		MaxWidth+=8;
	}
	RECT rc;
	GetWindowRect(hwnd,&rc);
	SendMessage(hwnd,CB_SETDROPPEDWIDTH,max(MaxWidth,rc.right-rc.left),0);
	return MaxWidth;
}


bool FileOpenDialog(HWND hwndOwner,wstring *pFileName,LPCWSTR pszTitle,
					LPCWSTR pFilter,LPCWSTR pszDefaultExtension)
{
	OPENFILENAME ofn;
	WCHAR szFileName[MAX_PATH],szInitialDir[MAX_PATH];

	szInitialDir[0]=L'\0';
	if (!pFileName->empty()) {
		LPCWSTR pszPath=pFileName->c_str();
		LPCWSTR pszFileName=::PathFindFileName(pszPath);
		::lstrcpyn(szFileName,pszFileName,_countof(szFileName));
		if (pszFileName>pszPath)
			::lstrcpyn(szInitialDir,pszPath,(int)min(pszFileName-pszPath,_countof(szInitialDir)));
	} else {
		szFileName[0]=L'\0';
	}
	ZeroMemory(&ofn,sizeof(ofn));
	ofn.lStructSize=sizeof(ofn);
	ofn.hwndOwner=hwndOwner;
	ofn.lpstrFilter=pFilter;
	ofn.nFilterIndex=1;
	ofn.lpstrFile=szFileName;
	ofn.nMaxFile=_countof(szFileName);
	ofn.lpstrInitialDir=szInitialDir[0]!=L'\0'?szInitialDir:NULL;
	ofn.lpstrTitle=pszTitle;
	ofn.Flags=OFN_EXPLORER | OFN_HIDEREADONLY | OFN_DONTADDTORECENT;
	ofn.lpstrDefExt=pszDefaultExtension;
	if (!::GetOpenFileName(&ofn))
		return false;
	*pFileName=szFileName;
	return true;
}


static int CALLBACK BrowseFolderCallback(HWND hwnd,UINT uMsg,LPARAM lpData,LPARAM lParam)
{
	switch (uMsg) {
	case BFFM_INITIALIZED:
		if (((LPWSTR)lParam)[0]!=L'\0') {
			WCHAR szDirectory[MAX_PATH];

			lstrcpy(szDirectory,(LPWSTR)lParam);
			PathRemoveBackslash(szDirectory);
			SendMessage(hwnd,BFFM_SETSELECTION,TRUE,reinterpret_cast<LPARAM>(szDirectory));
		}
		break;
	}
	return 0;
}

bool BrowseFolderDialog(HWND hwndOwner,wstring *pFolder,LPCWSTR pszTitle)
{
	WCHAR szDirectory[MAX_PATH];
	BROWSEINFO bi;

	if (!pFolder->empty())
		lstrcpy(szDirectory,pFolder->c_str());
	else
		szDirectory[0]=L'\0';
	bi.hwndOwner=hwndOwner;
	bi.pidlRoot=NULL;
	bi.pszDisplayName=szDirectory;
	bi.lpszTitle=pszTitle;
	bi.ulFlags=BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE;
	bi.lpfn=BrowseFolderCallback;
	bi.lParam=reinterpret_cast<LPARAM>(szDirectory);
	PIDLIST_ABSOLUTE pidl=SHBrowseForFolder(&bi);
	if (pidl==NULL)
		return false;
	BOOL fRet=SHGetPathFromIDList(pidl,szDirectory);
	CoTaskMemFree(pidl);
	if (!fRet)
		return false;
	*pFolder=szDirectory;
	return true;
}


}	// namespace EDCBSupport
