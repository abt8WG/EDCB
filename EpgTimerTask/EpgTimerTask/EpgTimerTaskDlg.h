
// EpgTimerTaskDlg.h : �w�b�_�[ �t�@�C��
//

#pragma once
#include "../../Common/Util.h"
#include "../../Common/StringUtil.h"
#include "../../Common/PathUtil.h"
#include "../../Common/PipeServer.h"
#include "../../Common/SendCtrlCmd.h"
#include "../../Common/CtrlCmdUtil.h"
#include "../../Common/CtrlCmdUtil2.h"
#include "QueryWaitDlg.h"

#define WM_TRAY_PUSHICON (WM_USER+51) //�g���C�A�C�R�������ꂽ
#define WM_QUERY_SUSPEND (WM_USER+52)
#define WM_QUERY_REBOOT (WM_USER+53)
#define WM_END_DIALOG (WM_USER+54)

#define TRAYICON_ID 200
#define RETRY_ADD_TRAY 1000
#define RETRY_CHG_TRAY 1001

// CEpgTimerTaskDlg �_�C�A���O
class CEpgTimerTaskDlg
{
// �R���X�g���N�V����
public:
	CEpgTimerTaskDlg();	// �W���R���X�g���N�^�[
	INT_PTR DoModal();

// �_�C�A���O �f�[�^
	enum { IDD = IDD_EPGTIMERTASK_DIALOG };

protected:
	//�^�X�N�g���C
	BOOL DeleteTaskBar(HWND hWnd, UINT uiID);
	BOOL AddTaskBar(HWND hWnd, UINT uiMsg, UINT uiID, HICON hIcon, wstring strTips);
	BOOL ChgTipsTaskBar(HWND hWnd, UINT uiID, HICON hIcon, wstring strTips);

// ����
protected:
	HICON m_hIcon;
	HICON m_hIcon2;
	HICON m_hIconRed;
	HICON m_hIconGreen;
	HWND m_hDlg;
	UINT m_uMsgTaskbarCreated;

	CPipeServer m_cPipe;
	DWORD m_dwSrvStatus;

	//�O������R�}���h�֌W
	static int CALLBACK OutsideCmdCallback(void* pParam, CMD_STREAM* pCmdParam, CMD_STREAM* pResParam);
	//CMD_TIMER_GUI_VIEW_EXECUTE View�A�v���iEpgDataCap_Bon.exe�j���N��
	void CmdViewExecute(CMD_STREAM* pCmdParam, CMD_STREAM* pResParam);
	//CMD_TIMER_GUI_QUERY_SUSPEND �X�^���o�C�A�x�~�A�V���b�g�_�E���ɓ����Ă������̊m�F�����[�U�[�ɍs��
	void CmdViewQuerySuspend(CMD_STREAM* pCmdParam, CMD_STREAM* pResParam);
	//CMD_TIMER_GUI_QUERY_REBOOT PC�ċN���ɓ����Ă������̊m�F�����[�U�[�ɍs��
	void CmdViewQueryReboot(CMD_STREAM* pCmdParam, CMD_STREAM* pResParam);
	//CMD_TIMER_GUI_SRV_STATUS_CHG �T�[�o�[�̃X�e�[�^�X�ύX�ʒm�i1:�ʏ�A2:EPG�f�[�^�擾�J�n�A3:�\��^��J�n�j
	void CmdSrvStatusChg(CMD_STREAM* pCmdParam, CMD_STREAM* pResParam);

	// �������ꂽ�A���b�Z�[�W���蓖�Ċ֐�
	BOOL OnInitDialog();
	LRESULT WindowProc(UINT message, WPARAM wParam, LPARAM lParam);
	void OnBnClickedButtonEnd();
	void OnBnClickedButtonS4();
	void OnBnClickedButtonS3();
	void OnDestroy();
	void OnTimer(UINT_PTR nIDEvent);

	static INT_PTR CALLBACK DlgProc(HWND hDlg, UINT uMsg, WPARAM wParam, LPARAM lParam);
};
