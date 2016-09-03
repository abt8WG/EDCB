#include "StdAfx.h"
#include "NotifyManager.h"
#include <process.h>
#include <Objbase.h>

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
	this->guiFlag = FALSE;
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
	for( size_t i = 0; i < this->registGUIList.size(); CloseHandle(this->registGUIList[i++].second) );

	DeleteCriticalSection(&this->managerLock);
}

void CNotifyManager::RegistGUI(DWORD processID)
{
	CBlockLock lock(&this->managerLock);

	for( size_t i = 0; i < this->registGUIList.size(); i++ ){
		if( this->registGUIList[i].first == processID ){
			if( WaitForSingleObject(this->registGUIList[i].second, 0) == WAIT_TIMEOUT ){
				return;
			}
			UnRegistGUI(this->registGUIList[i].first);
			break;
		}
	}
	HANDLE hProcess = OpenProcess(SYNCHRONIZE, FALSE, processID);
	if( hProcess ){
		this->registGUIList.push_back(std::make_pair(processID, hProcess));
		SetNotifySrvStatus(0xFFFFFFFF);
	}
}

void CNotifyManager::RegistTCP(const REGIST_TCP_INFO& info)
{
	CBlockLock lock(&this->managerLock);

	UnRegistTCP(info);
	this->registTCPList.push_back(info);
	SetNotifySrvStatus(0xFFFFFFFF);
}

void CNotifyManager::UnRegistGUI(DWORD processID)
{
	CBlockLock lock(&this->managerLock);

	for( size_t i = 0; i < this->registGUIList.size(); i++ ){
		if( this->registGUIList[i].first == processID ){
			CloseHandle(this->registGUIList[i].second);
			this->registGUIList.erase(this->registGUIList.begin() + i);
			break;
		}
	}
}

void CNotifyManager::UnRegistTCP(const REGIST_TCP_INFO& info)
{
	CBlockLock lock(&this->managerLock);

	for( size_t i = 0; i < this->registTCPList.size(); i++ ){
		if( this->registTCPList[i].ip == info.ip && this->registTCPList[i].port == info.port ){
			this->registTCPList.erase(this->registTCPList.begin() + i);
			break;
		}
	}
}

BOOL CNotifyManager::IsRegistTCP(const REGIST_TCP_INFO& info) const
{
	CBlockLock lock(&this->managerLock);

	for( size_t i = 0; i < this->registTCPList.size(); i++ ){
		if( this->registTCPList[i].ip == info.ip && this->registTCPList[i].port == info.port ){
			return TRUE;
		}
	}

	return FALSE;
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

vector<DWORD> CNotifyManager::GetRegistGUI() const
{
	CBlockLock lock(&this->managerLock);

	vector<DWORD> list;
	for( size_t i = 0; i < this->registGUIList.size(); i++ ){
		if( WaitForSingleObject(this->registGUIList[i].second, 0) == WAIT_TIMEOUT ){
			list.push_back(this->registGUIList[i].first);
		}
	}
	return list;
}

vector<REGIST_TCP_INFO> CNotifyManager::GetRegistTCP() const
{
	CBlockLock lock(&this->managerLock);

	return this->registTCPList;
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
			SendNotify();
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
		SendNotify();
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
	SendNotify();
}

void CNotifyManager::SendNotify()
{
	if( this->notifyThread == NULL ){
		this->notifyThread = (HANDLE)_beginthreadex(NULL, 0, SendNotifyThread_, this, 0, NULL);
	}
	SetEvent(this->notifyEvent);
}

UINT WINAPI CNotifyManager::SendNotifyThread_(LPVOID param)
{
	CoInitialize(NULL);
	__try {
		CNotifyManager* sys = (CNotifyManager*)param;
		return sys->SendNotifyThread();
	}
	__except (FilterException(GetExceptionInformation())) { }
	CoUninitialize();
	return 0;
}

UINT CNotifyManager::SendNotifyThread()
{
	CSendCtrlCmd sendCtrl;
	BOOL wait1Sec = FALSE;
	BOOL waitNotify = FALSE;
	DWORD waitNotifyTick = 0;
	for(;;){
		vector<DWORD> registGUI;
		vector<REGIST_TCP_INFO> registTCP;
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
			registGUI = this->GetRegistGUI();
			for( size_t i = 0; i < this->registGUIList.size(); ){
				if( std::find(registGUI.begin(), registGUI.end(), this->registGUIList[i].first) == registGUI.end() ){
					//終了したGUIを削除
					this->UnRegistGUI(this->registGUIList[i].first);
				}else{
					i++;
				}
			}
			registTCP = this->GetRegistTCP();
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

		for( size_t i = 0; i < registGUI.size(); i++ ){
			if( this->notifyStopFlag != FALSE ){
				//キャンセルされた
				break;
			}
			{
				sendCtrl.SetSendMode(FALSE);
				sendCtrl.SetPipeSetting(CMD2_GUI_CTRL_WAIT_CONNECT, CMD2_GUI_CTRL_PIPE, registGUI[i]);
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
			}
		}

		for( size_t i = 0; i < registTCP.size(); i++ ){
			if( this->notifyStopFlag != FALSE ){
				//キャンセルされた
				break;
			}

			sendCtrl.SetSendMode(TRUE);
			sendCtrl.SetNWSetting(registTCP[i].ip, registTCP[i].port, L"");
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
				//送信できなかったもの削除
				_OutputDebugString(L"notifyErr %s:%d\r\n", registTCP[i].ip.c_str(), registTCP[i].port);
				this->UnRegistTCP(registTCP[i]);
			}
		}
	}

	return 0;
}
