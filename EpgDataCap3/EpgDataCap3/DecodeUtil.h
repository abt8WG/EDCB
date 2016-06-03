#pragma once

#include "EpgDBUtil.h"
#include "../../Common/TSPacketUtil.h"
#include "../../Common/TSBuffUtil.h"
#include "../../Common/EpgDataCap3Def.h"

#include "./Table/TableUtil.h"

class CDecodeUtil
{
public:
	CDecodeUtil(void);

	void SetEpgDB(CEpgDBUtil* epgDBUtil);
	void AddTSData(BYTE* data);

	//��̓f�[�^�̌��݂̃X�g���[���h�c���擾����
	//�����F
	// originalNetworkID		[OUT]���݂�originalNetworkID
	// transportStreamID		[OUT]���݂�transportStreamID
	BOOL GetTSID(
		WORD* originalNetworkID,
		WORD* transportStreamID
		);

	//���X�g���[���̃T�[�r�X�ꗗ���擾����
	//�����F
	// serviceListSize			[OUT]serviceList�̌�
	// serviceList				[OUT]�T�[�r�X���̃��X�g�iDLL���Ŏ����I��delete����B���Ɏ擾���s���܂ŗL���j
	BOOL GetServiceListActual(
		DWORD* serviceListSize,
		SERVICE_INFO** serviceList
		);

	//�X�g���[�����̌��݂̎��ԏ����擾����
	//�����F
	// time				[OUT]�X�g���[�����̌��݂̎���
	// tick				[OUT]time���擾�������_�̃`�b�N�J�E���g
	BOOL GetNowTime(
		FILETIME* time,
		DWORD* tick = NULL
		);

protected:
	struct NIT_SECTION_INFO{
		BYTE last_section_number;
		map<BYTE, std::unique_ptr<const CNITTable>> nitSection;
	};
	struct SDT_SECTION_INFO{
		BYTE last_section_number;
		map<BYTE, std::unique_ptr<const CSDTTable>> sdtSection;
	};

	CEpgDBUtil* epgDBUtil;

	CTableUtil tableUtil;

	//PID���̃o�b�t�@�����O
	//�L�[ PID
	map<WORD, CTSBuffUtil> buffUtilMap;

	std::unique_ptr<const CPATTable> patInfo;
	NIT_SECTION_INFO nitActualInfo;
	SDT_SECTION_INFO sdtActualInfo;
	std::unique_ptr<const CBITTable> bitInfo;
	std::unique_ptr<const CSITTable> sitInfo;
	FILETIME totTime;
	FILETIME tdtTime;
	FILETIME sitTime;
	DWORD totTimeTick;
	DWORD tdtTimeTick;
	DWORD sitTimeTick;


	std::unique_ptr<SERVICE_INFO[]> serviceList;

protected:
	void Clear();
	void ClearBuff(WORD noClearPid);
	void ChangeTSIDClear(WORD noClearPid);

	BOOL CheckPAT(WORD PID, CPATTable* pat);
	BOOL CheckNIT(WORD PID, CNITTable* nit);
	BOOL CheckSDT(WORD PID, CSDTTable* sdt);
	BOOL CheckTOT(WORD PID, CTOTTable* tot);
	BOOL CheckTDT(WORD PID, CTDTTable* tdt);
	BOOL CheckEIT(WORD PID, CEITTable* eit);
	BOOL CheckBIT(WORD PID, CBITTable* bit);
	BOOL CheckSIT(WORD PID, CSITTable* sit);

	//���X�g���[���̃T�[�r�X�ꗗ��SIT����擾����
	//�����F
	// serviceListSize			[OUT]serviceList�̌�
	// serviceList				[OUT]�T�[�r�X���̃��X�g�iDLL���Ŏ����I��delete����B���Ɏ擾���s���܂ŗL���j
	BOOL GetServiceListSIT(
		DWORD* serviceListSize,
		SERVICE_INFO** serviceList
		);

};
