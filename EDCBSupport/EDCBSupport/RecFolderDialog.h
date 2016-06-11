#pragma once

namespace EDCBSupport
{

	// �^��t�H���_�ݒ�_�C�A���O
	class CRecFolderDialog
	{
	public:
		CRecFolderDialog(EDCBSettings &Settings,CSendCtrlCmd &SendCtrlCmd);
		~CRecFolderDialog();
		bool Show(HINSTANCE hinst,HWND hwndOwner,REC_FILE_SET_INFO *pRecFileSet);

	private:
		EDCBSettings &m_EDCBSettings;
		CSendCtrlCmd &m_SendCtrlCmd;
		REC_FILE_SET_INFO *m_pRecFileSet;

		void OnCommand(HWND hDlg,UINT Command,UINT NotifyCode);
		bool GetSettings(HWND hDlg);

		static INT_PTR CALLBACK DlgProc(HWND hDlg,UINT uMsg,WPARAM wParam,LPARAM lParam);
	};

}
