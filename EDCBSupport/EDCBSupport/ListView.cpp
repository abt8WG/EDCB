#include "stdafx.h"
#include "EDCBSupport.h"
#include "ListView.h"

// Explorerスタイルにするとグリッド線がおかしくなる...
#ifdef LISTVIEW_EXPLORER_STYLE
#include <uxtheme.h>
#pragma comment(lib,"uxtheme.lib")
#endif


namespace EDCBSupport
{

#define MAX_ITEM_TEXT_LENGTH 256


	CListView::CListView()
		: m_SortColumn(-1)
		, m_fSortAscending(true)
		, m_pEventHandler(NULL)
	{
	}


	CListView::~CListView()
	{
		Destroy();

		for (size_t i=0;i<m_ColumnList.size();i++)
			delete m_ColumnList[i];
	}


	bool CListView::Create(HWND hwndParent,INT_PTR ID)
	{
		Destroy();

		m_hwnd=::CreateWindowEx(0,WC_LISTVIEW,TEXT(""),
								WS_CHILD | WS_CLIPSIBLINGS | WS_BORDER | LVS_REPORT | LVS_SHOWSELALWAYS,
								0,0,0,0,
								hwndParent,
								reinterpret_cast<HMENU>(ID),
								g_hinstDLL,
								NULL);
		if (m_hwnd==NULL)
			return false;

#ifdef LISTVIEW_EXPLORER_STYLE
		::SetWindowTheme(m_hwnd,L"Explorer",NULL);
#endif

		ListView_SetExtendedListViewStyle(m_hwnd,
			LVS_EX_FULLROWSELECT | LVS_EX_GRIDLINES | LVS_EX_LABELTIP | LVS_EX_HEADERDRAGDROP);

		CreateColumns();

		return true;
	}


	int CListView::GetItemCount() const
	{
		if (m_hwnd==NULL)
			return 0;
		return ListView_GetItemCount(m_hwnd);
	}


	int CListView::GetSelectedItemCount() const
	{
		if (m_hwnd==NULL)
			return 0;
		return ListView_GetSelectedCount(m_hwnd);
	}


	int CListView::GetSelectedItem() const
	{
		if (m_hwnd==NULL)
			return -1;
		return ListView_GetNextItem(m_hwnd,-1,LVNI_SELECTED);
	}


	bool CListView::GetSelectedItems(std::vector<int> *pItemList) const
	{
		if (m_hwnd==NULL)
			return false;

		for (int i=-1;(i=ListView_GetNextItem(m_hwnd,i,LVNI_SELECTED))>=0;)
			pItemList->push_back(i);
		return true;
	}


	bool CListView::SetSortColumn(int ColumnID,bool fAscending)
	{
		if (m_hwnd==NULL) {
			m_SortColumn=ColumnID;
			m_fSortAscending=fAscending;
		} else {
			int Index;

			Index=GetColumnIndex(ColumnID);
			if (Index<0)
				return false;
			m_fSortAscending=fAscending;
			SetColumnSortMark(Index,true);
			if (m_SortColumn>=0 && m_SortColumn!=ColumnID) {
				Index=GetColumnIndex(m_SortColumn);
				if (Index>=0)
					SetColumnSortMark(Index,false);
			}
			m_SortColumn=ColumnID;
			SortItems();
		}
		return true;
	}


	int CListView::NumColumns() const
	{
		return (int)m_ColumnList.size();
	}


	const CListView::CColumn *CListView::GetColumnByIndex(int Index) const
	{
		if (Index<0 || (size_t)Index>=m_ColumnList.size())
			return NULL;
		return m_ColumnList[Index];
	}


	const CListView::CColumn *CListView::GetColumnByID(int ColumnID) const
	{
		int Index=GetColumnIndex(ColumnID);
		if (Index<0)
			return NULL;
		return m_ColumnList[Index];
	}


	int CListView::GetColumnWidth(int ColumnID) const
	{
		int Index=GetColumnIndex(ColumnID);
		if (Index<0)
			return 0;

		if (m_hwnd!=NULL) {
			return ListView_GetColumnWidth(m_hwnd,Index);
		}

		return m_ColumnList[Index]->GetWidth();
	}


	bool CListView::SetColumnWidth(int ColumnID,int Width)
	{
		int Index=GetColumnIndex(ColumnID);
		if (Index<0)
			return false;

		if (m_hwnd!=NULL) {
			return ListView_SetColumnWidth(m_hwnd,Index,Width)!=FALSE;
		}

		m_ColumnList[Index]->SetWidth(Width);

		return true;
	}


	int CListView::GetColumnOrder(int ColumnID) const
	{
		int Index=GetColumnIndex(ColumnID);
		if (Index<0)
			return -1;

		if (m_hwnd!=NULL) {
			LVCOLUMN lvc;
			lvc.mask=LVCF_ORDER;
			ListView_GetColumn(m_hwnd,Index,&lvc);
			return lvc.iOrder;
		}

		return m_ColumnList[Index]->GetOrder();
	}


	bool CListView::SetColumnOrder(int ColumnID,int Order)
	{
		int Index=GetColumnIndex(ColumnID);
		if (Index<0)
			return false;

		if (m_hwnd!=NULL) {
			//
			return false;
		} else {
			m_ColumnList[Index]->SetOrder(Order);
		}
		return true;
	}


	bool CListView::OnNotify(WPARAM wParam,LPARAM lParam,LRESULT *pResult)
	{
		LPNMLISTVIEW pnmlv=reinterpret_cast<LPNMLISTVIEW>(lParam);

		if (pnmlv->hdr.hwndFrom!=m_hwnd)
			return false;

		*pResult=0;

		switch (pnmlv->hdr.code) {
		case LVN_COLUMNCLICK:
			if (pnmlv->iSubItem>=0 && (size_t)pnmlv->iSubItem<m_ColumnList.size()) {
				int ID=m_ColumnList[pnmlv->iSubItem]->GetID();
				bool fAscending;

				if (ID==m_SortColumn) {
					fAscending=!m_fSortAscending;
				} else {
					fAscending=true;
				}
				SetSortColumn(ID,fAscending);
			}
			break;

		case NM_RCLICK:
			if (m_pEventHandler!=NULL)
				m_pEventHandler->OnRClick(this);
			break;

		case NM_DBLCLK:
			if (m_pEventHandler!=NULL) {
				LPNMITEMACTIVATE pnmia=reinterpret_cast<LPNMITEMACTIVATE>(lParam);

				m_pEventHandler->OnDoubleClick(this,pnmia->iItem);
			}
			break;

		case NM_CUSTOMDRAW:
			*pResult=OnCustomDraw(reinterpret_cast<LPNMLVCUSTOMDRAW>(lParam));
			break;

		case LVN_KEYDOWN:
			if (m_pEventHandler!=NULL) {
				LPNMLVKEYDOWN pnmlvk=reinterpret_cast<LPNMLVKEYDOWN>(lParam);

				m_pEventHandler->OnKeyDown(this,pnmlvk->wVKey);
			}
			break;

		default:
			return false;
		}

		return true;
	}


	void CListView::OnDestroy()
	{
		for (size_t i=0;i<m_ColumnList.size();i++) {
			CColumn *pColumn=m_ColumnList[i];

			pColumn->SetOrder(GetColumnOrder(pColumn->GetID()));
			pColumn->SetWidth(ListView_GetColumnWidth(m_hwnd,(int)i));
		}

		DeleteAllItems();
	}


	void CListView::InitColumnList(const ColumnInfo *pColumnList,size_t NumColumns)
	{
		for (size_t i=0;i<NumColumns;i++) {
			m_ColumnList.push_back(new CBasicColumn(pColumnList[i]));
			m_ColumnList.back()->SetOrder((int)i);
		}
	}


	void CListView::CreateColumns()
	{
		HDC hdc=::GetDC(m_hwnd);
		HFONT hFont=reinterpret_cast<HFONT>(::SendMessage(m_hwnd,WM_GETFONT,0,0));
		HGDIOBJ hOldFont=::SelectObject(hdc,hFont);
		TEXTMETRIC tm;
		::GetTextMetrics(hdc,&tm);
		::SelectObject(hdc,hOldFont);
		::ReleaseDC(m_hwnd,hdc);

		LVCOLUMN lvc;
		TCHAR szText[64];

		lvc.mask=LVCF_FMT | LVCF_WIDTH | LVCF_TEXT | LVCF_SUBITEM;
		for (int i=0;i<(int)m_ColumnList.size();i++) {
			const CColumn *pColumn=m_ColumnList[i];

			lvc.fmt=pColumn->GetFormat();
			if (pColumn->GetWidth()<0)
				lvc.cx=-pColumn->GetWidth()*tm.tmAveCharWidth;
			else
				lvc.cx=pColumn->GetWidth();
			pColumn->GetText(szText,_countof(szText));
			lvc.pszText=szText;
			lvc.iSubItem=i;
			ListView_InsertColumn(m_hwnd,i,&lvc);
			if (pColumn->GetID()==m_SortColumn)
				SetColumnSortMark(i,true);
		}

		if (m_ColumnList.size()>1) {
			class CCompareColumnOrder
			{
				const std::vector<CColumn*> &m_ColumnList;

			public:
				CCompareColumnOrder(const std::vector<CColumn*> &ColumnList)
					: m_ColumnList(ColumnList)
				{
				}

				bool operator()(int Item1,int Item2) const
				{
					return m_ColumnList[Item1]->GetOrder()<m_ColumnList[Item2]->GetOrder();
				}
			};

			std::vector<int> ColumnOrder;
			ColumnOrder.resize(m_ColumnList.size());
			for (int i=0;i<(int)m_ColumnList.size();i++)
				ColumnOrder[i]=i;
			std::sort(ColumnOrder.begin(),ColumnOrder.end(),
					  CCompareColumnOrder(m_ColumnList));

			ListView_SetColumnOrderArray(m_hwnd,(int)m_ColumnList.size(),&ColumnOrder[0]);
		}
	}


	int CListView::GetColumnIndex(int ColumnID) const
	{
		for (size_t i=0;i<m_ColumnList.size();i++) {
			if (m_ColumnList[i]->GetID()==ColumnID)
				return (int)i;
		}
		return -1;
	}


	bool CListView::InsertItem(int Index,CItem *pItem)
	{
		if (m_hwnd==NULL)
			return false;

		if (Index<0)
			Index=ListView_GetItemCount(m_hwnd);

		LVITEM lvi;
		TCHAR szText[MAX_ITEM_TEXT_LENGTH];

		lvi.mask=LVIF_TEXT | LVIF_PARAM;
		lvi.iItem=Index;
		lvi.iSubItem=0;
		lvi.pszText=TEXT("");
		lvi.lParam=reinterpret_cast<LPARAM>(pItem);
		lvi.iItem=ListView_InsertItem(m_hwnd,&lvi);
		if (lvi.iItem<0)
			return false;

		lvi.mask=LVIF_TEXT;
		lvi.pszText=szText;
		for (int i=0;i<(int)m_ColumnList.size();i++) {
			lvi.iSubItem=i;
			pItem->GetText(m_ColumnList[i]->GetID(),szText,_countof(szText));
			ListView_SetItem(m_hwnd,&lvi);
		}

		return true;
	}


	CListView::CItem *CListView::GetItem(int Index)
	{
		if (m_hwnd==NULL)
			return NULL;

		LVITEM lvi;
		lvi.mask=LVIF_PARAM;
		lvi.iItem=Index;
		lvi.iSubItem=0;
		if (!ListView_GetItem(m_hwnd,&lvi))
			return NULL;

		return reinterpret_cast<CItem*>(lvi.lParam);
	}


	const CListView::CItem *CListView::GetItem(int Index) const
	{
		if (m_hwnd==NULL)
			return NULL;

		LVITEM lvi;
		lvi.mask=LVIF_PARAM;
		lvi.iItem=Index;
		lvi.iSubItem=0;
		if (!ListView_GetItem(m_hwnd,&lvi))
			return NULL;

		return reinterpret_cast<const CItem*>(lvi.lParam);
	}


	bool CListView::UpdateItem(int Index) const
	{
		if (m_hwnd==NULL)
			return false;

		const CItem *pItem=GetItem(Index);
		if (pItem==NULL)
			return false;

		LVITEM lvi;
		TCHAR szText[MAX_ITEM_TEXT_LENGTH];

		lvi.mask=LVIF_TEXT;
		lvi.iItem=Index;
		lvi.pszText=szText;
		for (int i=0;i<(int)m_ColumnList.size();i++) {
			lvi.iSubItem=i;
			pItem->GetText(m_ColumnList[i]->GetID(),szText,_countof(szText));
			ListView_SetItem(m_hwnd,&lvi);
		}

		return true;
	}


	bool CListView::DeleteItem(int Index)
	{
		if (m_hwnd==NULL)
			return false;

		CItem *pItem=GetItem(Index);
		if (pItem==NULL
				|| !ListView_DeleteItem(m_hwnd,Index))
			return false;

		delete pItem;

		return true;
	}


	void CListView::DeleteAllItems()
	{
		if (m_hwnd!=NULL) {
			LVITEM lvi;

			lvi.mask=LVIF_PARAM;
			lvi.iSubItem=0;
			for (int i=GetItemCount()-1;i>=0;i--) {
				lvi.iItem=i;
				if (ListView_GetItem(m_hwnd,&lvi))
					delete reinterpret_cast<CItem*>(lvi.lParam);
			}

			ListView_DeleteAllItems(m_hwnd);
		}
	}


	void CListView::ReserveItemCount(int Items)
	{
		if (m_hwnd!=NULL)
			ListView_SetItemCount(m_hwnd,Items);
	}


	void CListView::SortItems()
	{
		if (m_hwnd!=NULL && m_SortColumn>=0) {
			ListView_SortItems(m_hwnd,ItemCompareFunc,
							   reinterpret_cast<LPARAM>(this));
		}
	}


	int CALLBACK CListView::ItemCompareFunc(LPARAM lParam1,LPARAM lParam2,LPARAM lParamSort)
	{
		const CItem *pItem1=reinterpret_cast<const CItem*>(lParam1);
		const CItem *pItem2=reinterpret_cast<const CItem*>(lParam2);
		CListView *pThis=reinterpret_cast<CListView*>(lParamSort);
		int Cmp=pItem1->Compare(pThis->m_SortColumn,pItem2);
		return pThis->m_fSortAscending?Cmp:-Cmp;
	}


	void CListView::SetColumnSortMark(int Index,bool fSet)
	{
		HWND hwndHeader=ListView_GetHeader(m_hwnd);
		HDITEM hdi;

		hdi.mask=HDI_FORMAT;
		Header_GetItem(hwndHeader,Index,&hdi);
		if (fSet)
			hdi.fmt=(hdi.fmt & ~(HDF_SORTUP | HDF_SORTDOWN)) | (m_fSortAscending?HDF_SORTUP:HDF_SORTDOWN);
		else
			hdi.fmt&=~(HDF_SORTUP | HDF_SORTDOWN);
		Header_SetItem(hwndHeader,Index,&hdi);
	}




	CListView::CColumn::CColumn()
		: m_Width(0)
		, m_Order(0)
	{
	}


	CListView::CBasicColumn::CBasicColumn(const ColumnInfo &Info)
		: ColumnInfo(Info)
	{
		m_Width=-Info.m_DefaultWidth;
	}

}
