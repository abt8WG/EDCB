#pragma once


#include "Widget.h"


namespace EDCBSupport
{

	class CTab : public CWidget
	{
	public:
		CTab();
		~CTab();
		bool Create(HWND hwndParent,INT_PTR ID=0) override;
		bool InsertItem(int Index,LPCTSTR pszText,int Image=-1);
		bool AddItem(LPCTSTR pszText,int Image=-1) { return InsertItem(-1,pszText,Image); }
		int GetTabHeight() const;
		void SetSel(int Index);
		int GetSel() const;
		void SetImageList(HIMAGELIST himl);

	private:
		HFONT m_hFont;
		HIMAGELIST m_himlIcons;
	};

}	// namespace EDCBSupport
