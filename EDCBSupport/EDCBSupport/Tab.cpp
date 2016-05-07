#include "stdafx.h"
#include "EDCBSupport.h"
#include "Tab.h"


namespace EDCBSupport
{

	CTab::CTab()
		: m_hFont(NULL)
		, m_himlIcons(NULL)
	{
	}


	CTab::~CTab()
	{
		Destroy();

		if (m_hFont!=NULL)
			::DeleteObject(m_hFont);

		if (m_himlIcons!=NULL)
			::ImageList_Destroy(m_himlIcons);
	}


	bool CTab::Create(HWND hwndParent,INT_PTR ID)
	{
		Destroy();

		m_hwnd=::CreateWindowEx(0,WC_TABCONTROL,NULL,
								WS_CHILD | WS_CLIPSIBLINGS | TCS_HOTTRACK,
								0,0,0,0,
								hwndParent,
								reinterpret_cast<HMENU>(ID),
								g_hinstDLL,NULL);
		if (m_hwnd==NULL)
			return false;

		if (m_hFont==NULL) {
			NONCLIENTMETRICS ncm;

			ncm.cbSize=sizeof(ncm);
			::SystemParametersInfo(SPI_GETNONCLIENTMETRICS,ncm.cbSize,&ncm,0);
			ncm.lfCaptionFont.lfWeight=FW_NORMAL;
			m_hFont=::CreateFontIndirect(&ncm.lfCaptionFont);
		}
		::SendMessage(m_hwnd,WM_SETFONT,reinterpret_cast<WPARAM>(m_hFont),FALSE);

		if (m_himlIcons!=NULL)
			TabCtrl_SetImageList(m_hwnd,m_himlIcons);

		return true;
	}


	bool CTab::InsertItem(int Index,LPCTSTR pszText,int Image)
	{
		if (m_hwnd==NULL)
			return false;

		if (Index<0)
			Index=TabCtrl_GetItemCount(m_hwnd);

		TCITEM tci;
		tci.mask=TCIF_TEXT | TCIF_IMAGE;
		tci.pszText=const_cast<LPTSTR>(pszText);
		tci.iImage=Image;
		return TabCtrl_InsertItem(m_hwnd,Index,&tci)>=0;
	}


	int CTab::GetTabHeight() const
	{
		if (m_hwnd==NULL)
			return 0;

		RECT rc;
		if (!TabCtrl_GetItemRect(m_hwnd,0,&rc))
			return 0;
		return rc.bottom;
	}


	void CTab::SetSel(int Index)
	{
		if (m_hwnd==NULL)
			return;
		TabCtrl_SetCurSel(m_hwnd,Index);
	}


	int CTab::GetSel() const
	{
		if (m_hwnd==NULL)
			return -1;
		return TabCtrl_GetCurSel(m_hwnd);
	}


	void CTab::SetImageList(HIMAGELIST himl)
	{
		if (m_hwnd!=NULL)
			TabCtrl_SetImageList(m_hwnd,himl);
		if (m_himlIcons!=NULL)
			::ImageList_Destroy(m_himlIcons);
		m_himlIcons=himl;
	}

}	// namespace EDCBSupport
