#ifndef __ERR_DEF_H__
#define __ERR_DEF_H__

#define ERR_FALSE		FALSE		//�ėp�G���[
#define NO_ERR			TRUE		//����
#define ERR_INIT		10			//���������s
#define ERR_NOT_INIT	11			//��������
#define ERR_SIZE		12			//���̓T�C�Y���s��
#define ERR_LOAD_MODULE	13			//���W���[���̃��[�h�Ɏ��s
#define ERR_INVALID_ARG	14			//����������
#define ERR_NOT_FIND	15			//��񂪌�����Ȃ�����
#define ERR_NEED_NEXT_PACKET	20	//����TS�p�P�b�g����Ȃ��Ɖ�͂ł��Ȃ�
#define ERR_CAN_NOT_ANALYZ		21	//�{����TS�p�P�b�g�H��͕s�\
#define ERR_NOT_FIRST 			22	//�ŏ���TS�p�P�b�g������
#define ERR_INVALID_PACKET		23	//�{����TS�p�P�b�g�H�p�P�b�g���ŉ��Ă邩��
#define ERR_NO_CHAGE			30	//�o�[�W�����̕ύX�Ȃ����߉�͕s�v

#define ERR_LOAD_B25	40			//B25Decorder.dll�̃��[�h�Ɏ��s
#define ERR_OPEN_TUNER	41			//�w��BonDriver��Open�Ɏ��s
#define ERR_FIND_TUNER	42			//�w��BonDriver�̌����Ɏ��s
#define ERR_LOAD_EPG	43			//EpgDataCap.dll�̃��[�h�Ɏ��s

#define NO_ERR_EPG_ALL		100		//EPG��񒙂܂��� Basic��Extend����
#define NO_ERR_EPG_BASIC	101		//EPG��񒙂܂��� Basic�̂�
#define NO_ERR_EPG_EXTENDED	102		//EPG��񒙂܂��� Extend�̂�

#define CMD_SUCCESS			NO_ERR		//����
#define CMD_ERR				ERR_FALSE	//�ėp�G���[
#define CMD_NEXT			202			//Enum�R�}���h�p�A��������
#define CMD_NON_SUPPORT		203			//���T�|�[�g�̃R�}���h
#define CMD_ERR_INVALID_ARG	204			//�����G���[
#define CMD_ERR_CONNECT		205			//�T�[�o�[�ɃR�l�N�g�ł��Ȃ�����
#define CMD_ERR_DISCONNECT	206			//�T�[�o�[����ؒf���ꂽ
#define CMD_ERR_TIMEOUT		207			//�^�C���A�E�g����
#define CMD_ERR_BUSY		208			//�r�W�[��ԂŌ��ݏ����ł��Ȃ��iEPG�f�[�^�ǂݍ��ݒ��A�^�撆�Ȃǁj
#define CMD_NO_RES			250			//Post�p�Ń��X�|���X�̕K�v�Ȃ�

//�`�����l���X�L�����AEPG�擾�̃X�e�[�^�X�p
#define	ST_STOP				300			//��~��
#define ST_WORKING			301			//���s��
#define ST_COMPLETE			302			//����
#define ST_CANCEL			303			//�L�����Z�����ꂽ

#endif
