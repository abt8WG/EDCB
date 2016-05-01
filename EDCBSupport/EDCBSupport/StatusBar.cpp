#include "stdafx.h"
#include "EDCBSupport.h"
#include "StatusBar.h"


namespace EDCBSupport
{

	CStatusBar::CStatusBar()
	{
	}


	CStatusBar::~CStatusBar()
	{
		Destroy();
	}


	bool CStatusBar::Create(HWND hwndParent,INT_PTR ID)
	{
		Destroy();

		m_hwnd=::CreateWindowEx(0,STATUSCLASSNAME,NULL,
								WS_CHILD | WS_CLIPSIBLINGS | SBARS_SIZEGRIP,
								0,0,0,0,
								hwndParent,
								reinterpret_cast<HMENU>(ID),
								g_hinstDLL,NULL);
		if (m_hwnd==NULL)
			return false;

		return true;
	}


	bool CStatusBar::SetText(LPCTSTR pszText)
	{
		if (m_hwnd==NULL)
			return false;

		::SendMessage(m_hwnd,SB_SETTEXT,SB_SIMPLEID,reinterpret_cast<LPARAM>(pszText));
		::SendMessage(m_hwnd,SB_SIMPLE,TRUE,0);

		return true;
	}

}	// namespace EDCBSupport
