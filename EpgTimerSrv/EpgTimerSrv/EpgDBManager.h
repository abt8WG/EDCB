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
			//�����������L�[���[�h���w�肳��Ă���̂Ō������Ȃ�
			return;
		}

#ifdef _DEBUG
		TIME_MEASURE_1(key->andKey.c_str());
#endif

		wstring andKey = key->andKey;
		BOOL caseFlag = FALSE;
		if( andKey.compare(0, 7, L"C!{999}") == 0 ){
			//�召��������ʂ���L�[���[�h���w�肳��Ă���
			andKey.erase(0, 7);
			caseFlag = TRUE;
		}

		DWORD chkDurationMinSec = 0;
		DWORD chkDurationMaxSec = MAXDWORD;
		if( andKey.compare(0, 4, L"D!{1") == 0 ){
			LPWSTR endp;
			DWORD dur = wcstoul(andKey.c_str() + 3, &endp, 10);
			if( endp - andKey.c_str() == 12 && endp[0] == L'}' ){
				//�ԑg�����i�荞�ރL�[���[�h���w�肳��Ă���
				andKey.erase(0, 13);
				chkDurationMinSec = dur / 10000 % 10000 * 60;
				chkDurationMaxSec = dur % 10000 == 0 ? MAXDWORD : dur % 10000 * 60;
			}
		}
		if( andKey.size() == 0 && key->notKey.size() == 0 && key->contentList.size() == 0 && key->videoList.size() == 0 && key->audioList.size() == 0){
			//�L�[���[�h���W�������w����Ȃ��̂Ō������Ȃ�
			if( g_compatFlags & SUPPORT_SEARCH_WITHOUT_KEYWORD ){
				//�݊�����: �L�[���[�h�Ȃ��̌�����������
			}else{
				return;
			}
		}

		//�L�[���[�h����
		vector<wstring> andKeyList;
		vector<wstring> notKeyList;

		if( key->regExpFlag == FALSE ){
			//���K�\���ł͂Ȃ��̂ŃL�[���[�h�̕���
			wstring buff = L"";
			if( andKey.size() > 0 ){
				wstring andBuff = andKey;
				Replace(andBuff, L"�@", L" ");
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
				Replace(notBuff, L"�@", L" ");
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
				//���������ł͑Ώۂ�S�p�󔒂̂܂ܔ�r���Ă������ߐ��K�\�����S�p�̃P�[�X�������B���ʂɒu��������
				Replace(andKeyList.back(), L"�@", L" ");
			}
			if( key->notKey.size() > 0 ){
				notKeyList.push_back(key->notKey);
				Replace(notKeyList.back(), L"�@", L" ");
			}
		}

		// vector �ł͂Ȃ� map ���g���̂ŕs�v
		//size_t resultSize = result.size();
		//auto compareResult = [](const SEARCH_RESULT_EVENT& a, const SEARCH_RESULT_EVENT& b) -> bool {
		//	return _Create64Key2(a.info->original_network_id, a.info->transport_stream_id, a.info->service_id, a.info->event_id) <
		//		_Create64Key2(b.info->original_network_id, b.info->transport_stream_id, b.info->service_id, b.info->event_id);
		//};
		wstring targetWord;

		//�T�[�r�X���ƂɌ���
		for( size_t i=0; i<key->serviceList.size(); i++ ){
			//map<LONGLONG, EPGDB_SERVICE_EVENT_INFO>::iterator itrService;
			//map<LONGLONG, REC_EVENT_SERVICE_DATA>::iterator itrService;
			auto itrService = epgMap.find(key->serviceList[i]);
			if( itrService != epgMap.end() ){
				//�T�[�r�X����
				//vector<EPGDB_EVENT_INFO>::iterator itrEvent_;
				//vector<REC_EVENT_SERVICE_DATA>::iterator itrEvent_;
				for( auto itrEvent_ = itrService->second.eventList.begin(); itrEvent_ != itrService->second.eventList.end(); itrEvent_++ ){
					pair<WORD, const EPGDB_EVENT_INFO*> autoEvent(std::make_pair(itrEvent_->event_id, &*itrEvent_));
					pair<WORD, const EPGDB_EVENT_INFO*>* itrEvent = &autoEvent;
					wstring matchKey = L"";
					if( key->freeCAFlag == 1 ){
						//���������̂�
						if(itrEvent->second->freeCAFlag == 1 ){
							//�L������
							continue;
						}
					}else if( key->freeCAFlag == 2 ){
						//�L�������̂�
						if(itrEvent->second->freeCAFlag == 0 ){
							//��������
							continue;
						}
					}
					//�W�������m�F
					if( key->contentList.size() > 0 ){
						//�W�������w�肠��̂ŃW�������ōi�荞��
						if( itrEvent->second->contentInfo == NULL ){
							if( itrEvent->second->shortInfo == NULL ){
								//2�߂̃T�[�r�X�H�ΏۊO�Ƃ���
								continue;
							}
							//�W���������Ȃ�
							BOOL findNo = FALSE;
							for( size_t j=0; j<key->contentList.size(); j++ ){
								if( key->contentList[j].content_nibble_level_1 == 0xFF &&
									key->contentList[j].content_nibble_level_2 == 0xFF
									){
									//�W�������Ȃ��̎w�肠��
									findNo = TRUE;
									break;
								}
							}
							if( key->notContetFlag == 0 ){
								if( findNo == FALSE ){
									continue;
								}
							}else{
								//NOT��������
								if( findNo == TRUE ){
									continue;
								}
							}
						}else{
							BOOL equal = IsEqualContent(&(key->contentList), &(itrEvent->second->contentInfo->nibbleList));
							if( key->notContetFlag == 0 ){
								if( equal == FALSE ){
									//�W�������Ⴄ�̂őΏۊO
									continue;
								}
							}else{
								//NOT��������
								if( equal == TRUE ){
									continue;
								}
							}
						}
					}

					//�f���m�F
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

					//�����m�F
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

					//���Ԋm�F
					if( key->dateList.size() > 0 ){
						if( itrEvent->second->StartTimeFlag == FALSE ){
							//�J�n���ԕs���Ȃ̂őΏۊO
							continue;
						}
						BOOL inTime = IsInDateTime(key->dateList, itrEvent->second->start_time);
						if( key->notDateFlag == 0 ){
							if( inTime == FALSE ){
								//���Ԕ͈͊O�Ȃ̂őΏۊO
								continue;
							}
						}else{
							//NOT��������
							if( inTime == TRUE ){
								continue;
							}
						}
					}

					//�ԑg���ōi�荞��
					if( itrEvent->second->DurationFlag == FALSE ){
						//�s���Ȃ̂ōi�荞�݂���Ă���ΑΏۊO
						if( 0 < chkDurationMinSec || chkDurationMaxSec < MAXDWORD ){
							continue;
						}
					}else{
						if( itrEvent->second->durationSec < chkDurationMinSec || chkDurationMaxSec < itrEvent->second->durationSec ){
							continue;
						}
					}

					//�L�[���[�h�m�F
					if( itrEvent->second->shortInfo == NULL || itrEvent->second->shortInfo->event_name.empty() ){
						if( andKeyList.size() != 0 ){
							//���e�ɂ�����炸�ΏۊO
							continue;
						}
					}else if( andKeyList.size() != 0 || notKeyList.size() != 0 ){
						// abt8WG: �����L�[���Ɍ��ʂ�ۑ����邽�߂̌����L�[�n�b�V���l�𐶐�����
						std::hash<int> hash_int;
						std::hash<wstring> hash_wstr;
						int flags = (key->titleOnlyFlag ? 1 : 0) | (key->regExpFlag ? 2 : 0) | (key->aimaiFlag ? 4 : 0) | (caseFlag ? 8 : 0);
						DWORD searchKeyHash = static_cast<DWORD>(hash_int(flags) ^ hash_wstr(key->andKey) ^ hash_wstr(key->notKey));

						if( key->searchKeyHash != 0 && key->searchKeyHash != searchKeyHash ){
							// search���e���ς���Ă�B�Â��������ʂ������B
							itrEvent->second->searchResult.erase(key->searchKeyHash);
							auto itrErase = find(itrEvent->second->searchIgnore.cbegin(), itrEvent->second->searchIgnore.cend(), key->searchKeyHash);
							if( itrErase != itrEvent->second->searchIgnore.cend() ){
								itrEvent->second->searchIgnore.erase(itrErase);
							}
							key->searchKeyHash = searchKeyHash;
						}

						// �}�b�`���Ȃ������Ƃ��ēo�^����Ă���Ό������Ȃ�
						if (find(itrEvent->second->searchIgnore.cbegin(), itrEvent->second->searchIgnore.cend(), searchKeyHash) != itrEvent->second->searchIgnore.cend()) {
							continue;
						}

						// �������ʂ��ۑ�����ĂȂ���Ό�������
						auto itrResult = key->searchKeyHash ? itrEvent->second->searchResult.find(searchKeyHash) : itrEvent->second->searchResult.end();
						if( itrResult == itrEvent->second->searchResult.end() ){
#if 1 /* �����Ώۂ̕�������L���b�V������ */
							//�����Ώە����񂪕ۑ�����Ă��Ȃ���΍쐬����
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
							//�����Ώۂ̕�����쐬
							targetWord = itrEvent->second->search_event_name;
							if( key->titleOnlyFlag == FALSE ){
								targetWord += L"\r\n";
								targetWord += itrEvent->second->search_text_char;
							}
#else
							//�����Ώۂ̕�����쐬
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
									//not�L�[���[�h���������̂őΏۊO
									if( key->searchKeyHash ) itrEvent->second->searchIgnore.push_back(searchKeyHash);
									continue;
								}
							}
							if( andKeyList.size() != 0 ){
								if( key->regExpFlag == FALSE && key->aimaiFlag == 1 ){
									//�����܂�����
									if( IsFindLikeKeyword(caseFlag, &andKeyList, targetWord, TRUE, &matchKey) == FALSE ){
										//and�L�[���[�h������Ȃ������̂őΏۊO
										if( key->searchKeyHash ) itrEvent->second->searchIgnore.push_back(searchKeyHash);
										continue;
									}
								}else{
									if( IsFindKeyword(key->regExpFlag, regExp, caseFlag, &andKeyList, targetWord, TRUE, &matchKey) == FALSE ){
										//and�L�[���[�h������Ȃ������̂őΏۊO
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
					////resultSize�܂�(���\�[�g)�ɑ��݂��Ȃ��Ƃ������ǉ�
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

