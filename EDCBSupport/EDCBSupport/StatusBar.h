#pragma once


#include "Widget.h"


namespace EDCBSupport
{

	// �X�e�[�^�X�o�[
	class CStatusBar : public CWidget
	{
	public:
		CStatusBar();
		~CStatusBar();
		bool Create(HWND hwndParent,INT_PTR ID=0) override;
		bool SetText(LPCTSTR pszText);
	};

}	// namespace EDCBSupport
