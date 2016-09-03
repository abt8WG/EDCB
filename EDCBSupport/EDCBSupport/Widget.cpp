#include "stdafx.h"
#include "EDCBSupport.h"
#include "ListView.h"


namespace EDCBSupport
{

	CWidget::CWidget()
		: m_hwnd(NULL)
	{
	}


	CWidget::~CWidget()
	{
		Destroy();
	}


	void CWidget::Destroy()
	{
		if (m_hwnd!=NULL) {
			OnDestroy();
			::DestroyWindow(m_hwnd);
			m_hwnd=NULL;
		}
	}


	void CWidget::SetPosition(int Left,int Top,int Width,int Height)
	{
		if (m_hwnd!=NULL)
			::SetWindowPos(m_hwnd,NULL,Left,Top,Width,Height,
						   SWP_NOZORDER | SWP_NOACTIVATE);
	}

	void CWidget::SetPosition(const RECT &Position)
	{
		if (m_hwnd!=NULL)
			SetPosition(Position.left,Position.top,
						Position.right-Position.left,
						Position.bottom-Position.top);
	}


	bool CWidget::GetPosition(RECT *pPosition) const
	{
		if (m_hwnd==NULL)
			return false;

		::GetWindowRect(m_hwnd,pPosition);
		HWND hwndParent=::GetAncestor(m_hwnd,GA_PARENT);
		if (hwndParent!=NULL)
			::MapWindowPoints(NULL,hwndParent,reinterpret_cast<POINT*>(pPosition),2);

		return true;
	}


	int CWidget::GetWidth() const
	{
		if (m_hwnd==NULL)
			return 0;

		RECT rc;
		::GetWindowRect(m_hwnd,&rc);
		return rc.right-rc.left;
	}


	int CWidget::GetHeight() const
	{
		if (m_hwnd==NULL)
			return 0;

		RECT rc;
		::GetWindowRect(m_hwnd,&rc);
		return rc.bottom-rc.top;
	}


	void CWidget::SetVisible(bool fVisible)
	{
		if (m_hwnd!=NULL)
			::ShowWindow(m_hwnd,fVisible?SW_SHOW:SW_HIDE);
	}

}
