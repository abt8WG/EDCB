#pragma once


#include "Widget.h"


namespace EDCBSupport
{

	// ステータスバー
	class CStatusBar : public CWidget
	{
	public:
		CStatusBar();
		~CStatusBar();
		bool Create(HWND hwndParent,INT_PTR ID=0) override;
		bool SetText(LPCTSTR pszText);
	};

}	// namespace EDCBSupport
