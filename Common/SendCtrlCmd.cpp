#include "StdAfx.h"
#include "SendCtrlCmd.h"
/*
#include <winsock2.h>
#include <ws2tcpip.h>
#pragma comment(lib, "wsock32.lib")
#pragma comment(lib, "Ws2_32.lib")
*/
#include "StringUtil.h"
#include "CryptUtil.h"

CSendCtrlCmd::CSendCtrlCmd(void)
{
	this->tcpFlag = FALSE;
	this->connectTimeOut = CONNECT_TIMEOUT;

	this->pipeName = CMD2_EPG_SRV_PIPE;
	this->eventName = CMD2_EPG_SRV_EVENT_WAIT_CONNECT;

	this->ip = L"127.0.0.1";
	this->port = 5678;

	this->pfnSend = SendPipe;
}


CSendCtrlCmd::~CSendCtrlCmd(void)
{
#ifndef SEND_CTRL_CMD_NO_TCP
	SetSendMode(FALSE);
#endif
}

#ifndef SEND_CTRL_CMD_NO_TCP

//コマンド送信方法の設定
//引数：
// tcpFlag		[IN] TRUE：TCP/IPモード、FALSE：名前付きパイプモード
void CSendCtrlCmd::SetSendMode(
	BOOL tcpFlag
	)
{
	if( this->tcpFlag == FALSE && tcpFlag ){
		WSAData wsaData;
		WSAStartup(MAKEWORD(2, 0), &wsaData);
		this->tcpFlag = TRUE;
	}else if( this->tcpFlag && tcpFlag == FALSE ){
		WSACleanup();
		this->tcpFlag = FALSE;
	}
}

#endif

//名前付きパイプモード時の接続先を設定
//EpgTimerSrv.exeに対するコマンドは設定しなくても可（デフォルト値になっている）
//引数：
// eventName	[IN]排他制御用Eventの名前
// pipeName		[IN]接続パイプの名前
void CSendCtrlCmd::SetPipeSetting(
	LPCWSTR eventName,
	LPCWSTR pipeName
	)
{
	this->eventName = eventName;
	this->pipeName = pipeName;
	this->pfnSend = SendPipe;
}

//名前付きパイプモード時の接続先を設定（接尾にプロセスIDを伴うタイプ）
//引数：
// pid			[IN]プロセスID
void CSendCtrlCmd::SetPipeSetting(
	LPCWSTR eventName,
	LPCWSTR pipeName,
	DWORD pid
	)
{
	Format(this->eventName, L"%s%d", eventName, pid);
	Format(this->pipeName, L"%s%d", pipeName, pid);
	this->pfnSend = SendPipe;
}

//TCP/IPモード時の接続先を設定
//引数：
// ip			[IN]接続先IP
// port			[IN]接続先ポート
// password		[IN]接続先パスワード
void CSendCtrlCmd::SetNWSetting(
	wstring ip,
	DWORD port,
	wstring password
	)
{
#ifndef SEND_CTRL_CMD_NO_TCP
	this->ip = ip;
	this->port = port;
	this->hmac.Close();
	this->hmac.SelectHash((ALG_ID)CALG_MD5);
	this->hmac.Create(password);
	this->pfnSend = SendTCP;
#endif
}

//接続処理時のタイムアウト設定
// timeOut		[IN]タイムアウト値（単位：ms）
void CSendCtrlCmd::SetConnectTimeOut(
	DWORD timeOut
	)
{
	this->connectTimeOut = timeOut;
}

static DWORD ReadFileAll(HANDLE hFile, BYTE* lpBuffer, DWORD dwToRead)
{
	DWORD dwRet = 0;
	for( DWORD dwRead; dwRet < dwToRead && ReadFile(hFile, lpBuffer + dwRet, dwToRead - dwRet, &dwRead, NULL); dwRet += dwRead );
	return dwRet;
}

DWORD CSendCtrlCmd::SendPipe(CSendCtrlCmd *t, CMD_STREAM* send, CMD_STREAM* res)
{
	return t->SendPipe(t->pipeName.c_str(), t->eventName.c_str(), t->connectTimeOut, send, res);
}
DWORD CSendCtrlCmd::SendPipe(LPCWSTR pipeName, LPCWSTR eventName, DWORD timeOut, CMD_STREAM* send, CMD_STREAM* res)
{
	if( pipeName == NULL || eventName == NULL || send == NULL || res == NULL ){
		return CMD_ERR_INVALID_ARG;
	}

	//接続待ち
	//CreateEvent()してはいけない。イベントを作成するのはサーバの仕事のはず
	//CreateEvent()してしまうとサーバが終了した後は常にタイムアウトまで待たされることになる
	HANDLE waitEvent = OpenEvent(SYNCHRONIZE, FALSE, eventName);
	if( waitEvent == NULL ){
		return CMD_ERR_CONNECT;
	}
	DWORD dwRet = WaitForSingleObject(waitEvent, timeOut);
	CloseHandle(waitEvent);
	if( dwRet == WAIT_TIMEOUT ){
		return CMD_ERR_TIMEOUT;
	}else if( dwRet != WAIT_OBJECT_0 ){
		return CMD_ERR_CONNECT;
	}

	//接続
	HANDLE pipe = CreateFile( pipeName, GENERIC_READ|GENERIC_WRITE, 0, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
	if( pipe == INVALID_HANDLE_VALUE ){
		_OutputDebugString(L"*+* ConnectPipe Err:%d\r\n", GetLastError());
		return CMD_ERR_CONNECT;
	}

	DWORD write = 0;

	//送信
	DWORD head[2];
	head[0] = send->param;
	head[1] = send->dataSize;
	if( WriteFile(pipe, head, sizeof(DWORD)*2, &write, NULL ) == FALSE ){
		CloseHandle(pipe);
		return CMD_ERR;
	}
	if( send->dataSize > 0 ){
		if( WriteFile(pipe, send->data, send->dataSize, &write, NULL ) == FALSE ){
			CloseHandle(pipe);
			return CMD_ERR;
		}
	}

	//受信
	if( ReadFileAll(pipe, (BYTE*)head, sizeof(head)) != sizeof(head) ){
		CloseHandle(pipe);
		return CMD_ERR;
	}
	res->param = head[0];
	res->dataSize = head[1];
	if( res->dataSize > 0 ){
		res->data = new BYTE[res->dataSize];
		if( ReadFileAll(pipe, res->data, res->dataSize) != res->dataSize ){
			CloseHandle(pipe);
			return CMD_ERR;
		}
	}
	CloseHandle(pipe);

	return res->param;
}

#ifndef SEND_CTRL_CMD_NO_TCP

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

DWORD CSendCtrlCmd::Authenticate(SOCKET sock, BYTE** pbdata, DWORD* pndata)
{
	if (!hmac.IsInitialized()) {
		return CMD_SUCCESS;
	}

	// nonce要求
	DWORD header[2];
	header[0] = CMD2_EPG_SRV_AUTH_REQUEST;
	header[1] = 0;
	if (send(sock, (char*)header, sizeof(header), 0) == SOCKET_ERROR) {
		return CMD_ERR;
	}

	// nonce を受け取る
	if (RecvAll(sock, (char*)header, sizeof(header), 0) != sizeof(header)) {
		return CMD_ERR;
	}
	if (header[0] != CMD_AUTH_REQUEST) {
		return CMD_ERR;
	}
	BYTE *nonce = new BYTE[header[1]];
	int read = RecvAll(sock, (char*)nonce, header[1], 0);
	if (read != (int)header[1]) {
		return CMD_ERR;
	}

	//サーバー応答の nonce とパスワードから応答パケットを作る
	DWORD sizeAuthPacket = sizeof(DWORD) * 2 + hmac.GetHashSize();
	if (*pndata > sizeof(DWORD) * 2) {
		sizeAuthPacket += hmac.GetHashSize();
	}

	// 認証応答パケット
	//   cmd[ 0～ 3] : CMD2_EPG_SRV_AUTH_REPLY
	//   cmd[ 4～ 7] : HMAC size (= 32 bytes)
	//   cmd[ 8～23] : HMAC for header (16 bytes)
	//   cmd[24～39] : HMAC for data (16 bytes) if exist;
	BYTE *cmd = new BYTE[sizeAuthPacket + *pndata];
	((DWORD*)cmd)[0] = CMD2_EPG_SRV_AUTH_REPLY;
	((DWORD*)cmd)[1] = sizeAuthPacket - sizeof(DWORD) * 2;

	// コマンドヘッダーのHMACを計算する
	hmac.CalcHmac(nonce, read);
	hmac.CalcHmac(*pbdata, sizeof(DWORD) * 2);
	hmac.GetHmac(cmd + sizeof(DWORD) * 2, hmac.GetHashSize());

	if (*pndata > sizeof(DWORD) * 2) {
		// コマンド本体のHMACを計算する
		hmac.CalcHmac(nonce, read);
		hmac.CalcHmac(*pbdata + sizeof(DWORD) * 2, *pndata - sizeof(DWORD) * 2);
		hmac.GetHmac(cmd + sizeof(DWORD) * 2 + hmac.GetHashSize(), hmac.GetHashSize());
	}

	// オリジナルのコマンドパケットを認証応答パケットの後ろに追加する
	memcpy(cmd + sizeAuthPacket, *pbdata, *pndata);

	SAFE_DELETE_ARRAY(*pbdata);
	*pbdata = cmd;
	*pndata += sizeAuthPacket;
	return CMD_SUCCESS;
}

DWORD CSendCtrlCmd::SendTCP(CSendCtrlCmd *t, CMD_STREAM* sendCmd, CMD_STREAM* resCmd)
{
	return t->SendTCP(t->ip, t->port, t->connectTimeOut, sendCmd, resCmd);
}
DWORD CSendCtrlCmd::SendTCP(wstring ip, DWORD port, DWORD timeOut, CMD_STREAM* sendCmd, CMD_STREAM* resCmd)
{
	if( sendCmd == NULL || resCmd == NULL ){
		return CMD_ERR_INVALID_ARG;
	}

	struct sockaddr_in server;
	SOCKET sock;

	sock = socket(AF_INET, SOCK_STREAM, 0);
	server.sin_family = AF_INET;
	server.sin_port = htons((WORD)port);
	string strA = "";
	WtoA(ip, strA);
	server.sin_addr.S_un.S_addr = inet_addr(strA.c_str());

	int ret = connect(sock, (struct sockaddr *)&server, sizeof(server));
	if( ret == SOCKET_ERROR ){
		int a= GetLastError();
		wstring aa;
		Format(aa,L"%d",a);
		OutputDebugString(aa.c_str());
		closesocket(sock);
		return CMD_ERR_CONNECT;
	}

	// 送信パケット生成
	DWORD sizeData = sizeof(DWORD) * 2 + sendCmd->dataSize;
	BYTE *data = new BYTE[sizeData];
	((DWORD*)data)[0] = sendCmd->param;
	((DWORD*)data)[1] = sendCmd->dataSize;
	memcpy(data + sizeof(DWORD) * 2, sendCmd->data, sendCmd->dataSize);
	SAFE_DELETE_ARRAY(sendCmd->data);
	sendCmd->data = data;

	if (Authenticate(sock, &sendCmd->data, &sizeData) != CMD_SUCCESS) {
		closesocket(sock);
		return CMD_ERR;
	}

	//送信: 認証応答パケットとコマンドパケットをまとめて送る
	ret = send(sock, (char*)sendCmd->data, sizeData, 0);
	if (ret == SOCKET_ERROR) {
		closesocket(sock);
		return CMD_ERR;
	}

	DWORD read = 0;
	DWORD head[2];
	//受信
	if( RecvAll(sock, (char*)head, sizeof(DWORD)*2, 0) != sizeof(DWORD)*2 ){
		closesocket(sock);
		return CMD_ERR;
	}
	resCmd->param = head[0];
	resCmd->dataSize = head[1];
	if( resCmd->dataSize > 0 ){
		resCmd->data = new BYTE[resCmd->dataSize];
		if( RecvAll(sock, (char*)resCmd->data, resCmd->dataSize, 0) != resCmd->dataSize ){
			closesocket(sock);
			return CMD_ERR;
		}
	}
	closesocket(sock);

	return resCmd->param;
}

#endif

DWORD CSendCtrlCmd::SendFileCopy(
	wstring val,
	BYTE** resVal,
	DWORD* resValSize
	)
{
	CMD_STREAM res;
	DWORD ret = SendCmdData(CMD2_EPG_SRV_FILE_COPY, val, &res);

	if( ret == CMD_SUCCESS ){
		if( res.dataSize == 0 ){
			return CMD_ERR;
		}
		*resValSize = res.dataSize;
		*resVal = new BYTE[res.dataSize];
		memcpy(*resVal, res.data, res.dataSize);
	}
	return ret;
}

DWORD CSendCtrlCmd::SendGetEpgFile2(
	wstring val,
	BYTE** resVal,
	DWORD* resValSize
	)
{
	CMD_STREAM res;
	DWORD ret = SendCmdData2(CMD2_EPG_SRV_GET_EPG_FILE2, val, &res);

	if( ret == CMD_SUCCESS ){
		WORD ver = 0;
		DWORD readSize = 0;
		if( ReadVALUE(&ver, res.data, res.dataSize, &readSize) == FALSE || res.dataSize <= readSize ){
			return CMD_ERR;
		}
		*resValSize = res.dataSize - readSize;
		*resVal = new BYTE[*resValSize];
		memcpy(*resVal, res.data + readSize, *resValSize);
	}
	return ret;
}

DWORD CSendCtrlCmd::SendCmdStream(CMD_STREAM* send, CMD_STREAM* res)
{
	DWORD ret = CMD_ERR;
	CMD_STREAM tmpRes;

	if( res == NULL ){
		res = &tmpRes;
	}
	if (this->pfnSend) {
		ret = this->pfnSend(this, send, res);
	}

	return ret;
}

