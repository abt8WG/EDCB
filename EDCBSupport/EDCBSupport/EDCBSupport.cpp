/*
	EDCB Support Plugin

	TVTest�̔ԑg�\��EpgDataCap_Bon�ƘA�g������v���O�C��
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


// �T�[�r�X���Ƃ̗\����
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

// �v���O�C���N���X
class CEDCBSupportPlugin : public TVTest::CTVTestPlugin, public CEDCBSupportCore
{
	// �F����
	enum {
		COLOR_NORMAL,		// �ʏ�̗\��
		COLOR_DISABLED,		// �����ɂȂ��Ă���\��
		COLOR_NOTUNER,		// �`���[�i�[������Ȃ��\��
		COLOR_CONFLICT,		// �ꕔ���Ԃ��Ă���\��
		NUM_COLORS
	};

	// �F�����̏��
	struct ColorInfo {
		LPCTSTR pszName;	// ���O
		bool fEnabled;		// �\���L��
		COLORREF Color;		// �F
	};

	// ���j���[�̃R�}���h
	enum {
		COMMAND_RESERVELIST,	// �\��ꗗ�\��
		COMMAND_RESERVE,		// �\��o�^/�ύX
		COMMAND_DELETE,			// �\��폜
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

	// �z�F�̐ݒ��������
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


// �v���O�C���̏���Ԃ�
bool CEDCBSupportPlugin::GetPluginInfo(TVTest::PluginInfo *pInfo)
{
	pInfo->Type           = TVTest::PLUGIN_TYPE_NORMAL;
	pInfo->Flags          = TVTest::PLUGIN_FLAG_HASSETTINGS;
	pInfo->pszPluginName  = L"EpgDataCap_Bon�A�g";
	pInfo->pszCopyright   = L"Copyright(c) HDUSTest�̒��̐l";
	pInfo->pszDescription = L"�ԑg�\��EpgDataCap_Bon�Ƃ̘A�g�@�\��ǉ����܂��B";
	return true;
}


// ����������
bool CEDCBSupportPlugin::Initialize()
{
	// �ԑg�\�̃C�x���g�̒ʒm��L���ɂ���
	m_pApp->EnableProgramGuideEvent(TVTest::PROGRAMGUIDE_EVENT_GENERAL);

	// �C�x���g�R�[���o�b�N�֐���o�^
	m_pApp->SetEventCallback(EventCallback,this);

	// �ԑg�\�̃R�}���h��o�^
	static const TVTest::ProgramGuideCommandInfo Command = {
		TVTest::PROGRAMGUIDE_COMMAND_TYPE_PROGRAM,
		0,
		COMMAND_RESERVE,
		L"Reserve",
		L"EpgTimer�ɗ\��o�^/�\��ύX",
	};
	m_pApp->RegisterProgramGuideCommand(&Command);

	return true;
}


// �I������
bool CEDCBSupportPlugin::Finalize()
{
	ProgramGuideFinalize(true);

	SaveSettings();

	return true;
}


// 16�i��������𐔒l�ɕϊ�
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

// �ݒ�̓ǂݍ���
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


// �ݒ�̕ۑ�
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


// �C�x���g�R�[���o�b�N�֐�
// �����C�x���g���N����ƌĂ΂��
LRESULT CALLBACK CEDCBSupportPlugin::EventCallback(UINT Event,LPARAM lParam1,LPARAM lParam2,void *pClientData)
{
	CEDCBSupportPlugin *pThis=static_cast<CEDCBSupportPlugin*>(pClientData);

	switch (Event) {
	case TVTest::EVENT_PLUGINENABLE:
		// �v���O�C���̗L����Ԃ��ω�����
		return pThis->EnablePlugin(lParam1!=0);

	case TVTest::EVENT_PLUGINSETTINGS:
		// �v���O�C���̐ݒ���s��
		return pThis->PluginSettings(reinterpret_cast<HWND>(lParam1));

	case TVTest::EVENT_PROGRAMGUIDE_INITIALIZE:
		// �ԑg�\�̏���������
		return pThis->ProgramGuideInitialize(reinterpret_cast<HWND>(lParam1),
											 pThis->m_pApp->IsPluginEnabled());

	case TVTest::EVENT_PROGRAMGUIDE_FINALIZE:
		// �ԑg�\�̏I������
		return pThis->ProgramGuideFinalize(true);

	case TVTest::EVENT_PROGRAMGUIDE_COMMAND:
		// �ԑg�\�̃R�}���h�̎��s
		return pThis->OnProgramGuideCommand(
			(UINT)lParam1,
			reinterpret_cast<const TVTest::ProgramGuideCommandParam*>(lParam2));

	case TVTest::EVENT_PROGRAMGUIDE_INITIALIZEMENU:
		// ���j���[�̏�����
		return pThis->InitializeMenu(
			reinterpret_cast<const TVTest::ProgramGuideInitializeMenuInfo *>(lParam1));

	case TVTest::EVENT_PROGRAMGUIDE_MENUSELECTED:
		// ���j���[���I�����ꂽ
		return pThis->OnMenuSelected((UINT)lParam1);

	case TVTest::EVENT_PROGRAMGUIDE_PROGRAM_DRAWBACKGROUND:
		// �ԑg�̔w�i��`��
		return pThis->DrawBackground(
			reinterpret_cast<const TVTest::ProgramGuideProgramInfo*>(lParam1),
			reinterpret_cast<const TVTest::ProgramGuideProgramDrawBackgroundInfo*>(lParam2));

	case TVTest::EVENT_PROGRAMGUIDE_PROGRAM_INITIALIZEMENU:
		// �ԑg�̃��j���[�̏�����
		return pThis->InitializeProgramMenu(
			reinterpret_cast<const TVTest::ProgramGuideProgramInfo*>(lParam1),
			reinterpret_cast<TVTest::ProgramGuideProgramInitializeMenuInfo*>(lParam2));

	case TVTest::EVENT_PROGRAMGUIDE_PROGRAM_MENUSELECTED:
		// �ԑg�̃��j���[���I�����ꂽ
		return pThis->OnProgramMenuSelected(
			reinterpret_cast<const TVTest::ProgramGuideProgramInfo*>(lParam1),(UINT)lParam2);
	}
	return 0;
}


// �v���O�C���̗L����Ԃ��ω�����
bool CEDCBSupportPlugin::EnablePlugin(bool fEnable)
{
	// �ԑg�\�̃C�x���g�̒ʒm�̗L��/������ݒ肷��
	m_pApp->EnableProgramGuideEvent(TVTest::PROGRAMGUIDE_EVENT_GENERAL |
									(fEnable?TVTest::PROGRAMGUIDE_EVENT_PROGRAM:0));

	// �������������I���������s��
	if (fEnable) {
		if (m_hwndProgramGuide!=NULL)
			ProgramGuideInitialize(m_hwndProgramGuide,true);
	} else {
		ProgramGuideFinalize(false);
	}

	// �ԑg�\���\������Ă���ꍇ�ĕ`�悳����
	if (m_hwndProgramGuide!=NULL)
		::InvalidateRect(m_hwndProgramGuide,NULL,TRUE);

	return true;
}


// �\��̎擾
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


// �C�x���g�̗\������擾����
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


// �ԑg�\�̏���������
bool CEDCBSupportPlugin::ProgramGuideInitialize(HWND hwnd,bool fEnable)
{
	// �E�B���h�E�n���h��������Ă���
	m_hwndProgramGuide=hwnd;

	if (fEnable) {
		// �ݒ�̓ǂݍ���
		LoadSettings();

		// �ڑ��ݒ�
		if (m_EDCBSettings.fUseNetwork)
			m_SendCtrlCmd.SetNWSetting(m_EDCBSettings.NetworkAddress,
									   m_EDCBSettings.NetworkPort,
									   m_EDCBSettings.NetworkPassword);
		m_SendCtrlCmd.SetSendMode(m_EDCBSettings.fUseNetwork);
		// �l�b�g���[�N���̃^�C���A�E�g���Ԃ͎��ۂɂ͎g���Ȃ�
		m_SendCtrlCmd.SetConnectTimeOut(m_EDCBSettings.fUseNetwork?10*1000:3*1000);

		// �R�}���h��M�T�[�o�[�J�n
		if (!m_EDCBSettings.fUseNetwork) {
			const DWORD ProcessID=::GetCurrentProcessId();
			WCHAR szEventName[256],szPipeName[256];
			::wsprintf(szEventName,L"%s%d",CMD2_GUI_CTRL_WAIT_CONNECT,(int)ProcessID);
			::wsprintf(szPipeName,L"%s%d",CMD2_GUI_CTRL_PIPE,(int)ProcessID);
			m_PipeServer.StartServer(szEventName,szPipeName,CtrlCmdCallback,this,
									 THREAD_PRIORITY_NORMAL/*,ProcessID*/);
		}

		// �����������X���b�h�J�n
		// (�ԑg�\�̕\����x�������Ȃ����߂ɁA�ʃX���b�h�ŏ������������s��)
		m_hCancelEvent=::CreateEvent(NULL,FALSE,FALSE,NULL);
		m_hInitializeThread=(HANDLE)::_beginthreadex(NULL,0,InitializeThread,this,0,NULL);
	}

	return true;
}


// �ԑg�\�̏I������
bool CEDCBSupportPlugin::ProgramGuideFinalize(bool fClose)
{
	m_ReserveListForm.Destroy();

	if (fClose)
		m_hwndProgramGuide=NULL;

	// �����������X���b�h�I��
	if (m_hInitializeThread!=NULL) {
		if (m_hCancelEvent!=NULL)
			::SetEvent(m_hCancelEvent);
		if (::WaitForSingleObject(m_hInitializeThread,10000)!=WAIT_OBJECT_0) {
			m_pApp->AddLog(L"�����������X���b�h�������I�������܂��B");
			::TerminateThread(m_hInitializeThread,-1);
		}
		::CloseHandle(m_hInitializeThread);
		m_hInitializeThread=NULL;
	}

	if (m_hCancelEvent!=NULL) {
		::CloseHandle(m_hCancelEvent);
		m_hCancelEvent=NULL;
	}

	// �o�^�̉���
	if (m_fGUIRegistered) {
		m_SendCtrlCmd.SendUnRegistGUI(::GetCurrentProcessId());
		m_fGUIRegistered=false;
	}
	m_PipeServer.StopServer();

	// �\�񃊃X�g�̉��
	m_ReserveListLock.Lock();
	vector<RESERVE_DATA>().swap(m_ReserveList);
	m_ServiceReserveMap.clear();
	m_ReserveListLock.Unlock();

	return true;
}


// �\��ꗗ�̕\��/��\��
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


// �ԑg�̔w�i��`��
bool CEDCBSupportPlugin::DrawBackground(const TVTest::ProgramGuideProgramInfo *pProgramInfo,
										const TVTest::ProgramGuideProgramDrawBackgroundInfo *pInfo)
{
	CBlockLock Lock(m_ReserveListLock);

	// �\����̎擾
	std::vector<RESERVE_DATA*> ReserveList;
	if (FindReserve(pProgramInfo,&ReserveList)==0)
		return false;

	// �v���O�����\��
	for (size_t i=0;i<ReserveList.size();i++) {
		if (ReserveList[i]->eventID==0xFFFF) {
			DrawReserveFrame(pProgramInfo,pInfo,ReserveList[i]);
		}
	}

	// �C�x���g�̗\��
	for (size_t i=0;i<ReserveList.size();i++) {
		if (ReserveList[i]->eventID!=0xFFFF) {
			DrawReserveFrame(pProgramInfo,pInfo,ReserveList[i]);
			break;
		}
	}

	return true;
}


// SYSTEMTIME ��b�P�ʂ̎����\���ɕϊ�
static LONGLONG SystemTimeToSeconds(const SYSTEMTIME &Time)
{
	FILETIME ft;

	::SystemTimeToFileTime(&Time,&ft);
	return (((LONGLONG)ft.dwHighDateTime<<32) | (LONGLONG)ft.dwLowDateTime)/10000000LL;
}

// �\��̘g�̈ʒu���擾
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


// �\��̘g��`��
bool CEDCBSupportPlugin::DrawReserveFrame(const TVTest::ProgramGuideProgramInfo *pProgramInfo,
										  const TVTest::ProgramGuideProgramDrawBackgroundInfo *pInfo,
										  const RESERVE_DATA *pReserveData) const
{
	// �F�̎擾
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

	// �g�̕`��
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


// �ʒu����\������擾����
RESERVE_DATA *CEDCBSupportPlugin::GetReserveFromPoint(const TVTest::ProgramGuideProgramInfo *pProgramInfo,
													  const RECT &ItemRect,const POINT &Point)
{
	// �\����̎擾
	std::vector<RESERVE_DATA*> ReserveList;
	if (FindReserve(pProgramInfo,&ReserveList)==0)
		return NULL;

	// �C�x���g�̗\��
	for (size_t i=0;i<ReserveList.size();i++) {
		if (ReserveList[i]->eventID!=0xFFFF)
			return ReserveList[i];
	}

	// �v���O�����\��
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


// �ԑg�\�̃R�}���h�̎��s
bool CEDCBSupportPlugin::OnProgramGuideCommand(UINT Command,
											   const TVTest::ProgramGuideCommandParam *pParam)
{
	if (Command==COMMAND_RESERVE) {
		// �N���b�N�ʒu�̗\����擾
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


// ���j���[�̏�����
int CEDCBSupportPlugin::InitializeMenu(const TVTest::ProgramGuideInitializeMenuInfo *pInfo)
{
	if (m_pApp->IsPluginEnabled()) {
		// ���j���[�ǉ�
		::AppendMenu(pInfo->hmenu,
					 MF_STRING | MF_ENABLED | (m_ReserveListForm.IsCreated()?MF_CHECKED:MF_UNCHECKED),
					 pInfo->Command+COMMAND_RESERVELIST,
					 TEXT("EpgTimer�̗\��ꗗ�\��"));

		// �g�p����R�}���h����Ԃ�
		return NUM_COMMANDS;
	}

	return 0;
}


// ���j���[���I�����ꂽ
bool CEDCBSupportPlugin::OnMenuSelected(UINT Command)
{
	if (Command==COMMAND_RESERVELIST) {
		// �\��ꗗ�\��
		ToggleShowReserveList();
	} else {
		return false;
	}
	return true;
}


// �ԑg�̃��j���[�̏�����
int CEDCBSupportPlugin::InitializeProgramMenu(const TVTest::ProgramGuideProgramInfo *pProgramInfo,
											  const TVTest::ProgramGuideProgramInitializeMenuInfo *pInfo)
{
	CBlockLock Lock(m_ReserveListLock);

	// �N���b�N�ʒu�̗\����擾
	RESERVE_DATA *pReserveData=GetReserveFromPoint(pProgramInfo,pInfo->ItemRect,pInfo->CursorPos);
	bool fReserved=pReserveData!=NULL;
	if (fReserved)
		m_CurReserveData=*pReserveData;
	else
		m_CurReserveData.reserveID=0;

	// ���j���[�ǉ�
	::AppendMenu(pInfo->hmenu,MF_STRING | MF_ENABLED,pInfo->Command+COMMAND_RESERVE,
				 fReserved?TEXT("EpgTimer�̗\��ύX"):TEXT("EpgTimer�ɗ\��o�^"));
	if (fReserved)
		::AppendMenu(pInfo->hmenu,MF_STRING | MF_ENABLED,pInfo->Command+COMMAND_DELETE,
					 TEXT("EpgTimer�̗\��폜"));
	::AppendMenu(pInfo->hmenu,
				 MF_STRING | MF_ENABLED | (m_ReserveListForm.IsCreated()?MF_CHECKED:MF_UNCHECKED),
				 pInfo->Command+COMMAND_RESERVELIST,
				 TEXT("EpgTimer�̗\��ꗗ�\��"));

	// �g�p����R�}���h����Ԃ�
	return NUM_COMMANDS;
}


// �ԑg�̃��j���[���I�����ꂽ
bool CEDCBSupportPlugin::OnProgramMenuSelected(const TVTest::ProgramGuideProgramInfo *pProgramInfo,UINT Command)
{
	if (Command==COMMAND_RESERVELIST) {
		// �\��ꗗ�\��
		ToggleShowReserveList();
	} else if (Command==COMMAND_RESERVE) {
		// �\��̓o�^/�ύX

		// �ԑg�̏����擾
		TVTest::EpgEventInfo *pEpgEventInfo=GetEventInfo(pProgramInfo);
		if (pEpgEventInfo==NULL)
			return true;

		HCURSOR hOldCursor=::SetCursor(::LoadCursor(NULL,IDC_WAIT));

		RESERVE_DATA ReserveData;
		bool fNew=m_CurReserveData.reserveID==0;
		if (!fNew) {
			// �����\��ύX
			ReserveData=m_CurReserveData;
		} else {
			// �V�K�\��ǉ�
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

		// �\��ݒ�_�C�A���O�\��
		CReserveDialog ReserveDialog(m_EDCBSettings,m_SendCtrlCmd);
		if (ReserveDialog.Show(g_hinstDLL,m_hwndProgramGuide,&ReserveData,pEpgEventInfo)) {
			hOldCursor=::SetCursor(::LoadCursor(NULL,IDC_WAIT));

			if (fNew) {
				m_DefaultRecSettings.RecMode=ReserveData.recSetting.recMode;
				m_DefaultRecSettings.Priority=ReserveData.recSetting.priority;
				m_DefaultRecSettings.TuijyuuFlag=ReserveData.recSetting.tuijyuuFlag;
				m_DefaultRecSettings.PittariFlag=ReserveData.recSetting.pittariFlag;
			}

			// �\��̐ݒ�
			vector<RESERVE_DATA> ReserveList;
			DWORD Err;

			ReserveList.push_back(ReserveData);
			if (fNew) {
				Err=m_SendCtrlCmd.SendAddReserve(ReserveList);
			} else {
				Err=m_SendCtrlCmd.SendChgReserve(ReserveList);
			}
			if (Err==CMD_SUCCESS) {
				// �\�񃊃X�g�Ď擾
				GetReserveList();
				::InvalidateRect(m_hwndProgramGuide,NULL,TRUE);
				m_ReserveListForm.NotifyReserveListChanged();
				::SetCursor(hOldCursor);
			} else {
				::SetCursor(hOldCursor);
				ShowCmdErrorMessage(m_hwndProgramGuide,TEXT("�^��̗\����s���܂���B"),Err);
			}
		}

		m_pApp->FreeEpgEventInfo(pEpgEventInfo);
	} else if (Command==COMMAND_DELETE) {
		// �\��̍폜

		if (m_CurReserveData.reserveID==0)
			return true;

		// �폜�̊m�F
		TCHAR szText[1024];
		if (!m_CurReserveData.title.empty()) {
			FormatString(szText,_countof(szText),
						 TEXT("�u%s�v�̗\����폜���܂���?"),
						 m_CurReserveData.title.c_str());
		} else {
			::lstrcpy(szText,TEXT("�\����폜���܂���?"));
			TVTest::EpgEventInfo *pEpgEventInfo=GetEventInfo(pProgramInfo);
			if (pEpgEventInfo!=NULL) {
				if (pEpgEventInfo->pszEventName!=NULL)
					FormatString(szText,_countof(szText),
								 TEXT("�u%s�v�̗\����폜���܂���?"),
								 pEpgEventInfo->pszEventName);
				m_pApp->FreeEpgEventInfo(pEpgEventInfo);
			}
		}

		if (::MessageBox(m_hwndProgramGuide,szText,TEXT("EpgTimer�̗\��폜"),
						 MB_OKCANCEL | MB_DEFBUTTON2 | MB_ICONQUESTION)==IDOK) {
			DeleteReserve(m_hwndProgramGuide,m_CurReserveData.reserveID);
		}
	} else {
		return false;
	}

	return true;
}


// �R�}���h��M
int CALLBACK CEDCBSupportPlugin::CtrlCmdCallback(void *pParam,CMD_STREAM *pCmdParam,CMD_STREAM *pResParam)
{
	pResParam->dataSize=0;
	pResParam->param=CMD_SUCCESS;

	switch (pCmdParam->param) {
	case CMD2_TIMER_GUI_UPDATE_RESERVE:
		// �\��ꗗ�̏�񂪍X�V���ꂽ
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

	// ���̕ӂ����ƑΉ�����K�v������̂��悭������Ȃ�
	// (CMD_SUCCESS ��Ԃ��Ă����Ȃ��ƈȌ�R�}���h�������Ă��Ȃ��Ȃ�̂Œ���)
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


// �����������X���b�h
unsigned int __stdcall CEDCBSupportPlugin::InitializeThread(void *pParam)
{
	CEDCBSupportPlugin *pThis=static_cast<CEDCBSupportPlugin*>(pParam);
	DWORD Err;

	if (!pThis->m_EDCBSettings.fUseNetwork) {
		bool fServiceInstalled=false;

		// EpgTimerSrv ���T�[�r�X�Ƃ��ăC���X�g�[������Ă���ΊJ�n����
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
							pThis->m_pApp->AddLog(L"EpgTimer�T�[�r�X���J�n���܂����B");
						else
							pThis->m_pApp->AddLog(L"EpgTimer�T�[�r�X�̊J�n���m�F�ł��܂���B");
					} else {
						pThis->m_pApp->AddLog(L"EpgTimer�T�[�r�X���J�n�ł��܂���B");
					}
				}
				if (hService!=NULL)
					::CloseServiceHandle(hService);
			}
			::CloseServiceHandle(hManager);
		}

		// EpgTimerSrv.exe ���N������
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
					pThis->m_pApp->AddLog(EPG_TIMER_SERVICE_EXE L"���N�����܂����B");
					::CloseHandle(pi.hProcess);
					::CloseHandle(pi.hThread);
				} else {
					pThis->m_pApp->AddLog(EPG_TIMER_SERVICE_EXE L"���N���ł��܂���B");
				}
			}
		}

		// �R�}���h����M���邽�߂ɓo�^
		Err=pThis->m_SendCtrlCmd.SendRegistGUI(::GetCurrentProcessId());
		if (Err==CMD_SUCCESS) {
			pThis->m_fGUIRegistered=true;
		} else {
			WCHAR szText[64];
			::wsprintfW(szText,L"GUI�o�^�ł��܂���B(�G���[�R�[�h %lu)",Err);
			pThis->m_pApp->AddLog(szText);
			LPCWSTR pszMessage=GetCmdErrorMessage(Err);
			if (pszMessage!=NULL)
				pThis->m_pApp->AddLog(pszMessage);
			return 1;
		}
	}

	// �\��̎擾
	Err=pThis->GetReserveList();
	if (Err==CMD_SUCCESS) {
		if (pThis->m_hwndProgramGuide!=NULL)
			::InvalidateRect(pThis->m_hwndProgramGuide,NULL,TRUE);
	} else {
		WCHAR szText[64];
		::wsprintfW(szText,L"�\�񂪎擾�ł��܂���B(�G���[�R�[�h %lu)",Err);
		pThis->m_pApp->AddLog(szText);
		LPCWSTR pszMessage=GetCmdErrorMessage(Err);
		if (pszMessage!=NULL)
			pThis->m_pApp->AddLog(pszMessage);
	}

	return 0;
}


// �v���O�C���̐ݒ���s��
bool CEDCBSupportPlugin::PluginSettings(HWND hwndOwner)
{
	LoadSettings();

	if (::DialogBoxParam(g_hinstDLL,MAKEINTRESOURCE(IDD_OPTIONS),
						 hwndOwner,SettingsDlgProc,
						 reinterpret_cast<LPARAM>(this))!=IDOK)
		return false;

	SaveSettings();

	// �ԑg�\���\������Ă�����X�V����
	if (m_pApp->IsPluginEnabled() && m_hwndProgramGuide!=NULL)
		::InvalidateRect(m_hwndProgramGuide,NULL,TRUE);

	ApplyColorScheme();

	return true;
}


// �F�̑I���_�C�A���O��\������
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


// �ݒ�_�C�A���O�v���V�[�W��
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

				// �F�{�^���̕`��
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
				if (BrowseFolderDialog(hDlg,&Folder,L"EpgDataCap_Bon�̃v���O�����̂���t�H���_���w�肵�Ă��������B"))
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


// �z�F��K�p����
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


// �R�}���h�̃G���[�̃��b�Z�[�W��\��
void CEDCBSupportPlugin::ShowCmdErrorMessage(HWND hwndOwner,LPCTSTR pszMessage,DWORD Err) const
{
	TCHAR szText[1024];

	LPCWSTR pszErrorMessage=GetCmdErrorMessage(Err);
	if (pszErrorMessage!=NULL)
		FormatString(szText,_countof(szText),TEXT("%s\r\n%s"),pszMessage,pszErrorMessage);
	else
		FormatString(szText,_countof(szText),TEXT("%s\r\n(�G���[�R�[�h %lu)"),pszMessage,Err);
	::MessageBox(hwndOwner,szText,NULL,MB_OK | MB_ICONEXCLAMATION);
}


// �\��̕ύX
bool CEDCBSupportPlugin::ChangeReserve(HWND hwndOwner,const RESERVE_DATA &ReserveData)
{
	std::vector<RESERVE_DATA> List;

	List.push_back(ReserveData);
	return ChangeReserve(hwndOwner,List);
}


// �\��̕ύX
bool CEDCBSupportPlugin::ChangeReserve(HWND hwndOwner,std::vector<RESERVE_DATA> &ReserveList)
{
	HCURSOR hOldCursor=::SetCursor(::LoadCursor(NULL,IDC_WAIT));

	DWORD Err=m_SendCtrlCmd.SendChgReserve(ReserveList);

	if (Err!=CMD_SUCCESS) {
		::SetCursor(hOldCursor);
		ShowCmdErrorMessage(hwndOwner,TEXT("�\��̕ύX���ł��܂���B"),Err);

		return false;
	}

	// �\�񃊃X�g�Ď擾
	GetReserveList();
	if (m_hwndProgramGuide!=NULL)
		::InvalidateRect(m_hwndProgramGuide,NULL,TRUE);
	m_ReserveListForm.NotifyReserveListChanged();

	::SetCursor(hOldCursor);

	return true;
}


// �\��̍폜
bool CEDCBSupportPlugin::DeleteReserve(HWND hwndOwner,DWORD ReserveID)
{
	std::vector<DWORD> List;

	List.push_back(ReserveID);
	return DeleteReserve(hwndOwner,List);
}


// �\��̍폜
bool CEDCBSupportPlugin::DeleteReserve(HWND hwndOwner,std::vector<DWORD> &ReserveIDList)
{
	HCURSOR hOldCursor=::SetCursor(::LoadCursor(NULL,IDC_WAIT));

	DWORD Err=m_SendCtrlCmd.SendDelReserve(ReserveIDList);

	if (Err!=CMD_SUCCESS) {
		::SetCursor(hOldCursor);
		ShowCmdErrorMessage(hwndOwner,TEXT("�\����폜�ł��܂���B"),Err);

		return false;
	}

	// �\�񃊃X�g�Ď擾
	GetReserveList();
	if (m_hwndProgramGuide!=NULL)
		::InvalidateRect(m_hwndProgramGuide,NULL,TRUE);
	m_ReserveListForm.NotifyReserveListChanged();

	::SetCursor(hOldCursor);

	return true;
}


// �\��ݒ�_�C�A���O�\��
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


// �^��ςݏ��̎擾
bool CEDCBSupportPlugin::GetRecordedFileList(HWND hwndOwner,std::vector<REC_FILE_INFO> *pFileList)
{
	HCURSOR hOldCursor=::SetCursor(::LoadCursor(NULL,IDC_WAIT));

	DWORD Err=m_SendCtrlCmd.SendEnumRecInfo(pFileList);

	if (Err!=CMD_SUCCESS) {
		::SetCursor(hOldCursor);
		ShowCmdErrorMessage(hwndOwner,TEXT("�^��ς݃t�@�C���̏����擾�ł��܂���B"),Err);

		return false;
	}

	::SetCursor(hOldCursor);

	return true;
}


// �^��ςݏ��̍폜
bool CEDCBSupportPlugin::DeleteRecordedFileInfo(HWND hwndOwner,DWORD ID)
{
	std::vector<DWORD> IDList;

	IDList.push_back(ID);
	return DeleteRecordedFileInfo(hwndOwner,IDList);
}


// �^��ςݏ��̍폜
bool CEDCBSupportPlugin::DeleteRecordedFileInfo(HWND hwndOwner,std::vector<DWORD> &IDList)
{
	HCURSOR hOldCursor=::SetCursor(::LoadCursor(NULL,IDC_WAIT));

	DWORD Err=m_SendCtrlCmd.SendDelRecInfo(IDList);

	if (Err!=CMD_SUCCESS) {
		::SetCursor(hOldCursor);
		ShowCmdErrorMessage(hwndOwner,TEXT("�^��ς݃t�@�C���̏����폜�ł��܂���B"),Err);

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


// �v���O�����\����J�n�������Ƀ\�[�g����
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


// �C�x���gID�ɊY������\���T��
RESERVE_DATA *CServiceReserveInfo::FindEvent(WORD EventID)
{
	std::map<WORD,RESERVE_DATA*>::iterator itr=m_EventReserveMap.find(EventID);

	if (itr==m_EventReserveMap.end())
		return NULL;
	return itr->second;
}


// �w�肳�ꂽ���Ԃ͈̔͂ɂ���v���O�����\���T��
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




// �v���O�C���N���X�̃C���X�^���X�𐶐�����
TVTest::CTVTestPlugin *CreatePluginClass()
{
	return new EDCBSupport::CEDCBSupportPlugin;
}
