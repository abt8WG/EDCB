#pragma once

#include "../../Common/StructDef.h"
#include "../../Common/EpgDataCap3Def.h"
#include "../../Common/BlockLock.h"
#include "../../Common/CommonDef.h"
#include "../../Common/Measure.h"

#import "RegExp.tlb" no_namespace named_guids

extern DWORD g_compatFlags;

class CEpgDBManager
{
public:
	typedef struct _SEARCH_RESULT_EVENT{
		const EPGDB_EVENT_INFO* info;
		wstring findKey;
	}SEARCH_RESULT_EVENT;

	typedef struct _SEARCH_RESULT_EVENT_DATA{
		EPGDB_EVENT_INFO info;
		wstring findKey;
	}SEARCH_RESULT_EVENT_DATA;

public:
	CEpgDBManager(void);
	~CEpgDBManager(void);

	BOOL ReloadEpgData();

	BOOL IsLoadingData();

	BOOL IsInitialLoadingDataDone();

	BOOL CancelLoadData();

	BOOL SearchEpg(vector<EPGDB_SEARCH_KEY_INFO>* key, vector<SEARCH_RESULT_EVENT_DATA>* result);

	//P = [](vector<SEARCH_RESULT_EVENT>&) -> void
	template<class P>
	BOOL SearchEpg(vector<EPGDB_SEARCH_KEY_INFO>* key, P enumProc) {
		CBlockLock lock(&this->epgMapLock);
		vector<SEARCH_RESULT_EVENT> result;
		CoInitialize(NULL);
		{
			IRegExpPtr regExp;
			for( size_t i = 0; i < key->size(); i++ ){
				SearchEvent(&(*key)[i], result, regExp);
			}
		}
		CoUninitialize();
		enumProc(result);
		return TRUE;
	}

	BOOL GetServiceList(vector<EPGDB_SERVICE_INFO>* list);

	//P = [](const vector<EPGDB_EVENT_INFO>&) -> void
	template<class P>
	BOOL EnumEventInfo(LONGLONG serviceKey, P enumProc) const {
		CBlockLock lock(&this->epgMapLock);
		map<LONGLONG, EPGDB_SERVICE_EVENT_INFO>::const_iterator itr = this->epgMap.find(serviceKey);
		if( itr == this->epgMap.end() || itr->second.eventList.empty() ){
			return FALSE;
		}
		enumProc(itr->second.eventList);
		return TRUE;
	}

	//P = [](const map<LONGLONG, EPGDB_SERVICE_EVENT_INFO>&) -> void
	template<class P>
	BOOL EnumEventAll(P enumProc) const {
		CBlockLock lock(&this->epgMapLock);
		if( this->epgMap.empty() ){
			return FALSE;
		}
		enumProc(this->epgMap);
		return TRUE;
	}

	BOOL SearchEpg(
		WORD ONID,
		WORD TSID,
		WORD SID,
		WORD EventID,
		EPGDB_EVENT_INFO* result
		);

	BOOL SearchEpg(
		WORD ONID,
		WORD TSID,
		WORD SID,
		LONGLONG startTime,
		DWORD durationSec,
		EPGDB_EVENT_INFO* result
		);

	BOOL SearchServiceName(
		WORD ONID,
		WORD TSID,
		WORD SID,
		wstring& serviceName
		);

	static void ConvertSearchText(wstring& str);

	// MAP : map<LONGLONG, EPGDB_SERVICE_EVENT_INFO>
	//       map<LONGLONG, REC_EVENT_SERVICE_DATA>
	template <typename MAP, typename RESULT_CB>
	static void SearchEvent(EPGDB_SEARCH_KEY_INFO* key, const MAP& epgMap, const RESULT_CB& onResultEvent, IRegExpPtr& regExp)
	{
		if( key == NULL ){
			return;
		}

		if( key->andKey.compare(0, 7, L"^!{999}") == 0 ){
			//無効を示すキーワードが指定されているので検索しない
			return;
		}

#ifdef _DEBUG
		TIME_MEASURE_1(key->andKey.c_str());
#endif

		wstring andKey = key->andKey;
		BOOL caseFlag = FALSE;
		if( andKey.compare(0, 7, L"C!{999}") == 0 ){
			//大小文字を区別するキーワードが指定されている
			andKey.erase(0, 7);
			caseFlag = TRUE;
		}

		DWORD chkDurationMinSec = 0;
		DWORD chkDurationMaxSec = MAXDWORD;
		if( andKey.compare(0, 4, L"D!{1") == 0 ){
			LPWSTR endp;
			DWORD dur = wcstoul(andKey.c_str() + 3, &endp, 10);
			if( endp - andKey.c_str() == 12 && endp[0] == L'}' ){
				//番組長を絞り込むキーワードが指定されている
				andKey.erase(0, 13);
				chkDurationMinSec = dur / 10000 % 10000 * 60;
				chkDurationMaxSec = dur % 10000 == 0 ? MAXDWORD : dur % 10000 * 60;
			}
		}
		if( andKey.size() == 0 && key->notKey.size() == 0 && key->contentList.size() == 0 && key->videoList.size() == 0 && key->audioList.size() == 0){
			//キーワードもジャンル指定もないので検索しない
			if( g_compatFlags & SUPPORT_SEARCH_WITHOUT_KEYWORD ){
				//互換動作: キーワードなしの検索を許可する
			}else{
				return;
			}
		}

		//キーワード分解
		vector<wstring> andKeyList;
		vector<wstring> notKeyList;

		if( key->regExpFlag == FALSE ){
			//正規表現ではないのでキーワードの分解
			wstring buff = L"";
			if( andKey.size() > 0 ){
				wstring andBuff = andKey;
				Replace(andBuff, L"　", L" ");
				do{
					Separate(andBuff, L" ", buff, andBuff);
					ConvertSearchText(buff);
					if( buff.size() > 0 ){
						andKeyList.push_back(buff);
					}
				}while( andBuff.size() != 0 );
			}

			if( key->notKey.size() > 0 ){
				wstring notBuff = key->notKey;
				Replace(notBuff, L"　", L" ");
				do{
					Separate(notBuff, L" ", buff, notBuff);
					ConvertSearchText(buff);
					if( buff.size() > 0 ){
						notKeyList.push_back(buff);
					}
				}while( notBuff.size() != 0 );
			}
		}else{
			if( andKey.size() > 0 ){
				andKeyList.push_back(andKey);
				//旧い処理では対象を全角空白のまま比較していたため正規表現も全角のケースが多い。特別に置き換える
				Replace(andKeyList.back(), L"　", L" ");
			}
			if( key->notKey.size() > 0 ){
				notKeyList.push_back(key->notKey);
				Replace(notKeyList.back(), L"　", L" ");
			}
		}

		// vector ではなく map を使うので不要
		//size_t resultSize = result.size();
		//auto compareResult = [](const SEARCH_RESULT_EVENT& a, const SEARCH_RESULT_EVENT& b) -> bool {
		//	return _Create64Key2(a.info->original_network_id, a.info->transport_stream_id, a.info->service_id, a.info->event_id) <
		//		_Create64Key2(b.info->original_network_id, b.info->transport_stream_id, b.info->service_id, b.info->event_id);
		//};
		wstring targetWord;

		//サービスごとに検索
		for( size_t i=0; i<key->serviceList.size(); i++ ){
			//map<LONGLONG, EPGDB_SERVICE_EVENT_INFO>::iterator itrService;
			//map<LONGLONG, REC_EVENT_SERVICE_DATA>::iterator itrService;
			auto itrService = epgMap.find(key->serviceList[i]);
			if( itrService != epgMap.end() ){
				//サービス発見
				//vector<EPGDB_EVENT_INFO>::iterator itrEvent_;
				//vector<REC_EVENT_SERVICE_DATA>::iterator itrEvent_;
				for( auto itrEvent_ = itrService->second.eventList.begin(); itrEvent_ != itrService->second.eventList.end(); itrEvent_++ ){
					pair<WORD, const EPGDB_EVENT_INFO*> autoEvent(std::make_pair(itrEvent_->event_id, &*itrEvent_));
					pair<WORD, const EPGDB_EVENT_INFO*>* itrEvent = &autoEvent;
					wstring matchKey = L"";
					if( key->freeCAFlag == 1 ){
						//無料放送のみ
						if(itrEvent->second->freeCAFlag == 1 ){
							//有料放送
							continue;
						}
					}else if( key->freeCAFlag == 2 ){
						//有料放送のみ
						if(itrEvent->second->freeCAFlag == 0 ){
							//無料放送
							continue;
						}
					}
					//ジャンル確認
					if( key->contentList.size() > 0 ){
						//ジャンル指定あるのでジャンルで絞り込み
						if( itrEvent->second->contentInfo == NULL ){
							if( itrEvent->second->shortInfo == NULL ){
								//2つめのサービス？対象外とする
								continue;
							}
							//ジャンル情報ない
							BOOL findNo = FALSE;
							for( size_t j=0; j<key->contentList.size(); j++ ){
								if( key->contentList[j].content_nibble_level_1 == 0xFF &&
									key->contentList[j].content_nibble_level_2 == 0xFF
									){
									//ジャンルなしの指定あり
									findNo = TRUE;
									break;
								}
							}
							if( key->notContetFlag == 0 ){
								if( findNo == FALSE ){
									continue;
								}
							}else{
								//NOT条件扱い
								if( findNo == TRUE ){
									continue;
								}
							}
						}else{
							BOOL equal = IsEqualContent(&(key->contentList), &(itrEvent->second->contentInfo->nibbleList));
							if( key->notContetFlag == 0 ){
								if( equal == FALSE ){
									//ジャンル違うので対象外
									continue;
								}
							}else{
								//NOT条件扱い
								if( equal == TRUE ){
									continue;
								}
							}
						}
					}

					//映像確認
					if( key->videoList.size() > 0 ){
						if( itrEvent->second->componentInfo == NULL ){
							continue;
						}
						BOOL findContent = FALSE;
						WORD type = ((WORD)itrEvent->second->componentInfo->stream_content) << 8 | itrEvent->second->componentInfo->component_type;
						for( size_t j=0; j<key->videoList.size(); j++ ){
							if( type == key->videoList[j]){
								findContent = TRUE;
								break;
							}
						}
						if( findContent == FALSE ){
							continue;
						}
					}

					//音声確認
					if( key->audioList.size() > 0 ){
						if( itrEvent->second->audioInfo == NULL ){
							continue;
						}
						BOOL findContent = FALSE;
						for( size_t j=0; j<itrEvent->second->audioInfo->componentList.size(); j++){
							WORD type = ((WORD)itrEvent->second->audioInfo->componentList[j].stream_content) << 8 | itrEvent->second->audioInfo->componentList[j].component_type;
							for( size_t k=0; k<key->audioList.size(); k++ ){
								if( type == key->audioList[k]){
									findContent = TRUE;
									break;
								}
							}
						}
						if( findContent == FALSE ){
							continue;
						}
					}

					//時間確認
					if( key->dateList.size() > 0 ){
						if( itrEvent->second->StartTimeFlag == FALSE ){
							//開始時間不明なので対象外
							continue;
						}
						BOOL inTime = IsInDateTime(key->dateList, itrEvent->second->start_time);
						if( key->notDateFlag == 0 ){
							if( inTime == FALSE ){
								//時間範囲外なので対象外
								continue;
							}
						}else{
							//NOT条件扱い
							if( inTime == TRUE ){
								continue;
							}
						}
					}

					//番組長で絞り込み
					if( itrEvent->second->DurationFlag == FALSE ){
						//不明なので絞り込みされていれば対象外
						if( 0 < chkDurationMinSec || chkDurationMaxSec < MAXDWORD ){
							continue;
						}
					}else{
						if( itrEvent->second->durationSec < chkDurationMinSec || chkDurationMaxSec < itrEvent->second->durationSec ){
							continue;
						}
					}

					//キーワード確認
					if( itrEvent->second->shortInfo == NULL || itrEvent->second->shortInfo->event_name.empty() ){
						if( andKeyList.size() != 0 ){
							//内容にかかわらず対象外
							continue;
						}
					}else if( andKeyList.size() != 0 || notKeyList.size() != 0 ){
						// abt8WG: 検索キー毎に結果を保存するための検索キーハッシュ値を生成する
						std::hash<int> hash_int;
						std::hash<wstring> hash_wstr;
						int flags = (key->titleOnlyFlag ? 1 : 0) | (key->regExpFlag ? 2 : 0) | (key->aimaiFlag ? 4 : 0) | (caseFlag ? 8 : 0);
						DWORD searchKeyHash = static_cast<DWORD>(hash_int(flags) ^ hash_wstr(key->andKey) ^ hash_wstr(key->notKey));

						if( key->searchKeyHash != 0 && key->searchKeyHash != searchKeyHash ){
							// search内容が変わってる。古い検索結果を消す。
							itrEvent->second->searchResult.erase(key->searchKeyHash);
							auto itrErase = find(itrEvent->second->searchIgnore.cbegin(), itrEvent->second->searchIgnore.cend(), key->searchKeyHash);
							if( itrErase != itrEvent->second->searchIgnore.cend() ){
								itrEvent->second->searchIgnore.erase(itrErase);
							}
							key->searchKeyHash = searchKeyHash;
						}

						// マッチしない検索として登録されていれば検索しない
						if (find(itrEvent->second->searchIgnore.cbegin(), itrEvent->second->searchIgnore.cend(), searchKeyHash) != itrEvent->second->searchIgnore.cend()) {
							continue;
						}

						// 検索結果が保存されてなければ検索する
						auto itrResult = key->searchKeyHash ? itrEvent->second->searchResult.find(searchKeyHash) : itrEvent->second->searchResult.end();
						if( itrResult == itrEvent->second->searchResult.end() ){
#if 1 /* 検索対象の文字列をキャッシュする */
							//検索対象文字列が保存されていなければ作成する
							if( itrEvent->second->search_event_name.size() == 0 ){
								itrEvent->second->search_event_name = itrEvent->second->shortInfo->event_name;
								itrEvent->second->search_text_char = itrEvent->second->shortInfo->text_char;
								if( itrEvent->second->extInfo != NULL ){
									itrEvent->second->search_text_char += L"\r\n";
									itrEvent->second->search_text_char += itrEvent->second->extInfo->text_char;
								}
								ConvertSearchText(itrEvent->second->search_event_name);
								ConvertSearchText(itrEvent->second->search_text_char);
							}
							//検索対象の文字列作成
							targetWord = itrEvent->second->search_event_name;
							if( key->titleOnlyFlag == FALSE ){
								targetWord += L"\r\n";
								targetWord += itrEvent->second->search_text_char;
							}
#else
							//検索対象の文字列作成
							targetWord = itrEvent->second->shortInfo->event_name;
							if( key->titleOnlyFlag == FALSE ){
								targetWord += L"\r\n";
								targetWord += itrEvent->second->shortInfo->text_char;
								if( itrEvent->second->extInfo != NULL ){
									targetWord += L"\r\n";
									targetWord += itrEvent->second->extInfo->text_char;
								}
							}
							ConvertSearchText(targetWord);
#endif
							if( notKeyList.size() != 0 ){
								if( IsFindKeyword(key->regExpFlag, regExp, caseFlag, &notKeyList, targetWord, FALSE) != FALSE ){
									//notキーワード見つかったので対象外
									if( key->searchKeyHash ) itrEvent->second->searchIgnore.push_back(searchKeyHash);
									continue;
								}
							}
							if( andKeyList.size() != 0 ){
								if( key->regExpFlag == FALSE && key->aimaiFlag == 1 ){
									//あいまい検索
									if( IsFindLikeKeyword(caseFlag, &andKeyList, targetWord, TRUE, &matchKey) == FALSE ){
										//andキーワード見つからなかったので対象外
										if( key->searchKeyHash ) itrEvent->second->searchIgnore.push_back(searchKeyHash);
										continue;
									}
								}else{
									if( IsFindKeyword(key->regExpFlag, regExp, caseFlag, &andKeyList, targetWord, TRUE, &matchKey) == FALSE ){
										//andキーワード見つからなかったので対象外
										if( key->searchKeyHash ) itrEvent->second->searchIgnore.push_back(searchKeyHash);
										continue;
									}
								}
							}
							if( key->searchKeyHash ) itrEvent->second->searchResult.insert(pair<DWORD, wstring>(searchKeyHash, matchKey));
						}else{
							matchKey = itrResult->second;
						}
					}

					SEARCH_RESULT_EVENT addItem;
					addItem.findKey = matchKey;
					addItem.info = itrEvent->second;
					////resultSizeまで(既ソート)に存在しないときだけ追加
					//auto itrResult = std::lower_bound(result.begin(), result.begin() + resultSize, addItem, compareResult);
					//if( itrResult == result.begin() + resultSize || compareResult(addItem, *itrResult) ){
					//	result.push_back(addItem);
					//}
					onResultEvent(addItem);
				}
			}
		}
	}

protected:
	mutable CRITICAL_SECTION epgMapLock;

	HANDLE loadThread;
	BOOL loadStop;
	BOOL initialLoadDone;

	map<LONGLONG, EPGDB_SERVICE_EVENT_INFO> epgMap;
protected:
	static BOOL ConvertEpgInfo(const EPGDB_SERVICE_INFO* service, const EPG_EVENT_INFO* src, EPGDB_EVENT_INFO* dest);
	static BOOL CALLBACK EnumEpgInfoListProc(DWORD epgInfoListSize, EPG_EVENT_INFO* epgInfoList, LPVOID param);
	void ClearEpgData();
	static UINT WINAPI LoadThread_(LPVOID param);

	UINT LoadThread();
	void SearchEvent(EPGDB_SEARCH_KEY_INFO* key, vector<SEARCH_RESULT_EVENT>& result, IRegExpPtr& regExp);
	static BOOL IsEqualContent(vector<EPGDB_CONTENT_DATA>* searchKey, vector<EPGDB_CONTENT_DATA>* eventData);
	static BOOL IsInDateTime(const vector<EPGDB_SEARCH_DATE_INFO>& dateList, const SYSTEMTIME& time);
	static BOOL IsFindKeyword(BOOL regExpFlag, IRegExpPtr& regExp, BOOL caseFlag, const vector<wstring>* keyList, const wstring& word, BOOL andMode, wstring* findKey = NULL);
	static BOOL IsFindLikeKeyword(BOOL caseFlag, const vector<wstring>* keyList, const wstring& word, BOOL andMode, wstring* findKey = NULL);
};

