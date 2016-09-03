#pragma once


namespace EDCBSupport
{

int FormatString(LPTSTR pszDest,int DestLength,LPCTSTR pszFormat, ...);
int FormatStringV(LPTSTR pszDest,int DestLength,LPCTSTR pszFormat,va_list Args);
int FormatInt(int Value,LPTSTR pszText,int MaxLength);
enum {
	SYSTEMTIME_FORMAT_LONG		=0x0001,
	SYSTEMTIME_FORMAT_TIME		=0x0002,
	SYSTEMTIME_FORMAT_TIMEONLY	=0x0004,
	SYSTEMTIME_FORMAT_SECONDS	=0x0008
};
int FormatSystemTime(const SYSTEMTIME &Time,LPTSTR pszText,int MaxLength,unsigned int Flags=0);

template<typename T> inline int CompareValue(T Value1,T Value2)
{
	return Value1<Value2?-1:Value1>Value2?1:0;
}

LPCWSTR GetCmdErrorMessage(DWORD Err);
LPCWSTR GetDayOfWeekText(UINT DayOfWeek);

int CompareSystemTime(const SYSTEMTIME &Time1,const SYSTEMTIME &Time2);
void GetEndTime(const SYSTEMTIME &StartTime,DWORD Duration,SYSTEMTIME *pEndTime);

BOOL WritePrivateProfileInt(LPCTSTR pszSection,LPCTSTR pszKey,int Value,LPCTSTR pszFileName);

void EnableDlgItem(HWND hDlg,int ID,bool fEnable);
void EnableDlgItems(HWND hDlg,int FirstID,int LastID,bool fEnable);
void SetComboBoxList(HWND hDlg,int ID,LPCTSTR const *pList,int Length);
int GetDlgItemString(HWND hDlg,int ID,wstring *pString);
int GetComboBoxString(HWND hDlg,int ID,int Index,wstring *pString);
int SetListBoxHorzExtent(HWND hDlg,int ID);
int SetComboBoxDroppedWidth(HWND hDlg,int ID);

bool FileOpenDialog(HWND hwndOwner,wstring *pFileName,LPCWSTR pszTitle,
					LPCWSTR pFilter,LPCWSTR pszDefaultExtension=NULL);
bool BrowseFolderDialog(HWND hwndOwner,wstring *pFolder,LPCWSTR pszTitle);

inline int RGBIntensity(COLORREF Color)
{
	return (GetRValue(Color)*299+GetGValue(Color)*587+GetBValue(Color)*114)/1000;
}

class CCriticalLock
{
	CRITICAL_SECTION m_CriticalSection;
public:
	CCriticalLock() { ::InitializeCriticalSection(&m_CriticalSection); }
	~CCriticalLock() { ::DeleteCriticalSection(&m_CriticalSection); }
	void Lock() { ::EnterCriticalSection(&m_CriticalSection); }
	void Unlock() { ::LeaveCriticalSection(&m_CriticalSection); }
};

class CBlockLock
{
	CCriticalLock &m_Lock;
public:
	CBlockLock(CCriticalLock &Lock) : m_Lock(Lock) { m_Lock.Lock(); }
	~CBlockLock() { m_Lock.Unlock(); }
};

}	// namespace EDCBSupport
