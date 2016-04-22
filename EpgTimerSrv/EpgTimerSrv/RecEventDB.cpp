#include "stdafx.h"

#include "RecEventDB.h"

#if ENABLE_GPL_CODE

#include "../../Common/EpgTimerUtil.h"
#include "../../Common/TimeUtil.h"
#include "../../Common/PathUtil.h"

bool CRecEventDB::Load(const std::wstring& filePath, const REC_INFO_MAP& recFiles) {
	OutputDebugString(L"Start Load RecEventDB\r\n");
	DWORD time = GetTickCount();

	Clear();

	this->filePath = filePath;

	RegistRecFiles(recFiles);
	LoadDBFile();
	ReadTsFiles(recFiles);
	
	needUpdateServiceMap = true;

	_OutputDebugString(L"End Load RecEventDB %dmsec\r\n", GetTickCount() - time);
	return true;
}

void CRecEventDB::AddRecInfo(const REC_FILE_INFO& item) {
	// �p�X���Ȃ��i�^��ɐ������Ă��Ȃ��j���ڂ͖���
	if (item.recFilePath.size() > 0) {
		idMap[item.recFilePath] = item.id;
		REC_EVENT_INFO* eventInfo = CreateNew(item.id);
		eventInfo->recFileId = item.id;
		eventInfo->filePath = item.recFilePath;

		directoryCache.CachePath(item.recFilePath);
		ReadTsFile(eventInfo, item.recFilePath, item.serviceID, item.eventID, false);
		needUpdateServiceMap = true;
	}
}

bool CRecEventDB::Save() {
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
			buffer.Add((DWORD)recInfo->filePath.size());
			buffer.AddData((BYTE*)recInfo->filePath.c_str(), recInfo->filePath.size() * sizeof(wchar_t));
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

void CRecEventDB::Clear() {
	for (std::pair<DWORD, const REC_EVENT_INFO*> entry : eventMapNew) {
		SAFE_DELETE(entry.second);
	}
	eventMapNew.clear();
	serviceMapNew.clear();
	for (std::pair<DWORD, const REC_EVENT_INFO*> entry : eventMap) {
		SAFE_DELETE(entry.second);
	}
	eventMap.clear();
	serviceMap.clear();
}

void CRecEventDB::MergeNew() {
	eventMap.insert(eventMapNew.begin(), eventMapNew.end());
	AddToServiceMap(eventMapNew, serviceMap);
	eventMapNew.clear();
	serviceMapNew.clear();
}

// �^��t�@�C���������@�}�b�`�����^��t�@�C����ID��Ԃ�
vector<DWORD> CRecEventDB::SearchRecFile(const EPGDB_SEARCH_KEY_INFO& item, bool fromNew) {
	if (fromNew) {
		UpdateServiceMap();
		return SearchRecFile_(item, serviceMapNew);
	}
	return SearchRecFile_(item, serviceMap);
}

void CRecEventDB::UpdateFileExist() const {
	DWORD time = GetTickCount();

	directoryCache.UpdateDirectoryInfo();
	for (auto recInfo : GetAll()) {
		if (recInfo->filePath.size() > 0) {
			recInfo->fileExist = directoryCache.Exist(recInfo->filePath, true);
		}
	}

	_OutputDebugString(L"UpdateFileExist %dmsec\r\n", GetTickCount() - time);
}

// mergeNew�Ăяo���ザ��Ȃ��ƐV�����̂�Get�ł��Ȃ�
const REC_EVENT_INFO* CRecEventDB::Get(DWORD id) const {
	auto it = eventMap.find(id);
	if (it != eventMap.end()) {
		return it->second;
	}
	return NULL;
}

bool CRecEventDB::HasEpgInfo(DWORD id) const {
	auto it = eventMap.find(id);
	if (it != eventMap.end()) {
		return it->second->HasEpgInfo();
	}
	return false;
}

vector<REC_EVENT_INFO*> CRecEventDB::GetAll() const {
	vector<REC_EVENT_INFO*> v;
	v.reserve(eventMap.size() + eventMapNew.size());
	for (auto& entry : eventMap) v.push_back(entry.second);
	for (auto& entry : eventMapNew) v.push_back(entry.second);
	return v;
}

void CRecEventDB::RegistRecFiles(const REC_INFO_MAP& recFiles) {
	for (const auto& recInfo : recFiles) {
		DWORD fileId = recInfo.first;
		if (recInfo.second.recFilePath.size() > 0) {
			idMap[recInfo.second.recFilePath] = fileId;
			REC_EVENT_INFO* eventInfo = CreateNew(fileId);
			eventInfo->recFileId = fileId;
			eventInfo->filePath = recInfo.second.recFilePath;
			directoryCache.CachePath(recInfo.second.recFilePath);
		}
	}
	directoryCache.UpdateDirectoryInfo();
}

bool CRecEventDB::LoadDBFile() {
	if (filePath.empty()) {
		return false;
	}
	HANDLE hFile = CreateFile(filePath.c_str(), GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
	if (hFile == INVALID_HANDLE_VALUE) {
		if (GetLastError() == ERROR_FILE_NOT_FOUND) {
			return true;
		}
		OutputDebugString(L"CRecEventDB::LoadDBFile(): Error: Cannot open file\r\n");
		return false;
	}

	CMediaData rawDataBuffer;
	DWORD dwFileSize = GetFileSize(hFile, NULL);
	if (dwFileSize != INVALID_FILE_SIZE && dwFileSize != 0) {
		rawDataBuffer.SetSize(dwFileSize);
		DWORD dwRead;
		if (ReadFile(hFile, rawDataBuffer.GetData(), dwFileSize, &dwRead, NULL) && dwRead != 0) {
			LoadDBRawData(rawDataBuffer.GetData(), rawDataBuffer.GetSize());
			CloseHandle(hFile);
			return true;
		}
	}
	CloseHandle(hFile);
	return false;
}

bool CRecEventDB::LoadEventInfo(REC_EVENT_INFO* eventInfo, BYTE* cur, BYTE* end) {
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

void CRecEventDB::LoadDBRawData(BYTE* data, DWORD size) {
	if (eitDetector.Initialize() == false) return;

	BYTE* cur = data;
	BYTE* end = cur + size;

	DWORD dwVersion; if (!ReadFromMemory(&dwVersion, cur, end)) return;
	if (dwVersion != 1) {
		// ���Ή��o�[�W����
		return;
	}

	while (cur < end) {
		DWORD filepathLen; if (!ReadFromMemory(&filepathLen, cur, end)) break;
		if (cur + filepathLen * sizeof(wchar_t) > end) break;
		wstring filepath((wchar_t*)cur, filepathLen); cur += filepathLen * sizeof(wchar_t);

		DWORD loadErrorCount; if (!ReadFromMemory(&loadErrorCount, cur, end)) break;
		DWORD dataLen; if (!ReadFromMemory(&dataLen, cur, end)) break;

		if (cur < end) {
			auto it = idMap.find(filepath);
			if (it != idMap.end()) {
				auto it2 = eventMapNew.find(it->second);
				if (it2 != eventMapNew.end()) {
					REC_EVENT_INFO* eventInfo = it2->second;

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
						}
					}
				}
			}
		}

		cur += dataLen;
	}
}

void CRecEventDB::ReadTsFile(REC_EVENT_INFO* eventInfo, const std::wstring& recFilePath, WORD serviceId, WORD eventId, bool withCache) {
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

void CRecEventDB::ReadTsFiles(const REC_INFO_MAP& recFiles) {
	for (const auto& recInfo : recFiles) {
		if (recInfo.second.recFilePath.size() > 0) {
			auto it = idMap.find(recInfo.second.recFilePath);
			if (it != idMap.end()) {
				auto it2 = eventMapNew.find(it->second);
				if (it2 != eventMapNew.end()) {
					REC_EVENT_INFO* eventInfo = it2->second;
					if (eventInfo->HasEpgInfo() == false && eventInfo->loadErrorCount <= 4) {
						ReadTsFile(eventInfo, recInfo.second.recFilePath, recInfo.second.serviceID, recInfo.second.eventID, true);
						if (!eventInfo->HasEpgInfo()) {
							eventInfo->loadErrorCount++;
						}
					}
				}
			}
		}
	}
}

void CRecEventDB::UpdateServiceMap() {
	if (needUpdateServiceMap) {
		serviceMapNew.clear();
		AddToServiceMap(eventMapNew, serviceMapNew);
		needUpdateServiceMap = false;
	}
}

void CRecEventDB::AddToServiceMap(REC_EVENT_MAP& from, SERVICE_EVENT_MAP& to) {
	for (std::pair<DWORD, REC_EVENT_INFO*> entry : from) {
		REC_EVENT_INFO* eventInfo = entry.second;
		if (eventInfo->HasEpgInfo()) {
			// �L���ȃf�[�^������Ƃ�����
			LONGLONG key = _Create64Key(eventInfo->original_network_id, eventInfo->transport_stream_id, eventInfo->service_id);
			to[key].eventList.resize(to[key].eventList.size() + 1);
			to[key].eventList.back().DeepCopy(*eventInfo);
			//to[key].eventList.push_back(*eventInfo);
		}
	}
}

REC_EVENT_INFO* CRecEventDB::GetOrNew(DWORD fileId) {
	auto it = eventMapNew.find(fileId);
	if (it == eventMapNew.end()) {
		REC_EVENT_INFO* data = new REC_EVENT_INFO();
		eventMapNew[fileId] = data;
		return data;
	}
	return it->second;
}

REC_EVENT_INFO* CRecEventDB::CreateNew(DWORD fileId) {
	auto it = eventMapNew.find(fileId);
	if (it != eventMapNew.end()) {
		delete it->second;
	}
	return eventMapNew[fileId] = new REC_EVENT_INFO();
}

// �^��t�@�C���������@�}�b�`�����^��t�@�C����ID��Ԃ�
vector<DWORD> CRecEventDB::SearchRecFile_(const EPGDB_SEARCH_KEY_INFO& item, SERVICE_EVENT_MAP& targetEvents) {
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

#endif
