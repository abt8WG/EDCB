//  (C) Copyright Nekopanda 2015.
#pragma once

#include <vector>
#include <map>

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

// 録画ファイルの番組情報データベース
// 新しく追加されたファイルはNewに入れられる
// MergeNew()を呼び出すとデータ本体とマージされる
class CRecEventDB {
public:
	typedef std::map<DWORD, REC_FILE_INFO> REC_INFO_MAP;
	typedef std::map<DWORD, REC_EVENT_INFO*> REC_EVENT_MAP;
	typedef std::map<LONGLONG, REC_EVENT_SERVICE_DATA> SERVICE_EVENT_MAP;

	// DBファイルを読み込む
	bool Load(const std::wstring& filePath, const REC_INFO_MAP& recFiles);
	// 録画済みを追加
	void AddRecInfo(const REC_FILE_INFO& item);
	// DBをファイルに保存
	bool Save();
	// メモリ上のデータを削除
	void Clear();
	// 新規録画済みをデータ本体とマージ
	void MergeNew();
	// 録画ファイルを検索　マッチした録画ファイルのIDを返す
	vector<DWORD> SearchRecFile(const EPGDB_SEARCH_KEY_INFO& item, bool fromNew = false);
	// 録画ファイルの存在情報を更新
	void UpdateFileExist();
	// マージされていない新規録画済みの個数
	int GetNewCount() const { return (int)eventMapNew.size(); }
	// MergeNew呼び出し後じゃないと新しいのはGetできない
	const REC_EVENT_INFO* Get(DWORD id) const;
	bool HasEpgInfo(DWORD id) const;

private:
	// SERVICE_EVENT_MAPは検索用
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

	vector<REC_EVENT_INFO*> GetAll();
	
	bool LoadFile();
	bool LoadEventInfo(REC_EVENT_INFO* eventInfo, BYTE* cur, BYTE* end);
	void LoadRawData(BYTE* data, DWORD size);
	void LoadTs(REC_EVENT_INFO* eventInfo, const std::wstring& recFilePath, WORD serviceId, WORD eventId, bool withCache);
	void LoadRecFiles(const REC_INFO_MAP& recFiles);

	void UpdateServiceMap();
	void AddToServiceMap(REC_EVENT_MAP& from, SERVICE_EVENT_MAP& to);

	REC_EVENT_INFO* GetOrNew(DWORD fileId);
	REC_EVENT_INFO* CreateNew(DWORD fileId);
	
	// 録画ファイルを検索　マッチした録画ファイルのIDを返す
	vector<DWORD> SearchRecFile_(const EPGDB_SEARCH_KEY_INFO& item, SERVICE_EVENT_MAP& targetEvents);
};
