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
	SetSendMode(FALSE);
}

//�R�}���h���M���@�̐ݒ�
//�����F
// tcpFlag		[IN] TRUE�FTCP/IP���[�h�AFALSE�F���O�t���p�C�v���[�h
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

//���O�t���p�C�v���[�h���̐ڑ����ݒ�
//EpgTimerSrv.exe�ɑ΂���R�}���h�͐ݒ肵�Ȃ��Ă��i�f�t�H���g�l�ɂȂ��Ă���j
//�����F
// eventName	[IN]�r������pEvent�̖��O
// pipeName		[IN]�ڑ��p�C�v�̖��O
void CSendCtrlCmd::SetPipeSetting(
	LPCWSTR eventName,
	LPCWSTR pipeName
	)
{
	this->eventName = eventName;
	this->pipeName = pipeName;
	this->pfnSend = SendPipe;
}

//���O�t���p�C�v���[�h���̐ڑ����ݒ�i�ڔ��Ƀv���Z�XID�𔺂��^�C�v�j
//�����F
// pid			[IN]�v���Z�XID
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

//TCP/IP���[�h���̐ڑ����ݒ�
//�����F
// ip			[IN]�ڑ���IP
// port			[IN]�ڑ���|�[�g
// password		[IN]�ڑ���p�X���[�h
void CSendCtrlCmd::SetNWSetting(
	wstring ip,
	DWORD port,
	wstring password
	)
{
	this->ip = ip;
	this->port = port;
	this->hmac.Close();
	this->hmac.SelectHash((ALG_ID)CALG_MD5);
	this->hmac.Create(password);
	this->pfnSend = SendTCP;
}

//�ڑ��������̃^�C���A�E�g�ݒ�
// timeOut		[IN]�^�C���A�E�g�l�i�P�ʁFms�j
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

	//�ڑ��҂�
	//CreateEvent()���Ă͂����Ȃ��B�C�x���g���쐬����̂̓T�[�o�̎d���̂͂�
	//CreateEvent()���Ă��܂��ƃT�[�o���I��������͏�Ƀ^�C���A�E�g�܂ő҂�����邱�ƂɂȂ�
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

	//�ڑ�
	HANDLE pipe = CreateFile( pipeName, GENERIC_READ|GENERIC_WRITE, 0, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
	if( pipe == INVALID_HANDLE_VALUE ){
		_OutputDebugString(L"*+* ConnectPipe Err:%d\r\n", GetLastError());
		return CMD_ERR_CONNECT;
	}

	DWORD write = 0;

	//���M
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

	//��M
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

	// nonce�v��
	DWORD header[2];
	header[0] = CMD2_EPG_SRV_AUTH_REQUEST;
	header[1] = 0;
	if (send(sock, (char*)header, sizeof(header), 0) == SOCKET_ERROR) {
		return CMD_ERR;
	}

	// nonce ���󂯎��
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

	//�T�[�o�[������ nonce �ƃp�X���[�h���牞���p�P�b�g�����
	DWORD sizeAuthPacket = sizeof(DWORD) * 2 + hmac.GetHashSize();
	if (*pndata > sizeof(DWORD) * 2) {
		sizeAuthPacket += hmac.GetHashSize();
	}

	// �F�؉����p�P�b�g
	//   cmd[ 0�` 3] : CMD2_EPG_SRV_AUTH_REPLY
	//   cmd[ 4�` 7] : HMAC size (= 32 bytes)
	//   cmd[ 8�`23] : HMAC for header (16 bytes)
	//   cmd[24�`39] : HMAC for data (16 bytes) if exist;
	BYTE *cmd = new BYTE[sizeAuthPacket + *pndata];
	((DWORD*)cmd)[0] = CMD2_EPG_SRV_AUTH_REPLY;
	((DWORD*)cmd)[1] = sizeAuthPacket - sizeof(DWORD) * 2;

	// �R�}���h�w�b�_�[��HMAC���v�Z����
	hmac.CalcHmac(nonce, read);
	hmac.CalcHmac(*pbdata, sizeof(DWORD) * 2);
	hmac.GetHmac(cmd + sizeof(DWORD) * 2, hmac.GetHashSize());

	if (*pndata > sizeof(DWORD) * 2) {
		// �R�}���h�{�̂�HMAC���v�Z����
		hmac.CalcHmac(nonce, read);
		hmac.CalcHmac(*pbdata + sizeof(DWORD) * 2, *pndata - sizeof(DWORD) * 2);
		hmac.GetHmac(cmd + sizeof(DWORD) * 2 + hmac.GetHashSize(), hmac.GetHashSize());
	}

	// �I���W�i���̃R�}���h�p�P�b�g��F�؉����p�P�b�g�̌��ɒǉ�����
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

	// ���M�p�P�b�g����
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

	//���M: �F�؉����p�P�b�g�ƃR�}���h�p�P�b�g���܂Ƃ߂đ���
	ret = send(sock, (char*)sendCmd->data, sizeData, 0);
	if (ret == SOCKET_ERROR) {
		closesocket(sock);
		return CMD_ERR;
	}

	DWORD read = 0;
	DWORD head[2];
	//��M
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

