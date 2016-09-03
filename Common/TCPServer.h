#pragma once

#include "StringUtil.h"
#include "CtrlCmdDef.h"
#include "StructDef.h"
#include "CryptUtil.h"

#include <winsock2.h>
#include <ws2tcpip.h>
#pragma comment(lib, "Ws2_32.lib")

typedef int (CALLBACK *CMD_CALLBACK_PROC)(void* pParam, CMD_STREAM* pCmdParam, CMD_STREAM* pResParam);

class CTCPServer
{
public:
	CTCPServer(void);
	~CTCPServer(void);

	BOOL StartServer(
		DWORD dwPort, 
		DWORD dwResponseTimeout,
		LPCWSTR acl,
		LPCWSTR password,
		CMD_CALLBACK_PROC pfnCmdProc, 
		void* pParam
		);
	void StopServer();
	void NotifyUpdate();

protected:
	CRITICAL_SECTION m_lock;
	CMD_CALLBACK_PROC m_pCmdProc;
	void* m_pParam;
	DWORD m_dwPort;
	DWORD m_dwResponseTimeout;
	wstring m_acl;
	CCryptUtil m_hmac;

	WSAEVENT m_hNotifyEvent;
	BOOL m_stopFlag;
	HANDLE m_hThread;

	SOCKET m_sock;
	
	struct AUTH_INFO {
		DWORD nonceSize;
		BYTE *nonceData;

		CMD_STREAM stRes; // CMD2_EPG_SRV_AUTH_REPLY, HMAC size and HMAC data

		AUTH_INFO() : nonceSize(0), nonceData(NULL) {}
		~AUTH_INFO() { SAFE_DELETE_ARRAY(nonceData); }
	};

protected:
	static UINT WINAPI ServerThread(LPVOID pParam);

	BOOL Authenticate(SOCKET sock, AUTH_INFO *auth);
	BOOL CheckCmd(DWORD cmd, DWORD size);
	BOOL ReceiveHeader(SOCKET sock, CMD_STREAM& stCmd, AUTH_INFO* auth = nullptr);
	BOOL ReceiveData(SOCKET sock, CMD_STREAM& stCmd, AUTH_INFO* auth = nullptr);
	BOOL SendData(SOCKET sock, CMD_STREAM& stRes);
};
