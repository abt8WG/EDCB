#include "StdAfx.h"
#include "EpgTimerSrvMain.h"
#include "SyoboiCalUtil.h"
#include "UpnpSsdpServer.h"
#include "NetPathUtil.h"
#include "../../Common/PipeServer.h"
#include "../../Common/TCPServer.h"
#include "../../Common/SendCtrlCmd.h"
#include "../../Common/PathUtil.h"
#include "../../Common/TimeUtil.h"
#include "../../Common/BlockLock.h"
#include "../../Common/CryptUtil.h"
#include "resource.h"
#include <shellapi.h>
#include <tlhelp32.h>
#include <wincrypt.h>
#include <LM.h>
#include <set>
#pragma comment (lib, "netapi32.lib")

static const char UPNP_URN_DMS_1[] = "urn:schemas-upnp-org:device:MediaServer:1";
static const char UPNP_URN_CDS_1[] = "urn:schemas-upnp-org:service:ContentDirectory:1";
static const char UPNP_URN_CMS_1[] = "urn:schemas-upnp-org:service:ConnectionManager:1";
static const char UPNP_URN_AVT_1[] = "urn:schemas-upnp-org:service:AVTransport:1";

struct MAIN_WINDOW_CONTEXT {
	CEpgTimerSrvMain* const sys;
	const bool serviceFlag;
	const DWORD awayMode;
	const UINT msgTaskbarCreated;
	CPipeServer pipeServer;
	CTCPServer tcpServer;
	CHttpServer httpServer;
	CUpnpSsdpServer upnpServer;
	HANDLE resumeTimer;
	__int64 resumeTime;
	WORD shutdownModePending;
	DWORD shutdownPendingTick;
	HWND hDlgQueryShutdown;
	WORD queryShutdownMode;
	bool taskFlag;
	bool showBalloonTip;
	DWORD notifySrvStatus;
	__int64 notifyActiveTime;
	MAIN_WINDOW_CONTEXT(CEpgTimerSrvMain* sys_, bool serviceFlag_, DWORD awayMode_)
		: sys(sys_)
		, serviceFlag(serviceFlag_)
		, awayMode(awayMode_)
		, msgTaskbarCreated(RegisterWindowMessage(L"TaskbarCreated"))
		, resumeTimer(NULL)
		, shutdownModePending(0)
		, shutdownPendingTick(0)
		, hDlgQueryShutdown(NULL)
		, taskFlag(false)
		, showBalloonTip(false)
		, notifySrvStatus(0)
		, notifyActiveTime(LLONG_MAX) {}
};

CEpgTimerSrvMain::CEpgTimerSrvMain()
	: reserveManager(notifyManager, epgDB)
	, hwndMain(NULL)
	, nwtvUdp(false)
	, nwtvTcp(false)
{
	memset(this->notifyUpdateCount, 0, sizeof(this->notifyUpdateCount));
	InitializeCriticalSection(&this->settingLock);
}

CEpgTimerSrvMain::~CEpgTimerSrvMain()
{
	DeleteCriticalSection(&this->settingLock);
}

bool CEpgTimerSrvMain::Main(bool serviceFlag_)
{
	this->notifyManager.SetGUI(!serviceFlag_);
	this->residentFlag = serviceFlag_;

	DWORD awayMode;
	OSVERSIONINFOEX osvi;
	osvi.dwOSVersionInfoSize = sizeof(osvi);
	osvi.dwMajorVersion = 6;
	awayMode = VerifyVersionInfo(&osvi, VER_MAJORVERSION, VerSetConditionMask(0, VER_MAJORVERSION, VER_GREATER_EQUAL)) ? ES_AWAYMODE_REQUIRED : 0;

	wstring settingPath;
	GetSettingPath(settingPath);
	this->epgAutoAdd.ParseText((settingPath + L"\\" + EPG_AUTO_ADD_TEXT_NAME).c_str());
	this->manualAutoAdd.ParseText((settingPath + L"\\" + MANUAL_AUTO_ADD_TEXT_NAME).c_str());

	//非表示のメインウィンドウを作成
	WNDCLASSEX wc = {};
	wc.cbSize = sizeof(WNDCLASSEX);
	wc.lpfnWndProc = MainWndProc;
	wc.hInstance = GetModuleHandle(NULL);
	wc.lpszClassName = SERVICE_NAME;
	wc.hIcon = (HICON)LoadImage(NULL, IDI_INFORMATION, IMAGE_ICON, 0, 0, LR_SHARED);
	if( RegisterClassEx(&wc) == 0 ){
		return false;
	}
	MAIN_WINDOW_CONTEXT ctx(this, serviceFlag_, awayMode);
	if( CreateWindowEx(0, SERVICE_NAME, SERVICE_NAME, 0, 0, 0, 0, 0, NULL, NULL, GetModuleHandle(NULL), &ctx) == NULL ){
		return false;
	}
	this->notifyManager.SetNotifyWindow(this->hwndMain, WM_RECEIVE_NOTIFY);

	//メッセージループ
	MSG msg;
	while( GetMessage(&msg, NULL, 0, 0) > 0 ){
		if( ctx.hDlgQueryShutdown == NULL || IsDialogMessage(ctx.hDlgQueryShutdown, &msg) == FALSE ){
			TranslateMessage(&msg);
			DispatchMessage(&msg);
		}
	}
	return true;
}

LRESULT CALLBACK CEpgTimerSrvMain::MainWndProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam)
{
	enum {
		TIMER_RELOAD_EPG_CHK_PENDING = 1,
		TIMER_QUERY_SHUTDOWN_PENDING,
		TIMER_RETRY_ADD_TRAY,
		TIMER_SET_RESUME,
		TIMER_CHECK,
		TIMER_RESET_HTTP_SERVER,
	};

	MAIN_WINDOW_CONTEXT* ctx = (MAIN_WINDOW_CONTEXT*)GetWindowLongPtr(hwnd, GWLP_USERDATA);
	if( uMsg != WM_CREATE && ctx == NULL ){
		return DefWindowProc(hwnd, uMsg, wParam, lParam);
	}

	switch( uMsg ){
	case WM_CREATE:
		ctx = (MAIN_WINDOW_CONTEXT*)((LPCREATESTRUCT)lParam)->lpCreateParams;
		SetWindowLongPtr(hwnd, GWLP_USERDATA, (LONG_PTR)ctx);
		ctx->sys->hwndMain = hwnd;
		ctx->sys->reserveManager.Initialize();
		ctx->sys->ReloadSetting();
		ctx->sys->ReloadNetworkSetting();
		ctx->pipeServer.StartServer(CMD2_EPG_SRV_EVENT_WAIT_CONNECT, CMD2_EPG_SRV_PIPE, CtrlCmdPipeCallback, ctx->sys, ctx->serviceFlag);
		ctx->sys->epgDB.ReloadEpgData();
		SendMessage(hwnd, WM_RELOAD_EPG_CHK, 0, 0);
		SendMessage(hwnd, WM_TIMER, TIMER_SET_RESUME, 0);
		SetTimer(hwnd, TIMER_SET_RESUME, 30000, NULL);
		SetTimer(hwnd, TIMER_CHECK, 1000, NULL);
		OutputDebugString(L"*** Server initialized ***\r\n");
		return 0;
	case WM_DESTROY:
		if( ctx->resumeTimer ){
			CloseHandle(ctx->resumeTimer);
		}
		ctx->upnpServer.Stop();
		ctx->httpServer.StopServer();
		ctx->tcpServer.StopServer();
		ctx->pipeServer.StopServer();
		ctx->sys->reserveManager.Finalize();
		OutputDebugString(L"*** Server finalized ***\r\n");
		//タスクトレイから削除
		SendMessage(hwnd, WM_SHOW_TRAY, FALSE, FALSE);
		ctx->sys->hwndMain = NULL;
		SetWindowLongPtr(hwnd, GWLP_USERDATA, 0);
		PostQuitMessage(0);
		return 0;
	case WM_ENDSESSION:
		if( wParam ){
			DestroyWindow(hwnd);
		}
		return 0;
	case WM_RESET_SERVER:
		{
			//サーバリセット処理
			unsigned short tcpPort_;
			DWORD tcpResTo;
			wstring tcpAcl;
			wstring tcpPasssword;
			{
				CBlockLock lock(&ctx->sys->settingLock);
				tcpPort_ = ctx->sys->tcpPort;
				tcpResTo = ctx->sys->tcpResponseTimeoutSec * 1000;
				tcpAcl = ctx->sys->tcpAccessControlList;
				tcpPasssword = ctx->sys->tcpPassword;
			}
			if( tcpPort_ == 0 ){
				ctx->tcpServer.StopServer();
			}else{
				ctx->tcpServer.StartServer(tcpPort_, tcpResTo ? tcpResTo : MAXDWORD, tcpAcl.c_str(), tcpPasssword.c_str(), CtrlCmdTcpCallback, ctx->sys);
			}
			SetTimer(hwnd, TIMER_RESET_HTTP_SERVER, 200, NULL);
		}
		break;
	case WM_RELOAD_EPG_CHK:
		//EPGリロード完了のチェックを開始
		SetTimer(hwnd, TIMER_RELOAD_EPG_CHK_PENDING, 200, NULL);
		KillTimer(hwnd, TIMER_QUERY_SHUTDOWN_PENDING);
		ctx->shutdownPendingTick = GetTickCount();
		break;
	case WM_REQUEST_SHUTDOWN:
		//シャットダウン処理
		if( wParam == 0x01FF ){
			SetShutdown(4);
		}else if( ctx->sys->IsSuspendOK() ){
			if( LOBYTE(wParam) == 1 || LOBYTE(wParam) == 2 ){
				//ストリーミングを終了する
				ctx->sys->streamingManager.CloseAllFile();
				//スリープ抑止解除
				SetThreadExecutionState(ES_CONTINUOUS);
				//rebootFlag時は(指定+5分前)に復帰
				if( ctx->sys->SetResumeTimer(&ctx->resumeTimer, &ctx->resumeTime, ctx->sys->wakeMarginSec + (HIBYTE(wParam) != 0 ? 300 : 0)) ){
					SetShutdown(LOBYTE(wParam));
					if( HIBYTE(wParam) != 0 ){
						//再起動問い合わせ
						if( SendMessage(hwnd, WM_QUERY_SHUTDOWN, 0x0100, 0) == FALSE ){
							SetShutdown(4);
						}
					}
				}
			}else if( LOBYTE(wParam) == 3 ){
				SetShutdown(3);
			}
		}
		break;
	case WM_QUERY_SHUTDOWN:
		if( ctx->sys->notifyManager.IsGUI() ){
			//直接尋ねる
			if( ctx->hDlgQueryShutdown == NULL ){
				INITCOMMONCONTROLSEX icce;
				icce.dwSize = sizeof(icce);
				icce.dwICC = ICC_PROGRESS_CLASS;
				InitCommonControlsEx(&icce);
				ctx->queryShutdownMode = (WORD)wParam;
				CreateDialogParam(GetModuleHandle(NULL), MAKEINTRESOURCE(IDD_EPGTIMERSRV_DIALOG), hwnd, QueryShutdownDlgProc, (LPARAM)ctx);
			}
		}else if( ctx->sys->QueryShutdown(HIBYTE(wParam), LOBYTE(wParam)) == false ){
			//GUI経由で問い合わせ開始できなかった
			return FALSE;
		}
		return TRUE;
	case WM_RECEIVE_NOTIFY:
		//通知を受け取る
		{
			vector<NOTIFY_SRV_INFO> list(1);
			if( wParam ){
				//更新だけ
				list.back().notifyID = NOTIFY_UPDATE_SRV_STATUS;
				list.back().param1 = ctx->notifySrvStatus;
			}else{
				list = ctx->sys->notifyManager.RemoveSentList();
				ctx->tcpServer.NotifyUpdate();
			}
			for( vector<NOTIFY_SRV_INFO>::const_iterator itr = list.begin(); itr != list.end(); itr++ ){
				if( itr->notifyID == NOTIFY_UPDATE_SRV_STATUS ){
					ctx->notifySrvStatus = itr->param1;
					if( ctx->taskFlag ){
						NOTIFYICONDATA nid = {};
						nid.cbSize = NOTIFYICONDATA_V2_SIZE;
						nid.hWnd = hwnd;
						nid.uID = 1;
						int iconID = ctx->notifySrvStatus == 1 ? IDI_ICON_RED :
						             ctx->notifySrvStatus == 2 ? IDI_ICON_GREEN : IDI_ICON_BLUE;
						HRESULT (WINAPI* pfnLoadIconMetric)(HINSTANCE,PCWSTR,int,HICON*) =
							(HRESULT (WINAPI*)(HINSTANCE,PCWSTR,int,HICON*))GetProcAddress(GetModuleHandle(L"comctl32.dll"), "LoadIconMetric");
						if( pfnLoadIconMetric == NULL ||
						    pfnLoadIconMetric(GetModuleHandle(NULL), MAKEINTRESOURCE(iconID), LIM_SMALL, &nid.hIcon) != S_OK ){
							nid.hIcon = (HICON)LoadImage(GetModuleHandle(NULL), MAKEINTRESOURCE(iconID), IMAGE_ICON, 16, 16, 0);
						}
						if( ctx->notifyActiveTime != LLONG_MAX ){
							SYSTEMTIME st;
							ConvertSystemTime(ctx->notifyActiveTime + 30 * I64_1SEC, &st);
							swprintf_s(nid.szTip, L"次の予約・取得：%d/%d(%c) %d:%02d",
								st.wMonth, st.wDay, wstring(L"日月火水木金土").at(st.wDayOfWeek % 7), st.wHour, st.wMinute);
						}
						nid.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
						nid.uCallbackMessage = WM_TRAY_PUSHICON;
						if( Shell_NotifyIcon(NIM_MODIFY, &nid) == FALSE && Shell_NotifyIcon(NIM_ADD, &nid) == FALSE ){
							SetTimer(hwnd, TIMER_RETRY_ADD_TRAY, 5000, NULL);
						}
						if( nid.hIcon ){
							DestroyIcon(nid.hIcon);
						}
					}
				}else if( itr->notifyID < _countof(ctx->sys->notifyUpdateCount) ){
					//更新系の通知をカウント。書き込みがここだけかつDWORDなので排他はしない
					ctx->sys->notifyUpdateCount[itr->notifyID]++;
				}else{
					NOTIFYICONDATA nid = {};
					wcscpy_s(nid.szInfoTitle,
						itr->notifyID == NOTIFY_UPDATE_PRE_REC_START ? L"予約録画開始準備" :
						itr->notifyID == NOTIFY_UPDATE_REC_START ? L"録画開始" :
						itr->notifyID == NOTIFY_UPDATE_REC_END ? L"録画終了" :
						itr->notifyID == NOTIFY_UPDATE_REC_TUIJYU ? L"追従発生" :
						itr->notifyID == NOTIFY_UPDATE_CHG_TUIJYU ? L"番組変更" :
						itr->notifyID == NOTIFY_UPDATE_PRE_EPGCAP_START ? L"EPG取得" :
						itr->notifyID == NOTIFY_UPDATE_EPGCAP_START ? L"EPG取得" :
						itr->notifyID == NOTIFY_UPDATE_EPGCAP_END ? L"EPG取得" : L"");
					if( nid.szInfoTitle[0] ){
						wstring info = itr->notifyID == NOTIFY_UPDATE_EPGCAP_START ? wstring(L"開始") :
						               itr->notifyID == NOTIFY_UPDATE_EPGCAP_END ? wstring(L"終了") : itr->param4;
						if( ctx->sys->saveNotifyLog ){
							//通知情報ログ保存
							wstring logPath;
							GetModuleFolderPath(logPath);
							logPath += L"\\EpgTimerSrvNotifyLog.txt";
							HANDLE hFile = CreateFile(logPath.c_str(), FILE_APPEND_DATA, FILE_SHARE_READ, NULL, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
							if( hFile != INVALID_HANDLE_VALUE ){
								SYSTEMTIME st = itr->time;
								wstring log;
								Format(log, L"%d/%02d/%02d %02d:%02d:%02d.%03d [%s] %s",
									st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond, st.wMilliseconds, nid.szInfoTitle, info.c_str());
								Replace(log, L"\r\n", L"  ");
								string logA;
								WtoA(log + L"\r\n", logA);
								LARGE_INTEGER liPos = {};
								SetFilePointerEx(hFile, liPos, NULL, FILE_END);
								DWORD dwWritten;
								WriteFile(hFile, logA.c_str(), (DWORD)logA.size(), &dwWritten, NULL);
								CloseHandle(hFile);
							}
						}
						if( ctx->showBalloonTip ){
							//バルーンチップ表示
							nid.cbSize = NOTIFYICONDATA_V2_SIZE;
							nid.hWnd = hwnd;
							nid.uID = 1;
							nid.uFlags = NIF_INFO;
							nid.dwInfoFlags = NIIF_INFO;
							nid.uTimeout = 10000; //効果はない
							if( info.size() > 63 ){
								info.resize(62);
								info += L'…';
							}
							Shell_NotifyIcon(NIM_MODIFY, &nid);
							wcscpy_s(nid.szInfo, info.c_str());
							Shell_NotifyIcon(NIM_MODIFY, &nid);
						}
					}
				}
			}
		}
		break;
	case WM_TRAY_PUSHICON:
		//タスクトレイ関係
		switch( LOWORD(lParam) ){
		case WM_LBUTTONUP:
			{
				wstring moduleFolder;
				GetModuleFolderPath(moduleFolder);
				if( GetFileAttributes((moduleFolder + L"\\EpgTimer.lnk").c_str()) != INVALID_FILE_ATTRIBUTES ){
					//EpgTimer.lnk(ショートカット)を優先的に開く
					ShellExecute(NULL, L"open", (moduleFolder + L"\\EpgTimer.lnk").c_str(), NULL, NULL, SW_SHOWNORMAL);
				}else{
					//EpgTimer.exeがあれば起動
					PROCESS_INFORMATION pi;
					STARTUPINFO si = {};
					si.cb = sizeof(si);
					if( CreateProcess((moduleFolder + L"\\EpgTimer.exe").c_str(), NULL, NULL, NULL, FALSE, 0, NULL, NULL, &si, &pi) != FALSE ){
						CloseHandle(pi.hThread);
						CloseHandle(pi.hProcess);
					}
				}
			}
			break;
		case WM_RBUTTONUP:
			{
				HMENU hMenu = LoadMenu(GetModuleHandle(NULL), MAKEINTRESOURCE(IDR_MENU_TRAY));
				if( hMenu ){
					POINT point;
					GetCursorPos(&point);
					SetForegroundWindow(hwnd);
					TrackPopupMenu(GetSubMenu(hMenu, 0), 0, point.x, point.y, 0, hwnd, NULL);
					DestroyMenu(hMenu);
				}
			}
			break;
		}
		break;
	case WM_SHOW_TRAY:
		//タスクトレイに表示/非表示する
		if( ctx->taskFlag && wParam == FALSE ){
			NOTIFYICONDATA nid = {};
			nid.cbSize = NOTIFYICONDATA_V2_SIZE;
			nid.hWnd = hwnd;
			nid.uID = 1;
			Shell_NotifyIcon(NIM_DELETE, &nid);
		}
		ctx->taskFlag = wParam != FALSE;
		ctx->showBalloonTip = ctx->taskFlag && lParam;
		if( ctx->taskFlag ){
			SetTimer(hwnd, TIMER_RETRY_ADD_TRAY, 0, NULL);
		}
		return TRUE;
	case WM_TIMER:
		switch( wParam ){
		case TIMER_RELOAD_EPG_CHK_PENDING:
			if( GetTickCount() - ctx->shutdownPendingTick > 30000 ){
				//30秒以内にシャットダウン問い合わせできなければキャンセル
				if( ctx->shutdownModePending ){
					ctx->shutdownModePending = 0;
					OutputDebugString(L"Shutdown cancelled\r\n");
				}
			}
			if( ctx->sys->epgDB.IsLoadingData() == FALSE ){
				KillTimer(hwnd, TIMER_RELOAD_EPG_CHK_PENDING);
				if( ctx->shutdownModePending ){
					//このタイマはWM_TIMER以外でもKillTimer()するためメッセージキューに残った場合に対処するためシフト
					ctx->shutdownPendingTick -= 100000;
					SetTimer(hwnd, TIMER_QUERY_SHUTDOWN_PENDING, 200, NULL);
				}
				//リロード終わったので自動予約登録処理を行う
				ctx->sys->reserveManager.CheckTuijyu();
				bool addCountUpdated = false;
				bool addReserve = false;
				{
					TIME_MEASURE();

					CBlockLock lock(&ctx->sys->settingLock);
					for( map<DWORD, EPG_AUTO_ADD_DATA>::const_iterator itr = ctx->sys->epgAutoAdd.GetMap().begin(); itr != ctx->sys->epgAutoAdd.GetMap().end(); itr++ ){
						DWORD addCount = itr->second.addCount;
						_OutputDebugString(L"キーワード予約登録処理:%03d %s\r\n", itr->second.dataID, itr->second.searchInfo.andKey.c_str());  
						addReserve |= ctx->sys->AutoAddReserveEPG(itr->second, true);
						if( addCount != itr->second.addCount ){
							addCountUpdated = true;
						}
					}
					for( map<DWORD, MANUAL_AUTO_ADD_DATA>::const_iterator itr = ctx->sys->manualAutoAdd.GetMap().begin(); itr != ctx->sys->manualAutoAdd.GetMap().end(); itr++ ){
						_OutputDebugString(L"プログラム予約登録処理:%03d %s\r\n", itr->second.dataID, itr->second.title.c_str());
						addReserve |= ctx->sys->AutoAddReserveProgram(itr->second, true);
					}
					_CrtMemState s;
					_CrtMemCheckpoint(&s);
					_CrtMemDumpStatistics(&s);
				}
				if( addCountUpdated ){
					//予約登録数の変化を通知する
					ctx->sys->notifyManager.AddNotify(NOTIFY_UPDATE_AUTOADD_EPG);
				}
				if( addReserve ){
					//予約情報の変化を通知する
					ctx->sys->reserveManager.AddNotifyAndPostBat(NOTIFY_UPDATE_RESERVE_INFO);
				}
				ctx->sys->reserveManager.AddNotifyAndPostBat(NOTIFY_UPDATE_EPGDATA);
				ctx->sys->reserveManager.AddNotifyAndPostBat(NOTIFY_UPDATE_RESERVE_INFO);

				if( ctx->sys->useSyoboi ){
					//しょぼいカレンダー対応
					CSyoboiCalUtil syoboi;
					vector<RESERVE_DATA> reserveList = ctx->sys->reserveManager.GetReserveDataAll();
					vector<TUNER_RESERVE_INFO> tunerList = ctx->sys->reserveManager.GetTunerReserveAll();
					syoboi.SendReserve(&reserveList, &tunerList);
				}
			}
			break;
		case TIMER_QUERY_SHUTDOWN_PENDING:
			if( GetTickCount() - ctx->shutdownPendingTick >= 100000 ){
				if( GetTickCount() - ctx->shutdownPendingTick - 100000 > 30000 ){
					//30秒以内にシャットダウン問い合わせできなければキャンセル
					KillTimer(hwnd, TIMER_QUERY_SHUTDOWN_PENDING);
					if( ctx->shutdownModePending ){
						ctx->shutdownModePending = 0;
						OutputDebugString(L"Shutdown cancelled\r\n");
					}
				}else if( ctx->shutdownModePending && ctx->sys->IsSuspendOK() ){
					KillTimer(hwnd, TIMER_QUERY_SHUTDOWN_PENDING);
					if( ctx->sys->IsUserWorking() == false &&
					    1 <= LOBYTE(ctx->shutdownModePending) && LOBYTE(ctx->shutdownModePending) <= 3 ){
						//シャットダウン問い合わせ
						if( SendMessage(hwnd, WM_QUERY_SHUTDOWN, ctx->shutdownModePending, 0) == FALSE ){
							SendMessage(hwnd, WM_REQUEST_SHUTDOWN, ctx->shutdownModePending, 0);
						}
					}
					ctx->shutdownModePending = 0;
				}
			}
			break;
		case TIMER_RETRY_ADD_TRAY:
			KillTimer(hwnd, TIMER_RETRY_ADD_TRAY);
			SendMessage(hwnd, WM_RECEIVE_NOTIFY, TRUE, 0);
			break;
		case TIMER_SET_RESUME:
			{
				//復帰タイマ更新(powercfg /waketimersでデバッグ可能)
				ctx->sys->SetResumeTimer(&ctx->resumeTimer, &ctx->resumeTime, ctx->sys->wakeMarginSec);
				//スリープ抑止
				EXECUTION_STATE esFlags = ctx->shutdownModePending == 0 && ctx->sys->IsSuspendOK() ? ES_CONTINUOUS : ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ctx->awayMode;
				if( SetThreadExecutionState(esFlags) != esFlags ){
					_OutputDebugString(L"SetThreadExecutionState(0x%08x)\r\n", (DWORD)esFlags);
				}
				//チップヘルプの更新が必要かチェック
				__int64 activeTime = ctx->sys->reserveManager.GetSleepReturnTime(GetNowI64Time());
				if( activeTime != ctx->notifyActiveTime ){
					ctx->notifyActiveTime = activeTime;
					SetTimer(hwnd, TIMER_RETRY_ADD_TRAY, 0, NULL);
				}
			}
			break;
		case TIMER_CHECK:
			{
				DWORD ret = ctx->sys->reserveManager.Check();
				switch( HIWORD(ret) ){
				case CReserveManager::CHECK_EPGCAP_END:
					if( ctx->sys->epgDB.ReloadEpgData() != FALSE ){
						//EPGリロード完了後にデフォルトのシャットダウン動作を試みる
						SendMessage(hwnd, WM_RELOAD_EPG_CHK, 0, 0);
						ctx->shutdownModePending = ctx->sys->defShutdownMode;
					}
					SendMessage(hwnd, WM_TIMER, TIMER_SET_RESUME, 0);
					break;
				case CReserveManager::CHECK_NEED_SHUTDOWN:
					if( ctx->sys->epgDB.ReloadEpgData() != FALSE ){
						//EPGリロード完了後に要求されたシャットダウン動作を試みる
						SendMessage(hwnd, WM_RELOAD_EPG_CHK, 0, 0);
						ctx->shutdownModePending = LOWORD(ret);
						if( LOBYTE(ctx->shutdownModePending) == 0 ){
							ctx->shutdownModePending = ctx->sys->defShutdownMode;
						}
					}
					SendMessage(hwnd, WM_TIMER, TIMER_SET_RESUME, 0);
					break;
				case CReserveManager::CHECK_RESERVE_MODIFIED:
					SendMessage(hwnd, WM_TIMER, TIMER_SET_RESUME, 0);
					break;
				}
			}
			break;
		case TIMER_RESET_HTTP_SERVER:
			ctx->upnpServer.Stop();
			if( ctx->httpServer.StopServer(true) ){
				KillTimer(hwnd, TIMER_RESET_HTTP_SERVER);
				CHttpServer::SERVER_OPTIONS op;
				bool enableSsdpServer_;
				{
					CBlockLock lock(&ctx->sys->settingLock);
					op = ctx->sys->httpOptions;
					enableSsdpServer_ = ctx->sys->enableSsdpServer;
				}
				if( op.ports.empty() == false && ctx->sys->httpServerRandom.empty() ){
					HCRYPTPROV prov;
					if( CryptAcquireContext(&prov, NULL, NULL, PROV_RSA_FULL, CRYPT_VERIFYCONTEXT) ){
						unsigned __int64 r[4] = {};
						if( CryptGenRandom(prov, sizeof(r), (BYTE*)r) ){
							Format(ctx->sys->httpServerRandom, "%016I64x%016I64x%016I64x%016I64x", r[0], r[1], r[2], r[3]);
						}
						CryptReleaseContext(prov, 0);
					}
				}
				if( op.ports.empty() == false &&
				    ctx->sys->httpServerRandom.empty() == false &&
				    ctx->httpServer.StartServer(op, InitLuaCallback, ctx->sys) &&
				    enableSsdpServer_ ){
					//"ddd.xml"の先頭から2KB以内に"<UDN>uuid:{UUID}</UDN>"が必要
					char dddBuf[2048] = {};
					HANDLE hFile = CreateFile((op.rootPath + L"\\dlna\\dms\\ddd.xml").c_str(),
					                          GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
					if( hFile != INVALID_HANDLE_VALUE ){
						DWORD dwRead;
						ReadFile(hFile, dddBuf, sizeof(dddBuf) - 1, &dwRead, NULL);
						CloseHandle(hFile);
					}
					string dddStr = dddBuf;
					size_t udnFrom = dddStr.find("<UDN>uuid:");
					if( udnFrom != string::npos && dddStr.size() > udnFrom + 10 + 36 && dddStr.compare(udnFrom + 10 + 36, 6, "</UDN>") == 0 ){
						string notifyUuid(dddStr, udnFrom + 5, 41);
						//最後にみつかった':'より後ろか先頭を_wtoiした結果を通知ポートとする
						unsigned short notifyPort = (unsigned short)_wtoi(op.ports.c_str() +
							(op.ports.find_last_of(':') == wstring::npos ? 0 : op.ports.find_last_of(':') + 1));
						//UPnPのUDP(Port1900)部分を担当するサーバ
						LPCSTR targetArray[] = { "upnp:rootdevice", UPNP_URN_DMS_1, UPNP_URN_CDS_1, UPNP_URN_CMS_1, UPNP_URN_AVT_1 };
						vector<CUpnpSsdpServer::SSDP_TARGET_INFO> targetList(2 + _countof(targetArray));
						targetList[0].target = notifyUuid;
						Format(targetList[0].location, "http://$HOST$:%d/dlna/dms/ddd.xml", notifyPort);
						targetList[0].usn = targetList[0].target;
						targetList[0].notifyFlag = true;
						targetList[1].target = "ssdp:all";
						targetList[1].location = targetList[0].location;
						targetList[1].usn = notifyUuid + "::" + "upnp:rootdevice";
						targetList[1].notifyFlag = false;
						for( size_t i = 2; i < targetList.size(); i++ ){
							targetList[i].target = targetArray[i - 2];
							targetList[i].location = targetList[0].location;
							targetList[i].usn = notifyUuid + "::" + targetList[i].target;
							targetList[i].notifyFlag = true;
						}
						ctx->upnpServer.Start(targetList);
					}else{
						OutputDebugString(L"Invalid ddd.xml\r\n");
					}
				}
			}
			break;
		}
		break;
	case WM_COMMAND:
		switch( LOWORD(wParam) ){
		case IDC_BUTTON_S3:
		case IDC_BUTTON_S4:
			if( ctx->sys->IsSuspendOK() ){
				PostMessage(hwnd, WM_REQUEST_SHUTDOWN, MAKEWORD(LOWORD(wParam) == IDC_BUTTON_S3 ? 1 : 2, HIBYTE(ctx->sys->defShutdownMode)), 0);
			}else{
				MessageBox(hwnd, L"移行できる状態ではありません。\r\n（もうすぐ予約が始まる。または抑制条件のexeが起動している。など）", NULL, MB_ICONERROR);
			}
			break;
		case IDC_BUTTON_END:
			if( MessageBox(hwnd, SERVICE_NAME L" を終了します。", L"確認", MB_OKCANCEL | MB_ICONINFORMATION) == IDOK ){
				SendMessage(hwnd, WM_CLOSE, 0, 0);
			}
			break;
		}
		break;
	default:
		if( uMsg == ctx->msgTaskbarCreated ){
			//シェルの再起動時
			SetTimer(hwnd, TIMER_RETRY_ADD_TRAY, 0, NULL);
		}
		break;
	}
	return DefWindowProc(hwnd, uMsg, wParam, lParam);
}

INT_PTR CALLBACK CEpgTimerSrvMain::QueryShutdownDlgProc(HWND hDlg, UINT uMsg, WPARAM wParam, LPARAM lParam)
{
	MAIN_WINDOW_CONTEXT* ctx = (MAIN_WINDOW_CONTEXT*)GetWindowLongPtr(hDlg, GWLP_USERDATA);

	switch( uMsg ){
	case WM_INITDIALOG:
		ctx = (MAIN_WINDOW_CONTEXT*)lParam;
		SetWindowLongPtr(hDlg, GWLP_USERDATA, (LONG_PTR)ctx);
		ctx->hDlgQueryShutdown = hDlg;
		SetDlgItemText(hDlg, IDC_STATIC_SHUTDOWN,
			LOBYTE(ctx->queryShutdownMode) == 1 ? L"スタンバイに移行します。" :
			LOBYTE(ctx->queryShutdownMode) == 2 ? L"休止に移行します。" :
			LOBYTE(ctx->queryShutdownMode) == 3 ? L"シャットダウンします。" : L"再起動します。");
		SetTimer(hDlg, 1, 1000, NULL);
		SendDlgItemMessage(hDlg, IDC_PROGRESS_SHUTDOWN, PBM_SETRANGE, 0, MAKELONG(0, LOBYTE(ctx->queryShutdownMode) == 0 ? 30 : 15));
		SendDlgItemMessage(hDlg, IDC_PROGRESS_SHUTDOWN, PBM_SETPOS, LOBYTE(ctx->queryShutdownMode) == 0 ? 30 : 15, 0);
		return TRUE;
	case WM_DESTROY:
		ctx->hDlgQueryShutdown = NULL;
		break;
	case WM_TIMER:
		if( SendDlgItemMessage(hDlg, IDC_PROGRESS_SHUTDOWN, PBM_SETPOS,
		    SendDlgItemMessage(hDlg, IDC_PROGRESS_SHUTDOWN, PBM_GETPOS, 0, 0) - 1, 0) <= 1 ){
			SendMessage(hDlg, WM_COMMAND, MAKEWPARAM(IDOK, BN_CLICKED), (LPARAM)hDlg);
		}
		break;
	case WM_COMMAND:
		switch( LOWORD(wParam) ){
		case IDOK:
			if( LOBYTE(ctx->queryShutdownMode) == 0 ){
				//再起動
				PostMessage(ctx->sys->hwndMain, WM_REQUEST_SHUTDOWN, 0x01FF, 0);
			}else if( ctx->sys->IsSuspendOK() ){
				//スタンバイ休止または電源断
				PostMessage(ctx->sys->hwndMain, WM_REQUEST_SHUTDOWN, HIBYTE(ctx->queryShutdownMode) == 0xFF ?
					MAKEWORD(LOBYTE(ctx->queryShutdownMode), HIBYTE(ctx->sys->defShutdownMode)) : ctx->queryShutdownMode, 0);
			}
			//FALL THROUGH!
		case IDCANCEL:
			DestroyWindow(hDlg);
			break;
		}
		break;
	}
	return FALSE;
}

void CEpgTimerSrvMain::StopMain()
{
	volatile HWND hwndMain_ = this->hwndMain;
	if( hwndMain_ ){
		SendNotifyMessage(hwndMain_, WM_CLOSE, 0, 0);
	}
}

bool CEpgTimerSrvMain::IsSuspendOK()
{
	DWORD marginSec;
	bool ngFileStreaming_;
	{
		CBlockLock lock(&this->settingLock);
		//rebootFlag時の復帰マージンを基準に3分余裕を加えたものと抑制条件のどちらか大きいほう
		marginSec = max(this->wakeMarginSec + 300 + 180, this->noStandbySec);
		ngFileStreaming_ = this->ngFileStreaming;
	}
	__int64 now = GetNowI64Time();
	//シャットダウン可能で復帰が間に合うときだけ
	return (ngFileStreaming_ == false || this->streamingManager.IsStreaming() == FALSE) &&
	       this->reserveManager.IsActive() == false &&
	       this->reserveManager.GetSleepReturnTime(now) > now + marginSec * I64_1SEC &&
	       IsFindNoSuspendExe() == false &&
	       IsFindShareTSFile() == false;
}

void CEpgTimerSrvMain::ReloadNetworkSetting()
{
	CBlockLock lock(&this->settingLock);

	wstring iniPath;
	GetModuleIniPath(iniPath);
	this->tcpPort = 0;
	if( GetPrivateProfileInt(L"SET", L"EnableTCPSrv", 0, iniPath.c_str()) != 0 ){
		this->tcpAccessControlList = GetPrivateProfileToString(L"SET", L"TCPAccessControlList", L"+127.0.0.1,+192.168.0.0/16", iniPath.c_str());
		this->tcpResponseTimeoutSec = GetPrivateProfileInt(L"SET", L"TCPResponseTimeoutSec", 120, iniPath.c_str());
		this->tcpPort = (unsigned short)GetPrivateProfileInt(L"SET", L"TCPPort", 4510, iniPath.c_str());
		wstring base64str = GetPrivateProfileToString(L"SET", L"TCPAccessPassword", L"", iniPath.c_str());
		wstring decrypt;
		if( CCryptUtil::Decrypt(base64str, decrypt) ){
			this->tcpPassword = base64str;
		}else if( base64str.size() <= MAX_PASSWORD_LENGTH && CCryptUtil::Encrypt(base64str, this->tcpPassword) ){
			WritePrivateProfileString(L"SET", L"TCPAccessPassword", this->tcpPassword.c_str(), iniPath.c_str());
		}
	}
	this->httpOptions.ports.clear();
	int enableHttpSrv = GetPrivateProfileInt(L"SET", L"EnableHttpSrv", 0, iniPath.c_str());
	if( enableHttpSrv != 0 ){
		this->httpOptions.rootPath = GetPrivateProfileToString(L"SET", L"HttpPublicFolder", L"", iniPath.c_str());
		if(this->httpOptions.rootPath.empty() ){
			GetModuleFolderPath(this->httpOptions.rootPath);
			this->httpOptions.rootPath += L"\\HttpPublic";
		}
		ChkFolderPath(this->httpOptions.rootPath);
		if( this->dmsPublicFileList.empty() || CompareNoCase(this->httpOptions.rootPath, this->dmsPublicFileList[0].second) != 0 ){
			//公開フォルダの場所が変わったのでクリア
			this->dmsPublicFileList.clear();
		}
		this->httpOptions.accessControlList = GetPrivateProfileToString(L"SET", L"HttpAccessControlList", L"+127.0.0.1", iniPath.c_str());
		this->httpOptions.authenticationDomain = GetPrivateProfileToString(L"SET", L"HttpAuthenticationDomain", L"", iniPath.c_str());
		this->httpOptions.numThreads = GetPrivateProfileInt(L"SET", L"HttpNumThreads", 5, iniPath.c_str());
		this->httpOptions.requestTimeout = GetPrivateProfileInt(L"SET", L"HttpRequestTimeoutSec", 120, iniPath.c_str()) * 1000;
		this->httpOptions.sslCipherList = GetPrivateProfileToString(L"SET", L"HttpSslCipherList", L"HIGH:!aNULL:!MD5", iniPath.c_str());
		this->httpOptions.sslProtocolVersion = GetPrivateProfileInt(L"SET", L"HttpSslProtocolVersion", 2, iniPath.c_str());
		this->httpOptions.keepAlive = GetPrivateProfileInt(L"SET", L"HttpKeepAlive", 0, iniPath.c_str()) != 0;
		this->httpOptions.ports = GetPrivateProfileToString(L"SET", L"HttpPort", L"5510", iniPath.c_str());
		this->httpOptions.saveLog = enableHttpSrv == 2;
	}
	this->enableSsdpServer = GetPrivateProfileInt(L"SET", L"EnableDMS", 0, iniPath.c_str()) != 0;

	PostMessage(this->hwndMain, WM_RESET_SERVER, 0, 0);
}

void CEpgTimerSrvMain::ReloadSetting()
{
	this->reserveManager.ReloadSetting();

	CBlockLock lock(&this->settingLock);

	wstring iniPath;
	GetModuleIniPath(iniPath);
	int residentMode = GetPrivateProfileInt(L"SET", L"ResidentMode", 0, iniPath.c_str());
	this->residentFlag = residentMode >= 1; //常駐するかどうか(CMD2_EPG_SRV_CLOSEを無視)
	if( this->residentFlag ){
		//タスクトレイに表示するかどうか
		PostMessage(this->hwndMain, WM_SHOW_TRAY, residentMode >= 2,
			GetPrivateProfileInt(L"SET", L"NoBalloonTip", 0, iniPath.c_str()) == 0);
	}
	this->saveNotifyLog = GetPrivateProfileInt(L"SET", L"SaveNotifyLog", 0, iniPath.c_str()) != 0;
	this->wakeMarginSec = GetPrivateProfileInt(L"SET", L"WakeTime", 5, iniPath.c_str()) * 60;
	this->autoAddHour = GetPrivateProfileInt(L"SET", L"AutoAddDays", 8, iniPath.c_str()) * 24 +
	                    GetPrivateProfileInt(L"SET", L"AutoAddHour", 0, iniPath.c_str());
	this->chkGroupEvent = GetPrivateProfileInt(L"SET", L"ChkGroupEvent", 1, iniPath.c_str()) != 0;
	this->defShutdownMode = MAKEWORD((GetPrivateProfileInt(L"SET", L"RecEndMode", 0, iniPath.c_str()) + 3) % 4 + 1,
	                                 (GetPrivateProfileInt(L"SET", L"Reboot", 0, iniPath.c_str()) != 0 ? 1 : 0));
	this->ngUsePCTime = 0;
	if( GetPrivateProfileInt(L"NO_SUSPEND", L"NoUsePC", 0, iniPath.c_str()) != 0 ){
		this->ngUsePCTime = GetPrivateProfileInt(L"NO_SUSPEND", L"NoUsePCTime", 3, iniPath.c_str()) * 60 * 1000;
		//閾値が0のときは常に使用中扱い
		if( this->ngUsePCTime == 0 ){
			this->ngUsePCTime = MAXDWORD;
		}
	}
	this->ngFileStreaming = GetPrivateProfileInt(L"NO_SUSPEND", L"NoFileStreaming", 0, iniPath.c_str()) != 0;
	this->ngShareFile = GetPrivateProfileInt(L"NO_SUSPEND", L"NoShareFile", 0, iniPath.c_str()) != 0;
	this->noStandbySec = GetPrivateProfileInt(L"NO_SUSPEND", L"NoStandbyTime", 10, iniPath.c_str()) * 60;
	this->useSyoboi = GetPrivateProfileInt(L"SYOBOI", L"use", 0, iniPath.c_str()) != 0;

	this->noSuspendExeList.clear();
	int count = GetPrivateProfileInt(L"NO_SUSPEND", L"Count", 0, iniPath.c_str());
	if( count == 0 ){
		this->noSuspendExeList.push_back(L"EpgDataCap_Bon.exe");
	}
	for( int i = 0; i < count; i++ ){
		WCHAR key[16];
		wsprintf(key, L"%d", i);
		wstring buff = GetPrivateProfileToString(L"NO_SUSPEND", key, L"", iniPath.c_str());
		if( buff.empty() == false ){
			this->noSuspendExeList.push_back(buff);
		}
	}

	this->tvtestUseBon.clear();
	count = GetPrivateProfileInt(L"TVTEST", L"Num", 0, iniPath.c_str());
	for( int i = 0; i < count; i++ ){
		WCHAR key[16];
		wsprintf(key, L"%d", i);
		wstring buff = GetPrivateProfileToString(L"TVTEST", key, L"", iniPath.c_str());
		if( buff.empty() == false ){
			this->tvtestUseBon.push_back(buff);
		}
	}
}

pair<wstring, REC_SETTING_DATA> CEpgTimerSrvMain::LoadRecSetData(WORD preset) const
{
	wstring iniPath;
	GetModuleIniPath(iniPath);
	WCHAR defIndex[32];
	WCHAR defName[32];
	WCHAR defFolderName[2][32];
	defIndex[preset == 0 ? 0 : wsprintf(defIndex, L"%d", preset)] = L'\0';
	wsprintf(defName, L"REC_DEF%s", defIndex);
	wsprintf(defFolderName[0], L"REC_DEF_FOLDER%s", defIndex);
	wsprintf(defFolderName[1], L"REC_DEF_FOLDER_1SEG%s", defIndex);

	pair<wstring, REC_SETTING_DATA> ret;
	ret.first = preset == 0 ? wstring(L"デフォルト") : GetPrivateProfileToString(defName, L"SetName", L"", iniPath.c_str());
	REC_SETTING_DATA& rs = ret.second;
	rs.recMode = (BYTE)GetPrivateProfileInt(defName, L"RecMode", 1, iniPath.c_str());
	rs.priority = (BYTE)GetPrivateProfileInt(defName, L"Priority", 2, iniPath.c_str());
	rs.tuijyuuFlag = (BYTE)GetPrivateProfileInt(defName, L"TuijyuuFlag", 1, iniPath.c_str());
	rs.serviceMode = (BYTE)GetPrivateProfileInt(defName, L"ServiceMode", 0, iniPath.c_str());
	rs.pittariFlag = (BYTE)GetPrivateProfileInt(defName, L"PittariFlag", 0, iniPath.c_str());
	rs.batFilePath = GetPrivateProfileToString(defName, L"BatFilePath", L"", iniPath.c_str());
	rs.recTag = GetPrivateProfileToString(defName, L"RecTag", L"", iniPath.c_str());
	for( int i = 0; i < 2; i++ ){
		vector<REC_FILE_SET_INFO>& recFolderList = i == 0 ? rs.recFolderList : rs.partialRecFolder;
		int count = GetPrivateProfileInt(defFolderName[i], L"Count", 0, iniPath.c_str());
		for( int j = 0; j < count; j++ ){
			recFolderList.resize(j + 1);
			WCHAR key[32];
			wsprintf(key, L"%d", j);
			recFolderList[j].recFolder = GetPrivateProfileToString(defFolderName[i], key, L"", iniPath.c_str());
			wsprintf(key, L"WritePlugIn%d", j);
			recFolderList[j].writePlugIn = GetPrivateProfileToString(defFolderName[i], key, L"Write_Default.dll", iniPath.c_str());
			wsprintf(key, L"RecNamePlugIn%d", j);
			recFolderList[j].recNamePlugIn = GetPrivateProfileToString(defFolderName[i], key, L"", iniPath.c_str());
		}
	}
	rs.suspendMode = (BYTE)GetPrivateProfileInt(defName, L"SuspendMode", 0, iniPath.c_str());
	rs.rebootFlag = (BYTE)GetPrivateProfileInt(defName, L"RebootFlag", 0, iniPath.c_str());
	rs.useMargineFlag = (BYTE)GetPrivateProfileInt(defName, L"UseMargineFlag", 0, iniPath.c_str());
	rs.startMargine = GetPrivateProfileInt(defName, L"StartMargine", 0, iniPath.c_str());
	rs.endMargine = GetPrivateProfileInt(defName, L"EndMargine", 0, iniPath.c_str());
	rs.continueRecFlag = (BYTE)GetPrivateProfileInt(defName, L"ContinueRec", 0, iniPath.c_str());
	rs.partialRecFlag = (BYTE)GetPrivateProfileInt(defName, L"PartialRec", 0, iniPath.c_str());
	rs.tunerID = GetPrivateProfileInt(defName, L"TunerID", 0, iniPath.c_str());
	return ret;
}

bool CEpgTimerSrvMain::SetResumeTimer(HANDLE* resumeTimer, __int64* resumeTime, DWORD marginSec)
{
	__int64 returnTime = this->reserveManager.GetSleepReturnTime(GetNowI64Time() + marginSec * I64_1SEC);
	if( returnTime == LLONG_MAX ){
		if( *resumeTimer != NULL ){
			CloseHandle(*resumeTimer);
			*resumeTimer = NULL;
		}
		return true;
	}
	__int64 setTime = returnTime - marginSec * I64_1SEC;
	if( *resumeTimer != NULL && *resumeTime == setTime ){
		//同時刻でセット済み
		return true;
	}
	if( *resumeTimer == NULL ){
		*resumeTimer = CreateWaitableTimer(NULL, FALSE, NULL);
	}
	if( *resumeTimer != NULL ){
		FILETIME locTime;
		locTime.dwLowDateTime = (DWORD)setTime;
		locTime.dwHighDateTime = (DWORD)(setTime >> 32);
		FILETIME utcTime = {};
		LocalFileTimeToFileTime(&locTime, &utcTime);
		LARGE_INTEGER liTime;
		liTime.QuadPart = (LONGLONG)utcTime.dwHighDateTime << 32 | utcTime.dwLowDateTime;
		if( SetWaitableTimer(*resumeTimer, &liTime, 0, NULL, NULL, TRUE) != FALSE ){
			*resumeTime = setTime;
			return true;
		}
		CloseHandle(*resumeTimer);
		*resumeTimer = NULL;
	}
	return false;
}

void CEpgTimerSrvMain::SetShutdown(BYTE shutdownMode)
{
	HANDLE hToken;
	if ( OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, &hToken) ){
		TOKEN_PRIVILEGES tokenPriv;
		LookupPrivilegeValue(NULL, SE_SHUTDOWN_NAME, &tokenPriv.Privileges[0].Luid);
		tokenPriv.PrivilegeCount = 1;
		tokenPriv.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;
		AdjustTokenPrivileges(hToken, FALSE, &tokenPriv, 0, NULL, NULL);
		CloseHandle(hToken);
	}
	if( shutdownMode == 1 ){
		//スタンバイ(同期)
		SetSystemPowerState(TRUE, FALSE);
	}else if( shutdownMode == 2 ){
		//休止(同期)
		SetSystemPowerState(FALSE, FALSE);
	}else if( shutdownMode == 3 ){
		//電源断(非同期)
		ExitWindowsEx(EWX_POWEROFF, 0);
	}else if( shutdownMode == 4 ){
		//再起動(非同期)
		ExitWindowsEx(EWX_REBOOT, 0);
	}
}

bool CEpgTimerSrvMain::QueryShutdown(BYTE rebootFlag, BYTE suspendMode)
{
	CSendCtrlCmd ctrlCmd;
	vector<DWORD> registGUI = this->notifyManager.GetRegistGUI();
	for( size_t i = 0; i < registGUI.size(); i++ ){
		ctrlCmd.SetPipeSetting(CMD2_GUI_CTRL_WAIT_CONNECT, CMD2_GUI_CTRL_PIPE, registGUI[i]);
		//通信できる限り常に成功するので、重複問い合わせを考慮する必要はない
		if( suspendMode == 0 && ctrlCmd.SendGUIQueryReboot(rebootFlag) == CMD_SUCCESS ||
		    suspendMode != 0 && ctrlCmd.SendGUIQuerySuspend(rebootFlag, suspendMode) == CMD_SUCCESS ){
			return true;
		}
	}
	return false;
}

bool CEpgTimerSrvMain::IsUserWorking() const
{
	CBlockLock lock(&this->settingLock);

	//最終入力時刻取得
	LASTINPUTINFO lii;
	lii.cbSize = sizeof(LASTINPUTINFO);
	return this->ngUsePCTime == MAXDWORD || this->ngUsePCTime && GetLastInputInfo(&lii) && GetTickCount() - lii.dwTime < this->ngUsePCTime;
}

bool CEpgTimerSrvMain::IsFindShareTSFile() const
{
	bool found = false;
	FILE_INFO_3* fileInfo;
	DWORD entriesread;
	DWORD totalentries;
	if( this->ngShareFile && NetFileEnum(NULL, NULL, NULL, 3, (LPBYTE*)&fileInfo, MAX_PREFERRED_LENGTH, &entriesread, &totalentries, NULL) == NERR_Success ){
		for( DWORD i = 0; i < entriesread; i++ ){
			if( IsExt(fileInfo[i].fi3_pathname, L".ts") != FALSE ){
				OutputDebugString(L"共有フォルダTSアクセス\r\n");
				found = true;
				break;
			}
		}
		NetApiBufferFree(fileInfo);
	}
	return found;
}

bool CEpgTimerSrvMain::IsFindNoSuspendExe() const
{
	CBlockLock lock(&this->settingLock);

	if( this->noSuspendExeList.empty() == false ){
		//Toolhelpスナップショットを作成する
		HANDLE hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
		if( hSnapshot != INVALID_HANDLE_VALUE ){
			bool found = false;
			PROCESSENTRY32 procent;
			procent.dwSize = sizeof(PROCESSENTRY32);
			if( Process32First(hSnapshot, &procent) != FALSE ){
				do{
					for( size_t i = 0; i < this->noSuspendExeList.size(); i++ ){
						//procent.szExeFileにプロセス名
						wstring strExe = wstring(procent.szExeFile).substr(0, this->noSuspendExeList[i].size());
						if( CompareNoCase(strExe, this->noSuspendExeList[i]) == 0 ){
							_OutputDebugString(L"起動exe:%s\r\n", procent.szExeFile);
							found = true;
							break;
						}
					}
				}while( found == false && Process32Next(hSnapshot, &procent) != FALSE );
			}
			CloseHandle(hSnapshot);
			return found;
		}
	}
	return false;
}

bool CEpgTimerSrvMain::AutoAddReserveEPG(const EPG_AUTO_ADD_DATA& data, bool noReportNotify)
{
	CBlockLock lock(&this->settingLock);

	vector<RESERVE_DATA> reserveData;
	bool ret = reserveManager.AutoAddReserveEPG(data, this->autoAddHour, this->chkGroupEvent, &reserveData, noReportNotify);

	vector<RESERVE_BASIC_DATA> reserveBasic; reserveBasic.resize(reserveData.size());
	std::copy(reserveData.begin(), reserveData.end(), reserveBasic.begin());
	epgAutoAdd.SetAddList(data.dataID, reserveBasic);
	epgAutoAdd.SetSearchKeyHash(data.dataID, data.searchInfo.searchKeyHash); //検索キーハッシュ値の更新
	return ret;
}

bool CEpgTimerSrvMain::AutoAddReserveProgram(const MANUAL_AUTO_ADD_DATA& data, bool noReportNotify)
{
	vector<RESERVE_DATA> setList;
	SYSTEMTIME baseTime;
	GetLocalTime(&baseTime);
	__int64 now = ConvertI64Time(baseTime);
	baseTime.wHour = 0;
	baseTime.wMinute = 0;
	baseTime.wSecond = 0;
	baseTime.wMilliseconds = 0;
	__int64 baseStartTime = ConvertI64Time(baseTime);

	for( int i = 0; i < 8; i++ ){
		//今日から8日分を調べる
		if( data.dayOfWeekFlag >> ((i + baseTime.wDayOfWeek) % 7) & 1 ){
			__int64 startTime = baseStartTime + (data.startTime + i * 24 * 60 * 60) * I64_1SEC;
			if( startTime > now ){
				//同一時間の予約がすでにあるかチェック
				if( this->reserveManager.IsFindProgramReserve(
				    data.originalNetworkID, data.transportStreamID, data.serviceID, startTime, data.durationSecond) == false ){
					//見つからなかったので予約追加
					setList.resize(setList.size() + 1);
					RESERVE_DATA& item = setList.back();
					item.title = data.title;
					ConvertSystemTime(startTime, &item.startTime); 
					item.startTimeEpg = item.startTime;
					item.durationSecond = data.durationSecond;
					item.stationName = data.stationName;
					item.originalNetworkID = data.originalNetworkID;
					item.transportStreamID = data.transportStreamID;
					item.serviceID = data.serviceID;
					item.eventID = 0xFFFF;
					item.recSetting = data.recSetting;
					item.comment = L"プログラム自動予約";
				}
			}
		}
	}
	return setList.empty() == false && this->reserveManager.AddReserveData(setList, true, false, noReportNotify);
}

void CEpgTimerSrvMain::UpdateRecFileInfo() {
	CBlockLock lock(&this->settingLock);

	if (reserveManager.GetNewRecInfoCount() > 0) {
		//DWORD time = GetTickCount();
		// 新規録画ファイルがある場合は録画ファイルとの関連付けを更新
		auto list = reserveManager.UpdateAndMergeNewRecInfo(epgAutoAdd.GetMap());
		// 新規関連付け分を追加
		for (const auto& entry : list) {
			epgAutoAdd.AddRecList(entry.first, entry.second);
		}
		//_OutputDebugString(L"UpdateRecFileInfo %dmsec\r\n", GetTickCount() - time);
	}
}

bool CEpgTimerSrvMain::RemoveNolinkedReserve(vector<DWORD> beforeReserveIds) {
	CBlockLock lock(&this->settingLock);

	// 重複をなくす
	std::set<DWORD> beforeIdSet;
	for (DWORD id : beforeReserveIds) {
		beforeIdSet.insert(id);
	}

	__int64 now = GetNowI64Time();

	std::vector<DWORD> removeIds;
	for (DWORD id : beforeIdSet) {
		RESERVE_DATA rsv;
		if (reserveManager.GetReserveData(id, &rsv)) {
			// 自動予約で追加された？
			if (rsv.comment.compare(0, 7, EPG_AUTO_ADD_TEXT) == 0) {
				// 該当自動予約がない？
				if (rsv.autoAddInfo.size() == 0) {
					// 番組情報はある？
					EPGDB_EVENT_INFO info;
					if (epgDB.SearchEpg(rsv.originalNetworkID, rsv.transportStreamID, rsv.serviceID, rsv.eventID, &info)) {
						// 時間未定でなく開始時刻を過ぎていない？
						if (info.StartTimeFlag != 0 && now < ConvertI64Time(info.start_time)) {

							_OutputDebugString(L"自動予約設定変更の伴い予約を削除: %s\r\n", info.shortInfo->event_name.c_str());

							removeIds.push_back(id);
						}
						
					}
				}
			}
		}
	}

	if (removeIds.size() > 0) {
		reserveManager.DelReserveData(removeIds);
		return true;
	}

	return false;
}

vector<EPG_AUTO_ADD_DATA> CEpgTimerSrvMain::GetAutoAddList()
{
	CBlockLock lock(&this->settingLock);

	UpdateRecFileInfo();

	vector<EPG_AUTO_ADD_DATA> ret;
	ret.reserve(epgAutoAdd.GetMap().size());
	for (const auto& entry : epgAutoAdd.GetMap()) {
		ret.push_back(entry.second);
	}

	return ret;
}

bool CEpgTimerSrvMain::DelAutoAdd(vector<DWORD>& val) {
	bool modified = false;

	CBlockLock lock(&settingLock);

	for (size_t i = 0; i < val.size(); i++) {
		auto it = epgAutoAdd.GetMap().find(val[i]);
		if (it != epgAutoAdd.GetMap().end()) {
			reserveManager.AutoAddDeleted(it->second);
			epgAutoAdd.DelData(val[i]);
			modified = true;
		}
	}
	if (modified) {
		epgAutoAdd.SaveText();
		notifyManager.AddNotify(NOTIFY_UPDATE_AUTOADD_EPG);
	}
	return modified;
}

bool CEpgTimerSrvMain::ChgAutoAdd(vector<EPG_AUTO_ADD_DATA>& val) {
	bool modified = false;
	vector<DWORD> reserveList;
	{
		CBlockLock lock(&settingLock);
		for (size_t i = 0; i < val.size(); i++) {
			if (epgAutoAdd.ChgData(val[i]) == false) {
				val.erase(val.begin() + (i--));
			}
			else {
				val[i] = epgAutoAdd.GetMap().at(val[i].dataID);
				for (const RESERVE_BASIC_DATA& rsv : val[i].reserveList) {
					reserveList.push_back(rsv.reserveID);
				}
				auto list = reserveManager.AutoAddUpdateRecInfo(val[i]);
				epgAutoAdd.SetRecList(val[i].dataID, list);
				modified = true;
			}
		}
		if (modified) {
			epgAutoAdd.SaveText();
		}
	}
	if (modified) {
		bool addReserve = false;
		for (size_t i = 0; i < val.size(); i++) {
			addReserve |= AutoAddReserveEPG(val[i], true);
		}
		RemoveNolinkedReserve(reserveList);
		notifyManager.AddNotify(NOTIFY_UPDATE_AUTOADD_EPG);
		if( addReserve ){
			reserveManager.AddNotifyAndPostBat(NOTIFY_UPDATE_RESERVE_INFO);
		}
	}
	return modified;
}

bool CEpgTimerSrvMain::AddAutoAdd(vector<EPG_AUTO_ADD_DATA>& val) {
	{
		CBlockLock lock(&settingLock);
		for (size_t i = 0; i < val.size(); i++) {
			DWORD id = epgAutoAdd.AddData(val[i]);
			val[i] = epgAutoAdd.GetMap().at(id);
			auto list = reserveManager.AutoAddUpdateRecInfo(val[i]);
			epgAutoAdd.SetRecList(val[i].dataID, list);
		}
		epgAutoAdd.SaveText();
	}
	bool addReserve = false;
	for (size_t i = 0; i < val.size(); i++) {
		addReserve |= AutoAddReserveEPG(val[i], true);
	}
	notifyManager.AddNotify(NOTIFY_UPDATE_AUTOADD_EPG);
	if( addReserve ){
		reserveManager.AddNotifyAndPostBat(NOTIFY_UPDATE_RESERVE_INFO);
	}
	return true;
}

static void SearchPgCallback(vector<CEpgDBManager::SEARCH_RESULT_EVENT>* pval, void* param)
{
	vector<const EPGDB_EVENT_INFO*> valp;
	valp.reserve(pval->size());
	for( size_t i = 0; i < pval->size(); i++ ){
		valp.push_back((*pval)[i].info);
	}
	CMD_STREAM *resParam = (CMD_STREAM*)param;
	resParam->param = CMD_SUCCESS;
	resParam->data = NewWriteVALUE(valp, resParam->dataSize);
}

//大変行儀が悪いが、正しくver渡すために外に置いておく。
static WORD CommitedVerForNewCMD=(WORD)CMD_VER;//一応初期化
static void SearchPg2Callback(vector<CEpgDBManager::SEARCH_RESULT_EVENT>* pval, void* param)
{
	vector<const EPGDB_EVENT_INFO*> valp;
	valp.reserve(pval->size());
	for( size_t i = 0; i < pval->size(); i++ ){
		valp.push_back((*pval)[i].info);
	}
	CMD_STREAM *resParam = (CMD_STREAM*)param;
	resParam->param = CMD_SUCCESS;
	resParam->data = NewWriteVALUE2WithVersion(CommitedVerForNewCMD, valp, resParam->dataSize);
}

//戻り値の型を変更出来るように、一応分けておく
static void SearchPgByKey2Callback(vector<CEpgDBManager::SEARCH_RESULT_EVENT>* pval, void* param)
{
	SearchPg2Callback(pval, param);
}

static void EnumPgInfoCallback(const vector<EPGDB_EVENT_INFO>* pval, void* param)
{
	CMD_STREAM *resParam = (CMD_STREAM*)param;
	resParam->param = CMD_SUCCESS;
	resParam->data = NewWriteVALUE(*pval, resParam->dataSize);
}

static void EnumPgAllCallback(vector<const EPGDB_SERVICE_EVENT_INFO*>* pval, void* param)
{
	CMD_STREAM *resParam = (CMD_STREAM*)param;
	resParam->param = CMD_SUCCESS;
	resParam->data = NewWriteVALUE(*pval, resParam->dataSize);
}

int CALLBACK CEpgTimerSrvMain::CtrlCmdPipeCallback(void* param, CMD_STREAM* cmdParam, CMD_STREAM* resParam)
{
	return CtrlCmdCallback(param, cmdParam, resParam, false);
}

int CALLBACK CEpgTimerSrvMain::CtrlCmdTcpCallback(void* param, CMD_STREAM* cmdParam, CMD_STREAM* resParam)
{
	return CtrlCmdCallback(param, cmdParam, resParam, true);
}

int CEpgTimerSrvMain::CtrlCmdCallback(void* param, CMD_STREAM* cmdParam, CMD_STREAM* resParam, bool tcpFlag)
{
	//この関数はPipeとTCPとで同時に呼び出されるかもしれない(各々が同時に複数呼び出すことはない)
	CEpgTimerSrvMain* sys = (CEpgTimerSrvMain*)param;

	LARGE_INTEGER startTime;
	LARGE_INTEGER endTime;
	QueryPerformanceCounter(&startTime);

	resParam->dataSize = 0;
	resParam->param = CMD_ERR;


	switch( cmdParam->param ){
	case CMD2_EPG_SRV_RELOAD_EPG:
		if( sys->epgDB.IsLoadingData() != FALSE ){
			resParam->param = CMD_ERR_BUSY;
		}else if( sys->epgDB.ReloadEpgData() != FALSE ){
			PostMessage(sys->hwndMain, WM_RELOAD_EPG_CHK, 0, 0);
			resParam->param = CMD_SUCCESS;
		}
		break;
	case CMD2_EPG_SRV_RELOAD_SETTING:
		sys->ReloadSetting();
		sys->ReloadNetworkSetting();
		resParam->param = CMD_SUCCESS;
		break;
	case CMD2_EPG_SRV_CLOSE:
		if( sys->residentFlag == false ){
			sys->StopMain();
			resParam->param = CMD_SUCCESS;
		}
		break;
	case CMD2_EPG_SRV_REGIST_GUI:
		{
			DWORD processID = 0;
			if( ReadVALUE(&processID, cmdParam->data, cmdParam->dataSize, NULL) ){
				//CPipeServerの仕様的にこの時点で相手と通信できるとは限らない。接続待機用イベントが作成されるまで少し待つ
				wstring eventName;
				Format(eventName, L"%s%d", CMD2_GUI_CTRL_WAIT_CONNECT, processID);
				for( int i = 0; i < 100; i++ ){
					HANDLE waitEvent = OpenEvent(SYNCHRONIZE, FALSE, eventName.c_str());
					if( waitEvent ){
						CloseHandle(waitEvent);
						break;
					}
					Sleep(100);
				}
				resParam->param = CMD_SUCCESS;
				sys->notifyManager.RegistGUI(processID);
			}
		}
		break;
	case CMD2_EPG_SRV_UNREGIST_GUI:
		{
			DWORD processID = 0;
			if( ReadVALUE(&processID, cmdParam->data, cmdParam->dataSize, NULL) ){
				resParam->param = CMD_SUCCESS;
				sys->notifyManager.UnRegistGUI(processID);
			}
		}
		break;
	case CMD2_EPG_SRV_REGIST_GUI_TCP:
		{
			REGIST_TCP_INFO val;
			if( ReadVALUE(&val, cmdParam->data, cmdParam->dataSize, NULL) ){
				resParam->param = CMD_SUCCESS;
				sys->notifyManager.RegistTCP(val);
			}
		}
		break;
	case CMD2_EPG_SRV_UNREGIST_GUI_TCP:
		{
			REGIST_TCP_INFO val;
			if( ReadVALUE(&val, cmdParam->data, cmdParam->dataSize, NULL) ){
				resParam->param = CMD_SUCCESS;
				sys->notifyManager.UnRegistTCP(val);
			}
		}
		break;
	case CMD2_EPG_SRV_ISREGIST_GUI_TCP:
		{
			REGIST_TCP_INFO val;
			if( ReadVALUE(&val, cmdParam->data, cmdParam->dataSize, NULL) ){
				BOOL registered = sys->notifyManager.IsRegistTCP(val);
				resParam->data = NewWriteVALUE(registered, resParam->dataSize);
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_ENUM_RESERVE:
		resParam->data = NewWriteVALUE(sys->reserveManager.GetReserveDataAll(), resParam->dataSize);
		resParam->param = CMD_SUCCESS;
		break;
	case CMD2_EPG_SRV_GET_RESERVE:
		{
			DWORD reserveID;
			if( ReadVALUE(&reserveID, cmdParam->data, cmdParam->dataSize, NULL) ){
				RESERVE_DATA info;
				if( sys->reserveManager.GetReserveData(reserveID, &info) ){
					resParam->data = NewWriteVALUE(info, resParam->dataSize);
					resParam->param = CMD_SUCCESS;
				}
			}
		}
		break;
	case CMD2_EPG_SRV_ADD_RESERVE:
		{
			vector<RESERVE_DATA> list;
			if( ReadVALUE(&list, cmdParam->data, cmdParam->dataSize, NULL) &&
			    sys->reserveManager.AddReserveData(list) ){
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_DEL_RESERVE:
		{
			vector<DWORD> list;
			if( ReadVALUE(&list, cmdParam->data, cmdParam->dataSize, NULL) ){
				sys->reserveManager.DelReserveData(list);
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_CHG_RESERVE:
		{
			vector<RESERVE_DATA> list;
			if( ReadVALUE(&list, cmdParam->data, cmdParam->dataSize, NULL) &&
			    sys->reserveManager.ChgReserveData(list) ){
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_ENUM_RECINFO:
		resParam->data = NewWriteVALUE(sys->reserveManager.GetRecFileInfoAll(), resParam->dataSize);
		resParam->param = CMD_SUCCESS;
		break;
	case CMD2_EPG_SRV_DEL_RECINFO:
		{
			vector<DWORD> list;
			if( ReadVALUE(&list, cmdParam->data, cmdParam->dataSize, NULL) ){
				sys->reserveManager.DelRecFileInfo(list);
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_ENUM_SERVICE:
		if( sys->epgDB.IsInitialLoadingDataDone() == FALSE ){
			resParam->param = CMD_ERR_BUSY;
		}else{
			vector<EPGDB_SERVICE_INFO> list;
			if( sys->epgDB.GetServiceList(&list) != FALSE ){
				resParam->data = NewWriteVALUE(list, resParam->dataSize);
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_ENUM_PG_INFO:
		if( sys->epgDB.IsInitialLoadingDataDone() == FALSE ){
			resParam->param = CMD_ERR_BUSY;
		}else{
			LONGLONG serviceKey;
			if( ReadVALUE(&serviceKey, cmdParam->data, cmdParam->dataSize, NULL) ){
				sys->epgDB.EnumEventInfo(serviceKey, EnumPgInfoCallback, resParam);
			}
		}
		break;
	case CMD2_EPG_SRV_SEARCH_PG:
		if( sys->epgDB.IsInitialLoadingDataDone() == FALSE ){
			resParam->param = CMD_ERR_BUSY;
		}else{
			vector<EPGDB_SEARCH_KEY_INFO> key;
			if( ReadVALUE(&key, cmdParam->data, cmdParam->dataSize, NULL) ){
				sys->epgDB.SearchEpg(&key, SearchPgCallback, resParam);
			}
		}
		break;
	case CMD2_EPG_SRV_GET_PG_INFO:
		if( sys->epgDB.IsInitialLoadingDataDone() == FALSE ){
			resParam->param = CMD_ERR_BUSY;
		}else{
			ULONGLONG key;
			EPGDB_EVENT_INFO val;
			if( ReadVALUE(&key, cmdParam->data, cmdParam->dataSize, NULL) &&
			    sys->epgDB.SearchEpg(key>>48&0xFFFF, key>>32&0xFFFF, key>>16&0xFFFF, key&0xFFFF, &val) ){
				resParam->data = NewWriteVALUE(val, resParam->dataSize);
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_CHK_SUSPEND:
		if( sys->IsSuspendOK() ){
			resParam->param = CMD_SUCCESS;
		}
		break;
	case CMD2_EPG_SRV_SUSPEND:
		{
			WORD val;
			if( ReadVALUE(&val, cmdParam->data, cmdParam->dataSize, NULL) && sys->IsSuspendOK() ){
				if( HIBYTE(val) == 0xFF ){
					val = MAKEWORD(LOBYTE(val), HIBYTE(sys->defShutdownMode));
				}
				PostMessage(sys->hwndMain, WM_REQUEST_SHUTDOWN, val, 0);
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_REBOOT:
		{
			PostMessage(sys->hwndMain, WM_REQUEST_SHUTDOWN, 0x01FF, 0);
			resParam->param = CMD_SUCCESS;
		}
		break;
	case CMD2_EPG_SRV_EPG_CAP_NOW:
		if( sys->epgDB.IsInitialLoadingDataDone() == FALSE ){
			resParam->param = CMD_ERR_BUSY;
		}else if( sys->reserveManager.RequestStartEpgCap() ){
			resParam->param = CMD_SUCCESS;
		}
		break;
	case CMD2_EPG_SRV_ENUM_AUTO_ADD:
		{
			vector<EPG_AUTO_ADD_DATA> val = sys->GetAutoAddList();
			resParam->data = NewWriteVALUE(val, resParam->dataSize);
			resParam->param = CMD_SUCCESS;
		}
		break;
	case CMD2_EPG_SRV_ADD_AUTO_ADD:
		{
			vector<EPG_AUTO_ADD_DATA> val;
			if( ReadVALUE(&val, cmdParam->data, cmdParam->dataSize, NULL) ){
				sys->AddAutoAdd(val);
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_DEL_AUTO_ADD:
		{
			vector<DWORD> val;
			if( ReadVALUE(&val, cmdParam->data, cmdParam->dataSize, NULL) ){
				sys->DelAutoAdd(val);
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_CHG_AUTO_ADD:
		{
			vector<EPG_AUTO_ADD_DATA> val;
			if( ReadVALUE(&val, cmdParam->data, cmdParam->dataSize, NULL ) ){
				sys->ChgAutoAdd(val);
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_ENUM_MANU_ADD:
		{
			vector<MANUAL_AUTO_ADD_DATA> val;
			{
				CBlockLock lock(&sys->settingLock);
				map<DWORD, MANUAL_AUTO_ADD_DATA>::const_iterator itr;
				for( itr = sys->manualAutoAdd.GetMap().begin(); itr != sys->manualAutoAdd.GetMap().end(); itr++ ){
					val.push_back(itr->second);
				}
			}
			resParam->data = NewWriteVALUE(val, resParam->dataSize);
			resParam->param = CMD_SUCCESS;
		}
		break;
	case CMD2_EPG_SRV_ADD_MANU_ADD:
		{
			vector<MANUAL_AUTO_ADD_DATA> val;
			if( ReadVALUE(&val, cmdParam->data, cmdParam->dataSize, NULL) ){
				{
					CBlockLock lock(&sys->settingLock);
					for( size_t i = 0; i < val.size(); i++ ){
						val[i].dataID = sys->manualAutoAdd.AddData(val[i]);
					}
					sys->manualAutoAdd.SaveText();
				}
				bool addReserve = false;
				for( size_t i = 0; i < val.size(); i++ ){
					addReserve |= sys->AutoAddReserveProgram(val[i], true);
				}
				sys->notifyManager.AddNotify(NOTIFY_UPDATE_AUTOADD_MANUAL);
				if( addReserve ){
					sys->reserveManager.AddNotifyAndPostBat(NOTIFY_UPDATE_RESERVE_INFO);
				}
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_DEL_MANU_ADD:
		{
			vector<DWORD> val;
			if( ReadVALUE(&val, cmdParam->data, cmdParam->dataSize, NULL) ){
				CBlockLock lock(&sys->settingLock);
				for( size_t i = 0; i < val.size(); i++ ){
					sys->manualAutoAdd.DelData(val[i]);
				}
				sys->manualAutoAdd.SaveText();
				sys->notifyManager.AddNotify(NOTIFY_UPDATE_AUTOADD_MANUAL);
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_CHG_MANU_ADD:
		{
			vector<MANUAL_AUTO_ADD_DATA> val;
			if( ReadVALUE(&val, cmdParam->data, cmdParam->dataSize, NULL ) ){
				{
					CBlockLock lock(&sys->settingLock);
					for( size_t i = 0; i < val.size(); i++ ){
						if( sys->manualAutoAdd.ChgData(val[i]) == false ){
							val.erase(val.begin() + (i--));
						}
					}
					sys->manualAutoAdd.SaveText();
				}
				bool addReserve = false;
				for( size_t i = 0; i < val.size(); i++ ){
					addReserve |= sys->AutoAddReserveProgram(val[i], true);
				}
				sys->notifyManager.AddNotify(NOTIFY_UPDATE_AUTOADD_MANUAL);
				if( addReserve ){
					sys->reserveManager.AddNotifyAndPostBat(NOTIFY_UPDATE_RESERVE_INFO);
				}
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_ENUM_TUNER_RESERVE:
		resParam->data = NewWriteVALUE(sys->reserveManager.GetTunerReserveAll(), resParam->dataSize);
		resParam->param = CMD_SUCCESS;
		break;
	case CMD2_EPG_SRV_FILE_COPY:
		{
			wstring val;
			if( ReadVALUE(&val, cmdParam->data, cmdParam->dataSize, NULL) ){
				wstring path;
				DWORD flags;
				if( CompareNoCase(val, L"ChSet5.txt") == 0 ){
					GetSettingPath(path);
					path += L"\\ChSet5.txt";
					flags = OPEN_EXISTING;
				}else if( CompareNoCase(val, L"Common.ini") == 0 ){
					GetCommonIniPath(path);
					flags = OPEN_ALWAYS;
				}else if( CompareNoCase(val, L"EpgTimerSrv.ini") == 0 ){
					GetEpgTimerSrvIniPath(path);
					flags = OPEN_ALWAYS;
				}else if( CompareNoCase(val, L"EpgDataCap_Bon.ini") == 0 ){
					GetModuleFolderPath(path);
					path += L"\\EpgDataCap_Bon.ini";
					flags = OPEN_EXISTING;
				}else{
					break;
				}
				HANDLE hFile = CreateFile(path.c_str(), GENERIC_READ, FILE_SHARE_READ, NULL, flags, FILE_ATTRIBUTE_NORMAL, NULL);
				if( hFile != INVALID_HANDLE_VALUE ){
					DWORD dwFileSize = GetFileSize(hFile, NULL);
					if (dwFileSize != INVALID_FILE_SIZE && dwFileSize != 0) {
						BYTE* data = new BYTE[dwFileSize];
						DWORD dwRead;
						if (ReadFile(hFile, data, dwFileSize, &dwRead, NULL) && dwRead != 0) {
							resParam->dataSize = dwRead;
							resParam->data = data;
						}
						else {
							delete[] data;
						}
					}
					CloseHandle(hFile);
					resParam->param = CMD_SUCCESS;
				}
			}
		}
		break;
	case CMD2_EPG_SRV_UPDATE_SETTING:
		{
			resParam->param = CMD_ERR;
			wstring val;
			if (ReadVALUE(&val, cmdParam->data, cmdParam->dataSize, NULL)) {
				bool needToUpdate = false;
				wstring path;
				wstring section;
				wstring key;
				wstring value;
				string text[1];	// 0:ChSet5.txt
				struct ignorecase_compare {
					bool operator()(const wstring& a, const wstring& b) const { return CompareNoCase(a, b) < 0; }
				};
				map<wstring, std::function<bool()>, ignorecase_compare> specialKey;
				int indexText = -1;
				size_t pos = 0, found;
				while ((found = val.find_first_of(L'\n', pos)) != wstring::npos) {
					wstring line = wstring(val, pos, found - pos);
					if (line.at(line.length() - 1) == L'\r')
						line.resize(line.length() - 1);
					pos = found + 1;

					// ファイル名の取得 ";<filename>" 
					if (line.length() > 3 && line[0] == L';' && line[1] == L'<' && line.find_first_of(L'>') == line.length() - 1) {
						wstring filename = wstring(line, 2, line.length() - 3);
						section.clear();
						specialKey.clear();
						indexText = -1;
						if (CompareNoCase(filename, L"ChSet5.txt") == 0) {
							indexText = 0;
						}
						else if (CompareNoCase(filename, L"Common.ini") == 0) {
							GetCommonIniPath(path);
						}
						else if (CompareNoCase(filename, L"EpgTimerSrv.ini") == 0) {
							GetEpgTimerSrvIniPath(path);
							specialKey.insert(pair<wstring, std::function<bool()>>(L"TCPAccessPassword", [&](){
								wstring temp;
								if (tcpFlag) return false;
								if (CCryptUtil::Decrypt(value, temp)) return true;
								return value.length() <= MAX_PASSWORD_LENGTH && CCryptUtil::Encrypt(value, value);
							}));
						}
						else if (CompareNoCase(val, L"EpgDataCap_Bon.ini") == 0) {
							GetModuleFolderPath(path);
							path += L"\\EpgDataCap_Bon.ini";
						}
						else {
							break;
						}
					}
					else if (indexText >= 0) {
						// テキストファイル形式
						string str;
						WtoA(line, str);
						text[indexText] += str + "\r\n";
					}
					else if (path.length() > 0) {
						// INIファイル形式
						// セクション名の取得 "[section]" 
						if (line.length() > 2 && line[0] == L'[') {
							if (line.find_first_of(L']') != line.length() - 1) {
								break;
							}
							section = wstring(line, 1, line.length() - 2);
						}
						else if (section.length() > 0) {
							// キー＆バリューの取得
							size_t delim = line.find_first_of(L'=');
							if (delim == 0 || delim == wstring::npos)
								break;
							key = wstring(line, 0, delim);
							value = wstring(line, delim + 1);
							int nOffset = key.at(0) == L';' ? 1 : 0;
							auto itr = specialKey.find(key.c_str() + nOffset);
							if (itr != specialKey.end() && !itr->second())
								continue;
							if (nOffset == 1) {
								// ";key=" の書式のとき、キーの削除をする
								if (!value.empty())
									break;
								WritePrivateProfileStringW(section.c_str(), key.c_str()+1, NULL, path.c_str());
								needToUpdate = true;
							}
							else {
								WritePrivateProfileStringW(section.c_str(), key.c_str(), value.c_str(), path.c_str());
								needToUpdate = true;
							}
						}
						else {
							break;
						}
					}
					else {
						break;
					}
				}
				if (found == wstring::npos) {
					if (text[0].length() > 0)
					{
						// ChSet5.txt の更新
						GetSettingPath(path);
						path += L"\\ChSet5.txt";
						HANDLE hFile = CreateFile(path.c_str(), GENERIC_WRITE, FILE_SHARE_WRITE, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
						if (hFile != INVALID_HANDLE_VALUE) {
							DWORD dwWrite;
							WriteFile(hFile, text[0].c_str(), (DWORD)text[0].length(), &dwWrite, NULL);
							CloseHandle(hFile);
							needToUpdate = true;
						}
					}
					if (needToUpdate) {
						sys->ReloadSetting();
						sys->notifyManager.AddNotify(NOTIFY_UPDATE_PROFILE);
					}
					resParam->param = CMD_SUCCESS;
				}
			}
		}
		break;
	case CMD2_EPG_SRV_ENUM_PG_ALL:
		if( sys->epgDB.IsInitialLoadingDataDone() == FALSE ){
			resParam->param = CMD_ERR_BUSY;
		}else{
			sys->epgDB.EnumEventAll(EnumPgAllCallback, resParam);
		}
		break;
	case CMD2_EPG_SRV_ENUM_REC_FOLDER:
		{
			vector<REC_FOLDER_INFO> resultList;
			wstring val;
			if (ReadVALUE(&val, cmdParam->data, cmdParam->dataSize, NULL)) {
				int i = 0;
				if (val.empty()) {
					GetRecFolderPath(val, i++);
				}
				do {
					if (val.empty()) continue;

					// C: などドライブ名のみの場合、末尾に "\\" が無ければ追加する。
					if (val.size() == 2 && val.compare(1, 1, L":") == 0) {
						val += L"\\";
					}

					REC_FOLDER_INFO rfi = {};
					rfi.recFolder = val;

					// UNC の場合、末尾に "\\" が無ければ追加する。GetDiskFreeSpaceEx の要求仕様。
					if (*val.rbegin() != L'\\' && val.compare(0, 2, L"\\\\") == 0) {
						val += L"\\";
					}

					ULARGE_INTEGER free;
					ULARGE_INTEGER total;
					if (GetDiskFreeSpaceEx(val.c_str(), &free, &total, NULL) != 0) {
						rfi.freeBytes = free.QuadPart;
						rfi.totalBytes = total.QuadPart;
						resultList.push_back(rfi);
					}
				} while (i && GetRecFolderPath(val, i++));
				resParam->data = NewWriteVALUE(resultList, resParam->dataSize);
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_ENUM_PLUGIN:
		{
			WORD mode;
			if( ReadVALUE(&mode, cmdParam->data, cmdParam->dataSize, NULL) && (mode == 1 || mode == 2) ){
				wstring path;
				GetModuleFolderPath(path);
				WIN32_FIND_DATA findData;
				//指定フォルダのファイル一覧取得
				HANDLE hFind = FindFirstFile((path + (mode == 1 ? L"\\RecName\\RecName*.dll" : L"\\Write\\Write*.dll")).c_str(), &findData);
				if( hFind != INVALID_HANDLE_VALUE ){
					vector<wstring> fileList;
					do{
						if( (findData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) == 0 && IsExt(findData.cFileName, L".dll") ){
							fileList.push_back(findData.cFileName);
						}
					}while( FindNextFile(hFind, &findData) );
					FindClose(hFind);
					resParam->data = NewWriteVALUE(fileList, resParam->dataSize);
					resParam->param = CMD_SUCCESS;
				}
			}
		}
		break;
	case CMD2_EPG_SRV_GET_CHG_CH_TVTEST:
		{
			LONGLONG key;
			if( ReadVALUE(&key, cmdParam->data, cmdParam->dataSize, NULL) ){
				CBlockLock lock(&sys->settingLock);
				TVTEST_CH_CHG_INFO info;
				info.chInfo.useSID = TRUE;
				info.chInfo.ONID = key >> 32 & 0xFFFF;
				info.chInfo.TSID = key >> 16 & 0xFFFF;
				info.chInfo.SID = key & 0xFFFF;
				info.chInfo.useBonCh = FALSE;
				vector<DWORD> idList = sys->reserveManager.GetSupportServiceTuner(info.chInfo.ONID, info.chInfo.TSID, info.chInfo.SID);
				for( int i = (int)idList.size() - 1; i >= 0; i-- ){
					info.bonDriver = sys->reserveManager.GetTunerBonFileName(idList[i]);
					for( size_t j = 0; j < sys->tvtestUseBon.size(); j++ ){
						if( CompareNoCase(sys->tvtestUseBon[j], info.bonDriver) == 0 ){
							if( sys->reserveManager.IsOpenTuner(idList[i]) == false ){
								info.chInfo.useBonCh = TRUE;
								sys->reserveManager.GetTunerCh(idList[i], info.chInfo.ONID, info.chInfo.TSID, info.chInfo.SID, &info.chInfo.space, &info.chInfo.ch);
							}
							break;
						}
					}
					if( info.chInfo.useBonCh ){
						resParam->data = NewWriteVALUE(info, resParam->dataSize);
						resParam->param = CMD_SUCCESS;
						break;
					}
				}
			}
		}
		break;
	case CMD2_EPG_SRV_PROFILE_UPDATE:
		{
			sys->notifyManager.AddNotify(NOTIFY_UPDATE_PROFILE);
		}
		break;
	case CMD2_EPG_SRV_NWTV_SET_CH:
		{
			SET_CH_INFO val;
			if( ReadVALUE(&val, cmdParam->data, cmdParam->dataSize, NULL) && val.useSID ){
				CBlockLock lock(&sys->settingLock);
				vector<DWORD> idList = sys->reserveManager.GetSupportServiceTuner(val.ONID, val.TSID, val.SID);
				vector<DWORD> idUseList;
				for( int i = (int)idList.size() - 1; i >= 0; i-- ){
					wstring bonDriver = sys->reserveManager.GetTunerBonFileName(idList[i]);
					for( size_t j = 0; j < sys->tvtestUseBon.size(); j++ ){
						if( CompareNoCase(sys->tvtestUseBon[j], bonDriver) == 0 ){
							idUseList.push_back(idList[i]);
							break;
						}
					}
				}
				if( sys->reserveManager.SetNWTVCh(sys->nwtvUdp, sys->nwtvTcp, val, idUseList) ){
					resParam->param = CMD_SUCCESS;
				}
			}
		}
		break;
	case CMD2_EPG_SRV_NWTV_CLOSE:
		{
			if( sys->reserveManager.CloseNWTV() ){
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_NWTV_MODE:
		{
			DWORD val;
			if( ReadVALUE(&val, cmdParam->data, cmdParam->dataSize, NULL) ){
				CBlockLock lock(&sys->settingLock);
				sys->nwtvUdp = val == 1 || val == 3;
				sys->nwtvTcp = val == 2 || val == 3;
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_NWPLAY_OPEN:
		{
			wstring val;
			DWORD id;
			if( ReadVALUE(&val, cmdParam->data, cmdParam->dataSize, NULL) && sys->streamingManager.OpenFile(val.c_str(), &id) ){
				resParam->data = NewWriteVALUE(id, resParam->dataSize);
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_NWPLAY_CLOSE:
		{
			DWORD val;
			if( ReadVALUE(&val, cmdParam->data, cmdParam->dataSize, NULL) && sys->streamingManager.CloseFile(val) ){
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_NWPLAY_PLAY:
		{
			DWORD val;
			if( ReadVALUE(&val, cmdParam->data, cmdParam->dataSize, NULL) && sys->streamingManager.StartSend(val) ){
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_NWPLAY_STOP:
		{
			DWORD val;
			if( ReadVALUE(&val, cmdParam->data, cmdParam->dataSize, NULL) && sys->streamingManager.StopSend(val) ){
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_NWPLAY_GET_POS:
		{
			NWPLAY_POS_CMD val;
			if( ReadVALUE(&val, cmdParam->data, cmdParam->dataSize, NULL) && sys->streamingManager.GetPos(&val) ){
				resParam->data = NewWriteVALUE(val, resParam->dataSize);
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_NWPLAY_SET_POS:
		{
			NWPLAY_POS_CMD val;
			if( ReadVALUE(&val, cmdParam->data, cmdParam->dataSize, NULL) && sys->streamingManager.SetPos(&val) ){
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_NWPLAY_SET_IP:
		{
			NWPLAY_PLAY_INFO val;
			if( ReadVALUE(&val, cmdParam->data, cmdParam->dataSize, NULL) && sys->streamingManager.SetIP(&val) ){
				resParam->data = NewWriteVALUE(val, resParam->dataSize);
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_NWPLAY_TF_OPEN:
		{
			DWORD val;
			NWPLAY_TIMESHIFT_INFO resVal;
			DWORD ctrlID;
			DWORD processID;
			if( ReadVALUE(&val, cmdParam->data, cmdParam->dataSize, NULL) &&
			    sys->reserveManager.GetRecFilePath(val, resVal.filePath, &ctrlID, &processID) &&
			    sys->streamingManager.OpenTimeShift(resVal.filePath.c_str(), &resVal.ctrlID) ){
				resParam->data = NewWriteVALUE(resVal, resParam->dataSize);
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_GET_NETWORK_PATH:
		{
			wstring val, resVal;
			if (ReadVALUE(&val, cmdParam->data, cmdParam->dataSize, NULL) &&
				GetNetworkPath(val, resVal)) {
				resParam->data = NewWriteVALUE(resVal, resParam->dataSize);
				resParam->param = CMD_SUCCESS;
			}
		}
		break;

	////////////////////////////////////////////////////////////
	//CMD_VER対応コマンド
	case CMD2_EPG_SRV_ENUM_RESERVE2:
		{
			WORD ver;
			if( ReadVALUE(&ver, cmdParam->data, cmdParam->dataSize, NULL) ){
				//ver>=5では録画予定ファイル名も返す
				resParam->data = NewWriteVALUE2WithVersion(ver, sys->reserveManager.GetReserveDataAll(ver >= 5), resParam->dataSize);
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_GET_RESERVE2:
		{
			WORD ver;
			DWORD readSize;
			if( ReadVALUE(&ver, cmdParam->data, cmdParam->dataSize, &readSize) ){
				DWORD reserveID;
				if( ReadVALUE2(ver, &reserveID, cmdParam->data + readSize, cmdParam->dataSize - readSize, NULL) ){
					RESERVE_DATA info;
					if( sys->reserveManager.GetReserveData(reserveID, &info) ){
						resParam->data = NewWriteVALUE2WithVersion(ver, info, resParam->dataSize);
						resParam->param = CMD_SUCCESS;
					}
				}
			}
		}
		break;
	case CMD2_EPG_SRV_ADD_RESERVE2:
		{
			WORD ver;
			DWORD readSize;
			if( ReadVALUE(&ver, cmdParam->data, cmdParam->dataSize, &readSize) ){
				vector<RESERVE_DATA> list;
				if( ReadVALUE2(ver, &list, cmdParam->data + readSize, cmdParam->dataSize - readSize, NULL) &&
				    sys->reserveManager.AddReserveData(list) ){
					resParam->data = NewWriteVALUE(ver, resParam->dataSize);
					resParam->param = CMD_SUCCESS;
				}
			}
		}
		break;
	case CMD2_EPG_SRV_CHG_RESERVE2:
		{
			WORD ver;
			DWORD readSize;
			if( ReadVALUE(&ver, cmdParam->data, cmdParam->dataSize, &readSize) ){
				vector<RESERVE_DATA> list;
				if( ReadVALUE2(ver, &list, cmdParam->data + readSize, cmdParam->dataSize - readSize, NULL) &&
				    sys->reserveManager.ChgReserveData(list) ){
					resParam->data = NewWriteVALUE(ver, resParam->dataSize);
					resParam->param = CMD_SUCCESS;
				}
			}
		}
		break;
	case CMD2_EPG_SRV_ADDCHK_RESERVE2:
		resParam->param = CMD_NON_SUPPORT;
		break;
	case CMD2_EPG_SRV_GET_EPG_FILETIME2:
		resParam->param = CMD_NON_SUPPORT;
		break;
	case CMD2_EPG_SRV_GET_EPG_FILE2:
		resParam->param = CMD_NON_SUPPORT;
		break;
	case CMD2_EPG_SRV_SEARCH_PG2:
		if( sys->epgDB.IsInitialLoadingDataDone() == FALSE ){
			resParam->param = CMD_ERR_BUSY;
		}else{
			DWORD readSize;
			if( ReadVALUE(&CommitedVerForNewCMD, cmdParam->data, cmdParam->dataSize, &readSize) ){
				vector<EPGDB_SEARCH_KEY_INFO> key;
				if( ReadVALUE2(CommitedVerForNewCMD, &key, cmdParam->data + readSize, cmdParam->dataSize - readSize, NULL) ){
					sys->epgDB.SearchEpg(&key, SearchPg2Callback, resParam);
				}
			}
		}
		break;
	case CMD2_EPG_SRV_SEARCH_PG_BYKEY2:
		if( sys->epgDB.IsInitialLoadingDataDone() == FALSE ){
			resParam->param = CMD_ERR_BUSY;
		}else{
			DWORD readSize;
			if( ReadVALUE(&CommitedVerForNewCMD, cmdParam->data, cmdParam->dataSize, &readSize) ){
				vector<EPGDB_SEARCH_KEY_INFO> key;
				if( ReadVALUE2(CommitedVerForNewCMD, &key, cmdParam->data + readSize, cmdParam->dataSize - readSize, NULL) ){
					sys->epgDB.SearchEpgByKey(&key, SearchPgByKey2Callback, resParam);
				}
			}
		}
		break;
	case CMD2_EPG_SRV_ENUM_AUTO_ADD2:
		{
			WORD ver;
			if( ReadVALUE(&ver, cmdParam->data, cmdParam->dataSize, NULL) ){
				vector<EPG_AUTO_ADD_DATA> val = sys->GetAutoAddList();
				resParam->data = NewWriteVALUE2WithVersion(ver, val, resParam->dataSize);
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_ADD_AUTO_ADD2:
		{
			WORD ver;
			DWORD readSize;
			if( ReadVALUE(&ver, cmdParam->data, cmdParam->dataSize, &readSize) ){
				vector<EPG_AUTO_ADD_DATA> val;
				if( ReadVALUE2(ver, &val, cmdParam->data + readSize, cmdParam->dataSize - readSize, NULL) ){
					sys->AddAutoAdd(val);
					resParam->data = NewWriteVALUE(ver, resParam->dataSize);
					resParam->param = CMD_SUCCESS;
				}
			}
		}
		break;
	case CMD2_EPG_SRV_CHG_AUTO_ADD2:
		{
			WORD ver;
			DWORD readSize;
			if( ReadVALUE(&ver, cmdParam->data, cmdParam->dataSize, &readSize) ){
				vector<EPG_AUTO_ADD_DATA> val;
				if( ReadVALUE2(ver, &val, cmdParam->data + readSize, cmdParam->dataSize - readSize, NULL) ){
					sys->ChgAutoAdd(val);
					resParam->data = NewWriteVALUE(ver, resParam->dataSize);
					resParam->param = CMD_SUCCESS;
				}
			}
		}
		break;
	case CMD2_EPG_SRV_ENUM_MANU_ADD2:
		{
			WORD ver;
			if( ReadVALUE(&ver, cmdParam->data, cmdParam->dataSize, NULL) ){
				vector<MANUAL_AUTO_ADD_DATA> val;
				{
					CBlockLock lock(&sys->settingLock);
					map<DWORD, MANUAL_AUTO_ADD_DATA>::const_iterator itr;
					for( itr = sys->manualAutoAdd.GetMap().begin(); itr != sys->manualAutoAdd.GetMap().end(); itr++ ){
						val.push_back(itr->second);
					}
				}
				resParam->data = NewWriteVALUE2WithVersion(ver, val, resParam->dataSize);
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_ADD_MANU_ADD2:
		{
			WORD ver;
			DWORD readSize;
			if( ReadVALUE(&ver, cmdParam->data, cmdParam->dataSize, &readSize) ){
				vector<MANUAL_AUTO_ADD_DATA> val;
				if( ReadVALUE2(ver, &val, cmdParam->data + readSize, cmdParam->dataSize - readSize, NULL) ){
					{
						CBlockLock lock(&sys->settingLock);
						for( size_t i = 0; i < val.size(); i++ ){
							val[i].dataID = sys->manualAutoAdd.AddData(val[i]);
						}
						sys->manualAutoAdd.SaveText();
					}
					bool addReserve = false;
					for( size_t i = 0; i < val.size(); i++ ){
						addReserve |= sys->AutoAddReserveProgram(val[i], true);
					}
					sys->notifyManager.AddNotify(NOTIFY_UPDATE_AUTOADD_MANUAL);
					if( addReserve ){
						sys->reserveManager.AddNotifyAndPostBat(NOTIFY_UPDATE_RESERVE_INFO);
					}
					resParam->data = NewWriteVALUE(ver, resParam->dataSize);
					resParam->param = CMD_SUCCESS;
				}
			}
		}
		break;
	case CMD2_EPG_SRV_CHG_MANU_ADD2:
		{
			WORD ver;
			DWORD readSize;
			if( ReadVALUE(&ver, cmdParam->data, cmdParam->dataSize, &readSize) ){
				vector<MANUAL_AUTO_ADD_DATA> val;
				if( ReadVALUE2(ver, &val, cmdParam->data + readSize, cmdParam->dataSize - readSize, NULL) ){
					{
						CBlockLock lock(&sys->settingLock);
						for( size_t i = 0; i < val.size(); i++ ){
							if( sys->manualAutoAdd.ChgData(val[i]) == false ){
								val.erase(val.begin() + (i--));
							}
						}
						sys->manualAutoAdd.SaveText();
					}
					bool addReserve = false;
					for( size_t i = 0; i < val.size(); i++ ){
						addReserve |= sys->AutoAddReserveProgram(val[i], true);
					}
					sys->notifyManager.AddNotify(NOTIFY_UPDATE_AUTOADD_MANUAL);
					if( addReserve ){
						sys->reserveManager.AddNotifyAndPostBat(NOTIFY_UPDATE_RESERVE_INFO);
					}
					resParam->data = NewWriteVALUE(ver, resParam->dataSize);
					resParam->param = CMD_SUCCESS;
				}
			}
		}
		break;
	case CMD2_EPG_SRV_ENUM_RECINFO2:
	case CMD2_EPG_SRV_ENUM_RECINFO_BASIC2:
		{
			WORD ver;
			if( ReadVALUE(&ver, cmdParam->data, cmdParam->dataSize, NULL) ){
				sys->UpdateRecFileInfo(); // nekopanda版 (8bae159)
				resParam->data = NewWriteVALUE2WithVersion(ver,
					sys->reserveManager.GetRecFileInfoAll(cmdParam->param == CMD2_EPG_SRV_ENUM_RECINFO2), resParam->dataSize);
				resParam->param = CMD_SUCCESS;
			}
		}
		break;
	case CMD2_EPG_SRV_GET_RECINFO_LIST2:
		{
			OutputDebugString(L"CMD2_EPG_SRV_GET_RECINFO_LIST2\r\n");
			WORD ver;
			DWORD readSize;
			if( ReadVALUE(&ver, cmdParam->data, cmdParam->dataSize, &readSize) ){
				vector<DWORD> idList;
				if( ReadVALUE2(ver, &idList, cmdParam->data + readSize, cmdParam->dataSize - readSize, NULL) ){
					resParam->data = NewWriteVALUE2WithVersion(ver,
						sys->reserveManager.GetRecFileInfoList(idList), resParam->dataSize);
					resParam->param = CMD_SUCCESS;
				}
			}
		}
		break;
	case CMD2_EPG_SRV_GET_RECINFO2:
		{
			WORD ver;
			DWORD readSize;
			if( ReadVALUE(&ver, cmdParam->data, cmdParam->dataSize, &readSize) ){
				REC_FILE_INFO info;
				if( ReadVALUE2(ver, &info.id, cmdParam->data + readSize, cmdParam->dataSize - readSize, NULL) &&
				    sys->reserveManager.GetRecFileInfo(info.id, &info) ){
					resParam->data = NewWriteVALUE2WithVersion(ver, info, resParam->dataSize);
					resParam->param = CMD_SUCCESS;
				}
			}
		}
		break;
	case CMD2_EPG_SRV_CHG_PROTECT_RECINFO2:
		{
			WORD ver;
			DWORD readSize;
			if( ReadVALUE(&ver, cmdParam->data, cmdParam->dataSize, &readSize) ){
				vector<REC_FILE_INFO> list;
				if( ReadVALUE2(ver, &list, cmdParam->data + readSize, cmdParam->dataSize - readSize, NULL) ){
					sys->reserveManager.ChgProtectRecFileInfo(list);
					resParam->data = NewWriteVALUE(ver, resParam->dataSize);
					resParam->param = CMD_SUCCESS;
				}
			}
		}
		break;
    case CMD2_EPG_SRV_FILE_COPY2:
        {
            WORD ver;
            DWORD readSize;
            if( ReadVALUE(&ver, cmdParam->data, cmdParam->dataSize, &readSize) ){
                vector<wstring> list;
                if( ReadVALUE2(ver, &list, cmdParam->data + readSize, cmdParam->dataSize - readSize, NULL) ){
                    vector<FILE_DATA> result;
                    vector<wstring>::iterator itr;
                    for( itr = list.begin(); itr != list.end(); itr++ ){
                        FILE_DATA data1;
                        data1.Name = *itr;

                        wstring path=L"";
                        if( CompareNoCase(*itr, L"ChSet5.txt") == 0 ){
                            GetSettingPath(path);
                            path += L"\\" + *itr;
                        }else if( CompareNoCase(*itr, L"EpgTimerSrv.ini") == 0 ){
                            GetEpgTimerSrvIniPath(path);
                        }else if( CompareNoCase(*itr, L"Common.ini") == 0 ){
                            GetCommonIniPath(path);
                        }else if( CompareNoCase(*itr, L"EpgDataCap_Bon.ini") == 0 
                            || CompareNoCase(*itr, L"BonCtrl.ini") == 0
                            || CompareNoCase(*itr, L"Bitrate.ini") == 0 ){
                            GetModuleFolderPath(path);
                            path += L"\\" + *itr;
                        }

                        if(path != L""){
                            HANDLE hFile = CreateFile(path.c_str(), GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
                            if( hFile != INVALID_HANDLE_VALUE ){
                                DWORD dwFileSize = GetFileSize(hFile, NULL);
                                if( dwFileSize != INVALID_FILE_SIZE && dwFileSize != 0 ){
                                    DWORD dwRead;
                                    BYTE* buff = new BYTE[dwFileSize];
                                    if( ReadFile(hFile, buff, dwFileSize, &dwRead, NULL) && dwRead != 0 ){
                                        data1.Size = dwFileSize;
                                        data1.Data = buff;
                                    }
                                    else{
                                        delete[] buff;
                                    }
                                }
                                CloseHandle(hFile);
                            }
                        }

                        result.push_back(data1);
                    }

                    resParam->data = NewWriteVALUE2WithVersion(ver, result, resParam->dataSize);
                    resParam->param = CMD_SUCCESS;

                    vector<FILE_DATA>::iterator itr2;
                    for( itr2 = result.begin(); itr2 != result.end(); itr2++ ){
                        delete[] itr2->Data;
                        itr2->Data = NULL;
                    }
                }
            }
        }
        break;
	case CMD2_EPG_SRV_GET_STATUS_NOTIFY2:
		{
			WORD ver;
			DWORD readSize;
			if( ReadVALUE(&ver, cmdParam->data, cmdParam->dataSize, &readSize) ){
				DWORD count;
				if( ReadVALUE2(ver, &count, cmdParam->data + readSize, cmdParam->dataSize - readSize, NULL) ){
					NOTIFY_SRV_INFO info;
					if( sys->notifyManager.GetNotify(&info, count) ){
						resParam->data = NewWriteVALUE2WithVersion(ver, info, resParam->dataSize);
						resParam->param = CMD_SUCCESS;
					}else{
						resParam->param = CMD_NO_RES;
					}
				}
			}
		}
		break;
#if 1
	////////////////////////////////////////////////////////////
	//旧バージョン互換コマンド
	case CMD_EPG_SRV_GET_RESERVE_INFO:
		{
			resParam->param = OLD_CMD_ERR;
			DWORD reserveID;
			if( ReadVALUE(&reserveID, cmdParam->data, cmdParam->dataSize, NULL) ){
				RESERVE_DATA info;
				if( sys->reserveManager.GetReserveData(reserveID, &info) ){
					resParam->data = DeprecatedNewWriteVALUE(info, resParam->dataSize);
					resParam->param = OLD_CMD_SUCCESS;
				}
			}
		}
		break;
	case CMD_EPG_SRV_ADD_RESERVE:
		{
			resParam->param = OLD_CMD_ERR;
			vector<RESERVE_DATA> list(1);
			if( DeprecatedReadVALUE(&list.back(), cmdParam->data, cmdParam->dataSize) &&
			    sys->reserveManager.AddReserveData(list) ){
				resParam->param = OLD_CMD_SUCCESS;
			}
		}
		break;
	case CMD_EPG_SRV_DEL_RESERVE:
		{
			resParam->param = OLD_CMD_ERR;
			RESERVE_DATA item;
			if( DeprecatedReadVALUE(&item, cmdParam->data, cmdParam->dataSize) ){
				vector<DWORD> list(1, item.reserveID);
				sys->reserveManager.DelReserveData(list);
				resParam->param = OLD_CMD_SUCCESS;
			}
		}
		break;
	case CMD_EPG_SRV_CHG_RESERVE:
		{
			resParam->param = OLD_CMD_ERR;
			vector<RESERVE_DATA> list(1);
			if( DeprecatedReadVALUE(&list.back(), cmdParam->data, cmdParam->dataSize) &&
			    sys->reserveManager.ChgReserveData(list) ){
				resParam->param = OLD_CMD_SUCCESS;
			}
		}

		break;
	case CMD_EPG_SRV_ADD_AUTO_ADD:
		{
			resParam->param = OLD_CMD_ERR;
			EPG_AUTO_ADD_DATA item;
			if( DeprecatedReadVALUE(&item, cmdParam->data, cmdParam->dataSize) ){
				{
					CBlockLock lock(&sys->settingLock);
					item.dataID = sys->epgAutoAdd.AddData(item);
					sys->epgAutoAdd.SaveText();
				}
				resParam->param = OLD_CMD_SUCCESS;
				sys->AutoAddReserveEPG(item);
				sys->notifyManager.AddNotify(NOTIFY_UPDATE_AUTOADD_EPG);
			}
		}
		break;
	case CMD_EPG_SRV_DEL_AUTO_ADD:
		{
			resParam->param = OLD_CMD_ERR;
			EPG_AUTO_ADD_DATA item;
			if( DeprecatedReadVALUE(&item, cmdParam->data, cmdParam->dataSize) ){
				CBlockLock lock(&sys->settingLock);
				sys->epgAutoAdd.DelData(item.dataID);
				sys->epgAutoAdd.SaveText();
				resParam->param = OLD_CMD_SUCCESS;
				sys->notifyManager.AddNotify(NOTIFY_UPDATE_AUTOADD_EPG);
			}
		}
		break;
	case CMD_EPG_SRV_CHG_AUTO_ADD:
		{
			resParam->param = OLD_CMD_ERR;
			EPG_AUTO_ADD_DATA item;
			if( DeprecatedReadVALUE(&item, cmdParam->data, cmdParam->dataSize) ){
				{
					CBlockLock lock(&sys->settingLock);
					sys->epgAutoAdd.ChgData(item);
					sys->epgAutoAdd.SaveText();
				}
				resParam->param = OLD_CMD_SUCCESS;
				sys->AutoAddReserveEPG(item);
				sys->notifyManager.AddNotify(NOTIFY_UPDATE_AUTOADD_EPG);
			}
		}
		break;
	case CMD_EPG_SRV_SEARCH_PG_FIRST:
		{
			resParam->param = OLD_CMD_ERR;
			if( sys->epgDB.IsInitialLoadingDataDone() == FALSE ){
				resParam->param = CMD_ERR_BUSY;
			}else{
				EPG_AUTO_ADD_DATA item;
				if( DeprecatedReadVALUE(&item, cmdParam->data, cmdParam->dataSize) ){
					vector<EPGDB_SEARCH_KEY_INFO> key(1, item.searchInfo);
					vector<CEpgDBManager::SEARCH_RESULT_EVENT_DATA> val;
					if( sys->epgDB.SearchEpg(&key, &val) != FALSE ){
						sys->oldSearchList[tcpFlag].clear();
						sys->oldSearchList[tcpFlag].resize(val.size());
						for( size_t i = 0; i < val.size(); i++ ){
							sys->oldSearchList[tcpFlag][val.size() - i - 1].DeepCopy(val[i].info);
						}
						if( sys->oldSearchList[tcpFlag].empty() == false ){
							resParam->data = DeprecatedNewWriteVALUE(sys->oldSearchList[tcpFlag].back(), resParam->dataSize);
							sys->oldSearchList[tcpFlag].pop_back();
							resParam->param = sys->oldSearchList[tcpFlag].empty() ? OLD_CMD_SUCCESS : OLD_CMD_NEXT;
						}
					}
				}
			}
		}
		break;
	case CMD_EPG_SRV_SEARCH_PG_NEXT:
		{
			resParam->param = OLD_CMD_ERR;
			if( sys->oldSearchList[tcpFlag].empty() == false ){
				resParam->data = DeprecatedNewWriteVALUE(sys->oldSearchList[tcpFlag].back(), resParam->dataSize);
				sys->oldSearchList[tcpFlag].pop_back();
				resParam->param = sys->oldSearchList[tcpFlag].empty() ? OLD_CMD_SUCCESS : OLD_CMD_NEXT;
			}
		}
		break;
	//旧バージョン互換コマンドここまで
#endif
	default:
		resParam->param = CMD_NON_SUPPORT;
		break;
	}

	QueryPerformanceCounter(&endTime);
	sys->OutputDebugCmd(cmdParam->param, resParam->param, endTime.QuadPart - startTime.QuadPart);
	return 0;
}

void CEpgTimerSrvMain::OutputDebugCmd(DWORD cmd, DWORD result, LONGLONG ticks)
{
	static float millisec = 0.0f;
	BOOL oldCmd = false;
	WCHAR tempCmd[80];
	WCHAR tempResult[80];
	LPCWSTR strCmd = tempCmd;
	LPCWSTR strResult = tempResult;

	if( millisec == 0.0f ){
		LARGE_INTEGER frequency;
		QueryPerformanceFrequency(&frequency);
		millisec = (frequency.QuadPart == 0) ? 1.0f : 1000.0f / frequency.QuadPart;
	}

	if( cmd == CMD2_EPG_SRV_GET_STATUS_NOTIFY2 && result == CMD_NO_RES ){
		return;
	}

	switch( cmd ){
	case CMD2_EPG_SRV_RELOAD_EPG: strCmd = L"CMD2_EPG_SRV_RELOAD_EPG"; break;
	case CMD2_EPG_SRV_RELOAD_SETTING: strCmd = L"CMD2_EPG_SRV_RELOAD_SETTING"; break;
	case CMD2_EPG_SRV_CLOSE: strCmd = L"CMD2_EPG_SRV_CLOSE"; break;
	case CMD2_EPG_SRV_REGIST_GUI: strCmd = L"CMD2_EPG_SRV_REGIST_GUI"; break;
	case CMD2_EPG_SRV_UNREGIST_GUI: strCmd = L"CMD2_EPG_SRV_UNREGIST_GUI"; break;
	case CMD2_EPG_SRV_REGIST_GUI_TCP: strCmd = L"CMD2_EPG_SRV_REGIST_GUI_TCP"; break;
	case CMD2_EPG_SRV_UNREGIST_GUI_TCP: strCmd = L"CMD2_EPG_SRV_UNREGIST_GUI_TCP"; break;
	case CMD2_EPG_SRV_ISREGIST_GUI_TCP: strCmd = L"CMD2_EPG_SRV_ISREGIST_GUI_TCP"; break;
	case CMD2_EPG_SRV_ENUM_RESERVE: strCmd = L"CMD2_EPG_SRV_ENUM_RESERVE"; break;
	case CMD2_EPG_SRV_GET_RESERVE: strCmd = L"CMD2_EPG_SRV_GET_RESERVE"; break;
	case CMD2_EPG_SRV_ADD_RESERVE: strCmd = L"CMD2_EPG_SRV_ADD_RESERVE"; break;
	case CMD2_EPG_SRV_DEL_RESERVE: strCmd = L"CMD2_EPG_SRV_DEL_RESERVE"; break;
	case CMD2_EPG_SRV_CHG_RESERVE: strCmd = L"CMD2_EPG_SRV_CHG_RESERVE"; break;
	case CMD2_EPG_SRV_ENUM_RECINFO: strCmd = L"CMD2_EPG_SRV_ENUM_RECINFO"; break;
	case CMD2_EPG_SRV_DEL_RECINFO: strCmd = L"CMD2_EPG_SRV_DEL_RECINFO"; break;
	case CMD2_EPG_SRV_ENUM_SERVICE: strCmd = L"CMD2_EPG_SRV_ENUM_SERVICE"; break;
	case CMD2_EPG_SRV_ENUM_PG_INFO: strCmd = L"CMD2_EPG_SRV_ENUM_PG_INFO"; break;
	case CMD2_EPG_SRV_SEARCH_PG: strCmd = L"CMD2_EPG_SRV_SEARCH_PG"; break;
	case CMD2_EPG_SRV_GET_PG_INFO: strCmd = L"CMD2_EPG_SRV_GET_PG_INFO"; break;
	case CMD2_EPG_SRV_CHK_SUSPEND: strCmd = L"CMD2_EPG_SRV_CHK_SUSPEND"; break;
	case CMD2_EPG_SRV_SUSPEND: strCmd = L"CMD2_EPG_SRV_SUSPEND"; break;
	case CMD2_EPG_SRV_REBOOT: strCmd = L"CMD2_EPG_SRV_REBOOT"; break;
	case CMD2_EPG_SRV_EPG_CAP_NOW: strCmd = L"CMD2_EPG_SRV_EPG_CAP_NOW"; break;
	case CMD2_EPG_SRV_ENUM_AUTO_ADD: strCmd = L"CMD2_EPG_SRV_ENUM_AUTO_ADD"; break;
	case CMD2_EPG_SRV_ADD_AUTO_ADD: strCmd = L"CMD2_EPG_SRV_ADD_AUTO_ADD"; break;
	case CMD2_EPG_SRV_DEL_AUTO_ADD: strCmd = L"CMD2_EPG_SRV_DEL_AUTO_ADD"; break;
	case CMD2_EPG_SRV_CHG_AUTO_ADD: strCmd = L"CMD2_EPG_SRV_CHG_AUTO_ADD"; break;
	case CMD2_EPG_SRV_ENUM_MANU_ADD: strCmd = L"CMD2_EPG_SRV_ENUM_MANU_ADD"; break;
	case CMD2_EPG_SRV_ADD_MANU_ADD: strCmd = L"CMD2_EPG_SRV_ADD_MANU_ADD"; break;
	case CMD2_EPG_SRV_DEL_MANU_ADD: strCmd = L"CMD2_EPG_SRV_DEL_MANU_ADD"; break;
	case CMD2_EPG_SRV_CHG_MANU_ADD: strCmd = L"CMD2_EPG_SRV_CHG_MANU_ADD"; break;
	case CMD2_EPG_SRV_ENUM_TUNER_RESERVE: strCmd = L"CMD2_EPG_SRV_ENUM_TUNER_RESERVE"; break;
	case CMD2_EPG_SRV_FILE_COPY: strCmd = L"CMD2_EPG_SRV_FILE_COPY"; break;
	case CMD2_EPG_SRV_UPDATE_SETTING: strCmd = L"CMD2_EPG_SRV_UPDATE_SETTING"; break;
	case CMD2_EPG_SRV_ENUM_PG_ALL: strCmd = L"CMD2_EPG_SRV_ENUM_PG_ALL"; break;
	case CMD2_EPG_SRV_ENUM_REC_FOLDER: strCmd = L"CMD2_EPG_SRV_ENUM_REC_FOLDER"; break;
	case CMD2_EPG_SRV_ENUM_PLUGIN: strCmd = L"CMD2_EPG_SRV_ENUM_PLUGIN"; break;
	case CMD2_EPG_SRV_GET_CHG_CH_TVTEST: strCmd = L"CMD2_EPG_SRV_GET_CHG_CH_TVTEST"; break;
	case CMD2_EPG_SRV_PROFILE_UPDATE: strCmd = L"CMD2_EPG_SRV_PROFILE_UPDATE"; break;
	case CMD2_EPG_SRV_NWTV_SET_CH: strCmd = L"CMD2_EPG_SRV_NWTV_SET_CH"; break;
	case CMD2_EPG_SRV_NWTV_CLOSE: strCmd = L"CMD2_EPG_SRV_NWTV_CLOSE"; break;
	case CMD2_EPG_SRV_NWTV_MODE: strCmd = L"CMD2_EPG_SRV_NWTV_MODE"; break;
	case CMD2_EPG_SRV_NWPLAY_OPEN: strCmd = L"CMD2_EPG_SRV_NWPLAY_OPEN"; break;
	case CMD2_EPG_SRV_NWPLAY_CLOSE: strCmd = L"CMD2_EPG_SRV_NWPLAY_CLOSE"; break;
	case CMD2_EPG_SRV_NWPLAY_PLAY: strCmd = L"CMD2_EPG_SRV_NWPLAY_PLAY"; break;
	case CMD2_EPG_SRV_NWPLAY_STOP: strCmd = L"CMD2_EPG_SRV_NWPLAY_STOP"; break;
	case CMD2_EPG_SRV_NWPLAY_GET_POS: strCmd = L"CMD2_EPG_SRV_NWPLAY_GET_POS"; break;
	case CMD2_EPG_SRV_NWPLAY_SET_POS: strCmd = L"CMD2_EPG_SRV_NWPLAY_SET_POS"; break;
	case CMD2_EPG_SRV_NWPLAY_SET_IP: strCmd = L"CMD2_EPG_SRV_NWPLAY_SET_IP"; break;
	case CMD2_EPG_SRV_NWPLAY_TF_OPEN: strCmd = L"CMD2_EPG_SRV_NWPLAY_TF_OPEN"; break;
	case CMD2_EPG_SRV_GET_NETWORK_PATH: strCmd = L"CMD2_EPG_SRV_GET_NETWORK_PATH"; break;

	////////////////////////////////////////////////////////////
	//CMD_VER対応コマンド
	case CMD2_EPG_SRV_ENUM_RESERVE2: strCmd = L"CMD2_EPG_SRV_ENUM_RESERVE2"; break;
	case CMD2_EPG_SRV_GET_RESERVE2: strCmd = L"CMD2_EPG_SRV_GET_RESERVE2"; break;
	case CMD2_EPG_SRV_ADD_RESERVE2: strCmd = L"CMD2_EPG_SRV_ADD_RESERVE2"; break;
	case CMD2_EPG_SRV_CHG_RESERVE2: strCmd = L"CMD2_EPG_SRV_CHG_RESERVE2"; break;
	case CMD2_EPG_SRV_ADDCHK_RESERVE2: strCmd = L"CMD2_EPG_SRV_ADDCHK_RESERVE2"; break;
	case CMD2_EPG_SRV_GET_EPG_FILETIME2: strCmd = L"CMD2_EPG_SRV_GET_EPG_FILETIME2"; break;
	case CMD2_EPG_SRV_GET_EPG_FILE2: strCmd = L"CMD2_EPG_SRV_GET_EPG_FILE2"; break;
	case CMD2_EPG_SRV_SEARCH_PG2: strCmd = L"CMD2_EPG_SRV_SEARCH_PG2"; break;
	case CMD2_EPG_SRV_SEARCH_PG_BYKEY2: strCmd = L"CMD2_EPG_SRV_SEARCH_PG_BYKEY2"; break;
	case CMD2_EPG_SRV_ENUM_AUTO_ADD2: strCmd = L"CMD2_EPG_SRV_ENUM_AUTO_ADD2"; break;
	case CMD2_EPG_SRV_ADD_AUTO_ADD2: strCmd = L"CMD2_EPG_SRV_ADD_AUTO_ADD2"; break;
	case CMD2_EPG_SRV_CHG_AUTO_ADD2: strCmd = L"CMD2_EPG_SRV_CHG_AUTO_ADD2"; break;
	case CMD2_EPG_SRV_ENUM_MANU_ADD2: strCmd = L"CMD2_EPG_SRV_ENUM_MANU_ADD2"; break;
	case CMD2_EPG_SRV_ADD_MANU_ADD2: strCmd = L"CMD2_EPG_SRV_ADD_MANU_ADD2"; break;
	case CMD2_EPG_SRV_CHG_MANU_ADD2: strCmd = L"CMD2_EPG_SRV_CHG_MANU_ADD2"; break;
	case CMD2_EPG_SRV_ENUM_RECINFO2: strCmd = L"CMD2_EPG_SRV_ENUM_RECINFO2"; break;
	case CMD2_EPG_SRV_ENUM_RECINFO_BASIC2: strCmd = L"CMD2_EPG_SRV_ENUM_RECINFO_BASIC2"; break;
	case CMD2_EPG_SRV_GET_RECINFO2: strCmd = L"CMD2_EPG_SRV_GET_RECINFO2"; break;
	case CMD2_EPG_SRV_CHG_PROTECT_RECINFO2: strCmd = L"CMD2_EPG_SRV_CHG_PROTECT_RECINFO2"; break;
    case CMD2_EPG_SRV_FILE_COPY2: strCmd = L"CMD2_EPG_SRV_FILE_COPY2"; break;
	case CMD2_EPG_SRV_GET_STATUS_NOTIFY2: strCmd = L"CMD2_EPG_SRV_GET_STATUS_NOTIFY2"; break;

	////////////////////////////////////////////////////////////
	//旧バージョン互換コマンド
	case CMD_EPG_SRV_GET_RESERVE_INFO: strCmd = L"CMD_EPG_SRV_GET_RESERVE_INFO"; oldCmd = true; break;
	case CMD_EPG_SRV_ADD_RESERVE: strCmd = L"CMD_EPG_SRV_ADD_RESERVE"; oldCmd = true; break;
	case CMD_EPG_SRV_DEL_RESERVE: strCmd = L"CMD_EPG_SRV_DEL_RESERVE"; oldCmd = true; break;
	case CMD_EPG_SRV_CHG_RESERVE: strCmd = L"CMD_EPG_SRV_CHG_RESERVE"; oldCmd = true; break;
	case CMD_EPG_SRV_ADD_AUTO_ADD: strCmd = L"CMD_EPG_SRV_ADD_AUTO_ADD"; oldCmd = true; break;
	case CMD_EPG_SRV_DEL_AUTO_ADD: strCmd = L"CMD_EPG_SRV_DEL_AUTO_ADD"; oldCmd = true; break;
	case CMD_EPG_SRV_CHG_AUTO_ADD: strCmd = L"CMD_EPG_SRV_CHG_AUTO_ADD"; oldCmd = true; break;
	case CMD_EPG_SRV_SEARCH_PG_FIRST: strCmd = L"CMD_EPG_SRV_SEARCH_PG_FIRST"; oldCmd = true; break;
	case CMD_EPG_SRV_SEARCH_PG_NEXT: strCmd = L"CMD_EPG_SRV_SEARCH_PG_NEXT"; oldCmd = true; break;
	//旧バージョン互換コマンドここまで
	default: wsprintf(tempCmd, L"CMD_Unknown_%d", cmd); break;
	}

	if( oldCmd ){
		switch( result ){
		case OLD_CMD_SUCCESS: strResult = L"OLD_CMD_SUCCESS"; break;
		case OLD_CMD_ERR: strResult = L"OLD_CMD_ERR"; break;
		case OLD_CMD_NEXT: strResult = L"OLD_CMD_NEXT"; break;
		default: wsprintf(tempResult, L"Unknown_OldErrCode_%d", result); break;
		}
	}else{
		switch( result ){
		case CMD_SUCCESS: strResult = L"CMD_SUCCESS"; break;
		case CMD_ERR: strResult = L"CMD_ERR"; break;
		case CMD_AUTH_REQUEST: strResult = L"CMD_AUTH_REQUEST"; break;
		case CMD_NEXT: strResult = L"CMD_NEXT"; break;
		case CMD_NON_SUPPORT: strResult = L"CMD_NON_SUPPORT"; break;
		case CMD_ERR_INVALID_ARG: strResult = L"CMD_ERR_INVALID_ARG"; break;
		case CMD_ERR_CONNECT: strResult = L"CMD_ERR_CONNECT"; break;
		case CMD_ERR_DISCONNECT: strResult = L"CMD_ERR_DISCONNECT"; break;
		case CMD_ERR_TIMEOUT: strResult = L"CMD_ERR_TIMEOUT"; break;
		case CMD_ERR_BUSY: strResult = L"CMD_ERR_BUSY"; break;
		case CMD_NO_RES: strResult = L"CMD_NO_RES"; break;
		default: wsprintf(tempResult, L"Unknown_ErrCode_%d", result); break;
		}
	}

	_OutputDebugString(L"%s: %s: %dms\r\n", strCmd, strResult, static_cast<int>(ticks * millisec));
}
