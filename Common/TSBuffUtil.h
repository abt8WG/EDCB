#pragma once

#include "TSPacketUtil.h"

class CTSBuffUtil
{
public:
	static const DWORD ERR_ADD_NEXT = 100;
	static const DWORD ERR_NOT_SUPPORT = 101;

	CTSBuffUtil(BOOL supportPES = FALSE);

	//Add188TS()��TRUE��Ԃ���GetSectionBuff()��1��ȏ㐬������B���̂Ƃ��󂯎��Ȃ������o�b�t�@�͎���Add188TS()�ŏ�����
	DWORD Add188TS(CTSPacketUtil* tsPacket);
	BOOL GetSectionBuff(BYTE** sectionData, DWORD* dataSize);
	BOOL IsPES();

protected:
	DWORD sectionSize;
	vector<BYTE> sectionBuff;
	vector<BYTE> carryPacket;

	WORD lastPID;
	BYTE lastCounter;
	BOOL duplicateFlag;

	BOOL supportPES;
	BOOL PESMode;
protected:
	void Clear();
	BOOL CheckCounter(CTSPacketUtil* tsPacket);
	DWORD AddSectionBuff(CTSPacketUtil* tsPacket);
	DWORD AddPESBuff(CTSPacketUtil* tsPacket);
};
