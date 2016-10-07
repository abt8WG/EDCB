#pragma once

#include "../../Common/ParseTextInstances.h"
#include "EpgDBManager.h"
#include "NotifyManager.h"
#include "TunerManager.h"
#include "BatManager.h"

//予約を管理しチューナに割り当てる
//必ずオブジェクト生成→Initialize()→…→Finalize()→破棄の順番で利用しなければならない
class CReserveManager
{
public:
	enum {
		CHECK_EPGCAP_END = 1,	//EPG取得が完了した
		CHECK_NEED_SHUTDOWN,	//システムシャットダウンを試みる必要がある
		CHECK_RESERVE_MODIFIED,	//予約になんらかの変化があった
	};
	CReserveManager(CNotifyManager& notifyManager_, CEpgDBManager& epgDBManager_);
	~CReserveManager();
	void Initialize();
	void Finalize();
	void ReloadSetting();
	//予約情報一覧を取得する
	vector<RESERVE_DATA> GetReserveDataAll(bool getRecFileName = false) const;
	//チューナ毎の予約情報を取得する
	vector<TUNER_RESERVE_INFO> GetTunerReserveAll() const;
	//予約情報を取得する
	bool GetReserveData(DWORD id, RESERVE_DATA* reserveData, bool getRecFileName = false) const;
	//予約情報を追加する
	bool AddReserveData(const vector<RESERVE_DATA>& reserveList, bool setReserveStatus = false, const bool noReportNotify = false);
	//予約情報を変更する
	bool ChgReserveData(const vector<RESERVE_DATA>& reserveList, bool setReserveStatus = false);
	//予約情報を削除する
	void DelReserveData(const vector<DWORD>& idList);
	//録画済み情報一覧を取得する
	vector<REC_FILE_INFO> GetRecFileInfoAll(bool getExtraInfo = true) const;
	//録画済み情報を取得する
	bool GetRecFileInfo(DWORD id, REC_FILE_INFO* recInfo, bool getExtraInfo = true) const;
	//録画済み情報を削除する
	void DelRecFileInfo(const vector<DWORD>& idList);
	//録画済み情報のプロテクトを変更する
	//infoList: 録画済み情報一覧(idとprotectFlagのみ参照)
	void ChgProtectRecFileInfo(const vector<REC_FILE_INFO>& infoList);
	//EIT[schedule]をもとに追従処理する
	void CheckTuijyu();
	//予約管理する
	//概ね1秒ごとに呼ぶ
	//戻り値: 0またはHIWORDにCHECK_*
	DWORD Check();
	//EPG取得開始を要求する
	bool RequestStartEpgCap();
	//チューナ起動やEPG取得やバッチ処理が行われているか
	bool IsActive() const;
	//baseTime以後に録画またはEPG取得を開始する最小時刻を取得する
	__int64 GetSleepReturnTime(__int64 baseTime) const;
	//指定イベントの予約が存在するかどうか
	bool IsFindReserve(WORD onid, WORD tsid, WORD sid, WORD eid) const;
	//指定時刻のプログラム予約があるかどうか
	bool IsFindProgramReserve(WORD onid, WORD tsid, WORD sid, __int64 startTime, DWORD durationSec) const;
	//指定サービスを利用できるチューナID一覧を取得する
	vector<DWORD> GetSupportServiceTuner(WORD onid, WORD tsid, WORD sid) const;
	bool GetTunerCh(DWORD tunerID, WORD onid, WORD tsid, WORD sid, DWORD* space, DWORD* ch) const;
	wstring GetTunerBonFileName(DWORD tunerID) const;
	bool IsOpenTuner(DWORD tunerID) const;
	//ネットワークモードでチューナを起動しチャンネル設定する
	//tunerIDList: 起動させるときはこのリストにあるチューナを候補にする
	bool SetNWTVCh(bool nwUdp, bool nwTcp, const SET_CH_INFO& chInfo, const vector<DWORD>& tunerIDList);
	//ネットワークモードのチューナを閉じる
	bool CloseNWTV();
	//予約が録画中であればその録画ファイル名を取得する
	bool GetRecFilePath(DWORD reserveID, wstring& filePath) const;
	//指定EPGイベントは録画済みかどうか
	bool IsFindRecEventInfo(const EPGDB_EVENT_INFO& info, WORD chkDay) const;
	//自動予約によって作成された指定イベントの予約を無効にする
	bool ChgAutoAddNoRec(WORD onid, WORD tsid, WORD sid, WORD eid);
	//チャンネル情報を取得する
	vector<CH_DATA5> GetChDataList() const;
	//パラメータなしの通知を追加する
	void AddNotifyAndPostBat(DWORD notifyID);
private:
	struct CHK_RESERVE_DATA {
		__int64 cutStartTime;
		__int64 cutEndTime;
		__int64 startOrder;
		__int64 effectivePriority;
		bool started;
		const RESERVE_DATA* r;
	};
	//チューナに割り当てられていない予約一覧を取得する
	vector<DWORD> GetNoTunerReserveAll() const;
	//予約をチューナに割り当てる
	//reloadTime: なんらかの変更があった最小予約位置
	void ReloadBankMap(__int64 reloadTime = 0);
	//ある予約をバンクに追加したときに発生するコスト(単位:10秒)を計算する
	//戻り値: 重なりが無ければ0、別チャンネルの重なりがあれば重なりの秒数だけ加算、同一チャンネルのみの重なりがあれば-1
	__int64 ChkInsertStatus(vector<CHK_RESERVE_DATA>& bank, CHK_RESERVE_DATA& inItem, bool modifyBank) const;
	//マージンを考慮した予約時刻を計算する(常にendTime>=startTime)
	void CalcEntireReserveTime(__int64* startTime, __int64* endTime, const RESERVE_DATA& data) const;
	//追従通知用メッセージを取得する
	static wstring GetNotifyChgReserveMessage(const RESERVE_DATA& oldInfo, const RESERVE_DATA& newInfo);
	//最新EPG(チューナからの情報)をもとに追従処理する
	void CheckTuijyuTuner();
	//ディスクの空き容量を調べて必要なら自動削除する
	void CheckAutoDel() const;
	//チューナ割り当てされていない古い予約を終了処理する
	void CheckOverTimeReserve();
	//予約終了を処理する
	//shutdownMode: 最後に処理した予約の録画後動作を記録
	void ProcessRecEnd(const vector<CTunerBankCtrl::CHECK_RESULT>& retList, int* shutdownMode = NULL);
	//EPG取得可能なチューナIDのリストを取得する
	vector<DWORD> GetEpgCapTunerIDList(__int64 now) const;
	//EPG取得処理を管理する
	//isEpgCap: EPG取得中のチューナが無ければfalse
	//戻り値: EPG取得が完了した瞬間にtrue
	bool CheckEpgCap(bool isEpgCap);
	//予約開始(視聴を除く)の最小時刻を取得する
	__int64 GetNearestRecReserveTime() const;
	//次のEPG取得時刻を取得する
	__int64 GetNextEpgCapTime(__int64 now, int* basicOnlyFlags = NULL) const;
	//バンクを監視して必要ならチューナを強制終了するスレッド
	static UINT WINAPI WatchdogThread(LPVOID param);
	//batPostManagerにバッチを追加する
	void AddPostBatWork(vector<BAT_WORK_INFO>& workList, LPCWSTR fileName);
	//バッチに渡す日時マクロを追加する
	static void AddTimeMacro(vector<pair<string, wstring>>& macroList, const SYSTEMTIME& startTime, DWORD durationSecond, LPCSTR suffix);
	//バッチに渡す予約情報マクロを追加する
	static void AddReserveDataMacro(vector<pair<string, wstring>>& macroList, const RESERVE_DATA& data, LPCSTR suffix);
	//バッチに渡す録画済み情報マクロを追加する
	static void AddRecInfoMacro(vector<pair<string, wstring>>& macroList, const REC_FILE_INFO& recInfo);

	mutable CRITICAL_SECTION managerLock;

	CNotifyManager& notifyManager;
	CEpgDBManager& epgDBManager;

	CParseReserveText reserveText;
	CParseRecInfoText recInfoText;
	CParseRecInfo2Text recInfo2Text;
	CParseChText5 chUtil;

	CTunerManager tunerManager;
	CBatManager batManager;
	CBatManager batPostManager;

	map<DWORD, std::unique_ptr<CTunerBankCtrl>> tunerBankMap;

	DWORD ngCapTimeSec;
	DWORD ngCapTunerTimeSec;
	bool epgCapTimeSync;
	//LOWORDに取得時刻の日曜日からのオフセット(分)、HIWORDに取得種別
	vector<DWORD> epgCapTimeList;
	vector<wstring> autoDelExtList;
	vector<wstring> autoDelFolderList;
	int defStartMargin;
	int defEndMargin;
	int notFindTuijyuHour;
	bool backPriority;
	bool fixedTunerPriority;
	int recInfo2DropChk;
	wstring recInfo2RegExp;
	bool defEnableCaption;
	bool defEnableData;
	bool errEndBatRun;
	wstring recNamePlugInFileName;
	bool recNameNoChkYen;
	int delReserveMode;

	DWORD checkCount;
	__int64 lastCheckEpgCap;
	bool epgCapRequested;
	bool epgCapWork;
	bool epgCapSetTimeSync;
	__int64 epgCapTimeSyncBase;
	__int64 epgCapTimeSyncDelayMin;
	__int64 epgCapTimeSyncDelayMax;
	DWORD epgCapTimeSyncTick;
	DWORD epgCapTimeSyncQuality;
	int epgCapBasicOnlyFlags;
	int shutdownModePending;
	bool reserveModified;

	HANDLE watchdogStopEvent;
	HANDLE watchdogThread;
};
