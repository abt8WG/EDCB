#include "StdAfx.h"
#include "TCPServer.h"
#include <process.h>
#include "ErrDef.h"
#include "CtrlCmdUtil.h"
#include "CryptUtil.h"
#include "BlockLock.h"

CTCPServer::CTCPServer(void)
{
	InitializeCriticalSection(&m_lock);
	m_pCmdProc = NULL;
	m_pParam = NULL;
	m_dwPort = 8081;

	m_stopFlag = FALSE;
	m_hThread = NULL;

	m_sock = INVALID_SOCKET;

	WSAData wsaData;
	WSAStartup(MAKEWORD(2,0), &wsaData);
}

CTCPServer::~CTCPServer(void)
{
	StopServer();
	WSACleanup();
	DeleteCriticalSection(&m_lock);
}

BOOL CTCPServer::StartServer(DWORD dwPort, DWORD dwResponseTimeout, LPCWSTR acl, LPCWSTR password, CMD_CALLBACK_PROC pfnCmdProc, void* pParam)
{
	if( pfnCmdProc == NULL || pParam == NULL ){
		return FALSE;
	}

	if( m_hThread != NULL &&
	    m_pCmdProc == pfnCmdProc &&
	    m_pParam == pParam &&
	    m_dwPort == dwPort ){
		// m_dwResponseTimeout, m_acl, m_hmac については ServerThread を終了させずに置き換える
		CBlockLock lock(&m_lock);
		m_dwResponseTimeout = dwResponseTimeout;
		m_acl = acl;
		m_hmac.Close();
		m_hmac.Create(password);
		return TRUE;
	}

	StopServer();

	m_pCmdProc = pfnCmdProc;
	m_pParam = pParam;
	m_dwPort = dwPort;
	m_dwResponseTimeout = dwResponseTimeout;
	m_acl = acl;
	m_hmac.Create(password);

	m_sock = socket(AF_INET, SOCK_STREAM, 0);
	if( m_sock == INVALID_SOCKET ){
		return FALSE;
	}
	struct sockaddr_in addr;
	addr.sin_family = AF_INET;
	addr.sin_port = htons((WORD)dwPort);
	addr.sin_addr.S_un.S_addr = INADDR_ANY;
	BOOL b=1;

	setsockopt(m_sock, SOL_SOCKET, SO_REUSEADDR, (const char *)&b, sizeof(b));

	bind(m_sock, (struct sockaddr *)&addr, sizeof(addr));

	listen(m_sock, 1);

	m_stopFlag = FALSE;
	m_hThread = (HANDLE)_beginthreadex(NULL, 0, ServerThread, (LPVOID)this, CREATE_SUSPENDED, NULL);
	ResumeThread(m_hThread);

	return TRUE;
}

void CTCPServer::StopServer()
{
	if( m_hThread != NULL ){
		m_stopFlag = TRUE;
		// スレッド終了待ち
		if ( ::WaitForSingleObject(m_hThread, 15000) == WAIT_TIMEOUT ){
			::TerminateThread(m_hThread, 0xffffffff);
		}
		CloseHandle(m_hThread);
		m_hThread = NULL;
	}
	
	if( m_sock != INVALID_SOCKET ){
		closesocket(m_sock);
		m_sock = INVALID_SOCKET;
	}

	m_hmac.Close();
}

static BOOL TestAcl(struct in_addr addr, wstring acl)
{
	//書式例: +192.168.0.0/16,-192.168.0.1
	BOOL ret = FALSE;
	for(;;){
		wstring val;
		BOOL sep = Separate(acl, L",", val, acl);
		if( val.empty() || val[0] != L'+' && val[0] != L'-' ){
			//書式エラー
			return FALSE;
		}
		wstring a, b, c, d, m;
		Separate(val.substr(1), L".", a, b);
		Separate(b, L".", b, c);
		Separate(c, L".", c, d);
		ULONG mask = Separate(d, L"/", d, m) ? _wtoi(m.c_str()) : 32;
		if( a.empty() || b.empty() || c.empty() || d.empty() || mask > 32 ){
			//書式エラー
			return FALSE;
		}
		mask = mask == 0 ? 0 : 0xFFFFFFFFUL << (32 - mask);
		ULONG host = (ULONG)_wtoi(a.c_str()) << 24 | _wtoi(b.c_str()) << 16 | _wtoi(c.c_str()) << 8 | _wtoi(d.c_str());
		if( (ntohl(addr.s_addr) & mask) == (host & mask) ){
			ret = val[0] == L'+';
		}
		if( sep == FALSE ){
			return ret;
		}
	}
}

static int RecvAll(SOCKET sock, char* buf, int len, int flags)
{
	int n = 0;
	while( n < len ){
		int ret = recv(sock, buf + n, len - n, flags);
		if( ret < 0 ){
			return ret;
		}else if( ret <= 0 ){
			break;
		}
		n += ret;
	}
	return n;
}

BOOL CTCPServer::ReceiveHeader(SOCKET sock, CMD_STREAM& stCmd, AUTH_INFO* auth)
{
	DWORD head[2];
	int iRet = RecvAll(sock, (char*)head, sizeof(DWORD) * 2, 0);
	if (iRet != sizeof(DWORD) * 2) {
		return FALSE;
	}
	stCmd.param = head[0];
	stCmd.dataSize = head[1];
	//_OutputDebugString(L"Cmd = %d, size = %d\n", stCmd.param, stCmd.dataSize);

	// Headerの改ざんチェック
	return auth == nullptr || auth->nonceSize == 0 ||
		(m_hmac.SelectHash(stCmd.dataSize > 0 ? auth->stRes.dataSize / 2 : auth->stRes.dataSize) &&
			m_hmac.CalcHmac(auth->nonceData, auth->nonceSize) &&
			m_hmac.CalcHmac((BYTE*)head, 8) &&
			m_hmac.CompareHmac(auth->stRes.data));
}

BOOL CTCPServer::ReceiveData(SOCKET sock, CMD_STREAM& stCmd, AUTH_INFO* auth)
{
	if (stCmd.dataSize == 0) {
		return TRUE;
	}

	SAFE_DELETE_ARRAY(stCmd.data);
	stCmd.data = new BYTE[stCmd.dataSize];
	if (RecvAll(sock, (char*)stCmd.data, stCmd.dataSize, 0) != stCmd.dataSize) {
		return FALSE;
	}

	// Dataの改ざんチェック
	return auth == nullptr || auth->nonceSize == 0 ||
		(m_hmac.CalcHmac(auth->nonceData, auth->nonceSize) &&
			m_hmac.CalcHmac((BYTE*)stCmd.data, stCmd.dataSize) &&
			m_hmac.CompareHmac(auth->stRes.data + m_hmac.GetHashSize()));
}

BOOL CTCPServer::SendData(SOCKET sock, CMD_STREAM& stRes)
{
	BYTE head[1024];
	((DWORD*)head)[0] = stRes.param;
	((DWORD*)head)[1] = stRes.dataSize;

	DWORD extSize = 0;
	if( stRes.dataSize > 0 ){
		extSize = min(stRes.dataSize, sizeof(head) - sizeof(DWORD)*2);
		memcpy(head + sizeof(DWORD)*2, stRes.data, extSize);
	}
	if( send(sock, (char*)head, sizeof(DWORD)*2 + extSize, 0) == SOCKET_ERROR ||
		stRes.dataSize > extSize && send(sock, (char*)stRes.data + extSize, stRes.dataSize - extSize, 0) == SOCKET_ERROR ){
		return FALSE;
	}
	return TRUE;
}

BOOL CTCPServer::Authenticate(SOCKET sock, AUTH_INFO *auth)
{
	// パスワードが設定されていない場合は認証処理を行わない
	if (!m_hmac.IsInitialized()) {
		return TRUE;
	}

	// AUTH_REQUEST コマンドのみ受け付ける
	CMD_STREAM stCmd;
	if (ReceiveHeader(sock, stCmd) == FALSE ||
		stCmd.param != CMD2_EPG_SRV_AUTH_REQUEST ||
		stCmd.dataSize != 0) {
		return FALSE;
	}

	// nonce を生成する
	auth->nonceSize = 8; // 何バイトでもいいけど長さにあまり意味はない (ユニークであれば十分)
	auth->nonceData = new BYTE[auth->nonceSize];
	m_hmac.GetRandom(auth->nonceData, auth->nonceSize);

	// 認証要求として、nonce をクライアントへ送る
	CMD_STREAM stAuth;
	stAuth.param = CMD_AUTH_REQUEST;
	stAuth.data = auth->nonceData;
	stAuth.dataSize = auth->nonceSize;
	if (SendData(sock, stAuth) == FALSE) {
		return FALSE;
	}
	stAuth.data = NULL; // nonceData を delete しない

	// 受信待機
	fd_set ready;
	struct timeval to;
	to.tv_sec = 1;
	to.tv_usec = 0;
	FD_ZERO(&ready);
	FD_SET(sock, &ready);
	if (select(0, &ready, NULL, NULL, &to) == SOCKET_ERROR) {
		return FALSE;
	}
	if (!FD_ISSET(sock, &ready)) {
		return FALSE;
	}

	// AUTH_REPLY レスポンスを受け取る
	if (ReceiveHeader(sock, auth->stRes) == FALSE ||
		auth->stRes.param != CMD2_EPG_SRV_AUTH_REPLY ||
		auth->stRes.dataSize > 64 * 2) { // HMAC-SHA512 以上のサイズの応答は受け付けない
		_OutputDebugString(L"Invalid authentication replay : param = %d, size = %d\r\n", auth->stRes.param, auth->stRes.dataSize);
		return FALSE;
	}
	return ReceiveData(sock, auth->stRes);
}

BOOL CTCPServer::CheckkCmd(DWORD cmd, DWORD size)
{
	// size が cmd に対し適正か確認

	// とりあえず 64KB 未満のみ受けつけるようにしておく
	return size < 64*1024 ? TRUE : FALSE;
}

UINT WINAPI CTCPServer::ServerThread(LPVOID pParam)
{
	CTCPServer* pSys = (CTCPServer*)pParam;

	struct WAIT_INFO {
		SOCKET sock;
		CMD_STREAM* cmd;
		DWORD tick;
	};
	vector<WAIT_INFO> waitList;

	struct ERR_COUNT {
		DWORD dwCount;
		DWORD dwTick;
	};
	std::map<ULONG, ERR_COUNT> blacklist;
	std::map<ULONG, ERR_COUNT>::iterator itr_bl;

	while( pSys->m_stopFlag == FALSE ){
		fd_set ready;
		struct timeval to;
		if( waitList.empty() ){
			//StopServer()が固まらない程度
			to.tv_sec = 1;
			to.tv_usec = 0;
		}else{
			//適度に迅速
			to.tv_sec = 0;
			to.tv_usec = 200000;
		}
		FD_ZERO(&ready);
		FD_SET(pSys->m_sock, &ready);
		for( size_t i = 0; i < waitList.size(); i++ ){
			FD_SET(waitList[i].sock, &ready);
		}

		if( select(0, &ready, NULL, NULL, &to ) == SOCKET_ERROR ){
			break;
		}
		for( size_t i = 0; i < waitList.size(); i++ ){
			if( FD_ISSET(waitList[i].sock, &ready) == 0 ){
				CMD_STREAM stRes;
				pSys->m_pCmdProc(pSys->m_pParam, waitList[i].cmd, &stRes);
				if( stRes.param == CMD_NO_RES ){
					//応答は保留された
					if( GetTickCount() - waitList[i].tick <= pSys->m_dwResponseTimeout ){
						continue;
					}
				}else{
					pSys->SendData(waitList[i].sock, stRes);
				}
				shutdown(waitList[i].sock, SD_BOTH);
			}
			//応答待ちソケットは応答済みかタイムアウトか切断を含むなんらかの受信があれば閉じる
			closesocket(waitList[i].sock);
			delete waitList[i].cmd;
			waitList.erase(waitList.begin() + i--);
		}

		if( FD_ISSET(pSys->m_sock, &ready) ){
			CBlockLock lock(&pSys->m_lock); // for pSys->m_acl
			struct sockaddr_in client;
			int len = sizeof(client);
			SOCKET sock = accept(pSys->m_sock, (struct sockaddr *)&client, &len);
			if( sock == INVALID_SOCKET ){
				closesocket(pSys->m_sock);
				pSys->m_sock = INVALID_SOCKET;
				break;
			}else if( TestAcl(client.sin_addr, pSys->m_acl) == FALSE ){
				_OutputDebugString(L"Deny from IP:0x%08x\r\n", ntohl(client.sin_addr.s_addr));
				closesocket(sock);
				continue;
			}else if( (itr_bl = blacklist.find(client.sin_addr.S_un.S_addr)) != blacklist.end() && itr_bl->second.dwCount >= 5 ){
				_OutputDebugString(L"Delay access from IP:0x%08x\r\n", ntohl(client.sin_addr.s_addr));
				if( GetTickCount() - itr_bl->second.dwTick < 30*1000 ){
					itr_bl->second.dwTick = GetTickCount();
					closesocket(sock);
					continue;
				}else{
					blacklist.erase(itr_bl->first);
				}
			}

			for(;;){
				AUTH_INFO auth;
				CMD_STREAM stCmd;
				CMD_STREAM stRes;

				if( pSys->Authenticate(sock, &auth) == FALSE ||
					pSys->ReceiveHeader(sock, stCmd, &auth) == FALSE ||
					pSys->CheckkCmd(stCmd.param, stCmd.dataSize) == FALSE ||
					pSys->ReceiveData(sock, stCmd, &auth) == FALSE ){
					_OutputDebugString(L"Authentication error from IP:0x%08x\r\n", ntohl(client.sin_addr.s_addr));
					blacklist[client.sin_addr.S_un.S_addr].dwCount++;
					blacklist[client.sin_addr.S_un.S_addr].dwTick = GetTickCount();
					break;
				}

				if( stCmd.param == CMD2_EPG_SRV_REGIST_GUI_TCP || stCmd.param == CMD2_EPG_SRV_UNREGIST_GUI_TCP || stCmd.param == CMD2_EPG_SRV_ISREGIST_GUI_TCP ){
					string ip = inet_ntoa(client.sin_addr);

					REGIST_TCP_INFO setParam;
					AtoW(ip, setParam.ip);
					ReadVALUE(&setParam.port, stCmd.data, stCmd.dataSize, NULL);

					SAFE_DELETE_ARRAY(stCmd.data);
					stCmd.data = NewWriteVALUE(setParam, stCmd.dataSize);
				}

				pSys->m_pCmdProc(pSys->m_pParam, &stCmd, &stRes);
				if( stRes.param == CMD_NO_RES ){
					if( stCmd.param == CMD2_EPG_SRV_GET_STATUS_NOTIFY2 ){
						//保留可能なコマンドは応答待ちリストに移動
						if( waitList.size() < FD_SETSIZE - 1 ){
							WAIT_INFO waitInfo;
							waitInfo.sock = sock;
							waitInfo.cmd = new CMD_STREAM;
							waitInfo.cmd->param = stCmd.param;
							waitInfo.cmd->dataSize = stCmd.dataSize;
							waitInfo.cmd->data = stCmd.data;
							waitInfo.tick = GetTickCount();
							waitList.push_back(waitInfo);
							sock = INVALID_SOCKET;
							stCmd.data = NULL;
						}
					}
					break;
				}

				if( pSys->SendData(sock, stRes) == FALSE ){
					break;
				}

				if( stRes.param != CMD_NEXT && stRes.param != OLD_CMD_NEXT ){
					//Enum用の繰り返しではない
					break;
				}
			}
			if( sock != INVALID_SOCKET ){
				shutdown(sock, SD_BOTH);
				closesocket(sock);
			}
		}
	}

	while( waitList.empty() == false ){
		closesocket(waitList.back().sock);
		delete waitList.back().cmd;
		waitList.pop_back();
	}

	return 0;
}
