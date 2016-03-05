/*
	EDCB Support Plugin

	TVTestの番組表をEpgDataCap_Bonと連携させるプラグイン
*/

#include "stdafx.h"
#include <process.h>
#define TVTEST_PLUGIN_CLASS_IMPLEMENT
#include "EDCBSupport.h"
#include "../../Common/PipeServer.h"
#include "../../Common/CryptUtil.h"
#include "ReserveDialog.h"
#include "ReserveListForm.h"
#include "resource.h"


namespace EDCBSupport
{


// サービスごとの予約情報
class CServiceReserveInfo
{
public:
	WORD m_NetworkID;
	WORD m_TransportStreamID;
	WORD m_ServiceID;
	std::map<WORD,RESERVE_DATA*> m_EventReserveMap;
	std::vector<RESERVE_DATA*> m_ProgramReserveList;

	CServiceReserveInfo(WORD NetworkID,WORD TransportStreamID,WORD ServiceID);
	void SortProgramReserveList();
	RESERVE_DATA *FindEvent(WORD EventID);
	int FindProgramReserve(const SYSTEMTIME &StartTime,DWORD Duration,
						   std::vector<RESERVE_DATA*> *pReserveList);
};

// プラグインクラス
class CEDCBSupportPlugin : public TVTest::CTVTestPlugin, public CEDCBSupportCore
{
	// 色分け
	enum {
		COLOR_NORMAL,		// 通常の予約
		COLOR_DISABLED,		// 無効になっている予約
		COLOR_NOTUNER,		// チューナーが足りない予約
		COLOR_CONFLICT,		// 一部かぶっている予約
		NUM_COLORS
	};

	// 色分けの情報
	struct ColorInfo {
		LPCTSTR pszName;	// 名前
		bool fEnabled;		// 表示有効
		COLORREF Color;		// 色
	};

	// メニューのコマンド
	enum {
		COMMAND_RESERVELIST,	// 予約一覧表示
		COMMAND_RESERVE,		// 予約登録/変更
		COMMAND_DELETE,			// 予約削除
		NUM_COMMANDS
	};

	typedef std::map<ULONGLONG,CServiceReserveInfo> ServiceReserveMap;

	ColorInfo m_ColorList[NUM_COLORS];
	int m_ColoringFrameWidth;
	int m_ColoringFrameHeight;

	HWND m_hwndProgramGuide;
	bool m_fSettingsLoaded;
	TCHAR m_szIniFileName[MAX_PATH];

	CPipeServer m_PipeServer;
	CCriticalLock m_ReserveListLock;
	std::vector<RESERVE_DATA> m_ReserveList;
	ServiceReserveMap m_ServiceReserveMap;
	RESERVE_DATA m_CurReserveData;
	HANDLE m_hInitializeThread;
	HANDLE m_hCancelEvent;
	bool m_fGUIRegistered;
	CReserveListForm m_ReserveListForm;

	struct RecSettings {
		BYTE RecMode;
		BYTE Priority;
		BYTE TuijyuuFlag;
		BYTE PittariFlag;
		RecSettings()
			: RecMode(RECMODE_SERVICE)
			, Priority(2)
			, TuijyuuFlag(1)
			, PittariFlag(0)
		{
		}
	};
	RecSettings m_DefaultRecSettings;

	static ULONGLONG ServiceReserveMapKey(WORD NID,WORD TSID,WORD SID) {
		return ((ULONGLONG)NID<<32) | ((ULONGLONG)TSID<<16) | (ULONGLONG)SID;
	}

	void LoadSettings();
	void SaveSettings();
	DWORD GetReserveList();
	int FindReserve(const TVTest::ProgramGuideProgramInfo *pProgramInfo,
					std::vector<RESERVE_DATA*> *pReserveList);
	bool EnablePlugin(bool fEnable);
	bool ProgramGuideInitialize(HWND hwnd,bool fEnable);
	bool ProgramGuideFinalize(bool fClose);
	bool ToggleShowReserveList();
	bool OnProgramGuideCommand(UINT Command,
							   const TVTest::ProgramGuideCommandParam *pParam);
	bool DrawBackground(const TVTest::ProgramGuideProgramInfo *pProgramInfo,
						const TVTest::ProgramGuideProgramDrawBackgroundInfo *pInfo);
	void GetReserveFrameRect(const TVTest::ProgramGuideProgramInfo *pProgramInfo,
							 const RESERVE_DATA *pReserveData,
							 const RECT &ItemRect,RECT *pFrameRect) const;
	bool DrawReserveFrame(const TVTest::ProgramGuideProgramInfo *pProgramInfo,
						  const TVTest::ProgramGuideProgramDrawBackgroundInfo *pInfo,
						  const RESERVE_DATA *pReserveData) const;
	RESERVE_DATA *GetReserveFromPoint(const TVTest::ProgramGuideProgramInfo *pProgramInfo,
									  const RECT &ItemRect,const POINT &Point);
	int InitializeMenu(const TVTest::ProgramGuideInitializeMenuInfo *pInfo);
	bool OnMenuSelected(UINT Command);
	int InitializeProgramMenu(const TVTest::ProgramGuideProgramInfo *pProgramInfo,
							  const TVTest::ProgramGuideProgramInitializeMenuInfo *pInfo);
	bool OnProgramMenuSelected(const TVTest::ProgramGuideProgramInfo *pProgramInfo,UINT Command);
	bool PluginSettings(HWND hwndOwner);
	void ApplyColorScheme();

	TVTest::EpgEventInfo *GetEventInfo(const TVTest::ProgramGuideProgramInfo *pProgramInfo)
	{
		TVTest::EpgEventQueryInfo QueryInfo;

		QueryInfo.NetworkID=pProgramInfo->NetworkID;
		QueryInfo.TransportStreamID=pProgramInfo->TransportStreamID;
		QueryInfo.ServiceID=pProgramInfo->ServiceID;
		QueryInfo.Type=TVTest::EPG_EVENT_QUERY_EVENTID;
		QueryInfo.Flags=0;
		QueryInfo.EventID=pProgramInfo->EventID;
		return m_pApp->GetEpgEventInfo(&QueryInfo);
	}

// CEDCBSupportCore
	void ShowCmdErrorMessage(HWND hwndOwner,LPCTSTR pszMessage,DWORD Err) const override;
	bool ChangeReserve(HWND hwndOwner,const RESERVE_DATA &ReserveData) override;
	bool ChangeReserve(HWND hwndOwner,std::vector<RESERVE_DATA> &ReserveList) override;
	bool DeleteReserve(HWND hwndOwner,DWORD ReserveID) override;
	bool DeleteReserve(HWND hwndOwner,std::vector<DWORD> &ReserveIDList) override;
	bool ShowReserveDialog(HWND hwndOwner,RESERVE_DATA *pReserveData) override;
	bool GetRecordedFileList(HWND hwndOwner,std::vector<REC_FILE_INFO> *pFileList) override;
	bool DeleteRecordedFileInfo(HWND hwndOwner,DWORD ID) override;
	bool DeleteRecordedFileInfo(HWND hwndOwner,std::vector<DWORD> &IDList) override;

	static LRESULT CALLBACK EventCallback(UINT Event,LPARAM lParam1,LPARAM lParam2,void *pClientData);
	static unsigned int __stdcall InitializeThread(void *pParam);
	static int CALLBACK CtrlCmdCallback(void *pParam,CMD_STREAM *pCmdParam,CMD_STREAM *pResParam);
	static INT_PTR CALLBACK SettingsDlgProc(HWND hDlg,UINT uMsg,WPARAM wParam,LPARAM lParam);

public:
	CEDCBSupportPlugin();
// CTVTestPlugin
	bool GetPluginInfo(TVTest::PluginInfo *pInfo) override;
	bool Initialize() override;
	bool Finalize() override;
};


#pragma warning(disable: 4355)

CEDCBSupportPlugin::CEDCBSupportPlugin()
	: m_ColoringFrameWidth(3)
	, m_ColoringFrameHeight(3)
	, m_hwndProgramGuide(NULL)
	, m_fSettingsLoaded(false)
	, m_hInitializeThread(NULL)
	, m_hCancelEvent(NULL)
	, m_fGUIRegistered(false)
	, m_ReserveListForm(this)
{
#ifdef _DEBUG
	_CrtSetDbgFlag(_CRTDBG_ALLOC_MEM_DF | _CRTDBG_LEAK_CHECK_DF);
#endif

	// 配色の設定を初期化
	m_ColorList[COLOR_NORMAL].pszName=TEXT("Normal");
	m_ColorList[COLOR_NORMAL].fEnabled=true;
	m_ColorList[COLOR_NORMAL].Color=RGB(64,255,0);
	m_ColorList[COLOR_DISABLED].pszName=TEXT("Disabled");
	m_ColorList[COLOR_DISABLED].fEnabled=false;
	m_ColorList[COLOR_DISABLED].Color=RGB(128,128,128);
	m_ColorList[COLOR_NOTUNER].pszName=TEXT("NoTuner");
	m_ColorList[COLOR_NOTUNER].fEnabled=true;
	m_ColorList[COLOR_NOTUNER].Color=RGB(255,64,0);
	m_ColorList[COLOR_CONFLICT].pszName=TEXT("Conflict");
	m_ColorList[COLOR_CONFLICT].fEnabled=true;
	m_ColorList[COLOR_CONFLICT].Color=RGB(255,192,0);
}


// プラグインの情報を返す
bool CEDCBSupportPlugin::GetPluginInfo(TVTest::PluginInfo *pInfo)
{
	pInfo->Type           = TVTest::PLUGIN_TYPE_NORMAL;
	pInfo->Flags          = TVTest::PLUGIN_FLAG_HASSETTINGS;
	pInfo->pszPluginName  = L"EpgDataCap_Bon連携";
	pInfo->pszCopyright   = L"Copyright(c) HDUSTestの中の人";
	pInfo->pszDescription = L"番組表にEpgDataCap_Bonとの連携機能を追加します。";
	return true;
}


// 初期化処理
bool CEDCBSupportPlugin::Initialize()
{
	// 番組表のイベントの通知を有効にする
	m_pApp->EnableProgramGuideEvent(TVTest::PROGRAMGUIDE_EVENT_GENERAL);

	// イベントコールバック関数を登録
	m_pApp->SetEventCallback(EventCallback,this);

	// 番組表のコマンドを登録
	static const TVTest::ProgramGuideCommandInfo Command = {
		TVTest::PROGRAMGUIDE_COMMAND_TYPE_PROGRAM,
		0,
		COMMAND_RESERVE,
		L"Reserve",
		L"EpgTimerに予約登録/予約変更",
	};
	m_pApp->RegisterProgramGuideCommand(&Command);

	return true;
}


// 終了処理
bool CEDCBSupportPlugin::Finalize()
{
	ProgramGuideFinalize(true);

	SaveSettings();

	return true;
}


// 16進数文字列を数値に変換
static UINT HexStringToInt(LPCTSTR pString,int Length)
{
	UINT Value=0;

	for (int i=0;i<Length;i++) {
		const TCHAR c=pString[i];

		Value<<=4;
		if (c>=_T('0') && c<=_T('9'))
			Value|=c-_T('0');
		else if (c>=_T('a') && c<=_T('f'))
			Value|=(c-_T('a'))+10;
		else if (c>=_T('A') && c<=_T('F'))
			Value|=(c-_T('A'))+10;
	}
	return Value;
}

// 設定の読み込み
void CEDCBSupportPlugin::LoadSettings()
{
	if (!m_fSettingsLoaded) {
		::GetModuleFileName(g_hinstDLL,m_szIniFileName,_countof(m_szIniFileName));
		::PathRenameExtension(m_szIniFileName,TEXT(".ini"));

		TCHAR szText[512];

		if (::GetPrivateProfileString(TEXT("Settings"),TEXT("EDCBFolder"),TEXT(""),
									  szText,_countof(szText),m_szIniFileName)>0)
			m_EDCBSettings.EDCBDirectory=szText;

		m_EDCBSettings.fUseNetwork=::GetPrivateProfileInt(
			TEXT("Settings"),TEXT("UseNetwork"),m_EDCBSettings.fUseNetwork,m_szIniFileName)!=0;
		if (::GetPrivateProfileString(TEXT("Settings"),TEXT("NetworkAddress"),
									  m_EDCBSettings.NetworkAddress.c_str(),
									  szText,_countof(szText),m_szIniFileName)>0)
			m_EDCBSettings.NetworkAddress=szText;
		m_EDCBSettings.NetworkPort=(WORD)::GetPrivateProfileInt(
			TEXT("Settings"),TEXT("NetworkPort"),m_EDCBSettings.NetworkPort,m_szIniFileName);
		if (::GetPrivateProfileString(TEXT("Settings"), TEXT("NetworkPassword"), m_EDCBSettings.NetworkPassword.c_str(), szText, _countof(szText), m_szIniFileName) > 0) {
			wstring decrypt;
			if (CCryptUtil::Decrypt(szText, decrypt, CRYPTPROTECT_UI_FORBIDDEN | CRYPTPROTECT_AUDIT)) {
				m_EDCBSettings.NetworkPassword = szText;
			}
			else if (wcslen(szText) <= MAX_PASSWORD_LENGTH && CCryptUtil::Encrypt(szText, m_EDCBSettings.NetworkPassword, CRYPTPROTECT_UI_FORBIDDEN | CRYPTPROTECT_AUDIT)) {
				::WritePrivateProfileString(TEXT("Settings"), TEXT("NetworkPassword"), m_EDCBSettings.NetworkPassword.c_str(), m_szIniFileName);
			}
		}

		for (int i=0;i<NUM_COLORS;i++) {
			TCHAR szKey[32];

			::wsprintf(szKey,TEXT("%sColor"),m_ColorList[i].pszName);
			if (::GetPrivateProfileString(TEXT("Settings"),szKey,TEXT(""),
										  szText,_countof(szText),
										  m_szIniFileName)==7
					&& szText[0]==_T('#')) {
				UINT Color=HexStringToInt(szText+1,6);
				m_ColorList[i].Color=RGB(Color>>16,(Color>>8)&0xFF,Color&0xFF);
			}
			::wsprintf(szKey,TEXT("%sColor_Enabled"),m_ColorList[i].pszName);
			m_ColorList[i].fEnabled=
				::GetPrivateProfileInt(TEXT("Settings"),szKey,
									   m_ColorList[i].fEnabled,m_szIniFileName)!=0;
		}

		ApplyColorScheme();

		int RecMode=::GetPrivateProfileInt(TEXT("DefaultRecSettings"),TEXT("RecMode"),
										   m_DefaultRecSettings.RecMode,m_szIniFileName);
		if (RecMode>=0 && RecMode<=RECMODE_NO)
			m_DefaultRecSettings.RecMode=(BYTE)RecMode;
		int Priority=::GetPrivateProfileInt(TEXT("DefaultRecSettings"),TEXT("Priority"),
											m_DefaultRecSettings.Priority,m_szIniFileName);
		m_DefaultRecSettings.Priority=(BYTE)(Priority<1?1:Priority>5?5:Priority);
		m_DefaultRecSettings.TuijyuuFlag=
			::GetPrivateProfileInt(TEXT("DefaultRecSettings"),TEXT("TuijyuuFlag"),
								   m_DefaultRecSettings.TuijyuuFlag,m_szIniFileName)!=0;
		m_DefaultRecSettings.PittariFlag=
			::GetPrivateProfileInt(TEXT("DefaultRecSettings"),TEXT("PittariFlag"),
								   m_DefaultRecSettings.PittariFlag,m_szIniFileName)!=0;

		int BatCount=::GetPrivateProfileInt(TEXT("BatFileHistory"),TEXT("Count"),
											0,m_szIniFileName);
		for (int i=BatCount-1;i>=0;i--) {
			TCHAR szKey[32],szFile[MAX_PATH];
			::wsprintf(szKey,TEXT("File%d"),i);
			if (GetPrivateProfileString(TEXT("BatFileHistory"),szKey,TEXT(""),
										szFile,_countof(szFile),m_szIniFileName)<1)
				break;
			CReserveDialog::AddBatFileList(szFile);
		}

		m_ReserveListForm.LoadSettings(m_szIniFileName);

		m_fSettingsLoaded=true;
	}
}


// 設定の保存
void CEDCBSupportPlugin::SaveSettings()
{
	if (m_fSettingsLoaded) {
		::WritePrivateProfileString(TEXT("Settings"),TEXT("EDCBFolder"),
									m_EDCBSettings.EDCBDirectory.c_str(),
									m_szIniFileName);
		WritePrivateProfileInt(TEXT("Settings"),TEXT("UseNetwork"),
							   m_EDCBSettings.fUseNetwork,
							   m_szIniFileName);
		::WritePrivateProfileString(TEXT("Settings"),TEXT("NetworkAddress"),
									m_EDCBSettings.NetworkAddress.c_str(),
									m_szIniFileName);
		WritePrivateProfileInt(TEXT("Settings"),TEXT("NetworkPort"),
							   m_EDCBSettings.NetworkPort,
							   m_szIniFileName);
		::WritePrivateProfileString(TEXT("Settings"), TEXT("NetworkPassword"),
								    m_EDCBSettings.NetworkPassword.c_str(),
								    m_szIniFileName);

		for (int i=0;i<NUM_COLORS;i++) {
			TCHAR szKey[32],szValue[32];

			::wsprintf(szKey,TEXT("%sColor"),m_ColorList[i].pszName);
			::wsprintf(szValue,TEXT("#%02x%02x%02x"),
					   GetRValue(m_ColorList[i].Color),
					   GetGValue(m_ColorList[i].Color),
					   GetBValue(m_ColorList[i].Color));
			::WritePrivateProfileString(TEXT("Settings"),szKey,szValue,m_szIniFileName);
			::wsprintf(szKey,TEXT("%sColor_Enabled"),m_ColorList[i].pszName);
			WritePrivateProfileInt(TEXT("Settings"),szKey,
								   m_ColorList[i].fEnabled,
								   m_szIniFileName);
		}

		WritePrivateProfileInt(TEXT("DefaultRecSettings"),TEXT("RecMode"),
							   m_DefaultRecSettings.RecMode,m_szIniFileName);
		WritePrivateProfileInt(TEXT("DefaultRecSettings"),TEXT("Priority"),
							   m_DefaultRecSettings.Priority,m_szIniFileName);
		WritePrivateProfileInt(TEXT("DefaultRecSettings"),TEXT("TuijyuuFlag"),
							   m_DefaultRecSettings.TuijyuuFlag,m_szIniFileName);
		WritePrivateProfileInt(TEXT("DefaultRecSettings"),TEXT("PittariFlag"),
							   m_DefaultRecSettings.PittariFlag,m_szIniFileName);

		const CReserveDialog::BatFileList &BatFileList=CReserveDialog::GetBatFileList();
		WritePrivateProfileInt(TEXT("BatFileHistory"),TEXT("Count"),
							   (int)BatFileList.size(),m_szIniFileName);
		for (size_t i=0;i<BatFileList.size();i++) {
			TCHAR szKey[32];
			::wsprintf(szKey,TEXT("File%d"),(int)i);
			WritePrivateProfileString(TEXT("BatFileHistory"),szKey,
									  BatFileList[i].c_str(),m_szIniFileName);
		}

		m_ReserveListForm.SaveSettings(m_szIniFileName);
	}
}


// イベントコールバック関数
// 何かイベントが起きると呼ばれる
LRESULT CALLBACK CEDCBSupportPlugin::EventCallback(UINT Event,LPARAM lParam1,LPARAM lParam2,void *pClientData)
{
	CEDCBSupportPlugin *pThis=static_cast<CEDCBSupportPlugin*>(pClientData);

	switch (Event) {
	case TVTest::EVENT_PLUGINENABLE:
		// プラグインの有効状態が変化した
		return pThis->EnablePlugin(lParam1!=0);

	case TVTest::EVENT_PLUGINSETTINGS:
		// プラグインの設定を行う
		return pThis->PluginSettings(reinterpret_cast<HWND>(lParam1));

	case TVTest::EVENT_PROGRAMGUIDE_INITIALIZE:
		// 番組表の初期化処理
		return pThis->ProgramGuideInitialize(reinterpret_cast<HWND>(lParam1),
											 pThis->m_pApp->IsPluginEnabled());

	case TVTest::EVENT_PROGRAMGUIDE_FINALIZE:
		// 番組表の終了処理
		return pThis->ProgramGuideFinalize(true);

	case TVTest::EVENT_PROGRAMGUIDE_COMMAND:
		// 番組表のコマンドの実行
		return pThis->OnProgramGuideCommand(
			(UINT)lParam1,
			reinterpret_cast<const TVTest::ProgramGuideCommandParam*>(lParam2));

	case TVTest::EVENT_PROGRAMGUIDE_INITIALIZEMENU:
		// メニューの初期化
		return pThis->InitializeMenu(
			reinterpret_cast<const TVTest::ProgramGuideInitializeMenuInfo *>(lParam1));

	case TVTest::EVENT_PROGRAMGUIDE_MENUSELECTED:
		// メニューが選択された
		return pThis->OnMenuSelected((UINT)lParam1);

	case TVTest::EVENT_PROGRAMGUIDE_PROGRAM_DRAWBACKGROUND:
		// 番組の背景を描画
		return pThis->DrawBackground(
			reinterpret_cast<const TVTest::ProgramGuideProgramInfo*>(lParam1),
			reinterpret_cast<const TVTest::ProgramGuideProgramDrawBackgroundInfo*>(lParam2));

	case TVTest::EVENT_PROGRAMGUIDE_PROGRAM_INITIALIZEMENU:
		// 番組のメニューの初期化
		return pThis->InitializeProgramMenu(
			reinterpret_cast<const TVTest::ProgramGuideProgramInfo*>(lParam1),
			reinterpret_cast<TVTest::ProgramGuideProgramInitializeMenuInfo*>(lParam2));

	case TVTest::EVENT_PROGRAMGUIDE_PROGRAM_MENUSELECTED:
		// 番組のメニューが選択された
		return pThis->OnProgramMenuSelected(
			reinterpret_cast<const TVTest::ProgramGuideProgramInfo*>(lParam1),(UINT)lParam2);
	}
	return 0;
}


// プラグインの有効状態が変化した
bool CEDCBSupportPlugin::EnablePlugin(bool fEnable)
{
	// 番組表のイベントの通知の有効/無効を設定する
	m_pApp->EnableProgramGuideEvent(TVTest::PROGRAMGUIDE_EVENT_GENERAL |
									(fEnable?TVTest::PROGRAMGUIDE_EVENT_PROGRAM:0));

	// 初期化処理か終了処理を行う
	if (fEnable) {
		if (m_hwndProgramGuide!=NULL)
			ProgramGuideInitialize(m_hwndProgramGuide,true);
	} else {
		ProgramGuideFinalize(false);
	}

	// 番組表が表示されている場合再描画させる
	if (m_hwndProgramGuide!=NULL)
		::InvalidateRect(m_hwndProgramGuide,NULL,TRUE);

	return true;
}


// 予約の取得
DWORD CEDCBSupportPlugin::GetReserveList()
{
	CBlockLock Lock(m_ReserveListLock);

	m_ReserveList.clear();
	m_ServiceReserveMap.clear();

	DWORD Err=m_SendCtrlCmd.SendEnumReserve(&m_ReserveList);
	if (Err!=CMD_SUCCESS)
		return Err;

	for (size_t i=0;i<m_ReserveList.size();i++) {
		RESERVE_DATA *pData=&m_ReserveList[i];
		std::pair<ServiceReserveMap::iterator,bool> Result=
			m_ServiceReserveMap.insert(std::pair<ULONGLONG,CServiceReserveInfo>(
				ServiceReserveMapKey(pData->originalNetworkID,
									 pData->transportStreamID,
									 pData->serviceID),
				CServiceReserveInfo(pData->originalNetworkID,
									pData->transportStreamID,
									pData->serviceID)));
		if (pData->eventID!=0xFFFF) {
			Result.first->second.m_EventReserveMap.insert(
				std::pair<WORD,RESERVE_DATA*>(pData->eventID,pData));
		} else {
			Result.first->second.m_ProgramReserveList.push_back(pData);
		}
	}

	for (ServiceReserveMap::iterator i=m_ServiceReserveMap.begin();
			i!=m_ServiceReserveMap.end();i++) {
		i->second.SortProgramReserveList();
	}

	return CMD_SUCCESS;
}


// イベントの予約情報を取得する
int CEDCBSupportPlugin::FindReserve(const TVTest::ProgramGuideProgramInfo *pProgramInfo,
									std::vector<RESERVE_DATA*> *pReserveList)
{
	int ReserveCount=0;

	ServiceReserveMap::iterator itrService=
		m_ServiceReserveMap.find(
			ServiceReserveMapKey(pProgramInfo->NetworkID,
								 pProgramInfo->TransportStreamID,
								 pProgramInfo->ServiceID));
	if (itrService!=m_ServiceReserveMap.end()) {
		RESERVE_DATA *pReserveData=itrService->second.FindEvent(pProgramInfo->EventID);
		if (pReserveData!=NULL) {
			pReserveList->push_back(pReserveData);
			ReserveCount++;
		}

		ReserveCount+=itrService->second.FindProgramReserve(pProgramInfo->StartTime,
															pProgramInfo->Duration,
															pReserveList);
	}

	return ReserveCount;
}


// 番組表の初期化処理
bool CEDCBSupportPlugin::ProgramGuideInitialize(HWND hwnd,bool fEnable)
{
	// ウィンドウハンドルを取っておく
	m_hwndProgramGuide=hwnd;

	if (fEnable) {
		// 設定の読み込み
		LoadSettings();

		// 接続設定
		if (m_EDCBSettings.fUseNetwork)
			m_SendCtrlCmd.SetNWSetting(m_EDCBSettings.NetworkAddress,
									   m_EDCBSettings.NetworkPort,
									   m_EDCBSettings.NetworkPassword);
		m_SendCtrlCmd.SetSendMode(m_EDCBSettings.fUseNetwork);
		// ネットワーク時のタイムアウト時間は実際には使われない
		m_SendCtrlCmd.SetConnectTimeOut(m_EDCBSettings.fUseNetwork?10*1000:3*1000);

		// コマンド受信サーバー開始
		if (!m_EDCBSettings.fUseNetwork) {
			const DWORD ProcessID=::GetCurrentProcessId();
			WCHAR szEventName[256],szPipeName[256];
			::wsprintf(szEventName,L"%s%d",CMD2_GUI_CTRL_WAIT_CONNECT,(int)ProcessID);
			::wsprintf(szPipeName,L"%s%d",CMD2_GUI_CTRL_PIPE,(int)ProcessID);
			m_PipeServer.StartServer(szEventName,szPipeName,CtrlCmdCallback,this,
									 THREAD_PRIORITY_NORMAL/*,ProcessID*/);
		}

		// 初期化処理スレッド開始
		// (番組表の表示を遅延させないために、別スレッドで初期化処理を行う)
		m_hCancelEvent=::CreateEvent(NULL,FALSE,FALSE,NULL);
		m_hInitializeThread=(HANDLE)::_beginthreadex(NULL,0,InitializeThread,this,0,NULL);
	}

	return true;
}


// 番組表の終了処理
bool CEDCBSupportPlugin::ProgramGuideFinalize(bool fClose)
{
	m_ReserveListForm.Destroy();

	if (fClose)
		m_hwndProgramGuide=NULL;

	// 初期化処理スレッド終了
	if (m_hInitializeThread!=NULL) {
		if (m_hCancelEvent!=NULL)
			::SetEvent(m_hCancelEvent);
		if (::WaitForSingleObject(m_hInitializeThread,10000)!=WAIT_OBJECT_0) {
			m_pApp->AddLog(L"初期化処理スレッドを強制終了させます。");
			::TerminateThread(m_hInitializeThread,-1);
		}
		::CloseHandle(m_hInitializeThread);
		m_hInitializeThread=NULL;
	}

	if (m_hCancelEvent!=NULL) {
		::CloseHandle(m_hCancelEvent);
		m_hCancelEvent=NULL;
	}

	// 登録の解除
	if (m_fGUIRegistered) {
		m_SendCtrlCmd.SendUnRegistGUI(::GetCurrentProcessId());
		m_fGUIRegistered=false;
	}
	m_PipeServer.StopServer();

	// 予約リストの解放
	m_ReserveListLock.Lock();
	vector<RESERVE_DATA>().swap(m_ReserveList);
	m_ServiceReserveMap.clear();
	m_ReserveListLock.Unlock();

	return true;
}


// 予約一覧の表示/非表示
bool CEDCBSupportPlugin::ToggleShowReserveList()
{
	if (!m_ReserveListForm.IsCreated()) {
		if (!CReserveListForm::Initialize(g_hinstDLL))
			return false;
		if (!m_ReserveListForm.Create(NULL))
			return false;
		m_ReserveListForm.Show();
		m_ReserveListForm.SetReserveList(&m_ReserveList);
	} else {
		m_ReserveListForm.Destroy();
	}
	return true;
}


// 番組の背景を描画
bool CEDCBSupportPlugin::DrawBackground(const TVTest::ProgramGuideProgramInfo *pProgramInfo,
										const TVTest::ProgramGuideProgramDrawBackgroundInfo *pInfo)
{
	CBlockLock Lock(m_ReserveListLock);

	// 予約情報の取得
	std::vector<RESERVE_DATA*> ReserveList;
	if (FindReserve(pProgramInfo,&ReserveList)==0)
		return false;

	// プログラム予約
	for (size_t i=0;i<ReserveList.size();i++) {
		if (ReserveList[i]->eventID==0xFFFF) {
			DrawReserveFrame(pProgramInfo,pInfo,ReserveList[i]);
		}
	}

	// イベントの予約
	for (size_t i=0;i<ReserveList.size();i++) {
		if (ReserveList[i]->eventID!=0xFFFF) {
			DrawReserveFrame(pProgramInfo,pInfo,ReserveList[i]);
			break;
		}
	}

	return true;
}


// SYSTEMTIME を秒単位の時刻表現に変換
static LONGLONG SystemTimeToSeconds(const SYSTEMTIME &Time)
{
	FILETIME ft;

	::SystemTimeToFileTime(&Time,&ft);
	return (((LONGLONG)ft.dwHighDateTime<<32) | (LONGLONG)ft.dwLowDateTime)/10000000LL;
}

// 予約の枠の位置を取得
void CEDCBSupportPlugin::GetReserveFrameRect(const TVTest::ProgramGuideProgramInfo *pProgramInfo,
											 const RESERVE_DATA *pReserveData,
											 const RECT &ItemRect,RECT *pFrameRect) const
{
	const LONGLONG EventStart=SystemTimeToSeconds(pProgramInfo->StartTime);
	const LONGLONG ReserveStart=SystemTimeToSeconds(pReserveData->startTime);
	const int Height=ItemRect.bottom-ItemRect.top;
	pFrameRect->top=ItemRect.top+
		(int)(ReserveStart-EventStart)*Height/(int)pProgramInfo->Duration;
	pFrameRect->bottom=ItemRect.top+
		(int)((ReserveStart+pReserveData->durationSecond)-EventStart)*Height/(int)pProgramInfo->Duration;
	pFrameRect->left=ItemRect.left;
	pFrameRect->right=ItemRect.right;
}


// 予約の枠を描画
bool CEDCBSupportPlugin::DrawReserveFrame(const TVTest::ProgramGuideProgramInfo *pProgramInfo,
										  const TVTest::ProgramGuideProgramDrawBackgroundInfo *pInfo,
										  const RESERVE_DATA *pReserveData) const
{
	// 色の取得
	int Color;
	if (pReserveData->recSetting.recMode==RECMODE_NO)
		Color=COLOR_DISABLED;
	else if (pReserveData->overlapMode==2)
		Color=COLOR_NOTUNER;
	else if (pReserveData->overlapMode==1)
		Color=COLOR_CONFLICT;
	else
		Color=COLOR_NORMAL;
	if (!m_ColorList[Color].fEnabled)
		return false;

	// 枠の描画
	HBRUSH hbr=::CreateSolidBrush(m_ColorList[Color].Color);

	RECT FrameRect;
	GetReserveFrameRect(pProgramInfo,pReserveData,pInfo->ItemRect,&FrameRect);

	RECT rc;
	rc.left=FrameRect.left;
	rc.right=FrameRect.right;
	if (FrameRect.top>=pInfo->ItemRect.top) {
		rc.top=FrameRect.top;
		rc.bottom=rc.top+m_ColoringFrameHeight;
		::FillRect(pInfo->hdc,&rc,hbr);
	}
	if (FrameRect.bottom<=pInfo->ItemRect.bottom) {
		rc.bottom=FrameRect.bottom;
		rc.top=rc.bottom-m_ColoringFrameHeight;
		::FillRect(pInfo->hdc,&rc,hbr);
	}
	rc.top=max(pInfo->ItemRect.top,FrameRect.top);
	rc.bottom=min(pInfo->ItemRect.bottom,FrameRect.bottom);
	rc.left=FrameRect.left;
	rc.right=rc.left+m_ColoringFrameWidth;
	::FillRect(pInfo->hdc,&rc,hbr);
	rc.right=FrameRect.right;
	rc.left=rc.right-m_ColoringFrameWidth;
	::FillRect(pInfo->hdc,&rc,hbr);

	::DeleteObject(hbr);

	return true;
}


// 位置から予約情報を取得する
RESERVE_DATA *CEDCBSupportPlugin::GetReserveFromPoint(const TVTest::ProgramGuideProgramInfo *pProgramInfo,
													  const RECT &ItemRect,const POINT &Point)
{
	// 予約情報の取得
	std::vector<RESERVE_DATA*> ReserveList;
	if (FindReserve(pProgramInfo,&ReserveList)==0)
		return NULL;

	// イベントの予約
	for (size_t i=0;i<ReserveList.size();i++) {
		if (ReserveList[i]->eventID!=0xFFFF)
			return ReserveList[i];
	}

	// プログラム予約
	for (size_t i=0;i<ReserveList.size();i++) {
		if (ReserveList[i]->eventID==0xFFFF) {
			RECT FrameRect;

			GetReserveFrameRect(pProgramInfo,ReserveList[i],ItemRect,&FrameRect);
			if (::PtInRect(&FrameRect,Point))
				return ReserveList[i];
		}
	}

	return NULL;
}


// 番組表のコマンドの実行
bool CEDCBSupportPlugin::OnProgramGuideCommand(UINT Command,
											   const TVTest::ProgramGuideCommandParam *pParam)
{
	if (Command==COMMAND_RESERVE) {
		// クリック位置の予約を取得
		m_ReserveListLock.Lock();
		RESERVE_DATA *pReserveData=
			GetReserveFromPoint(&pParam->Program,pParam->ItemRect,pParam->CursorPos);
		if (pReserveData!=NULL)
			m_CurReserveData=*pReserveData;
		else
			m_CurReserveData.reserveID=0;
		m_ReserveListLock.Unlock();
		OnProgramMenuSelected(&pParam->Program,Command);

		return true;
	}

	return false;
}


// メニューの初期化
int CEDCBSupportPlugin::InitializeMenu(const TVTest::ProgramGuideInitializeMenuInfo *pInfo)
{
	if (m_pApp->IsPluginEnabled()) {
		// メニュー追加
		::AppendMenu(pInfo->hmenu,
					 MF_STRING | MF_ENABLED | (m_ReserveListForm.IsCreated()?MF_CHECKED:MF_UNCHECKED),
					 pInfo->Command+COMMAND_RESERVELIST,
					 TEXT("EpgTimerの予約一覧表示"));

		// 使用するコマンド数を返す
		return NUM_COMMANDS;
	}

	return 0;
}


// メニューが選択された
bool CEDCBSupportPlugin::OnMenuSelected(UINT Command)
{
	if (Command==COMMAND_RESERVELIST) {
		// 予約一覧表示
		ToggleShowReserveList();
	} else {
		return false;
	}
	return true;
}


// 番組のメニューの初期化
int CEDCBSupportPlugin::InitializeProgramMenu(const TVTest::ProgramGuideProgramInfo *pProgramInfo,
											  const TVTest::ProgramGuideProgramInitializeMenuInfo *pInfo)
{
	CBlockLock Lock(m_ReserveListLock);

	// クリック位置の予約を取得
	RESERVE_DATA *pReserveData=GetReserveFromPoint(pProgramInfo,pInfo->ItemRect,pInfo->CursorPos);
	bool fReserved=pReserveData!=NULL;
	if (fReserved)
		m_CurReserveData=*pReserveData;
	else
		m_CurReserveData.reserveID=0;

	// メニュー追加
	::AppendMenu(pInfo->hmenu,MF_STRING | MF_ENABLED,pInfo->Command+COMMAND_RESERVE,
				 fReserved?TEXT("EpgTimerの予約変更"):TEXT("EpgTimerに予約登録"));
	if (fReserved)
		::AppendMenu(pInfo->hmenu,MF_STRING | MF_ENABLED,pInfo->Command+COMMAND_DELETE,
					 TEXT("EpgTimerの予約削除"));
	::AppendMenu(pInfo->hmenu,
				 MF_STRING | MF_ENABLED | (m_ReserveListForm.IsCreated()?MF_CHECKED:MF_UNCHECKED),
				 pInfo->Command+COMMAND_RESERVELIST,
				 TEXT("EpgTimerの予約一覧表示"));

	// 使用するコマンド数を返す
	return NUM_COMMANDS;
}


// 番組のメニューが選択された
bool CEDCBSupportPlugin::OnProgramMenuSelected(const TVTest::ProgramGuideProgramInfo *pProgramInfo,UINT Command)
{
	if (Command==COMMAND_RESERVELIST) {
		// 予約一覧表示
		ToggleShowReserveList();
	} else if (Command==COMMAND_RESERVE) {
		// 予約の登録/変更

		// 番組の情報を取得
		TVTest::EpgEventInfo *pEpgEventInfo=GetEventInfo(pProgramInfo);
		if (pEpgEventInfo==NULL)
			return true;

		HCURSOR hOldCursor=::SetCursor(::LoadCursor(NULL,IDC_WAIT));

		RESERVE_DATA ReserveData;
		bool fNew=m_CurReserveData.reserveID==0;
		if (!fNew) {
			// 既存予約変更
			ReserveData=m_CurReserveData;
		} else {
			// 新規予約追加
			if (pEpgEventInfo->pszEventName!=NULL)
				ReserveData.title=pEpgEventInfo->pszEventName;
			ReserveData.startTime=pProgramInfo->StartTime;
			ReserveData.durationSecond=pProgramInfo->Duration;
			ReserveData.originalNetworkID=pProgramInfo->NetworkID;
			ReserveData.transportStreamID=pProgramInfo->TransportStreamID;
			ReserveData.serviceID=pProgramInfo->ServiceID;
			ReserveData.eventID=pProgramInfo->EventID;
			ReserveData.startTimeEpg=pProgramInfo->StartTime;
			ReserveData.recSetting.recMode=m_DefaultRecSettings.RecMode;
			ReserveData.recSetting.priority=m_DefaultRecSettings.Priority;
			ReserveData.recSetting.tuijyuuFlag=m_DefaultRecSettings.TuijyuuFlag;
			ReserveData.recSetting.pittariFlag=m_DefaultRecSettings.PittariFlag;

			vector<EPGDB_SERVICE_INFO> ServiceList;
			if (m_SendCtrlCmd.SendEnumService(&ServiceList)==CMD_SUCCESS) {
				for (size_t i=0;i<ServiceList.size();i++) {
					EPGDB_SERVICE_INFO &ServiceInfo=ServiceList[i];

					if (ServiceInfo.ONID==pProgramInfo->NetworkID
							&& ServiceInfo.TSID==pProgramInfo->TransportStreamID
							&& ServiceInfo.SID==pProgramInfo->ServiceID) {
						ReserveData.stationName=ServiceList[i].service_name;
						break;
					}
				}
			}
		}

		::SetCursor(hOldCursor);

		// 予約設定ダイアログ表示
		CReserveDialog ReserveDialog(m_EDCBSettings,m_SendCtrlCmd);
		if (ReserveDialog.Show(g_hinstDLL,m_hwndProgramGuide,&ReserveData,pEpgEventInfo)) {
			hOldCursor=::SetCursor(::LoadCursor(NULL,IDC_WAIT));

			if (fNew) {
				m_DefaultRecSettings.RecMode=ReserveData.recSetting.recMode;
				m_DefaultRecSettings.Priority=ReserveData.recSetting.priority;
				m_DefaultRecSettings.TuijyuuFlag=ReserveData.recSetting.tuijyuuFlag;
				m_DefaultRecSettings.PittariFlag=ReserveData.recSetting.pittariFlag;
			}

			// 予約の設定
			vector<RESERVE_DATA> ReserveList;
			DWORD Err;

			ReserveList.push_back(ReserveData);
			if (fNew) {
				Err=m_SendCtrlCmd.SendAddReserve(ReserveList);
			} else {
				Err=m_SendCtrlCmd.SendChgReserve(ReserveList);
			}
			if (Err==CMD_SUCCESS) {
				// 予約リスト再取得
				GetReserveList();
				::InvalidateRect(m_hwndProgramGuide,NULL,TRUE);
				m_ReserveListForm.NotifyReserveListChanged();
				::SetCursor(hOldCursor);
			} else {
				::SetCursor(hOldCursor);
				ShowCmdErrorMessage(m_hwndProgramGuide,TEXT("録画の予約を行えません。"),Err);
			}
		}

		m_pApp->FreeEpgEventInfo(pEpgEventInfo);
	} else if (Command==COMMAND_DELETE) {
		// 予約の削除

		if (m_CurReserveData.reserveID==0)
			return true;

		// 削除の確認
		TCHAR szText[1024];
		if (!m_CurReserveData.title.empty()) {
			FormatString(szText,_countof(szText),
						 TEXT("「%s」の予約を削除しますか?"),
						 m_CurReserveData.title.c_str());
		} else {
			::lstrcpy(szText,TEXT("予約を削除しますか?"));
			TVTest::EpgEventInfo *pEpgEventInfo=GetEventInfo(pProgramInfo);
			if (pEpgEventInfo!=NULL) {
				if (pEpgEventInfo->pszEventName!=NULL)
					FormatString(szText,_countof(szText),
								 TEXT("「%s」の予約を削除しますか?"),
								 pEpgEventInfo->pszEventName);
				m_pApp->FreeEpgEventInfo(pEpgEventInfo);
			}
		}

		if (::MessageBox(m_hwndProgramGuide,szText,TEXT("EpgTimerの予約削除"),
						 MB_OKCANCEL | MB_DEFBUTTON2 | MB_ICONQUESTION)==IDOK) {
			DeleteReserve(m_hwndProgramGuide,m_CurReserveData.reserveID);
		}
	} else {
		return false;
	}

	return true;
}


// コマンド受信
int CALLBACK CEDCBSupportPlugin::CtrlCmdCallback(void *pParam,CMD_STREAM *pCmdParam,CMD_STREAM *pResParam)
{
	pResParam->dataSize=0;
	pResParam->param=CMD_SUCCESS;

	switch (pCmdParam->param) {
	case CMD2_TIMER_GUI_UPDATE_RESERVE:
		// 予約一覧の情報が更新された
		{
			CEDCBSupportPlugin *pThis=static_cast<CEDCBSupportPlugin*>(pParam);

			if (pThis->m_pApp->IsPluginEnabled()
					&& pThis->m_hwndProgramGuide!=NULL) {
				pThis->GetReserveList();
				::InvalidateRect(pThis->m_hwndProgramGuide,NULL,TRUE);
				pThis->m_ReserveListForm.NotifyReserveListChanged();
			}
		}
		break;

	// この辺ちゃんと対応する必要があるのかよく分からない
	// (CMD_SUCCESS を返しておかないと以後コマンドが送られてこなくなるので注意)
	case CMD2_TIMER_GUI_SHOW_DLG:
	case CMD2_TIMER_GUI_UPDATE_EPGDATA:
	case CMD2_TIMER_GUI_VIEW_EXECUTE:
	case CMD2_TIMER_GUI_QUERY_SUSPEND:
	case CMD2_TIMER_GUI_QUERY_REBOOT:
	case CMD2_TIMER_GUI_SRV_STATUS_CHG:
		break;

	default:
		pResParam->param=CMD_NON_SUPPORT;
		break;
	}

	return 0;
}


// 初期化処理スレッド
unsigned int __stdcall CEDCBSupportPlugin::InitializeThread(void *pParam)
{
	CEDCBSupportPlugin *pThis=static_cast<CEDCBSupportPlugin*>(pParam);
	DWORD Err;

	if (!pThis->m_EDCBSettings.fUseNetwork) {
		bool fServiceInstalled=false;

		// EpgTimerSrv がサービスとしてインストールされていれば開始する
		SC_HANDLE hManager=::OpenSCManager(0,0,SC_MANAGER_CONNECT);
		if (hManager!=NULL) {
			SC_HANDLE hService=::OpenService(hManager,SERVICE_NAME,SERVICE_QUERY_STATUS);
			if (hService!=NULL) {
				fServiceInstalled=true;
				SERVICE_STATUS Status;
				if (::QueryServiceStatus(hService,&Status)
						&& Status.dwCurrentState==SERVICE_STOPPED) {
					::CloseServiceHandle(hService);
					hService=::OpenService(hManager,SERVICE_NAME,
										   SERVICE_START | SERVICE_QUERY_STATUS);
					if (hService!=NULL && ::StartService(hService,0,NULL)) {
						bool fStarted=false;
						for (int i=0;i<30 && ::QueryServiceStatus(hService,&Status);i++) {
							if (Status.dwCurrentState==SERVICE_RUNNING) {
								fStarted=true;
								break;
							}
							if (::WaitForSingleObject(pThis->m_hCancelEvent,500)==WAIT_OBJECT_0) {
								::CloseServiceHandle(hService);
								::CloseServiceHandle(hManager);
								return 1;
							}
						}
						if (fStarted)
							pThis->m_pApp->AddLog(L"EpgTimerサービスを開始しました。");
						else
							pThis->m_pApp->AddLog(L"EpgTimerサービスの開始を確認できません。");
					} else {
						pThis->m_pApp->AddLog(L"EpgTimerサービスを開始できません。");
					}
				}
				if (hService!=NULL)
					::CloseServiceHandle(hService);
			}
			::CloseServiceHandle(hManager);
		}

		// EpgTimerSrv.exe を起動する
		if (!fServiceInstalled) {
			HANDLE hMutex=::OpenMutex(MUTEX_ALL_ACCESS,FALSE,EPG_TIMER_BON_SRV_MUTEX);
			if (hMutex!=NULL) {
				::CloseHandle(hMutex);
			} else if (!pThis->m_EDCBSettings.EDCBDirectory.empty()
					&& ::PathIsDirectory(pThis->m_EDCBSettings.EDCBDirectory.c_str())
					&& pThis->m_EDCBSettings.EDCBDirectory.length()+_countof(EPG_TIMER_SERVICE_EXE)<MAX_PATH) {
				WCHAR szEpgTimerPath[MAX_PATH];
				STARTUPINFO si;
				PROCESS_INFORMATION pi;

				::PathCombine(szEpgTimerPath,
							  pThis->m_EDCBSettings.EDCBDirectory.c_str(),
							  EPG_TIMER_SERVICE_EXE);
				::ZeroMemory(&si,sizeof(si));
				si.cb=sizeof(si);
				::ZeroMemory(&pi,sizeof(pi));
				if (::CreateProcess(NULL,szEpgTimerPath,NULL,NULL,FALSE,0,NULL,NULL,&si,&pi)) {
					pThis->m_pApp->AddLog(EPG_TIMER_SERVICE_EXE L"を起動しました。");
					::CloseHandle(pi.hProcess);
					::CloseHandle(pi.hThread);
				} else {
					pThis->m_pApp->AddLog(EPG_TIMER_SERVICE_EXE L"を起動できません。");
				}
			}
		}

		// コマンドを受信するために登録
		Err=pThis->m_SendCtrlCmd.SendRegistGUI(::GetCurrentProcessId());
		if (Err==CMD_SUCCESS) {
			pThis->m_fGUIRegistered=true;
		} else {
			WCHAR szText[64];
			::wsprintfW(szText,L"GUI登録できません。(エラーコード %lu)",Err);
			pThis->m_pApp->AddLog(szText);
			LPCWSTR pszMessage=GetCmdErrorMessage(Err);
			if (pszMessage!=NULL)
				pThis->m_pApp->AddLog(pszMessage);
			return 1;
		}
	}

	// 予約の取得
	Err=pThis->GetReserveList();
	if (Err==CMD_SUCCESS) {
		if (pThis->m_hwndProgramGuide!=NULL)
			::InvalidateRect(pThis->m_hwndProgramGuide,NULL,TRUE);
	} else {
		WCHAR szText[64];
		::wsprintfW(szText,L"予約が取得できません。(エラーコード %lu)",Err);
		pThis->m_pApp->AddLog(szText);
		LPCWSTR pszMessage=GetCmdErrorMessage(Err);
		if (pszMessage!=NULL)
			pThis->m_pApp->AddLog(pszMessage);
	}

	return 0;
}


// プラグインの設定を行う
bool CEDCBSupportPlugin::PluginSettings(HWND hwndOwner)
{
	LoadSettings();

	if (::DialogBoxParam(g_hinstDLL,MAKEINTRESOURCE(IDD_OPTIONS),
						 hwndOwner,SettingsDlgProc,
						 reinterpret_cast<LPARAM>(this))!=IDOK)
		return false;

	SaveSettings();

	// 番組表が表示されていたら更新する
	if (m_pApp->IsPluginEnabled() && m_hwndProgramGuide!=NULL)
		::InvalidateRect(m_hwndProgramGuide,NULL,TRUE);

	ApplyColorScheme();

	return true;
}


// 色の選択ダイアログを表示する
static bool ChooseColorDialog(HWND hwndOwner,COLORREF *pColor)
{
	static COLORREF CustomColors[16] = {
		RGB(0xFF,0xFF,0xFF),RGB(0xFF,0xFF,0xFF),RGB(0xFF,0xFF,0xFF),
		RGB(0xFF,0xFF,0xFF),RGB(0xFF,0xFF,0xFF),RGB(0xFF,0xFF,0xFF),
		RGB(0xFF,0xFF,0xFF),RGB(0xFF,0xFF,0xFF),RGB(0xFF,0xFF,0xFF),
		RGB(0xFF,0xFF,0xFF),RGB(0xFF,0xFF,0xFF),RGB(0xFF,0xFF,0xFF),
		RGB(0xFF,0xFF,0xFF),RGB(0xFF,0xFF,0xFF),RGB(0xFF,0xFF,0xFF),
		RGB(0xFF,0xFF,0xFF)
	};
	static bool fFullOpen=true;
	CHOOSECOLOR cc;

	cc.lStructSize=sizeof(CHOOSECOLOR);
	cc.hwndOwner=hwndOwner;
	cc.hInstance=NULL;
	cc.rgbResult=*pColor;
	cc.lpCustColors=CustomColors;
	cc.Flags=CC_RGBINIT;
	if (fFullOpen)
		cc.Flags|=CC_FULLOPEN;
	if (!::ChooseColor(&cc))
		return false;
	*pColor=cc.rgbResult;
	fFullOpen=(cc.Flags&CC_FULLOPEN)!=0;
	return true;
}


// 設定ダイアログプロシージャ
INT_PTR CALLBACK CEDCBSupportPlugin::SettingsDlgProc(HWND hDlg,UINT uMsg,WPARAM wParam,LPARAM lParam)
{
	switch (uMsg) {
	case WM_INITDIALOG:
		{
			CEDCBSupportPlugin *pThis=reinterpret_cast<CEDCBSupportPlugin*>(lParam);

			::SetProp(hDlg,TEXT("This"),pThis);

			::CheckRadioButton(hDlg,IDC_OPTIONS_USELOCAL,IDC_OPTIONS_USENETWORK,
							   pThis->m_EDCBSettings.fUseNetwork?IDC_OPTIONS_USENETWORK:IDC_OPTIONS_USELOCAL);

			::SetDlgItemText(hDlg,IDC_OPTIONS_EDCBFOLDER,pThis->m_EDCBSettings.EDCBDirectory.c_str());
			::SendDlgItemMessage(hDlg,IDC_OPTIONS_EDCBFOLDER,EM_LIMITTEXT,MAX_PATH-1,0);

			::SetDlgItemText(hDlg,IDC_OPTIONS_NETWORKADDRESS,pThis->m_EDCBSettings.NetworkAddress.c_str());
			::SetDlgItemInt(hDlg,IDC_OPTIONS_NETWORKPORT,pThis->m_EDCBSettings.NetworkPort,FALSE);
			wstring temp;
			if (CCryptUtil::Decrypt(pThis->m_EDCBSettings.NetworkPassword, temp, CRYPTPROTECT_UI_FORBIDDEN | CRYPTPROTECT_AUDIT)) {
				::SetDlgItemText(hDlg, IDC_OPTIONS_NETWORKPASSWORD, temp.c_str());
			}

			EnableDlgItems(hDlg,IDC_OPTIONS_EDCBFOLDER_LABEL,IDC_OPTIONS_EDCBFOLDER_BROWSE,
						   !pThis->m_EDCBSettings.fUseNetwork);
			EnableDlgItems(hDlg,IDC_OPTIONS_NETWORKADDRESS_LABEL,IDC_OPTIONS_NETWORKPASSWORD,
						   pThis->m_EDCBSettings.fUseNetwork);

			::CheckDlgButton(hDlg,IDC_OPTIONS_COLOR_NORMAL_ENABLE,
							 pThis->m_ColorList[COLOR_NORMAL].fEnabled?BST_CHECKED:BST_UNCHECKED);
			::SetDlgItemInt(hDlg,IDC_OPTIONS_COLOR_NORMAL_CHOOSE,
							pThis->m_ColorList[COLOR_NORMAL].Color,FALSE);
			::CheckDlgButton(hDlg,IDC_OPTIONS_COLOR_DISABLED_ENABLE,
							 pThis->m_ColorList[COLOR_DISABLED].fEnabled?BST_CHECKED:BST_UNCHECKED);
			::SetDlgItemInt(hDlg,IDC_OPTIONS_COLOR_DISABLED_CHOOSE,
							pThis->m_ColorList[COLOR_DISABLED].Color,FALSE);
			::CheckDlgButton(hDlg,IDC_OPTIONS_COLOR_NOTUNER_ENABLE,
							 pThis->m_ColorList[COLOR_NOTUNER].fEnabled?BST_CHECKED:BST_UNCHECKED);
			::SetDlgItemInt(hDlg,IDC_OPTIONS_COLOR_NOTUNER_CHOOSE,
							pThis->m_ColorList[COLOR_NOTUNER].Color,FALSE);
			::CheckDlgButton(hDlg,IDC_OPTIONS_COLOR_CONFLICT_ENABLE,
							 pThis->m_ColorList[COLOR_CONFLICT].fEnabled?BST_CHECKED:BST_UNCHECKED);
			::SetDlgItemInt(hDlg,IDC_OPTIONS_COLOR_CONFLICT_CHOOSE,
							pThis->m_ColorList[COLOR_CONFLICT].Color,FALSE);
		}
		return TRUE;

	case WM_NOTIFY:
		switch (reinterpret_cast<LPNMHDR>(lParam)->code) {
		case NM_CUSTOMDRAW:
			{
				LPNMCUSTOMDRAW pnmcd=reinterpret_cast<LPNMCUSTOMDRAW>(lParam);

				// 色ボタンの描画
				if ((pnmcd->hdr.idFrom==IDC_OPTIONS_COLOR_NORMAL_CHOOSE
						|| pnmcd->hdr.idFrom==IDC_OPTIONS_COLOR_DISABLED_CHOOSE
						|| pnmcd->hdr.idFrom==IDC_OPTIONS_COLOR_NOTUNER_CHOOSE
						|| pnmcd->hdr.idFrom==IDC_OPTIONS_COLOR_CONFLICT_CHOOSE)
						&& pnmcd->dwDrawStage==CDDS_PREPAINT) {
					CEDCBSupportPlugin *pThis=static_cast<CEDCBSupportPlugin*>(::GetProp(hDlg,TEXT("This")));
					COLORREF Color=(COLORREF)::GetDlgItemInt(hDlg,(int)pnmcd->hdr.idFrom,NULL,FALSE);
					HDC hdc=pnmcd->hdc;
					RECT rc=pnmcd->rc;

					::InflateRect(&rc,-8,-6);
					HGDIOBJ hOldPen=::SelectObject(hdc,::GetStockObject(WHITE_PEN));
					HBRUSH hBrush=::CreateSolidBrush(Color);
					HGDIOBJ hOldBrush=::SelectObject(hdc,hBrush);
					::Rectangle(hdc,rc.left,rc.top,rc.right-1,rc.bottom-1);
					::SelectObject(hdc,::GetStockObject(BLACK_PEN));
					::SelectObject(hdc,::GetStockObject(NULL_BRUSH));
					::InflateRect(&rc,1,1);
					::Rectangle(hdc,rc.left,rc.top,rc.right-1,rc.bottom-1);
					::SelectObject(hdc,hOldBrush);
					::SelectObject(hdc,hOldPen);
					::DeleteObject(hBrush);

					::SetWindowLongPtr(hDlg,DWLP_MSGRESULT,CDRF_SKIPDEFAULT);
					return TRUE;
				}
			}
			break;
		}
		break;

	case WM_COMMAND:
		switch (LOWORD(wParam)) {
		case IDC_OPTIONS_USELOCAL:
		case IDC_OPTIONS_USENETWORK:
			{
				bool fUseNetwork=::IsDlgButtonChecked(hDlg,IDC_OPTIONS_USENETWORK)==BST_CHECKED;

				EnableDlgItems(hDlg,IDC_OPTIONS_EDCBFOLDER_LABEL,IDC_OPTIONS_EDCBFOLDER_BROWSE,
							   !fUseNetwork);
				EnableDlgItems(hDlg,IDC_OPTIONS_NETWORKADDRESS_LABEL,IDC_OPTIONS_NETWORKPASSWORD,
							   fUseNetwork);
			}
			return TRUE;

		case IDC_OPTIONS_EDCBFOLDER_BROWSE:
			{
				wstring Folder;

				GetDlgItemString(hDlg,IDC_OPTIONS_EDCBFOLDER,&Folder);
				if (BrowseFolderDialog(hDlg,&Folder,L"EpgDataCap_Bonのプログラムのあるフォルダを指定してください。"))
					::SetDlgItemTextW(hDlg,IDC_OPTIONS_EDCBFOLDER,Folder.c_str());
			}
			return TRUE;

		case IDC_OPTIONS_COLOR_NORMAL_CHOOSE:
		case IDC_OPTIONS_COLOR_DISABLED_CHOOSE:
		case IDC_OPTIONS_COLOR_NOTUNER_CHOOSE:
		case IDC_OPTIONS_COLOR_CONFLICT_CHOOSE:
			{
				COLORREF Color=(COLORREF)::GetDlgItemInt(hDlg,LOWORD(wParam),NULL,FALSE);

				if (ChooseColorDialog(hDlg,&Color)) {
					::SetDlgItemInt(hDlg,LOWORD(wParam),Color,FALSE);
					::InvalidateRect(::GetDlgItem(hDlg,LOWORD(wParam)),NULL,TRUE);
				}
			}
			return TRUE;

		case IDOK:
			{
				CEDCBSupportPlugin *pThis=static_cast<CEDCBSupportPlugin*>(::GetProp(hDlg,TEXT("This")));

				GetDlgItemString(hDlg,IDC_OPTIONS_EDCBFOLDER,&pThis->m_EDCBSettings.EDCBDirectory);
				bool fUseNetwork=::IsDlgButtonChecked(hDlg,IDC_OPTIONS_USENETWORK)==BST_CHECKED;
				wstring NetworkAddress;
				GetDlgItemString(hDlg,IDC_OPTIONS_NETWORKADDRESS,&NetworkAddress);
				WORD NetworkPort=(WORD)::GetDlgItemInt(hDlg,IDC_OPTIONS_NETWORKPORT,NULL,FALSE);
				wstring temp1;
				wstring temp2;
				CCryptUtil::Decrypt(pThis->m_EDCBSettings.NetworkPassword, temp1, CRYPTPROTECT_UI_FORBIDDEN | CRYPTPROTECT_AUDIT);
				GetDlgItemString(hDlg, IDC_OPTIONS_NETWORKPASSWORD, &temp2);

				if (fUseNetwork!=pThis->m_EDCBSettings.fUseNetwork
						|| NetworkAddress!=pThis->m_EDCBSettings.NetworkAddress
						|| NetworkPort!=pThis->m_EDCBSettings.NetworkPort
						|| temp1!=temp2) {
					pThis->ProgramGuideFinalize(false);
					pThis->m_EDCBSettings.fUseNetwork=fUseNetwork;
					pThis->m_EDCBSettings.NetworkAddress=NetworkAddress;
					pThis->m_EDCBSettings.NetworkPort=NetworkPort;
					CCryptUtil::Encrypt(temp2, pThis->m_EDCBSettings.NetworkPassword, CRYPTPROTECT_UI_FORBIDDEN | CRYPTPROTECT_AUDIT);
					if (pThis->m_pApp->IsPluginEnabled() && pThis->m_hwndProgramGuide!=NULL) {
						pThis->ProgramGuideInitialize(pThis->m_hwndProgramGuide,true);
						::InvalidateRect(pThis->m_hwndProgramGuide,NULL,TRUE);
					}
				}

				pThis->m_ColorList[COLOR_NORMAL].fEnabled=
					::IsDlgButtonChecked(hDlg,IDC_OPTIONS_COLOR_NORMAL_ENABLE)==BST_CHECKED;
				pThis->m_ColorList[COLOR_NORMAL].Color=
					(COLORREF)::GetDlgItemInt(hDlg,IDC_OPTIONS_COLOR_NORMAL_CHOOSE,NULL,FALSE);
				pThis->m_ColorList[COLOR_DISABLED].fEnabled=
					::IsDlgButtonChecked(hDlg,IDC_OPTIONS_COLOR_DISABLED_ENABLE)==BST_CHECKED;
				pThis->m_ColorList[COLOR_DISABLED].Color=
					(COLORREF)::GetDlgItemInt(hDlg,IDC_OPTIONS_COLOR_DISABLED_CHOOSE,NULL,FALSE);
				pThis->m_ColorList[COLOR_NOTUNER].fEnabled=
					::IsDlgButtonChecked(hDlg,IDC_OPTIONS_COLOR_NOTUNER_ENABLE)==BST_CHECKED;
				pThis->m_ColorList[COLOR_NOTUNER].Color=
					(COLORREF)::GetDlgItemInt(hDlg,IDC_OPTIONS_COLOR_NOTUNER_CHOOSE,NULL,FALSE);
				pThis->m_ColorList[COLOR_CONFLICT].fEnabled=
					::IsDlgButtonChecked(hDlg,IDC_OPTIONS_COLOR_CONFLICT_ENABLE)==BST_CHECKED;
				pThis->m_ColorList[COLOR_CONFLICT].Color=
					(COLORREF)::GetDlgItemInt(hDlg,IDC_OPTIONS_COLOR_CONFLICT_CHOOSE,NULL,FALSE);
			}
		case IDCANCEL:
			::EndDialog(hDlg,LOWORD(wParam));
			return TRUE;
		}
		return TRUE;

	case WM_NCDESTROY:
		::RemoveProp(hDlg,TEXT("This"));
		return TRUE;
	}
	return FALSE;
}


// 配色を適用する
void CEDCBSupportPlugin::ApplyColorScheme()
{
	m_ReserveListForm.SetReserveListColor(CReserveListView::COLOR_DISABLED,
										  m_ColorList[COLOR_DISABLED].Color);
	m_ReserveListForm.SetReserveListColor(CReserveListView::COLOR_CONFLICT,
										  m_ColorList[COLOR_CONFLICT].Color);
	m_ReserveListForm.SetReserveListColor(CReserveListView::COLOR_NOTUNER,
										  m_ColorList[COLOR_NOTUNER].Color);
	m_ReserveListForm.SetFileListColor(CFileListView::COLOR_ERROR,
									   m_ColorList[COLOR_NOTUNER].Color);
}


// コマンドのエラーのメッセージを表示
void CEDCBSupportPlugin::ShowCmdErrorMessage(HWND hwndOwner,LPCTSTR pszMessage,DWORD Err) const
{
	TCHAR szText[1024];

	LPCWSTR pszErrorMessage=GetCmdErrorMessage(Err);
	if (pszErrorMessage!=NULL)
		FormatString(szText,_countof(szText),TEXT("%s\r\n%s"),pszMessage,pszErrorMessage);
	else
		FormatString(szText,_countof(szText),TEXT("%s\r\n(エラーコード %lu)"),pszMessage,Err);
	::MessageBox(hwndOwner,szText,NULL,MB_OK | MB_ICONEXCLAMATION);
}


// 予約の変更
bool CEDCBSupportPlugin::ChangeReserve(HWND hwndOwner,const RESERVE_DATA &ReserveData)
{
	std::vector<RESERVE_DATA> List;

	List.push_back(ReserveData);
	return ChangeReserve(hwndOwner,List);
}


// 予約の変更
bool CEDCBSupportPlugin::ChangeReserve(HWND hwndOwner,std::vector<RESERVE_DATA> &ReserveList)
{
	HCURSOR hOldCursor=::SetCursor(::LoadCursor(NULL,IDC_WAIT));

	DWORD Err=m_SendCtrlCmd.SendChgReserve(ReserveList);

	if (Err!=CMD_SUCCESS) {
		::SetCursor(hOldCursor);
		ShowCmdErrorMessage(hwndOwner,TEXT("予約の変更ができません。"),Err);

		return false;
	}

	// 予約リスト再取得
	GetReserveList();
	if (m_hwndProgramGuide!=NULL)
		::InvalidateRect(m_hwndProgramGuide,NULL,TRUE);
	m_ReserveListForm.NotifyReserveListChanged();

	::SetCursor(hOldCursor);

	return true;
}


// 予約の削除
bool CEDCBSupportPlugin::DeleteReserve(HWND hwndOwner,DWORD ReserveID)
{
	std::vector<DWORD> List;

	List.push_back(ReserveID);
	return DeleteReserve(hwndOwner,List);
}


// 予約の削除
bool CEDCBSupportPlugin::DeleteReserve(HWND hwndOwner,std::vector<DWORD> &ReserveIDList)
{
	HCURSOR hOldCursor=::SetCursor(::LoadCursor(NULL,IDC_WAIT));

	DWORD Err=m_SendCtrlCmd.SendDelReserve(ReserveIDList);

	if (Err!=CMD_SUCCESS) {
		::SetCursor(hOldCursor);
		ShowCmdErrorMessage(hwndOwner,TEXT("予約を削除できません。"),Err);

		return false;
	}

	// 予約リスト再取得
	GetReserveList();
	if (m_hwndProgramGuide!=NULL)
		::InvalidateRect(m_hwndProgramGuide,NULL,TRUE);
	m_ReserveListForm.NotifyReserveListChanged();

	::SetCursor(hOldCursor);

	return true;
}


// 予約設定ダイアログ表示
bool CEDCBSupportPlugin::ShowReserveDialog(HWND hwndOwner,RESERVE_DATA *pReserveData)
{
	CReserveDialog ReserveDialog(m_EDCBSettings,m_SendCtrlCmd);
	TVTest::EpgEventInfo *pEventInfo=NULL;

	if (pReserveData->eventID!=0xFFFF) {
		TVTest::ProgramGuideProgramInfo ProgramInfo;
		ProgramInfo.NetworkID=pReserveData->originalNetworkID;
		ProgramInfo.TransportStreamID=pReserveData->transportStreamID;
		ProgramInfo.ServiceID=pReserveData->serviceID;
		ProgramInfo.EventID=pReserveData->eventID;
		pEventInfo=GetEventInfo(&ProgramInfo);
	}

	bool fResult=ReserveDialog.Show(g_hinstDLL,hwndOwner,pReserveData,pEventInfo);

	if (pEventInfo!=NULL)
		m_pApp->FreeEpgEventInfo(pEventInfo);

	return fResult;
}


// 録画済み情報の取得
bool CEDCBSupportPlugin::GetRecordedFileList(HWND hwndOwner,std::vector<REC_FILE_INFO> *pFileList)
{
	HCURSOR hOldCursor=::SetCursor(::LoadCursor(NULL,IDC_WAIT));

	DWORD Err=m_SendCtrlCmd.SendEnumRecInfo(pFileList);

	if (Err!=CMD_SUCCESS) {
		::SetCursor(hOldCursor);
		ShowCmdErrorMessage(hwndOwner,TEXT("録画済みファイルの情報を取得できません。"),Err);

		return false;
	}

	::SetCursor(hOldCursor);

	return true;
}


// 録画済み情報の削除
bool CEDCBSupportPlugin::DeleteRecordedFileInfo(HWND hwndOwner,DWORD ID)
{
	std::vector<DWORD> IDList;

	IDList.push_back(ID);
	return DeleteRecordedFileInfo(hwndOwner,IDList);
}


// 録画済み情報の削除
bool CEDCBSupportPlugin::DeleteRecordedFileInfo(HWND hwndOwner,std::vector<DWORD> &IDList)
{
	HCURSOR hOldCursor=::SetCursor(::LoadCursor(NULL,IDC_WAIT));

	DWORD Err=m_SendCtrlCmd.SendDelRecInfo(IDList);

	if (Err!=CMD_SUCCESS) {
		::SetCursor(hOldCursor);
		ShowCmdErrorMessage(hwndOwner,TEXT("録画済みファイルの情報を削除できません。"),Err);

		return false;
	}

	m_ReserveListForm.NotifyRecordedListChanged();

	::SetCursor(hOldCursor);

	return true;
}




CServiceReserveInfo::CServiceReserveInfo(WORD NetworkID,WORD TransportStreamID,WORD ServiceID)
	: m_NetworkID(NetworkID)
	, m_TransportStreamID(TransportStreamID)
	, m_ServiceID(ServiceID)
{
}


// プログラム予約を開始日時順にソートする
void CServiceReserveInfo::SortProgramReserveList()
{
	class CCompareReserveStartTime
	{
	public:
		bool operator()(const RESERVE_DATA *pReserve1,const RESERVE_DATA *pReserve2) const
		{
			return CompareSystemTime(pReserve1->startTime,pReserve2->startTime)<0;
		}
	};

	if (m_ProgramReserveList.size()>1) {
		std::sort(m_ProgramReserveList.begin(),
				  m_ProgramReserveList.end(),
				  CCompareReserveStartTime());
	}
}


// イベントIDに該当する予約を探す
RESERVE_DATA *CServiceReserveInfo::FindEvent(WORD EventID)
{
	std::map<WORD,RESERVE_DATA*>::iterator itr=m_EventReserveMap.find(EventID);

	if (itr==m_EventReserveMap.end())
		return NULL;
	return itr->second;
}


// 指定された時間の範囲にあるプログラム予約を探す
int CServiceReserveInfo::FindProgramReserve(const SYSTEMTIME &StartTime,DWORD Duration,
											std::vector<RESERVE_DATA*> *pReserveList)
{
	SYSTEMTIME EndTime;
	GetEndTime(StartTime,Duration,&EndTime);

	int Low,High,Key;
	Low=0;
	High=(int)m_ProgramReserveList.size()-1;
	while (Low<=High) {
		Key=(Low+High)/2;
		RESERVE_DATA *pKeyData=m_ProgramReserveList[Key];
		int Cmp=CompareSystemTime(pKeyData->startTime,StartTime);

		if (Cmp==0)
			break;
		if (Cmp<0) {
			SYSTEMTIME End;
			GetEndTime(pKeyData->startTime,pKeyData->durationSecond,&End);
			Cmp=CompareSystemTime(End,StartTime);
			if (Cmp>0)
				break;
			Low=Key+1;
		} else {
			Cmp=CompareSystemTime(pKeyData->startTime,EndTime);
			if (Cmp<0)
				break;
			High=Key-1;
		}
	}
	if (Low>High)
		return 0;

	for (;Key>0;Key--) {
		RESERVE_DATA *pKeyData=m_ProgramReserveList[Key-1];
		SYSTEMTIME End;
		GetEndTime(pKeyData->startTime,pKeyData->durationSecond,&End);
		if (CompareSystemTime(End,StartTime)<=0)
			break;
	}

	int ReserveCount=0;
	for (;Key<(int)m_ProgramReserveList.size();Key++) {
		RESERVE_DATA *pReserveData=m_ProgramReserveList[Key];

		if (CompareSystemTime(pReserveData->startTime,EndTime)>=0)
			break;
		pReserveList->push_back(pReserveData);
		ReserveCount++;
	}

	return ReserveCount;
}


}	// namespace EDCBSupport




// プラグインクラスのインスタンスを生成する
TVTest::CTVTestPlugin *CreatePluginClass()
{
	return new EDCBSupport::CEDCBSupportPlugin;
}
