#pragma once

#include "../../Common/TimeShiftUtil.h"
#include "../../Common/InstanceManager.h"

class CFileStreamingManager
{
public:
	void CloseAllFile(
		);
	BOOL IsStreaming();

	BOOL OpenTimeShift(
		LPCWSTR filePath,
		DWORD* ctrlID
		);
	BOOL OpenFile(
		LPCWSTR filePath,
		DWORD* ctrlID
		);
	BOOL CloseFile(
		DWORD ctrlID
		);
	BOOL StartSend(
		DWORD ctrlID
		);
	BOOL StopSend(
		DWORD ctrlID
		);

	//�X�g���[���z�M�Ō��݂̑��M�ʒu�Ƒ��t�@�C���T�C�Y���擾����
	//�߂�l�F
	// �G���[�R�[�h
	//�����F
	// val				[IN/OUT]�T�C�Y���
	BOOL GetPos(
		NWPLAY_POS_CMD* val
		);

	//�X�g���[���z�M�ő��M�ʒu���V�[�N����
	//�߂�l�F
	// �G���[�R�[�h
	//�����F
	// val				[IN]�T�C�Y���
	BOOL SetPos(
		NWPLAY_POS_CMD* val
		);

	//�X�g���[���z�M�ő��M���ݒ肷��
	//�߂�l�F
	// �G���[�R�[�h
	//�����F
	// val				[IN]�T�C�Y���
	BOOL SetIP(
		NWPLAY_PLAY_INFO* val
		);

protected:
	//�񉼑z�f�X�g���N�^����!
	class CMyInstanceManager : public CInstanceManager<CTimeShiftUtil>
	{
	public:
		void clear() {
			CBlockLock lock(&this->m_lock);
			this->m_list.clear();
		}
		bool empty() {
			CBlockLock lock(&this->m_lock);
			return this->m_list.empty();
		}
	};
	CMyInstanceManager utilMng;
};

