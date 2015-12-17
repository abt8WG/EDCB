#include "StdAfx.h"
#include "TCPServer.h"
#include <process.h>
#include "ErrDef.h"
#include "CtrlCmdUtil.h"
#include "CryptUtil.h"

CTCPServer::CTCPServer(void)
{
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
}

BOOL CTCPServer::StartServer(DWORD dwPort, DWORD dwResponseTimeout, LPCWSTR acl, LPCWSTR password, CMD_CALLBACK_PROC pfnCmdProc, void* pParam)
{
	if( pfnCmdProc == NULL || pParam == NULL ){
		return FALSE;
	}

	StopServer();

	m_pCmdProc = pfnCmdProc;
	m_pParam = pParam;
	m_dwPort = dwPort;
	m_dwResponseTimeout = dwResponseTimeout;
	m_acl = acl;

	wstring wstr;
	Decrypt(password, wstr);
	WtoUTF8(wstr, m_password);

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
	DWORD socketBuffSize = 1024*1024;
	setsockopt(m_sock, SOL_SOCKET, SO_SNDBUF, (const char*)&socketBuffSize, sizeof(socketBuffSize));
	setsockopt(m_sock, SOL_SOCKET, SO_SNDBUF, (const char*)&socketBuffSize, sizeof(socketBuffSize));

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
		// �X���b�h�I���҂�
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
}

static BOOL TestAcl(struct in_addr addr, wstring acl)
{
	//������: +192.168.0.0/16,-192.168.0.1
	BOOL ret = FALSE;
	for(;;){
		wstring val;
		BOOL sep = Separate(acl, L",", val, acl);
		if( val.empty() || val[0] != L'+' && val[0] != L'-' ){
			//�����G���[
			return FALSE;
		}
		wstring a, b, c, d, m;
		Separate(val.substr(1), L".", a, b);
		Separate(b, L".", b, c);
		Separate(c, L".", c, d);
		ULONG mask = Separate(d, L"/", d, m) ? _wtoi(m.c_str()) : 32;
		if( a.empty() || b.empty() || c.empty() || d.empty() || mask > 32 ){
			//�����G���[
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

static BOOL ReceiveHeader(SOCKET sock, CMD_STREAM& stCmd)
{
	DWORD head[2];
	int iRet = recv(sock, (char*)head, sizeof(DWORD) * 2, 0);
	if (iRet != sizeof(DWORD) * 2) {
		return FALSE;
	}
	stCmd.param = head[0];
	stCmd.dataSize = head[1];
	//_OutputDebugString(L"Cmd = %d, size = %d\n", stCmd.param, stCmd.dataSize);
	return TRUE;
}

static BOOL ReceiveData(SOCKET sock, CMD_STREAM& stCmd)
{
	if (stCmd.dataSize > 0) {
		SAFE_DELETE_ARRAY(stCmd.data);
		stCmd.data = new BYTE[stCmd.dataSize];

		DWORD dwRead = 0;
		while (dwRead < stCmd.dataSize) {
			int iRet = recv(sock, (char*)(stCmd.data + dwRead), stCmd.dataSize - dwRead, 0);
			if (iRet == SOCKET_ERROR) {
				break;
			}
			else if (iRet == 0) {
				break;
			}
			dwRead += iRet;
		}
		if (dwRead < stCmd.dataSize) {
			return FALSE;
		}
	}
	return TRUE;
}

static BOOL SendData(SOCKET sock, CMD_STREAM& stRes)
{
	DWORD head[2] = { stRes.param, stRes.dataSize };
	int iRet = send(sock, (char*)head, sizeof(DWORD) * 2, 0);
	if (iRet == SOCKET_ERROR) {
		return FALSE;
	}
	if (stRes.dataSize > 0) {
		if (stRes.data == NULL) {
			return FALSE;
		}
		iRet = send(sock, (char*)(stRes.data), stRes.dataSize, 0);
		if (iRet == SOCKET_ERROR) {
			return FALSE;
		}
	}
	return TRUE;
}

 BOOL Authenticate(SOCKET sock, const string& password)
{
	// �p�X���[�h���ݒ肳��Ă��Ȃ��ꍇ�͔F�؏������s��Ȃ�
	if (password.empty()) {
		return TRUE;
	}

	CMD_STREAM stCmd;
	if (ReceiveHeader(sock, stCmd) == FALSE ||
		stCmd.param != CMD2_EPG_SRV_AUTH_REQUEST ||
		stCmd.dataSize != 0) {
		return FALSE;
	}

#if 1
	// nonce �𐶐����� (�y�ʔ�)
	// ���ʁA���ݎ����ƃ����_���l���q�����肷�邪�A
	// QueryPerformanceCounter ���i�ޑO�� nonce ���Đ������邱�Ƃ͂Ȃ� 64bit ��1������܂ł͊��S�Ƀ��j�[�N
	// �ł��邱�Ƃ���A�����ł� QueryPerformanceCounter ���̗p���Ă݂�B
	DWORD size = sizeof(LARGE_INTEGER);
	BYTE *msg = new BYTE[size];
	QueryPerformanceCounter((LARGE_INTEGER*)msg);
#else
	// nonce �𐶐����� (CryptGenRandom��)
	DWORD size = 20; // ���o�C�g�ł��������ǒ����ɂ��܂�Ӗ��͂Ȃ� (���j�[�N�ł���Ώ\��)
	BYTE *msg = new BYTE[size];
	GetRandom(msg, size);
#endif

	// �F�ؗv���Ƃ��āAnonce ���N���C�A���g�֑���
	CMD_STREAM stAuth;
	stAuth.param = CMD_AUTH_REQUEST;
	stAuth.data = msg;
	stAuth.dataSize = size;
	if (SendData(sock, stAuth) == FALSE) {
		return FALSE;
	}

	// ��M�ҋ@
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

	// ���X�|���X���󂯎��
	CMD_STREAM stRes;
	if (ReceiveHeader(sock, stRes) == FALSE ||
		stRes.param != CMD2_EPG_SRV_AUTH_REPLY ||
		stRes.dataSize > 64) { // HMAC-SHA512 �ȏ�̃T�C�Y�̉����͎󂯕t���Ȃ�
		return FALSE;
	}
	ReceiveData(sock, stRes);

	BYTE *hmacOut = NULL;
	DWORD szOut = 0;
	BOOL ret = FALSE;
	switch (stRes.dataSize)
	{
	case 128 / 8:  // HMAC-MD5 (������ HMAC-MD5 �ŏ\���ȋ��x�����邱�Ƃ��m���Ă���)
		ret = HMAC(CALG_MD5, (BYTE*)password.data(), (DWORD)password.size(), msg, size, &hmacOut, &szOut);
		break;
	case 160 / 8: // HMAC-SHA1
		ret = HMAC(CALG_SHA1, (BYTE*)password.data(), (DWORD)password.size(), msg, size, &hmacOut, &szOut);
		break;
	case 256 / 8: // HMAC-SHA256
		ret = HMAC(CALG_SHA_256, (BYTE*)password.data(), (DWORD)password.size(), msg, size, &hmacOut, &szOut);
		break;
	case 384 / 8: // HMAC-SHA384
		ret = HMAC(CALG_SHA_384, (BYTE*)password.data(), (DWORD)password.size(), msg, size, &hmacOut, &szOut);
		break;
	case 512 / 8: // HMAC-SHA512
		ret = HMAC(CALG_SHA_512, (BYTE*)password.data(), (DWORD)password.size(), msg, size, &hmacOut, &szOut);
		break;
	}
	ret = ret && (szOut == stRes.dataSize && memcmp(hmacOut, stRes.data, szOut) == 0);
	delete[] hmacOut;
	return ret;
}

BOOL CheckkCmd(DWORD cmd, DWORD size)
{
	// size �� cmd �ɑ΂��K�����m�F

	// �Ƃ肠���� 64KB �����̂ݎ󂯂���悤�ɂ��Ă���
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
			//StopServer()���ł܂�Ȃ����x
			to.tv_sec = 1;
			to.tv_usec = 0;
		}else{
			//�K�x�ɐv��
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
					//�����͕ۗ����ꂽ
					if( GetTickCount() - waitList[i].tick <= pSys->m_dwResponseTimeout ){
						continue;
					}
				}else{
					DWORD head[2] = { stRes.param, stRes.dataSize };
					if( send(waitList[i].sock, (const char*)head, sizeof(head), 0) != SOCKET_ERROR ){
						if( stRes.dataSize > 0 && stRes.data != NULL ){
							send(waitList[i].sock, (const char*)stRes.data, stRes.dataSize, 0);
						}
					}
				}
				shutdown(waitList[i].sock, SD_BOTH);
			}
			//�����҂��\�P�b�g�͉����ς݂��^�C���A�E�g���ؒf���܂ނȂ�炩�̎�M������Ε���
			closesocket(waitList[i].sock);
			delete waitList[i].cmd;
			waitList.erase(waitList.begin() + i--);
		}

		if( FD_ISSET(pSys->m_sock, &ready) ){
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
			}else if( (itr_bl = blacklist.find(client.sin_addr.S_un.S_addr)) != blacklist.end() && itr_bl->second.dwCount >= 5 ){
				_OutputDebugString(L"Delay access from IP:0x%08x\r\n", ntohl(client.sin_addr.s_addr));
				if( GetTickCount() - itr_bl->second.dwTick < 30*1000 ){
					itr_bl->second.dwTick = GetTickCount();
					closesocket(sock);
				}else{
					blacklist.erase(itr_bl->first);
				}
			}else{
				for(;;){
					CMD_STREAM stCmd;
					CMD_STREAM stRes;

					if( Authenticate(sock, pSys->m_password) == FALSE ){
						_OutputDebugString(L"Authentication error from IP:0x%08x\r\n", ntohl(client.sin_addr.s_addr));
						blacklist[client.sin_addr.S_un.S_addr].dwCount++;
						blacklist[client.sin_addr.S_un.S_addr].dwTick = GetTickCount();
						break;
					}

					if( ReceiveHeader(sock, stCmd) == FALSE || 
						CheckkCmd(stCmd.param, stCmd.dataSize) == FALSE ||
						ReceiveData(sock, stCmd) == FALSE ){
						break;
					}

					if( stCmd.param == CMD2_EPG_SRV_REGIST_GUI_TCP || stCmd.param == CMD2_EPG_SRV_UNREGIST_GUI_TCP || stCmd.param == CMD2_EPG_SRV_ISREGIST_GUI_TCP ){
						string ip = inet_ntoa(client.sin_addr);

						REGIST_TCP_INFO setParam;
						AtoW(ip, setParam.ip);
						ReadVALUE(&setParam.port, stCmd.data, stCmd.dataSize, NULL);

						SAFE_DELETE_ARRAY(stCmd.data);
						stCmd.data = NewWriteVALUE(&setParam, stCmd.dataSize);
					}

					pSys->m_pCmdProc(pSys->m_pParam, &stCmd, &stRes);
					if( stRes.param == CMD_NO_RES ){
						if( stCmd.param == CMD2_EPG_SRV_GET_STATUS_NOTIFY2 ){
							//�ۗ��\�ȃR�}���h�͉����҂����X�g�Ɉړ�
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

					if( SendData(sock, stRes) == FALSE ){
						break;
					}

					if( stRes.param != CMD_NEXT && stRes.param != OLD_CMD_NEXT ){
						//Enum�p�̌J��Ԃ��ł͂Ȃ�
						break;
					}
				}
				if( sock != INVALID_SOCKET ){
					shutdown(sock, SD_BOTH);
					closesocket(sock);
				}
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
