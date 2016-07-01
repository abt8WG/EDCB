#pragma once

#include "../Common/StructDef.h"
#include "../Common/EpgDataCap3Def.h"
#include "BonCtrlDef.h"

#include "../Common/ParseTextInstances.h"

class CChSetUtil
{
public:
	CChSetUtil(void);

	//チャンネル設定ファイルを読み込む
	BOOL LoadChSet(
		wstring chSet4FilePath,
		wstring chSet5FilePath
		);

	//チャンネル設定ファイルを保存する
	BOOL SaveChSet(
		wstring chSet4FilePath,
		wstring chSet5FilePath
		);

	//チャンネルスキャン用にクリアする
	BOOL Clear();

	//チャンネル情報を追加する
	BOOL AddServiceInfo(
		DWORD space,
		DWORD ch,
		wstring chName,
		SERVICE_INFO* serviceInfo
		);

	//サービス一覧を取得する
	BOOL GetEnumService(
		vector<CH_DATA4>* serviceList
		);

	//IDから物理チャンネルを検索する
	BOOL GetCh(
		WORD ONID,
		WORD TSID,
		DWORD& space,
		DWORD& ch
		);

	//EPG取得対象のサービス一覧を取得する
	//引数：
	// chList		[OUT]EPG取得するチャンネル一覧
	void GetEpgCapService(
		vector<EPGCAP_SERVICE_INFO>* chList
		);

	//現在のチューナに限定されないEPG取得対象のサービス一覧を取得する
	vector<EPGCAP_SERVICE_INFO> GetEpgCapServiceAll(
		int ONID = -1,
		int TSID = -1
		);

	//部分受信サービスかどうか
	BOOL IsPartial(
		WORD ONID,
		WORD TSID,
		WORD SID
		);

	//サービスタイプが映像サービスかどうか
	static BOOL IsVideoServiceType(
		WORD serviceType
		){
		return serviceType == 0x01 //デジタルTV
			|| serviceType == 0xA5 //プロモーション映像
			|| serviceType == 0xAD //超高精細度4K専用TV
			;
	}

protected:
	CParseChText4 chText4;
	CParseChText5 chText5;
};

