#include "StdAfx.h"
#include "EpgDBManager.h"
#include <process.h>

#include "../../Common/CommonDef.h"
#include "../../Common/TimeUtil.h"
#include "../../Common/StringUtil.h"
#include "../../Common/PathUtil.h"
#include "../../Common/EpgTimerUtil.h"
#include "../../Common/EpgDataCap3Util.h"

extern DWORD g_compatFlags;

CEpgDBManager::CEpgDBManager(void)
{
	InitializeCriticalSection(&this->epgMapLock);

    this->loadThread = NULL;
    this->loadStop = FALSE;
    this->initialLoadDone = FALSE;
}

CEpgDBManager::~CEpgDBManager(void)
{
	CancelLoadData();

	ClearEpgData();

	DeleteCriticalSection(&this->epgMapLock);
}

void CEpgDBManager::ClearEpgData()
{
	CBlockLock lock(&this->epgMapLock);
	this->epgMap.clear();
}

BOOL CEpgDBManager::ReloadEpgData()
{
	CancelLoadData();

	CBlockLock lock(&this->epgMapLock);

	BOOL ret = TRUE;
	if( this->loadThread == NULL ){
		//��M�X���b�h�N��
		this->loadThread = (HANDLE)_beginthreadex(NULL, 0, LoadThread_, (LPVOID)this, CREATE_SUSPENDED, NULL);
		SetThreadPriority( this->loadThread, THREAD_PRIORITY_NORMAL );
		ResumeThread(this->loadThread);
	}else{
		ret = FALSE;
	}

	return ret;
}

UINT WINAPI CEpgDBManager::LoadThread_(LPVOID param)
{
	CoInitialize(NULL);
	__try {
		CEpgDBManager* sys = (CEpgDBManager*)param;
		return sys->LoadThread();
	}
	__except (FilterException(GetExceptionInformation())) { }
	CoUninitialize();
	return 0;
}

UINT CEpgDBManager::LoadThread()
{
	OutputDebugString(L"Start Load EpgData\r\n");
	DWORD time = GetTickCount();

	CEpgDataCap3Util epgUtil;
	if( epgUtil.Initialize(FALSE) == FALSE ){
		OutputDebugString(L"��EpgDataCap3.dll�̏������Ɏ��s���܂����B\r\n");
		this->ClearEpgData();
		return 0;
	}

	//EPG�t�@�C���̌���
	vector<wstring> epgFileList;
	wstring epgDataPath = L"";
	GetSettingPath(epgDataPath);
	epgDataPath += EPG_SAVE_FOLDER;

	wstring searchKey = epgDataPath;
	searchKey += L"\\*_epg.dat";

	WIN32_FIND_DATA findData;
	HANDLE find;

	//�w��t�H���_�̃t�@�C���ꗗ�擾
	find = FindFirstFile( searchKey.c_str(), &findData);
	if ( find == INVALID_HANDLE_VALUE ) {
		//�P�����݂��Ȃ�
		epgUtil.UnInitialize();
		this->ClearEpgData();
		return 0;
	}
	do{
		if( (findData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) == 0 ){
			LONGLONG fileTime = (LONGLONG)findData.ftLastWriteTime.dwHighDateTime << 32 | findData.ftLastWriteTime.dwLowDateTime;
			if( fileTime != 0 ){
				//���������t�@�C�����ꗗ�ɒǉ�
				//���O���B������TSID==0xFFFF�̏ꍇ�͓����`�����l���̘A���ɂ��X�g���[�����N���A����Ȃ��\��������̂Ō��ɂ܂Ƃ߂�
				WCHAR prefix = fileTime + 7*24*60*60*I64_1SEC < GetNowI64Time() ? L'0' :
				               lstrlen(findData.cFileName) < 12 || _wcsicmp(findData.cFileName + lstrlen(findData.cFileName) - 12, L"ffff_epg.dat") ? L'1' : L'2';
				wstring item = prefix + epgDataPath + L'\\' + findData.cFileName;
				epgFileList.insert(std::lower_bound(epgFileList.begin(), epgFileList.end(), item), item);
			}
		}
	}while(FindNextFile(find, &findData));

	FindClose(find);

	//EPG�t�@�C���̉��
	for( vector<wstring>::iterator itr = epgFileList.begin(); itr != epgFileList.end(); itr++ ){
		if( this->loadStop ){
			//�L�����Z�����ꂽ
			epgUtil.UnInitialize();
			return 0;
		}
		//�ꎞ�t�@�C���̏�Ԃ𒲂ׂ�B�擾����CreateFile(tmp)��CloseHandle(tmp)��CopyFile(tmp,master)��DeleteFile(tmp)�̗����������x����
		wstring path = itr->c_str() + 1;
		HANDLE tmpFile = CreateFile((path + L".tmp").c_str(), GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_DELETE, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
		DWORD tmpError = GetLastError();
		if( tmpFile != INVALID_HANDLE_VALUE ){
			tmpError = NO_ERROR;
			FILETIME ft;
			if( GetFileTime(tmpFile, NULL, NULL, &ft) == FALSE || ((LONGLONG)ft.dwHighDateTime << 32 | ft.dwLowDateTime) + 300*I64_1SEC < GetNowI64Time() ){
				//�����炭��n������Ă��Ȃ��ꎞ�t�@�C���Ȃ̂Ŗ���
				tmpError = ERROR_FILE_NOT_FOUND;
			}
			CloseHandle(tmpFile);
		}
		if( (*itr)[0] == L'0' ){
			if( tmpError != NO_ERROR && tmpError != ERROR_SHARING_VIOLATION ){
				//1�T�Ԉȏ�O���ꎞ�t�@�C�����Ȃ��̂ō폜
				DeleteFile(path.c_str());
				_OutputDebugString(L"��delete %s\r\n", path.c_str());
			}
		}else{
			BYTE readBuff[188*256];
			BOOL swapped = FALSE;
			HANDLE file = INVALID_HANDLE_VALUE;
			if( tmpError == NO_ERROR ){
				//�ꎞ�t�@�C���������ď������ݒ��łȂ����R�s�[���O��������Ȃ��̂�3�b�҂�
				Sleep(3000);
			}else if( tmpError == ERROR_SHARING_VIOLATION ){
				//�ꎞ�t�@�C���������ď������ݒ������������㏑������邩������Ȃ��̂łł��邾���ޔ�������
				HANDLE masterFile = CreateFile(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
				if( masterFile != INVALID_HANDLE_VALUE ){
					file = CreateFile((path + L".swp").c_str(), GENERIC_READ | GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
					if( file != INVALID_HANDLE_VALUE ){
						swapped = TRUE;
						DWORD read;
						while( ReadFile(masterFile, readBuff, sizeof(readBuff), &read, NULL) && read != 0 ){
							DWORD written;
							WriteFile(file, readBuff, read, &written, NULL);
						}
						SetFilePointer(file, 0, 0, FILE_BEGIN);
						tmpFile = CreateFile((path + L".tmp").c_str(), GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_DELETE, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
						if( tmpFile != INVALID_HANDLE_VALUE || GetLastError() != ERROR_SHARING_VIOLATION ){
							//�ޔ𒆂ɏ������݂��I�����
							if( tmpFile != INVALID_HANDLE_VALUE ){
								CloseHandle(tmpFile);
							}
							CloseHandle(file);
							file = INVALID_HANDLE_VALUE;
						}
					}
					CloseHandle(masterFile);
				}
				if( file == INVALID_HANDLE_VALUE ){
					Sleep(3000);
				}
			}
			if( file == INVALID_HANDLE_VALUE ){
				file = CreateFile(path.c_str(), GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
			}
			if( file == INVALID_HANDLE_VALUE ){
				_OutputDebugString(L"Error %s\r\n", path.c_str());
			}else{
				//PAT�𑗂�(�X�g���[�����m���Ƀ��Z�b�g���邽��)
				DWORD seekPos = 0;
				DWORD read;
				for( DWORD i=0; ReadFile(file, readBuff, 188, &read, NULL) && read == 188; i+=188 ){
					//PID
					if( ((readBuff[1] & 0x1F) << 8 | readBuff[2]) == 0 ){
						//payload_unit_start_indicator
						if( (readBuff[1] & 0x40) != 0 ){
							if( seekPos != 0 ){
								break;
							}
						}else if( seekPos == 0 ){
							continue;
						}
						seekPos = i + 188;
						epgUtil.AddTSPacket(readBuff, 188);
					}
				}
				SetFilePointer(file, seekPos, 0, FILE_BEGIN);
				//TOT��擪�Ɏ����Ă��đ���(�X�g���[���̎������m�肳���邽��)
				BOOL ignoreTOT = FALSE;
				while( ReadFile(file, readBuff, 188, &read, NULL) && read == 188 ){
					if( ((readBuff[1] & 0x1F) << 8 | readBuff[2]) == 0x14 ){
						ignoreTOT = TRUE;
						epgUtil.AddTSPacket(readBuff, 188);
						break;
					}
				}
				SetFilePointer(file, seekPos, 0, FILE_BEGIN);
				while( ReadFile(file, readBuff, sizeof(readBuff), &read, NULL) && read != 0 ){
					for( DWORD i=0; i<read; i+=188 ){
						if( ignoreTOT && ((readBuff[i+1] & 0x1F) << 8 | readBuff[i+2]) == 0x14 ){
							ignoreTOT = FALSE;
						}else{
							epgUtil.AddTSPacket(readBuff+i, 188);
						}
					}
					Sleep(0);
				}
				CloseHandle(file);
			}
			if( swapped ){
				DeleteFile((path + L".swp").c_str());
			}
		}
		Sleep(0);
	}

	//EPG�f�[�^���擾
	DWORD serviceListSize = 0;
	SERVICE_INFO* serviceList = NULL;
	if( epgUtil.GetServiceListEpgDB(&serviceListSize, &serviceList) == FALSE ){
		epgUtil.UnInitialize();
		this->ClearEpgData();
		return 0;
	}

	{ //CBlockLock
	CBlockLock lock(&this->epgMapLock);

	this->ClearEpgData();

	for( DWORD i=0; i<serviceListSize; i++ ){
		LONGLONG key = _Create64Key(serviceList[i].original_network_id, serviceList[i].transport_stream_id, serviceList[i].service_id);
		EPGDB_SERVICE_EVENT_INFO* item = &this->epgMap.insert(std::make_pair(key, EPGDB_SERVICE_EVENT_INFO())).first->second;
		item->serviceInfo.ONID = serviceList[i].original_network_id;
		item->serviceInfo.TSID = serviceList[i].transport_stream_id;
		item->serviceInfo.SID = serviceList[i].service_id;
		if( serviceList[i].extInfo != NULL ){
			item->serviceInfo.service_type = serviceList[i].extInfo->service_type;
			item->serviceInfo.partialReceptionFlag = serviceList[i].extInfo->partialReceptionFlag;
			if( serviceList[i].extInfo->service_provider_name != NULL ){
				item->serviceInfo.service_provider_name = serviceList[i].extInfo->service_provider_name;
			}
			if( serviceList[i].extInfo->service_name != NULL ){
				item->serviceInfo.service_name = serviceList[i].extInfo->service_name;
			}
			if( serviceList[i].extInfo->network_name != NULL ){
				item->serviceInfo.network_name = serviceList[i].extInfo->network_name;
			}
			if( serviceList[i].extInfo->ts_name != NULL ){
				item->serviceInfo.ts_name = serviceList[i].extInfo->ts_name;
			}
			item->serviceInfo.remote_control_key_id = serviceList[i].extInfo->remote_control_key_id;
		}
		epgUtil.EnumEpgInfoList(item->serviceInfo.ONID, item->serviceInfo.TSID, item->serviceInfo.SID, EnumEpgInfoListProc, item);
	}

	} //CBlockLock

	_OutputDebugString(L"End Load EpgData %dmsec\r\n", GetTickCount()-time);
	epgUtil.UnInitialize();

	return 0;
}

//EPG�f�[�^���R�s�[����
BOOL CEpgDBManager::ConvertEpgInfo(const EPGDB_SERVICE_INFO* service, const EPG_EVENT_INFO* src, EPGDB_EVENT_INFO* dest)
{
	if (service == NULL || src == NULL || dest == NULL) {
		return FALSE;
	}

	// xtne6f���ɂ���� ConvertEpgInfo �� EpgDataCap_Bon �ƃR�[�h�����ʉ����ꂽ (4f20e6db42a79c27419937171f199a508d5bf53b)
	::ConvertEpgInfo(service->ONID, service->TSID, service->SID, src, dest);

	// nekopanda�� �ǉ��� (8bae159c539014a909e3fe563ecca652a2ed0bec)
	// EpgDataCap_Bon ���� serviceName ���Q�Ƃ��邱�Ƃ͂Ȃ������Ȃ̂ŁAEpgTimerSrv �ł̂ݐݒ肷�邱�Ƃɂ��Ă����B
	dest->serviceName = service->service_name;

	// �����p��������Đ������邽�߂ɃN���A���Ă����B
	dest->search_event_name.clear();
	dest->search_text_char.clear();
	dest->searchIgnore.clear();
	dest->searchResult.clear();

	return TRUE;
}

BOOL CALLBACK CEpgDBManager::EnumEpgInfoListProc(DWORD epgInfoListSize, EPG_EVENT_INFO* epgInfoList, LPVOID param)
{
	EPGDB_SERVICE_EVENT_INFO* item = (EPGDB_SERVICE_EVENT_INFO*)param;

	try{
		if( epgInfoList == NULL ){
			item->eventList.reserve(epgInfoListSize);
		}else{
			for( DWORD i=0; i<epgInfoListSize; i++ ){
				item->eventList.resize(item->eventList.size() + 1);
				ConvertEpgInfo(&item->serviceInfo, &epgInfoList[i], &item->eventList.back());
				if( item->eventList.back().shortInfo != NULL ){
					//�����H��APR(���s)���܂ނ���
					Replace(item->eventList.back().shortInfo->event_name, L"\r\n", L"");
				}
				//������͊��\�[�g�����d�l�ł͂Ȃ��̂ő}���\�[�g���Ă���
				for( size_t j = item->eventList.size() - 1; j > 0 && item->eventList[j].event_id < item->eventList[j-1].event_id; j-- ){
					std::swap(item->eventList[j], item->eventList[j-1]);
				}
			}
		}
	}catch( std::bad_alloc& ){
		return FALSE;
	}
	return TRUE;
}

BOOL CEpgDBManager::IsLoadingData()
{
	CBlockLock lock(&this->epgMapLock);
	return this->loadThread != NULL && WaitForSingleObject( this->loadThread, 0 ) == WAIT_TIMEOUT ? TRUE : FALSE;
}

BOOL CEpgDBManager::IsInitialLoadingDataDone()
{
	CBlockLock lock(&this->epgMapLock);
	return this->initialLoadDone != FALSE || this->loadThread != NULL && IsLoadingData() == FALSE ? TRUE : FALSE;
}

BOOL CEpgDBManager::CancelLoadData()
{
	for( int i = 0; i < 150; i++ ){
		{
			CBlockLock lock(&this->epgMapLock);
			if( this->loadThread == NULL ){
				return TRUE;
			}else if( i == 0 ){
				this->loadStop = TRUE;
			}else if( this->loadStop == FALSE ){
				return TRUE;
			}else if( IsLoadingData() == FALSE ){
				CloseHandle(this->loadThread);
				this->loadThread = NULL;
				this->loadStop = FALSE;
				this->initialLoadDone = TRUE;
				return TRUE;
			}
		}
		Sleep(100);
	}
	CBlockLock lock(&this->epgMapLock);
	if( this->loadStop != FALSE && IsLoadingData() != FALSE ){
		TerminateThread(this->loadThread, 0xffffffff);
		CloseHandle(this->loadThread);
		this->loadThread = NULL;
		this->loadStop = FALSE;
		this->initialLoadDone = TRUE;
	}

	return TRUE;
}

BOOL CEpgDBManager::SearchEpg(vector<EPGDB_SEARCH_KEY_INFO>* key, vector<SEARCH_RESULT_EVENT_DATA>* result)
{
	return SearchEpg(key, [=](vector<SEARCH_RESULT_EVENT>& val) {
		result->reserve(result->size() + val.size());
		for( vector<SEARCH_RESULT_EVENT>::iterator itr = val.begin(); itr != val.end(); itr++ ){
			result->resize(result->size() + 1);
			result->back().info.DeepCopy(*itr->info);
			result->back().findKey.swap(itr->findKey);
		}
	});
}

/*
BOOL CEpgDBManager::SearchEpg(vector<EPGDB_SEARCH_KEY_INFO>* key, void (*enumProc)(vector<SEARCH_RESULT_EVENT>*, void*), void* param)
{
	CBlockLock lock(&this->epgMapLock);

	BOOL ret = TRUE;

	map<ULONGLONG, SEARCH_RESULT_EVENT> resultMap;
	{
		IRegExpPtr regExp;
		for( size_t i=0; i<key->size(); i++ ){
			SearchEvent( &(*key)[i], &resultMap, regExp );
		}
	}

	vector<SEARCH_RESULT_EVENT> result;
	map<ULONGLONG, SEARCH_RESULT_EVENT>::iterator itr;
	for( itr = resultMap.begin(); itr != resultMap.end(); itr++ ){
		result.push_back(itr->second);
	}
	//�����̓��b�N��ԂȂ̂ŃR�[���o�b�N��Ŕr�����䂷�ׂ��łȂ�
	enumProc(&result, param);

	return ret;
}

BOOL CEpgDBManager::SearchEpgByKey(vector<EPGDB_SEARCH_KEY_INFO>* key, void (*enumProc)(vector<SEARCH_RESULT_EVENT>*, void*), void* param)
{
	CBlockLock lock(&this->epgMapLock);

	BOOL ret = TRUE;
	vector<SEARCH_RESULT_EVENT> result;
	SEARCH_RESULT_EVENT dummy;
	EPGDB_EVENT_INFO dummyinfo;

	dummyinfo.original_network_id = 0;
	dummyinfo.transport_stream_id = 0;
	dummyinfo.service_id = 0;
	dummyinfo.event_id = 0;
	dummyinfo.shortInfo = NULL; //�����Ă��R���X�g���N�^������������
	GetSystemTime(&dummyinfo.start_time);//�r���ɃR���o�[�g�֐�������̂ŁA�܂Ƃ��Ȓl�����Ă����B
	dummy.info = &dummyinfo;

	{
		IRegExpPtr regExp;
		for( size_t i=0; i<key->size(); i++ ){
			map<ULONGLONG, SEARCH_RESULT_EVENT> resultMap;
			SearchEvent( &(*key)[i], &resultMap, regExp );

			map<ULONGLONG, SEARCH_RESULT_EVENT>::iterator itr;
			for( itr = resultMap.begin(); itr != resultMap.end(); itr++ ){
				result.push_back(itr->second);
			}

			result.push_back(dummy);
		}
	}

	//�����̓��b�N��ԂȂ̂ŃR�[���o�b�N��Ŕr�����䂷�ׂ��łȂ�
	enumProc(&result, param);

	return ret;
}
*/

void CEpgDBManager::SearchEvent(EPGDB_SEARCH_KEY_INFO* key, vector<SEARCH_RESULT_EVENT>& result, IRegExpPtr& regExp)
{
	if( key == NULL ){
		return;
	}

	size_t resultSize = result.size();
	auto compareResult = [](const SEARCH_RESULT_EVENT& a, const SEARCH_RESULT_EVENT& b) -> bool {
		return _Create64Key2(a.info->original_network_id, a.info->transport_stream_id, a.info->service_id, a.info->event_id) <
		       _Create64Key2(b.info->original_network_id, b.info->transport_stream_id, b.info->service_id, b.info->event_id);
	};

	SearchEvent(key, this->epgMap, [&](CEpgDBManager::SEARCH_RESULT_EVENT addItem) {
		//resultSize�܂�(���\�[�g)�ɑ��݂��Ȃ��Ƃ������ǉ�
		auto itrResult = std::lower_bound(result.begin(), result.begin() + resultSize, addItem, compareResult);
		if( itrResult == result.begin() + resultSize || compareResult(addItem, *itrResult) ){
			result.push_back(addItem);
		}
	}, regExp);

	//�S�̂��\�[�g���ďd���폜
	std::sort(result.begin(), result.end(), compareResult);
	result.erase(std::unique(result.begin(), result.end(), [](const SEARCH_RESULT_EVENT& a, const SEARCH_RESULT_EVENT& b) {
		return a.info->original_network_id == b.info->original_network_id &&
		       a.info->transport_stream_id == b.info->transport_stream_id &&
		       a.info->service_id == b.info->service_id &&
		       a.info->event_id == b.info->event_id;
	}), result.end());
}

BOOL CEpgDBManager::IsEqualContent(vector<EPGDB_CONTENT_DATA>* searchKey, vector<EPGDB_CONTENT_DATA>* eventData)
{
	for( size_t i=0; i<searchKey->size(); i++ ){
		EPGDB_CONTENT_DATA c = (*searchKey)[i];
		if( (c.content_nibble_level_1 & 0xF0) == 0x70 ){
			//CS�g���p���ɕϊ�����
			c.user_nibble_1 = c.content_nibble_level_1 & 0x0F;
			c.user_nibble_2 = c.content_nibble_level_2;
			c.content_nibble_level_1 = 0x0E;
			c.content_nibble_level_2 = 0x01;
		}
		for( size_t j=0; j<eventData->size(); j++ ){
			//*�{���̔�r�R�[�h(CS�p�W�������R�[�h�Ή��ɏC����))
			if( c.content_nibble_level_1 == (*eventData)[j].content_nibble_level_1 ){
				if( c.content_nibble_level_2 == 0xFF ){
					//�����ނ��ׂ�
					return TRUE;
				}
				if( c.content_nibble_level_2 == (*eventData)[j].content_nibble_level_2 ){
					if( c.content_nibble_level_1 != 0x0E ){
						//�g���łȂ�
						return TRUE;
					}
					if( c.user_nibble_1 == (*eventData)[j].user_nibble_1 ){
						if( c.user_nibble_2 == 0xFF ){
							//�g�������ނ��ׂ�
							return TRUE;
						}
						if( c.user_nibble_2 == (*eventData)[j].user_nibble_2 ){
							return TRUE;
						}
					}
				}
			}
			//*/
		}
	}
	return FALSE;
}

BOOL CEpgDBManager::IsInDateTime(const vector<EPGDB_SEARCH_DATE_INFO>& dateList, const SYSTEMTIME& time)
{
	int weekMin = (time.wDayOfWeek * 24 + time.wHour) * 60 + time.wMinute;
	for( size_t i=0; i<dateList.size(); i++ ){
		int start = (dateList[i].startDayOfWeek * 24 + dateList[i].startHour) * 60 + dateList[i].startMin;
		int end = (dateList[i].endDayOfWeek * 24 + dateList[i].endHour) * 60 + dateList[i].endMin;
		if( start >= end ){
			if( start <= weekMin || weekMin <= end ){
				return TRUE;
			}
		}else{
			if( start <= weekMin && weekMin <= end ){
				return TRUE;
			}
		}
	}

	return FALSE;
}

static wstring::const_iterator SearchKeyword(const wstring& str, const wstring& key, BOOL caseFlag)
{
	return caseFlag ?
		std::search(str.begin(), str.end(), key.begin(), key.end()) :
		std::search(str.begin(), str.end(), key.begin(), key.end(),
			[](wchar_t l, wchar_t r) { return (L'a' <= l && l <= L'z' ? l - L'a' + L'A' : l) == (L'a' <= r && r <= L'z' ? r - L'a' + L'A' : r); });
}

BOOL CEpgDBManager::IsFindKeyword(BOOL regExpFlag, IRegExpPtr& regExp, BOOL caseFlag, const vector<wstring>* keyList, const wstring& word, BOOL andMode, wstring* findKey)
{
	if( regExpFlag == TRUE ){
		//���K�\�����[�h
		try{
			if( regExp == NULL ){
				regExp.CreateInstance(CLSID_RegExp);
			}
			if( regExp != NULL && word.size() > 0 && keyList->size() > 0 ){
				_bstr_t target( word.c_str() );
				_bstr_t pattern( (*keyList)[0].c_str() );

				regExp->PutGlobal( VARIANT_TRUE );
				regExp->PutIgnoreCase( caseFlag == FALSE ? VARIANT_TRUE : VARIANT_FALSE );
				regExp->PutPattern( pattern );

				IMatchCollectionPtr pMatchCol( regExp->Execute( target ) );

				if( pMatchCol->Count > 0 ){
					if( findKey != NULL ){
						IMatch2Ptr pMatch( pMatchCol->Item[0] );
						_bstr_t value( pMatch->Value );

						*findKey = !value ? L"" : value;
					}
					return TRUE;
				}
			}
		}catch( _com_error& ){
			//_OutputDebugString(L"%s\r\n", e.ErrorMessage());
		}
		return FALSE;
	}else{
		//�ʏ�
		if( andMode == TRUE ){
			for( size_t i=0; i<keyList->size(); i++ ){
				if( SearchKeyword(word, (*keyList)[i], caseFlag) == word.end() ){
					//������Ȃ������̂ŏI��
					return FALSE;
				}else{
					if( findKey != NULL ){
						if( findKey->size() > 0 ){
							*findKey += L" ";
						}
						*findKey += (*keyList)[i];
					}
				}
			}
			return TRUE;
		}else{
			for( size_t i=0; i<keyList->size(); i++ ){
				if( SearchKeyword(word, (*keyList)[i], caseFlag) != word.end() ){
					//���������̂ŏI��
					return TRUE;
				}
			}
			return FALSE;
		}
	}
}

BOOL CEpgDBManager::IsFindLikeKeyword(BOOL caseFlag, const vector<wstring>* keyList, const wstring& word, BOOL andMode, wstring* findKey)
{
	BOOL ret = FALSE;

	DWORD hitCount = 0;
	DWORD missCount = 0;
	for( size_t i=0; i<keyList->size(); i++ ){
		wstring key= L"";
		for( size_t j=0; j<(*keyList)[i].size(); j++ ){
			key += (*keyList)[i].at(j);
			if( SearchKeyword(word, key, caseFlag) == word.end() ){
				missCount+=1;
				key = (*keyList)[i].at(j);
				if( SearchKeyword(word, key, caseFlag) == word.end() ){
					missCount+=1;
					key = L"";
				}else{
					//hitCount+=1;
				}
			}else{
				hitCount+=(DWORD)key.size();
			}
		}
		if( andMode == FALSE ){
			DWORD totalCount = hitCount+missCount;
			DWORD per = (hitCount*100) / totalCount;
			if( per > 70 ){
				ret = TRUE;
				break;
			}
			hitCount = 0;
			missCount = 0;
		}else{
			if( findKey != NULL ){
				*findKey += (*keyList)[i];
			}
		}
	}
	if( andMode == TRUE ){
		DWORD totalCount = hitCount+missCount;
		DWORD per = (hitCount*100) / totalCount;
		if( per > 70 ){
			ret = TRUE;
		}else{
			ret = FALSE;
		}
	}
	return ret;
}

BOOL CEpgDBManager::GetServiceList(vector<EPGDB_SERVICE_INFO>* list)
{
	CBlockLock lock(&this->epgMapLock);

	BOOL ret = TRUE;
	map<LONGLONG, EPGDB_SERVICE_EVENT_INFO>::iterator itr;
	for( itr = this->epgMap.begin(); itr != this->epgMap.end(); itr++ ){
		list->push_back(itr->second.serviceInfo);
	}
	if( list->size() == 0 ){
		ret = FALSE;
	}

	return ret;
}

BOOL CEpgDBManager::SearchEpg(
	WORD ONID,
	WORD TSID,
	WORD SID,
	WORD EventID,
	EPGDB_EVENT_INFO* result
	)
{
	CBlockLock lock(&this->epgMapLock);

	BOOL ret = FALSE;

	LONGLONG key = _Create64Key(ONID, TSID, SID);
	map<LONGLONG, EPGDB_SERVICE_EVENT_INFO>::iterator itr;
	itr = this->epgMap.find(key);
	if( itr != this->epgMap.end() ){
		EPGDB_EVENT_INFO infoKey;
		infoKey.event_id = EventID;
		vector<EPGDB_EVENT_INFO>::iterator itrInfo;
		itrInfo = std::lower_bound(itr->second.eventList.begin(), itr->second.eventList.end(), infoKey,
		                           [](const EPGDB_EVENT_INFO& a, const EPGDB_EVENT_INFO& b) { return a.event_id < b.event_id; });
		if( itrInfo != itr->second.eventList.end() && itrInfo->event_id == EventID ){
			result->DeepCopy(*itrInfo);
			ret = TRUE;
		}
	}

	return ret;
}

BOOL CEpgDBManager::SearchEpg(
	WORD ONID,
	WORD TSID,
	WORD SID,
	LONGLONG startTime,
	DWORD durationSec,
	EPGDB_EVENT_INFO* result
	)
{
	CBlockLock lock(&this->epgMapLock);

	BOOL ret = FALSE;

	LONGLONG key = _Create64Key(ONID, TSID, SID);
	map<LONGLONG, EPGDB_SERVICE_EVENT_INFO>::iterator itr;
	itr = this->epgMap.find(key);
	if( itr != this->epgMap.end() ){
		vector<EPGDB_EVENT_INFO>::iterator itrInfo;
		for( itrInfo = itr->second.eventList.begin(); itrInfo != itr->second.eventList.end(); itrInfo++ ){
			if( itrInfo->StartTimeFlag == 1 && itrInfo->DurationFlag == 1 ){
				if( startTime == ConvertI64Time(itrInfo->start_time) &&
					durationSec == itrInfo->durationSec
					){
						result->DeepCopy(*itrInfo);
						ret = TRUE;
						break;
				}
			}
		}
	}

	return ret;
}

BOOL CEpgDBManager::SearchServiceName(
	WORD ONID,
	WORD TSID,
	WORD SID,
	wstring& serviceName
	)
{
	CBlockLock lock(&this->epgMapLock);

	BOOL ret = FALSE;

	LONGLONG key = _Create64Key(ONID, TSID, SID);
	map<LONGLONG, EPGDB_SERVICE_EVENT_INFO>::iterator itr;
	itr = this->epgMap.find(key);
	if( itr != this->epgMap.end() ){
		serviceName = itr->second.serviceInfo.service_name;
		ret = TRUE;
	}

	return ret;
}

//�����Ώۂ⌟���p�^�[������S���p�̋�ʂ���菜��(��ConvertText.txt�ɑ���)
//ConvertText.txt�ƈقȂ蔼�p���_�J�i��(�Ӑ}�ʂ�)�u������_�A�m�n�C�D�S�p�󔒂�u������_�A�\(U+2015)����(U+0396)��u�����Ȃ��_�ɒ���
void CEpgDBManager::ConvertSearchText(wstring& str)
{
	static const wchar_t FF0X_table[] = {
		//+0     +1     +2     +3     +4     +5     +6     +7     +8     +9     +A     +B     +C     +D     +E     +F
		0,     L'!',  0,     L'#',  L'$',  L'%',  L'&',  0,     L'(',  L')',  L'*',  L'+',  L',',  L'-',  L'.',  L'/',  // FF00 - FF0F
		L'0',  L'1',  L'2',  L'3',  L'4',  L'5',  L'6',  L'7',  L'8',  L'9',  L':',  L';',  L'<',  L'=',  L'>',  L'?',  // FF10 - FF1F
		L'@',  L'A',  L'B',  L'C',  L'D',  L'E',  L'F',  L'G',  L'H',  L'I',  L'J',  L'K',  L'L',  L'M',  L'N',  L'O',  // FF20 - FF2F
		L'P',  L'Q',  L'R',  L'S',  L'T',  L'U',  L'V',  L'W',  L'X',  L'Y',  L'Z',  L'[',  0,     L']',  L'^',  L'_',  // FF30 - FF3F
		L'`',  L'a',  L'b',  L'c',  L'd',  L'e',  L'f',  L'g',  L'h',  L'i',  L'j',  L'k',  L'l',  L'm',  L'n',  L'o',  // FF40 - FFFF
		L'p',  L'q',  L'r',  L's',  L't',  L'u',  L'v',  L'w',  L'x',  L'y',  L'z',  L'{',  L'|',  L'}',  L'~',  0,     // FF50 - FF5E
		0,     L'�B', L'�u', L'�v', L'�A', L'�E', L'��', L'�@', L'�B', L'�D', L'�F', L'�H', L'��', L'��', L'��', L'�b', // FF60 - FF6F
		L'�[', L'�A', L'�C', L'�E', L'�G', L'�I', L'�J', L'�L', L'�N', L'�P', L'�R', L'�T', L'�V', L'�X', L'�Z', L'�\', // FF70 - FF7F
		L'�^', L'�`', L'�c', L'�e', L'�g', L'�i', L'�j', L'�k', L'�l', L'�m', L'�n', L'�q', L'�t', L'�w', L'�z', L'�}', // FF80 - FF8F
		L'�~', L'��', L'��', L'��', L'��', L'��', L'��', L'��', L'��', L'��', L'��', L'��', L'��', L'��', L'�J', L'�K', // FF90 - FF9F
		0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     // FFA0 - FFAF
		0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     // FFB0 - FFBF
		0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     // FFC0 - FFCF
		0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     // FFD0 - FFDF
		0,     0,     0,     0,     0,     L'\\', 0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     // FFE0 - FFEF
		0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     0,     // FFF0 - FFFF
	};

	// str[str.size()] �������Ȃ��������߁Astr.c_str()[str.size()] �Ƃ��Ă����� loop���� c_str() �����ʂȂ̂ŊO�o���B
	const wchar_t *pstr = str.c_str();
	size_t j = 0;
	for (size_t i = 0; i < str.size(); i++) {
		wchar_t wch = pstr[i];
		if (wch >= 0xFF00 && wch <= 0xFFFF)
		{
			wchar_t wch2 = FF0X_table[wch & 0xFF];
			if (wch2)
			{
				if (pstr[i+1] == L'�' && ((wch >= L'�' && wch <= L'�') || (wch >= L'�' && wch <= L'�')))
				{
					wch2++; i++;
				}
				else if (pstr[i+1] == L'�' && wch >= L'�' && wch <= L'�')
				{
					wch2 += 2; i++;
				}
				wch = wch2;
			}
		}
		else if (wch == L'�f') { wch = L'\''; }
		else if (wch == L'�h') { wch = L'"'; }
		else if (wch == L'�@') { wch = L' '; }
		str[j++] = wch;
	}
	str.resize(j);
}
