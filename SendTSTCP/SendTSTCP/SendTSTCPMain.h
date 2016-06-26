#pragma once

#include <winsock2.h>
#include <ws2tcpip.h>

#include "../../Common/StringUtil.h"
#pragma comment(lib, "Ws2_32.lib")

class CSendTSTCPMain
{
public:
	CSendTSTCPMain(void);
	~CSendTSTCPMain(void);

	//DLL�̏�����
	//�߂�l�FTRUE:�����AFALSE:���s
	BOOL Initialize(
		);

	//DLL�̊J��
	//�߂�l�F�Ȃ�
	void UnInitialize(
		);

	//���M���ǉ�
	//�߂�l�F�G���[�R�[�h
	DWORD AddSendAddr(
		LPCWSTR lpcwszIP,
		DWORD dwPort
		);

	//���M��N���A
	//�߂�l�F�G���[�R�[�h
	DWORD ClearSendAddr(
		);

	//�f�[�^���M���J�n
	//�߂�l�F�G���[�R�[�h
	DWORD StartSend(
		);

	//�f�[�^���M���~
	//�߂�l�F�G���[�R�[�h
	DWORD StopSend(
		);

	//�f�[�^���M���J�n
	//�߂�l�F�G���[�R�[�h
	DWORD AddSendData(
		BYTE* pbData,
		DWORD dwSize
		);

	//���M�o�b�t�@���N���A
	//�߂�l�F�G���[�R�[�h
	DWORD ClearSendBuff(
		);


protected:
	HANDLE m_hStopConnectEvent;
	HANDLE m_hConnectThread;
	HANDLE m_hStopSendEvent;
	HANDLE m_hSendThread;

	CRITICAL_SECTION m_sendLock;
	CRITICAL_SECTION m_buffLock;

	typedef struct _TS_DATA{
		BYTE* pbBuff;
		DWORD dwSize;
		_TS_DATA(void){
			pbBuff = NULL;
			dwSize = 0;
		}
		~_TS_DATA(void){
			SAFE_DELETE_ARRAY(pbBuff);
		}
	}TS_DATA;
	vector<TS_DATA*> m_TSBuff;

	typedef struct _SEND_INFO{
		string strIP;
		DWORD dwPort;
		SOCKET sock;
		BOOL bConnect;
	}SEND_INFO;
	map<wstring, SEND_INFO> m_SendList;

protected:
	static UINT WINAPI SendThread(LPVOID pParam);
	static UINT WINAPI ConnectThread(LPVOID pParam);

};
