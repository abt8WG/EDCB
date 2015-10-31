#pragma once
#include <vector>
#include <string>

//UPnP��UDP(Port1900)������S������T�[�o
//UPnPCtrl�t�H���_�ɂ���C����x�[�X(?)�̃R�[�h��C++�ōĎ�����������
class CUpnpSsdpServer
{
public:
	static const int RECV_BUFF_SIZE = 2048;
	static const unsigned int NOTIFY_INTERVAL_SEC = 1000;
	static const unsigned int NOTIFY_FIRST_DELAY_SEC = 5;
	struct SSDP_TARGET_INFO {
		std::string target;
		std::string location;
		std::string usn;
		bool notifyFlag;
	};
	CUpnpSsdpServer();
	~CUpnpSsdpServer();
	bool Start(const std::vector<SSDP_TARGET_INFO>& targetList_);
	void Stop();
	static std::string GetUserAgent();
private:
	CUpnpSsdpServer(const CUpnpSsdpServer&);
	CUpnpSsdpServer& operator=(const CUpnpSsdpServer&);
	static std::vector<std::string> GetNICList();
	static UINT WINAPI SsdpThread(LPVOID param);
	std::string GetMSearchReply(const char* header, const char* host) const;
	void SendNotifyAliveOrByebye(bool byebyeFlag, const std::vector<std::string>& nicList);
	HANDLE ssdpThread;
	bool stopFlag;
	std::vector<SSDP_TARGET_INFO> targetList;
};
