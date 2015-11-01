//  (C) Copyright Nekopanda 2015.
#pragma once

#include <vector>
#include <map>

#include "../../Common/EpgTimerUtil.h"
#include "../../Common/TimeUtil.h"
#include "../../Common/PathUtil.h"
#include "../../Common/FileUtil.h"

#include "LightTsUtils.h"
#include "EpgDBManager.h"

struct REC_EVENT_INFO : public EPGDB_EVENT_INFO {
	DWORD recFileId;
	DWORD loadErrorCount;
	CMediaData rawData;

	int64_t startTime64;
	wstring filePath;
	bool fileExist;

	REC_EVENT_INFO()
		: EPGDB_EVENT_INFO()
		, recFileId(0)
		, loadErrorCount(0)
		, startTime64(0)
		, fileExist(false)
	{ }

	bool HasEpgInfo() const { return rawData.GetSize() > 0; }
};

struct REC_EVENT_SERVICE_DATA {
	std::vector<REC_EVENT_INFO*> eventList;

	const REC_EVENT_SERVICE_DATA* operator->() const {
		return this;
	}
};

// �^��t�@�C���̔ԑg���f�[�^�x�[�X
// �V�����ǉ����ꂽ�t�@�C����New�ɓ������
// mergeNew()���Ăяo���ƃf�[�^�{�̂ƃ}�[�W�����
// 
//
class CRecEventDB {
	//
public:

	typedef std::map<DWORD, REC_FILE_INFO> REC_INFO_MAP;
	typedef std::map<DWORD, REC_EVENT_INFO*> REC_EVENT_MAP;
	typedef std::map<LONGLONG, REC_EVENT_SERVICE_DATA> SERVICE_EVENT_MAP;

	bool Load(const std::wstring& filePath, const REC_INFO_MAP& recFiles) {
		OutputDebugString(L"Start Load RecEventDB\r\n");
		DWORD time = GetTickCount();

		Clear();

		this->filePath = filePath;
		if (LoadFile() == false) {
			OutputDebugString(L"RecEventDB�̓ǂݍ��݂Ɏ��s���܂���\r\n");
			return false;
		}
		LoadRecFiles(recFiles);
		needUpdateServiceMap = true;

		_OutputDebugString(L"End Load RecEventDB %dmsec\r\n", GetTickCount() - time);
		return true;
	}

	void AddRecInfo(const REC_FILE_INFO& item) {
		REC_EVENT_INFO* eventInfo = CreateNew(item.id);
		eventInfo->recFileId = item.id;
		eventInfo->filePath = item.recFilePath;

		if (item.recFilePath.size() > 0) {
			directoryCache.CachePath(item.recFilePath);
			LoadTs(eventInfo, item.recFilePath, item.serviceID, item.eventID, false);
			needUpdateServiceMap = true;
		}
	}

	bool Save() {
		if (filePath.empty()) {
			return true;
		}

		// �t�@�C�����J��
		HANDLE hFile = _CreateDirectoryAndFile(this->filePath.c_str(), GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
		if (hFile == INVALID_HANDLE_VALUE) {
			_OutputDebugString(L"CRecEventDB::Save(): Error: Cannot open file: %s\r\n", this->filePath.c_str());
			return false;
		}

		{
			// �o�b�t�@�ɏ�������
			CMediaData buffer;

			buffer.Add((DWORD)1); // �o�[�W����

			for (auto recInfo : GetAll()) {
				buffer.Add(recInfo->recFileId);
				buffer.Add(recInfo->loadErrorCount);
				buffer.Add(recInfo->rawData.GetSize());
				buffer.AddData(recInfo->rawData.GetData(), recInfo->rawData.GetSize());
			}

			// �t�@�C���ɏ�������
			DWORD dwWrite = 0;
			WriteFile(hFile, buffer.GetData(), buffer.GetSize(), &dwWrite, NULL);
			CloseHandle(hFile);

			if (dwWrite != buffer.GetSize()) {
				OutputDebugString(L"Error? CRecEventDB::Save() \r\n");
				return false;
			}
		}

		return true;
	}

	void Clear() {
		for (std::pair<DWORD, REC_EVENT_INFO*> entry : eventMapNew) {
			SAFE_DELETE(entry.second);
		}
		eventMapNew.clear();
		serviceMapNew.clear();
		for (std::pair<DWORD, REC_EVENT_INFO*> entry : eventMap) {
			SAFE_DELETE(entry.second);
		}
		eventMap.clear();
		serviceMap.clear();
	}

	void MergeNew() {
		eventMap.insert(eventMapNew.begin(), eventMapNew.end());
		AddToserviceMapNew(eventMapNew, serviceMap);
		eventMapNew.clear();
		serviceMapNew.clear();
	}
	
	// �^��t�@�C���������@�}�b�`�����^��t�@�C����ID��Ԃ�
	vector<DWORD> SearchRecFile(const EPGDB_SEARCH_KEY_INFO& item, bool fromNew = false) {
		if (fromNew) {
			UpdateServiceMap();
			return SearchRecFile_(item, serviceMapNew);
		}
		return SearchRecFile_(item, serviceMap);
	}

	void UpdateFileExist() {
		DWORD time = GetTickCount();

		directoryCache.UpdateDirectoryInfo();
		for (auto recInfo : GetAll()) {
			if (recInfo->filePath.size() > 0) {
				recInfo->fileExist = directoryCache.Exist(recInfo->filePath, true);
			}
		}

		_OutputDebugString(L"UpdateFileExist %dmsec\r\n", GetTickCount() - time);
	}

	int GetNewCount() const {
		return (int)eventMapNew.size();
	}

	// mergeNew�Ăяo���ザ��Ȃ��ƐV�����̂�Get�ł��Ȃ�
	const REC_EVENT_INFO* Get(DWORD id) {
		auto it = eventMap.find(id);
		if (it != eventMap.end()) {
			return it->second;
		}
		return NULL;
	}

	bool HasEpgInfo(DWORD id) {
		auto it = eventMap.find(id);
		if (it != eventMap.end()) {
			return it->second->HasEpgInfo();
		}
		return false;
	}

private:
	wstring filePath;
	REC_EVENT_MAP eventMapNew;
	SERVICE_EVENT_MAP serviceMapNew;
	bool needUpdateServiceMap;

	REC_EVENT_MAP eventMap;
	SERVICE_EVENT_MAP serviceMap;

	CDirectoryCache directoryCache;

	CTsPacketParser packetParser;
	CEitDetector eitDetector;

	class PacketWriter : public CTsFilter{
	public:
		virtual void StorePacket(CTsPacket* pPacket) {
			buffer->AddData(pPacket->GetData(), pPacket->GetSize());
		}
		CMediaData* buffer;
	};

	vector<REC_EVENT_INFO*> GetAll() {
		vector<REC_EVENT_INFO*> v;
		v.reserve(eventMap.size() + eventMapNew.size());
		for (auto& entry : eventMap) v.push_back(entry.second);
		for (auto& entry : eventMapNew) v.push_back(entry.second);
		return v;
	}

	bool LoadFile() {
		if (filePath.empty()) {
			return false;
		}
		HANDLE hFile = CreateFile(filePath.c_str(), GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
		if (hFile == INVALID_HANDLE_VALUE) {
			if (GetLastError() == ERROR_FILE_NOT_FOUND) {
				return true;
			}
			OutputDebugString(L"CRecEventDB::Load(): Error: Cannot open file\r\n");
			return false;
		}

		CMediaData rawDataBuffer;
		DWORD dwFileSize = GetFileSize(hFile, NULL);
		if (dwFileSize != INVALID_FILE_SIZE && dwFileSize != 0) {
			rawDataBuffer.SetSize(dwFileSize);
			DWORD dwRead;
			if (ReadFile(hFile, rawDataBuffer.GetData(), dwFileSize, &dwRead, NULL) && dwRead != 0) {
				LoadRawData(rawDataBuffer.GetData(), rawDataBuffer.GetSize());
				CloseHandle(hFile);
				return true;
			}
		}
		CloseHandle(hFile);
		return false;
	}

	bool LoadEventInfo(REC_EVENT_INFO* eventInfo, BYTE* cur, BYTE* end) {
		WORD eventId; if (!ReadFromMemory(&eventId, cur, end)) return false;
		DWORD patLen; if (!ReadFromMemory(&patLen, cur, end)) return false;
		BYTE* patData = cur; cur += patLen;
		DWORD eitLen; if (!ReadFromMemory(&eitLen, cur, end)) return false;
		BYTE* eitData = cur; cur += patLen;
		if (cur > end) return false;

		CMediaData eitSectionData(eitData, eitLen);
		CSiSectionEIT eit(&eitSectionData);
		if (eit.Check() && eit.Parse()) {
			for (int i = 0; i < eit.GetEventListCount(); ++i) {
				if (eit.GetEventInfo(i).GetEventID() == eventId) {
					eitDetector.GetEITConverter()->Feed(eit, i, eventInfo);
					return true;
				}
			}
		}
		return false;
	}

	void LoadRawData(BYTE* data, DWORD size) {
		if (eitDetector.Initialize() == false) return;

		BYTE* cur = data;
		BYTE* end = cur + size;

		DWORD dwVersion; if (!ReadFromMemory(&dwVersion, cur, end)) return;
		if (dwVersion != 1) {
			// ���Ή��o�[�W����
			return;
		}

		while (cur < end) {
			DWORD fileId; if (!ReadFromMemory(&fileId, cur, end)) break;
			DWORD loadErrorCount; if (!ReadFromMemory(&loadErrorCount, cur, end)) break;
			DWORD dataLen; if (!ReadFromMemory(&dataLen, cur, end)) break;

			REC_EVENT_INFO* eventInfo = CreateNew(fileId);

			eventInfo->recFileId = fileId;
			eventInfo->loadErrorCount = loadErrorCount;

			if (dataLen > 0) {
				eventInfo->rawData.AddData(cur, dataLen);

				if (cur + dataLen > end) {
					_OutputDebugString(L"RecEventDB�ǂݍ��ݒ��T�C�Y�G���[");
				}
				else {
					if (LoadEventInfo(eventInfo, cur, end) == false) {
						_OutputDebugString(L"RecEventDB�ǂݍ��ݒ��T�C�Y�G���[");
					}
					else {
						eventInfo->startTime64 = ConvertI64Time(eventInfo->start_time);
					}
					cur += dataLen;
				}
			}
		}
	}

	void LoadTs(REC_EVENT_INFO* eventInfo, const std::wstring& recFilePath, WORD serviceId, WORD eventId, bool withCache) {
		if (eitDetector.Initialize() == false) return;

		HANDLE hFile = directoryCache.Open(recFilePath.c_str(), withCache);
		if (hFile == INVALID_HANDLE_VALUE) {
			return;
		}

		packetParser.SetOutputFilter(&eitDetector);
		packetParser.Reset();
		eitDetector.Reset();
		eitDetector.SetTarget(eventInfo, serviceId, eventId);

		CMediaData buffer;
		buffer.SetSize(256 * 1024);

		// ���Ԉʒu�܂ŃV�[�N
		DWORD sizeHigh = 0;
		DWORD sizeLow = GetFileSize(hFile, &sizeHigh);
		int64_t mid = ((int64_t(sizeHigh) << 32) + sizeLow) / 4;
		LONG distLow = (LONG)mid;
		LONG distHidh = (LONG)(mid >> 32);
		SetFilePointer(hFile, distLow, &distHidh, FILE_BEGIN);

		DWORD dwRead = 0;
		DWORD dwTotalRead = 0;
		while (ReadFile(hFile, buffer.GetData(), buffer.GetSize(), &dwRead, NULL) && dwRead > 0) {
			dwTotalRead += dwRead;
			packetParser.InputRawData(buffer.GetData(), dwRead);
			if (eitDetector.IsDetected()) {
				CloseHandle(hFile);
				
				CMediaData* patData = eitDetector.GetPATData();
				CMediaData* eitData = eitDetector.GetEITData();
				DWORD patSize = patData->GetSize();
				DWORD eitSize = eitData->GetSize();
				eventInfo->rawData.ClearBuffer();
				eventInfo->rawData.Add(eventId);
				eventInfo->rawData.Add(patSize);
				eventInfo->rawData.AddData(*patData);
				eventInfo->rawData.Add(eitSize);
				eventInfo->rawData.AddData(*eitData);

				eventInfo->startTime64 = ConvertI64Time(eventInfo->start_time);

				return;
			}
			if (dwTotalRead > 128 * 1024 * 1024) { // 128MB�ȏ�ǂ�
				_OutputDebugString(L"RecEventEB LoadTs Failed %s : %d KB\r\n", recFilePath.c_str(), dwTotalRead / 1024);
				break;
			}
		}

		CloseHandle(hFile);
	}

	void LoadRecFiles(const REC_INFO_MAP& recFiles) {
		// �^��t�@�C���̃p�X���L���b�V�����Ă���
		for (const auto& recInfo : recFiles) {
			directoryCache.CachePath(recInfo.second.recFilePath);
		}
		directoryCache.UpdateDirectoryInfo();

		for (const auto& recInfo : recFiles) {
			DWORD fileId = recInfo.first;
			REC_EVENT_INFO* eventInfo = GetOrNew(fileId);
			eventInfo->recFileId = fileId;
			eventInfo->filePath = recInfo.second.recFilePath;

			if (recInfo.second.recFilePath.size() > 0) {

				if (eventInfo->HasEpgInfo() == false && eventInfo->loadErrorCount <= 4) {
					LoadTs(eventInfo, recInfo.second.recFilePath, recInfo.second.serviceID, recInfo.second.eventID, true);
					if (!eventInfo->HasEpgInfo()) {
						eventInfo->loadErrorCount++;
					}
				}
			}
		}
	}

	void UpdateServiceMap() {
		if (needUpdateServiceMap) {
			serviceMapNew.clear();
			AddToserviceMapNew(eventMapNew, serviceMapNew);
			needUpdateServiceMap = false;
		}
	}

	void AddToserviceMapNew(REC_EVENT_MAP& from, SERVICE_EVENT_MAP& to) {
		for (std::pair<DWORD, REC_EVENT_INFO*> entry : from) {
			REC_EVENT_INFO* eventInfo = entry.second;
			if (eventInfo->HasEpgInfo()) {
				// �L���ȃf�[�^������Ƃ�����
				LONGLONG key = _Create64Key(eventInfo->original_network_id, eventInfo->transport_stream_id, eventInfo->service_id);
				to[key].eventList.push_back(eventInfo);
			}
		}
	}

	REC_EVENT_INFO* GetOrNew(DWORD fileId) {
		auto it = eventMapNew.find(fileId);
		if (it == eventMapNew.end()) {
			REC_EVENT_INFO* data = new REC_EVENT_INFO();
			eventMapNew[fileId] = data;
			return data;
		}
		return it->second;
	}

	REC_EVENT_INFO* CreateNew(DWORD fileId) {
		auto it = eventMapNew.find(fileId);
		if (it != eventMapNew.end()) {
			delete it->second;
		}
		return eventMapNew[fileId] = new REC_EVENT_INFO();
	}

	// �^��t�@�C���������@�}�b�`�����^��t�@�C����ID��Ԃ�
	vector<DWORD> SearchRecFile_(const EPGDB_SEARCH_KEY_INFO& item, SERVICE_EVENT_MAP& targetEvents) {
		vector<REC_EVENT_INFO*> list;
		IRegExpPtr regExp;

		// ����
		EPGDB_SEARCH_KEY_INFO key = item;
		if (key.andKey.compare(0, 7, L"^!{999}") == 0) {
			//�����������L�[���[�h���폜
			key.andKey.erase(0, 7);
		}
		CEpgDBManager::SearchEvent(&key, targetEvents, [&](CEpgDBManager::SEARCH_RESULT_EVENT result) {
			REC_EVENT_INFO* eventInfo = (REC_EVENT_INFO*)result.info;
			list.push_back(eventInfo);
		}, regExp);

		// �������Ƀ\�[�g
		std::stable_sort(list.begin(), list.end(), [](REC_EVENT_INFO* a, REC_EVENT_INFO* b) {
			return a->startTime64 < b->startTime64;
		});

		// ID���X�g�ɕϊ�
		vector<DWORD> ret;
		ret.reserve(list.size());
		for (REC_EVENT_INFO* info : list) {
			ret.push_back(info->recFileId);
		}

		return ret;
	}
};
