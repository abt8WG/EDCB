// EpgTimerSrv.cpp : �A�v���P�[�V�����̃G���g�� �|�C���g���`���܂��B
//

#include "stdafx.h"
#include "EpgTimerSrv.h"
#include "EpgTimerSrvMain.h"
#include "../../Common/PathUtil.h"
#include "../../Common/ServiceUtil.h"
#include "../../Common/BlockLock.h"

#include "../../Common/CommonDef.h"
#include <WinSvc.h>
#include <ObjBase.h>

SERVICE_STATUS_HANDLE g_hStatusHandle;
CEpgTimerSrvMain* g_pMain;
static HANDLE g_hDebugLog = INVALID_HANDLE_VALUE;
static CRITICAL_SECTION g_debugLogLock;
static bool g_saveDebugLog = false;

// _CrtSetReportMode �� _CRTDBG_MODE_FILE �� ANSI �ł����o�͂ł��Ȃ��̂ŁAUnicode �o�͂� report hook �֐���p�ӂ���
int __cdecl reportWithUnicode(int /*nReportType*/, wchar_t* szMsg, int* /*pnRet*/)
{
	if (g_saveDebugLog && g_hDebugLog != INVALID_HANDLE_VALUE) {
		DWORD numOfSize = static_cast<DWORD>(sizeof(*szMsg) * wcslen(szMsg));
		WriteFile(g_hDebugLog, szMsg, numOfSize, &numOfSize, NULL);
	}
	return 0;
}

static void StartDebugLog()
{
	wstring iniPath;
	GetModuleIniPath(iniPath);
	if( GetPrivateProfileInt(L"SET", L"SaveDebugLog", 0, iniPath.c_str()) != 0 ){
		wstring logPath;
		GetModuleFolderPath(logPath);
		logPath += L"\\EpgTimerSrvDebugLog.txt";
		g_hDebugLog = CreateFile(logPath.c_str(), FILE_APPEND_DATA, FILE_SHARE_READ, NULL, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
		if( g_hDebugLog != INVALID_HANDLE_VALUE ){
			if( GetLastError() == ERROR_SUCCESS ){
				DWORD dwWritten;
				WriteFile(g_hDebugLog, "\xFF\xFE", sizeof(char) * 2, &dwWritten, NULL);
			}else{
				LARGE_INTEGER liPos = {};
				SetFilePointerEx(g_hDebugLog, liPos, NULL, FILE_END);
			}
			InitializeCriticalSection(&g_debugLogLock);
			g_saveDebugLog = true;
			OutputDebugString(L"****** LOG START ******\r\n");

			// Debug�ł� memory leak ���o���s��
			_CrtSetDbgFlag(_CRTDBG_ALLOC_MEM_DF | _CRTDBG_LEAK_CHECK_DF);
			// memory leak ���o�̏o�̓n���h���[��o�^
			_CrtSetReportHookW2(_CRT_RPTHOOK_INSTALL, reportWithUnicode);
		}
	}
}

static void StopDebugLog()
{
	if( g_saveDebugLog ){

		// ���J���� memory block ������� dump ���� (CEpgTimerSrvMain �N���X�� delete ����Ă���O��)
		_CrtDumpMemoryLeaks();

		// �Ǐ��I�� memory leak �𒲂ׂ�Ȃ�
		// _CrtMemState memchk;
		// _CrtMemCheckpoint(&memchk);
		// /* ���ׂ鏈�� */
		// _CrtMemDumpStatistics(&memchk);
		//

		// �o�̓n���h���[������
		_CrtSetReportHookW2(_CRT_RPTHOOK_REMOVE, reportWithUnicode);

		OutputDebugString(L"****** LOG STOP ******\r\n");
		g_saveDebugLog = false;
		DeleteCriticalSection(&g_debugLogLock);
		CloseHandle(g_hDebugLog);
		g_hDebugLog = INVALID_HANDLE_VALUE;
	}
}

int EpgTimerSrvMain(HINSTANCE hInstance,
	HINSTANCE hPrevInstance,
	LPTSTR    lpCmdLine,
	int       nCmdShow)
{
	SetDllDirectory(_T(""));

	if( lpCmdLine[0] == _T('-') || lpCmdLine[0] == _T('/') ){
		if( lstrcmpi(_T("install"), lpCmdLine + 1) == 0 ){
			bool installed = false;
			TCHAR exePath[512];
			if( GetModuleFileName(NULL, exePath, _countof(exePath)) != 0 ){
				SC_HANDLE hScm = OpenSCManager(NULL, NULL, SC_MANAGER_CONNECT | SC_MANAGER_CREATE_SERVICE);
				if( hScm != NULL ){
					SC_HANDLE hSrv = CreateService(
						hScm, SERVICE_NAME, SERVICE_NAME, 0, SERVICE_WIN32_OWN_PROCESS | SERVICE_INTERACTIVE_PROCESS,
						SERVICE_AUTO_START, SERVICE_ERROR_NORMAL, exePath, NULL, NULL, NULL, NULL, NULL);
					if( hSrv != NULL ){
						installed = true;
						CloseServiceHandle(hSrv);
					}
					CloseServiceHandle(hScm);
				}
			}
			if( installed == false ){
				//�R���\�[�����Ȃ��̂Ń��b�Z�[�W�{�b�N�X�œ`����
				MessageBox(NULL, L"Failed to install/remove " SERVICE_NAME L".\r\nRun as Administrator on Vista and later.", NULL, MB_ICONERROR);
			}
			return 0;
		}else if( lstrcmpi(_T("remove"), lpCmdLine + 1) == 0 ){
			bool removed = false;
			SC_HANDLE hScm = OpenSCManager(NULL, NULL, SC_MANAGER_CONNECT);
			if( hScm != NULL ){
				SC_HANDLE hSrv = OpenService(hScm, SERVICE_NAME, DELETE | SERVICE_STOP | SERVICE_QUERY_STATUS);
				if( hSrv != NULL ){
					SERVICE_STATUS srvStatus;
					if( QueryServiceStatus(hSrv, &srvStatus) != FALSE ){
						if( srvStatus.dwCurrentState == SERVICE_STOPPED || ControlService(hSrv, SERVICE_CONTROL_STOP, &srvStatus) != FALSE ){
							removed = DeleteService(hSrv) != FALSE;
						}
					}
					CloseServiceHandle(hSrv);
				}
				CloseServiceHandle(hScm);
			}
			if( removed == false ){
				MessageBox(NULL, L"Failed to install/remove " SERVICE_NAME L".\r\nRun as Administrator on Vista and later.", NULL, MB_ICONERROR);
			}
			return 0;
		}
	}


	if( IsInstallService(SERVICE_NAME) == FALSE ){
		//���ʂ�exe�Ƃ��ċN�����s��
		HANDLE hMutex = CreateMutex(NULL, TRUE, EPG_TIMER_BON_SRV_MUTEX);
		if( hMutex != NULL ){
			if( GetLastError() != ERROR_ALREADY_EXISTS ){
				StartDebugLog();
				//���C���X���b�h�ɑ΂���COM�̏�����
				CoInitialize(NULL);
				CEpgTimerSrvMain* pMain = new CEpgTimerSrvMain;
				if( pMain->Main(false) == false ){
					OutputDebugString(_T("_tWinMain(): Failed to start\r\n"));
				}
				delete pMain;
				CoUninitialize();
				StopDebugLog();
			}
			ReleaseMutex(hMutex);
			CloseHandle(hMutex);
		}
	}else if( IsStopService(SERVICE_NAME) == FALSE ){
		//�T�[�r�X�Ƃ��Ď��s
		HANDLE hMutex = CreateMutex(NULL, TRUE, EPG_TIMER_BON_SRV_MUTEX);
		if( hMutex != NULL ){
			if( GetLastError() != ERROR_ALREADY_EXISTS ){
				StartDebugLog();
				SERVICE_TABLE_ENTRY dispatchTable[] = {
					{ SERVICE_NAME, service_main },
					{ NULL, NULL }
				};
				if( StartServiceCtrlDispatcher(dispatchTable) == FALSE ){
					OutputDebugString(_T("_tWinMain(): StartServiceCtrlDispatcher failed\r\n"));
				}
				StopDebugLog();
			}
			ReleaseMutex(hMutex);
			CloseHandle(hMutex);
		}
	}else{
		//Stop��ԂȂ̂ŃT�[�r�X�̊J�n��v��
		bool started = false;
		SC_HANDLE hScm = OpenSCManager(NULL, NULL, SC_MANAGER_CONNECT);
		if( hScm != NULL ){
			SC_HANDLE hSrv = OpenService(hScm, SERVICE_NAME, SERVICE_START);
			if( hSrv != NULL ){
				started = StartService(hSrv, 0, NULL) != FALSE;
				CloseServiceHandle(hSrv);
			}
			CloseServiceHandle(hScm);
		}
		if( started == false ){
			OutputDebugString(_T("_tWinMain(): Failed to start\r\n"));
		}
	}

	return 0;
}

int APIENTRY _tWinMain(HINSTANCE hInstance,
	HINSTANCE hPrevInstance,
	LPTSTR    lpCmdLine,
	int       nCmdShow)
{
	__try {
		return EpgTimerSrvMain(hInstance, hPrevInstance, lpCmdLine, nCmdShow);
	}
	__except (FilterException(GetExceptionInformation())) { }
	return 0;
}

void WINAPI service_main(DWORD dwArgc, LPTSTR *lpszArgv)
{
	g_hStatusHandle = RegisterServiceCtrlHandlerEx(SERVICE_NAME, service_ctrl, NULL);
	if( g_hStatusHandle != NULL ){
		ReportServiceStatus(SERVICE_START_PENDING, 0, 1, 10000);
		//���C���X���b�h�ɑ΂���COM�̏�����
		CoInitialize(NULL);
		//�����ł͒P����(���Ԃ̂�����Ȃ�)�������̂ݍs��
		g_pMain = new CEpgTimerSrvMain;

		ReportServiceStatus(SERVICE_RUNNING, SERVICE_ACCEPT_STOP | SERVICE_ACCEPT_SHUTDOWN | SERVICE_ACCEPT_POWEREVENT, 0, 0);

		if( g_pMain->Main(true) == false ){
			OutputDebugString(_T("service_main(): Failed to start\r\n"));
		}
		delete g_pMain;
		g_pMain = NULL;
		CoUninitialize();
		ReportServiceStatus(SERVICE_STOPPED, 0, 0, 0);
	}
}

DWORD WINAPI service_ctrl(DWORD dwControl, DWORD dwEventType, LPVOID lpEventData, LPVOID lpContext)
{
	switch (dwControl){
		case SERVICE_CONTROL_STOP:
		case SERVICE_CONTROL_SHUTDOWN:
			ReportServiceStatus(SERVICE_STOP_PENDING, 0, 0, 10000);
			g_pMain->StopMain();
			return NO_ERROR;
		case SERVICE_CONTROL_POWEREVENT:
			if( dwEventType == PBT_APMQUERYSUSPEND ){
				//Vista�ȍ~�͌Ă΂�Ȃ�
				OutputDebugString(_T("PBT_APMQUERYSUSPEND\r\n"));
				if( g_pMain->IsSuspendOK() == false ){
					OutputDebugString(_T("BROADCAST_QUERY_DENY\r\n"));
					return BROADCAST_QUERY_DENY;
				}
			}else if( dwEventType == PBT_APMRESUMESUSPEND ){
				OutputDebugString(_T("PBT_APMRESUMESUSPEND\r\n"));
			}
			return NO_ERROR;
		default:
			break;
	}
	return ERROR_CALL_NOT_IMPLEMENTED;
}

void ReportServiceStatus(DWORD dwCurrentState, DWORD dwControlsAccepted, DWORD dwCheckPoint, DWORD dwWaitHint)
{
	SERVICE_STATUS ss;

	ss.dwServiceType = SERVICE_WIN32_OWN_PROCESS;
	ss.dwCurrentState = dwCurrentState;
	ss.dwControlsAccepted = dwControlsAccepted;
	ss.dwWin32ExitCode = NO_ERROR;
	ss.dwServiceSpecificExitCode = 0;
	ss.dwCheckPoint = dwCheckPoint;
	ss.dwWaitHint = dwWaitHint;

	SetServiceStatus(g_hStatusHandle, &ss);
}

void OutputDebugStringWrapper(LPCWSTR lpOutputString)
{
	if( g_saveDebugLog ){
		//�f�o�b�O�o�̓��O�ۑ�
		CBlockLock lock(&g_debugLogLock);
		SYSTEMTIME st;
		GetLocalTime(&st);
		WCHAR header[64];
		int len = wsprintf(header, L"[%02d%02d%02d%02d%02d%02d.%03d] ",
		                   st.wYear % 100, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond, st.wMilliseconds);
		DWORD dwWritten;
		WriteFile(g_hDebugLog, header, sizeof(WCHAR) * len, &dwWritten, NULL);
		if( lpOutputString ){
			len = lstrlen(lpOutputString);
			WriteFile(g_hDebugLog, lpOutputString, sizeof(WCHAR) * len, &dwWritten, NULL);
			if( len == 0 || lpOutputString[len - 1] != L'\n' ){
				WriteFile(g_hDebugLog, L"<NOBR>\r\n", sizeof(WCHAR) * 8, &dwWritten, NULL);
			}
		}
	}
	OutputDebugStringW(lpOutputString);
}
