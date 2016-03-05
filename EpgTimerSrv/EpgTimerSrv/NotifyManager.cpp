#include "StdAfx.h"
#include "NotifyManager.h"
#include <process.h>

#include "../../Common/CtrlCmdDef.h"
#include "../../Common/SendCtrlCmd.h"
#include "../../Common/BlockLock.h"
#include "../../Common/EpgTimerUtil.h"
#include "../../Common/StringUtil.h"

CNotifyManager::CNotifyManager(void)
{
	InitializeCriticalSection(&this->managerLock);

	this->notifyEvent = CreateEvent(NULL, FALSE, FALSE, NULL);
	this->notifyThread = NULL;
	this->notifyStopFlag = FALSE;
	this->srvStatus = 0;
	this->notifyCount = 1;
	this->notifyRemovePos = 0;
	this->hwndNotify = NULL;
}

CNotifyManager::~CNotifyManager(void)
{
	if( this->notifyThread != NULL ){
		this->notifyStopFlag = TRUE;
		::SetEvent(this->notifyEvent);
		// スレッド終了待ち
		if ( ::WaitForSingleObject(this->notifyThread, 20000) == WAIT_TIMEOUT ){
			::TerminateThread(this->notifyThread, 0xffffffff);
		}
		CloseHandle(this->notifyThread);
		this->notifyThread = NULL;
	}
	if( this->notifyEvent != NULL ){
		CloseHandle(this->notifyEvent);
		this->notifyEvent = NULL;
	}

	DeleteCriticalSection(&this->managerLock);
}

void CNotifyManager::RegistGUI(DWORD processID)
{
	CBlockLock lock(&this->managerLock);

	{
		this->registGUIMap.insert(pair<DWORD,DWORD>(processID,processID));
		SetNotifySrvStatus(0xFFFFFFFF);
	}
}

void CNotifyManager::RegistTCP(const REGIST_TCP_INFO& info)
{
	CBlockLock lock(&this->managerLock);

	{
		wstring key = L"";
		Format(key, L"%s:%d", info.ip.c_str(), info.port);

		this->registTCPMap.insert(pair<wstring,REGIST_TCP_INFO>(key,info));
		SetNotifySrvStatus(0xFFFFFFFF);
	}
}

void CNotifyManager::UnRegistGUI(DWORD processID)
{
	CBlockLock lock(&this->managerLock);

	{
		map<DWORD,DWORD>::iterator itr;
		itr = this->registGUIMap.find(processID);
		if( itr != this->registGUIMap.end() ){
			this->registGUIMap.erase(itr);
		}
	}
}

void CNotifyManager::UnRegistTCP(const REGIST_TCP_INFO& info)
{
	CBlockLock lock(&this->managerLock);

	{
		wstring key = L"";
		Format(key, L"%s:%d", info.ip.c_str(), info.port);

		map<wstring,REGIST_TCP_INFO>::iterator itr;
		itr = this->registTCPMap.find(key);
		if( itr != this->registTCPMap.end() ){
			this->registTCPMap.erase(itr);
		}
	}
}

BOOL CNotifyManager::IsRegistTCP(const REGIST_TCP_INFO& info) const
{
	CBlockLock lock(&this->managerLock);

	{
		wstring key = L"";
		Format(key, L"%s:%d", info.ip.c_str(), info.port);

		map<wstring,REGIST_TCP_INFO>::const_iterator itr;
		itr = this->registTCPMap.find(key);
		return itr != this->registTCPMap.end();
	}
}

void CNotifyManager::SetNotifyWindow(HWND hwnd, UINT msgID)
{
	CBlockLock lock(&this->managerLock);

	this->hwndNotify = hwnd;
	this->msgIDNotify = msgID;
	SetNotifySrvStatus(0xFFFFFFFF);
}

vector<NOTIFY_SRV_INFO> CNotifyManager::RemoveSentList()
{
	CBlockLock lock(&this->managerLock);

	vector<NOTIFY_SRV_INFO> list(this->notifySentList.begin() + this->notifyRemovePos, this->notifySentList.end());
	this->notifyRemovePos = this->notifySentList.size();
	return list;
}

BOOL CNotifyManager::GetNotify(NOTIFY_SRV_INFO* info, DWORD targetCount)
{
	CBlockLock lock(&this->managerLock);

	if( targetCount == 0 ){
		//現在のsrvStatusを返す
		NOTIFY_SRV_INFO status;
		status.notifyID = NOTIFY_UPDATE_SRV_STATUS;
		GetLocalTime(&status.time);
		status.param1 = this->srvStatus;
		//巡回カウンタは最後の通知と同値
		status.param3 = this->notifyCount;
		*info = status;
		return TRUE;
	}else if( this->notifySentList.empty() || targetCount - this->notifySentList.back().param3 < 0x80000000UL ){
		//存在するかどうかは即断できる
		return FALSE;
	}else{
		//巡回カウンタがtargetCountよりも大きくなる最初の通知を返す
		*info = *std::find_if(this->notifySentList.begin(), this->notifySentList.end(),
		                      [=](const NOTIFY_SRV_INFO& a) { return targetCount - a.param3 >= 0x80000000UL; });
		return TRUE;
	}
}

void CNotifyManager::GetRegistGUI(map<DWORD, DWORD>* registGUI) const
{
	CBlockLock lock(&this->managerLock);

	if( registGUI != NULL){
		*registGUI = registGUIMap;
	}
}

void CNotifyManager::GetRegistTCP(map<wstring, REGIST_TCP_INFO>* registTCP) const
{
	CBlockLock lock(&this->managerLock);

	if( registTCP != NULL){
		*registTCP = registTCPMap;
	}
}

void CNotifyManager::AddNotify(DWORD status)
{
	CBlockLock lock(&this->managerLock);

	{
		NOTIFY_SRV_INFO info;
		GetLocalTime(&info.time);
		info.notifyID = status;
		BOOL find = FALSE;
		for(size_t i=0; i<this->notifyList.size(); i++ ){
			if( this->notifyList[i].notifyID == status ){
				find = TRUE;
				break;
			}
		}
		//同じものがあるときは追加しない
		if( find == FALSE ){
			this->notifyList.push_back(info);
			_SendNotify();
		}
	}
}

void CNotifyManager::SetNotifySrvStatus(DWORD status)
{
	CBlockLock lock(&this->managerLock);

	if( status != this->srvStatus ){
		NOTIFY_SRV_INFO info;
		info.notifyID = NOTIFY_UPDATE_SRV_STATUS;
		GetLocalTime(&info.time);
		info.param1 = this->srvStatus = (status == 0xFFFFFFFF ? this->srvStatus : status);

		this->notifyList.push_back(info);
		_SendNotify();
	}
}

void CNotifyManager::AddNotifyMsg(DWORD notifyID, wstring msg)
{
	CBlockLock lock(&this->managerLock);

	{
		NOTIFY_SRV_INFO info;
		info.notifyID = notifyID;
		GetLocalTime(&info.time);
		info.param4 = msg;

		this->notifyList.push_back(info);
	}
	_SendNotify();
}

void CNotifyManager::_SendNotify()
{
	if( this->notifyThread == NULL ){
		this->notifyThread = (HANDLE)_beginthreadex(NULL, 0, SendNotifyThread_, this, 0, NULL);
	}
	SetEvent(this->notifyEvent);
}

UINT WINAPI CNotifyManager::SendNotifyThread_(LPVOID param)
{
	__try {
		CNotifyManager* sys = (CNotifyManager*)param;
		return sys->SendNotifyThread();
	}
	__except (FilterException(GetExceptionInformation())) { }
	return 0;
}

UINT CNotifyManager::SendNotifyThread()
{
	CSendCtrlCmd sendCtrl;
	map<DWORD,DWORD>::iterator itr;
	BOOL wait1Sec = FALSE;
	BOOL waitNotify = FALSE;
	DWORD waitNotifyTick;
	while(1){
		map<DWORD, DWORD> registGUI;
		map<wstring, REGIST_TCP_INFO> registTCP;
		NOTIFY_SRV_INFO notifyInfo;

		if( wait1Sec != FALSE ){
			wait1Sec = FALSE;
			Sleep(1000);
		}
		if( ::WaitForSingleObject(this->notifyEvent, INFINITE) != WAIT_OBJECT_0 || this->notifyStopFlag != FALSE ){
			//キャンセルされた
			break;
		}
		//現在の情報取得
		{
			CBlockLock lock(&this->managerLock);
			if( this->notifyList.empty() ){
				continue;
			}
			registGUI = this->registGUIMap;
			registTCP = this->registTCPMap;
			if( waitNotify != FALSE && GetTickCount() - waitNotifyTick < 5000 ){
				vector<NOTIFY_SRV_INFO>::const_iterator itrNotify;
				for( itrNotify = this->notifyList.begin(); itrNotify != this->notifyList.end(); itrNotify++ ){
					if( itrNotify->notifyID <= 100 ){
						break;
					}
				}
				if( itrNotify == this->notifyList.end() ){
					SetEvent(this->notifyEvent);
					wait1Sec = TRUE;
					continue;
				}
				//NotifyID<=100の通知は遅延させず先に送る
				notifyInfo = *itrNotify;
				this->notifyList.erase(itrNotify);
			}else{
				waitNotify = FALSE;
				notifyInfo = this->notifyList[0];
				this->notifyList.erase(this->notifyList.begin());
				//NotifyID>100の通知は遅延させる
				if( notifyInfo.notifyID > 100 ){
					waitNotify = TRUE;
					waitNotifyTick = GetTickCount();
				}
			}
			if( this->notifyList.empty() == false ){
				//次の通知がある
				SetEvent(this->notifyEvent);
			}
			//巡回カウンタをつける(0を避けるため奇数)
			this->notifyCount += 2;
			notifyInfo.param3 = this->notifyCount;
			//送信済みリストに追加してウィンドウメッセージで知らせる
			this->notifySentList.push_back(notifyInfo);
			if( this->notifySentList.size() > 100 ){
				this->notifySentList.erase(this->notifySentList.begin());
				if( this->notifyRemovePos != 0 ){
					this->notifyRemovePos--;
				}
			}
			if( this->hwndNotify != NULL ){
				PostMessage(this->hwndNotify, this->msgIDNotify, 0, 0);
			}
		}

		vector<DWORD> errID;
		for( itr = registGUI.begin(); itr != registGUI.end(); itr++){
			if( this->notifyStopFlag != FALSE ){
				//キャンセルされた
				break;
			}
			{
				sendCtrl.SetSendMode(FALSE);
				sendCtrl.SetPipeSetting(CMD2_GUI_CTRL_WAIT_CONNECT, CMD2_GUI_CTRL_PIPE, itr->first);
				sendCtrl.SetConnectTimeOut(10*1000);
				DWORD err = sendCtrl.SendGUINotifyInfo2(notifyInfo);
				if( err == CMD_NON_SUPPORT ){
					switch(notifyInfo.notifyID){
					case NOTIFY_UPDATE_EPGDATA:
						err = sendCtrl.SendGUIUpdateEpgData();
						break;
					case NOTIFY_UPDATE_RESERVE_INFO:
					case NOTIFY_UPDATE_REC_INFO:
					case NOTIFY_UPDATE_AUTOADD_EPG:
					case NOTIFY_UPDATE_AUTOADD_MANUAL:
						err = sendCtrl.SendGUIUpdateReserve();
						break;
					case NOTIFY_UPDATE_SRV_STATUS:
						err = sendCtrl.SendGUIStatusChg((WORD)notifyInfo.param1);
						break;
					default:
						break;
					}
				}
				if( err != CMD_SUCCESS && err != CMD_NON_SUPPORT){
					errID.push_back(itr->first);
				}
			}
		}

		map<wstring, REGIST_TCP_INFO>::iterator itrTCP;
		vector<wstring> errIP;
		for( itrTCP = registTCP.begin(); itrTCP != registTCP.end(); itrTCP++){
			if( this->notifyStopFlag != FALSE ){
				//キャンセルされた
				break;
			}

			sendCtrl.SetSendMode(TRUE);
			sendCtrl.SetNWSetting(itrTCP->second.ip, itrTCP->second.port, L"");
			sendCtrl.SetConnectTimeOut(10*1000);

			DWORD err = sendCtrl.SendGUINotifyInfo2(notifyInfo);
			if( err == CMD_NON_SUPPORT ){
				switch(notifyInfo.notifyID){
				case NOTIFY_UPDATE_EPGDATA:
					err = sendCtrl.SendGUIUpdateEpgData();
					break;
				case NOTIFY_UPDATE_RESERVE_INFO:
				case NOTIFY_UPDATE_REC_INFO:
				case NOTIFY_UPDATE_AUTOADD_EPG:
				case NOTIFY_UPDATE_AUTOADD_MANUAL:
					err = sendCtrl.SendGUIUpdateReserve();
					break;
				case NOTIFY_UPDATE_SRV_STATUS:
					err = sendCtrl.SendGUIStatusChg((WORD)notifyInfo.param1);
					break;
				default:
					break;
				}
			}
			if( err != CMD_SUCCESS && err != CMD_NON_SUPPORT){
				errIP.push_back(itrTCP->first);
			}
		}

		//送信できなかったもの削除
		CBlockLock lock(&this->managerLock);

		for( size_t i=0; i<errID.size(); i++ ){
			itr = this->registGUIMap.find(errID[i]);
			if( itr != this->registGUIMap.end() ){
				this->registGUIMap.erase(itr);
			}
		}
		for( size_t i=0; i<errIP.size(); i++ ){
			itrTCP = this->registTCPMap.find(errIP[i]);
			if( itrTCP != this->registTCPMap.end() ){
				_OutputDebugString(L"notifyErr %s:%d", itrTCP->second.ip.c_str(), itrTCP->second.port);
				this->registTCPMap.erase(itrTCP);
			}
		}
	}

	return 0;
}
