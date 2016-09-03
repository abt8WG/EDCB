#include "stdafx.h"
#include "EDCBSupport.h"
#include "ReserveListView.h"


namespace EDCBSupport
{

	CReserveListView::CReserveListView()
	{
		static const ColumnInfo ColumnList[] = {
			{COLUMN_TIME,			TEXT("Time"),			TEXT("日時"),			LVCFMT_LEFT,	20},
			{COLUMN_SERVICE,		TEXT("Service"),		TEXT("サービス名"),		LVCFMT_LEFT,	16},
			{COLUMN_TITLE,			TEXT("Title"),			TEXT("タイトル"),		LVCFMT_LEFT,	20},
			{COLUMN_STATUS,			TEXT("Status"),			TEXT("状態"),			LVCFMT_LEFT,	12},
			{COLUMN_RECMODE,		TEXT("RecMode"),		TEXT("録画モード"),		LVCFMT_LEFT,	12},
			{COLUMN_PRIORITY,		TEXT("Priority"),		TEXT("優先度"),			LVCFMT_LEFT,	4},
		};
		InitColumnList(ColumnList, _countof(ColumnList));

		m_SortColumn=COLUMN_TIME;

		m_ColorList[COLOR_DISABLED]=RGB(128,128,128);
		m_ColorList[COLOR_NOTUNER]=RGB(255,64,0);
		m_ColorList[COLOR_CONFLICT]=RGB(255,192,0);
	}


	CReserveListView::~CReserveListView()
	{
		Destroy();
	}


	void CReserveListView::SetReserveList(std::vector<RESERVE_DATA> &ReserveList)
	{
		if (ReserveList.size()==0) {
			DeleteAllItems();
			return;
		}

		std::vector<bool> ExistsFlags;
		ExistsFlags.resize(ReserveList.size());
		int ItemCount=GetItemCount();

		for (int i=0;i<ItemCount;) {
			CReserveListItem *pItem=dynamic_cast<CReserveListItem*>(GetItem(i));
			bool fFound=false;

			for (size_t j=0;j<ReserveList.size();j++) {
				RESERVE_DATA &ReserveData=ReserveList[j];

				if (!ExistsFlags[j]
						&& ReserveData.reserveID==pItem->GetReserveID()) {
					pItem->SetReserveData(&ReserveData);
					fFound=true;
					ExistsFlags[j]=true;
					break;
				}
			}
			if (fFound) {
				UpdateItem(i);
				i++;
			} else {
				DeleteItem(i);
				ItemCount--;
			}
		}

		for (size_t i=0;i<ReserveList.size();i++) {
			if (!ExistsFlags[i]) {
				AddItem(new CReserveListItem(&ReserveList[i]));
			}
		}

		SortItems();
	}


	RESERVE_DATA *CReserveListView::GetItemReserveData(int Index)
	{
		CReserveListItem *pItem=dynamic_cast<CReserveListItem*>(GetItem(Index));

		if (pItem==NULL)
			return NULL;
		return pItem->GetReserveData();
	}


	bool CReserveListView::SetColor(int Type,COLORREF Color)
	{
		if (Type<0 || Type>=NUM_COLORS)
			return false;
		if (m_ColorList[Type]!=Color) {
			m_ColorList[Type]=Color;
			if (m_hwnd!=NULL)
				::InvalidateRect(m_hwnd,NULL,TRUE);
		}
		return true;
	}


	LRESULT CReserveListView::OnCustomDraw(LPNMLVCUSTOMDRAW pnmlvcd)
	{
		switch (pnmlvcd->nmcd.dwDrawStage) {
		case CDDS_PREPAINT:
			return CDRF_NOTIFYITEMDRAW;

		case CDDS_ITEMPREPAINT:
			return CDRF_NOTIFYSUBITEMDRAW;

		case CDDS_ITEMPREPAINT | CDDS_SUBITEM:
			pnmlvcd->clrText=ListView_GetTextColor(m_hwnd);
			pnmlvcd->clrTextBk=ListView_GetTextBkColor(m_hwnd);
			if (m_ColumnList[pnmlvcd->iSubItem]->GetID()==COLUMN_STATUS) {
				const CReserveListItem *pItem=
					reinterpret_cast<const CReserveListItem*>(pnmlvcd->nmcd.lItemlParam);
				const RESERVE_DATA *pReserveData=pItem->GetReserveData();
				COLORREF BackColor;

				if (pReserveData->recSetting.recMode==RECMODE_NO) {
					BackColor=m_ColorList[COLOR_DISABLED];
				} else if (pReserveData->overlapMode==1) {
					BackColor=m_ColorList[COLOR_CONFLICT];
				} else if (pReserveData->overlapMode==2) {
					BackColor=m_ColorList[COLOR_NOTUNER];
				} else {
					break;
				}
				if (RGBIntensity(BackColor)<160)
					pnmlvcd->clrText=RGB(255,255,255);
				else
					pnmlvcd->clrText=RGB(0,0,0);
				pnmlvcd->clrTextBk=BackColor;
			}
			break;
		}

		return 0;
	}




	CReserveListView::CReserveListItem::CReserveListItem(RESERVE_DATA *pReserveData)
		: m_pReserveData(pReserveData)
		, m_ReserveID(pReserveData->reserveID)
	{
	}


	bool CReserveListView::CReserveListItem::GetText(int ID,LPTSTR pszText,int MaxLength) const
	{
		pszText[0]=_T('\0');

		switch (ID) {
		case COLUMN_TITLE:
			if (!m_pReserveData->title.empty())
				::lstrcpyn(pszText,m_pReserveData->title.c_str(),MaxLength);
			break;

		case COLUMN_TIME:
			{
				SYSTEMTIME EndTime;
				TCHAR szStartTime[64],szEndTime[64];

				GetEndTime(m_pReserveData->startTime,m_pReserveData->durationSecond,&EndTime);
				FormatSystemTime(m_pReserveData->startTime,szStartTime, _countof(szStartTime),
								 SYSTEMTIME_FORMAT_TIME | SYSTEMTIME_FORMAT_SECONDS);
				FormatSystemTime(EndTime,szEndTime, _countof(szEndTime),
								 SYSTEMTIME_FORMAT_TIMEONLY | SYSTEMTIME_FORMAT_SECONDS);
				FormatString(pszText,MaxLength,TEXT("%s ～ %s"),szStartTime,szEndTime);
			}
			break;

		case COLUMN_SERVICE:
			if (!m_pReserveData->stationName.empty())
				::lstrcpyn(pszText,m_pReserveData->stationName.c_str(),MaxLength);
			break;

		case COLUMN_STATUS:
			{
				LPCTSTR pszStatus=NULL;

				if (m_pReserveData->recSetting.recMode==RECMODE_NO) {
					pszStatus=TEXT("無効");
				} else {
					switch (m_pReserveData->overlapMode) {
					case 0:	pszStatus=TEXT("正常");				break;
					case 1:	pszStatus=TEXT("一部実行");			break;
					case 2:	pszStatus=TEXT("チューナー不足");	break;
					}
				}
				if (pszStatus!=NULL)
					::lstrcpyn(pszText,pszStatus,MaxLength);
			}
			break;

		case COLUMN_RECMODE:
			{
				LPCTSTR pszRecMode;

				switch (m_pReserveData->recSetting.recMode) {
				case RECMODE_ALL:
					pszRecMode=TEXT("全サービス");
					break;
				case RECMODE_SERVICE:
					pszRecMode=TEXT("指定サービス");
					break;
				case RECMODE_ALL_NOB25:
					pszRecMode=TEXT("全サービス(スクランブル解除なし)");
					break;
				case RECMODE_SERVICE_NOB25:
					pszRecMode=TEXT("指定サービス(スクランブル解除なし)");
					break;
				case RECMODE_VIEW:
					pszRecMode=TEXT("視聴");
					break;
				case RECMODE_NO:
					pszRecMode=TEXT("無効");
					break;
				case RECMODE_EPG:
					pszRecMode=TEXT("EPG取得");
					break;
				default:
					pszRecMode=NULL;
				}
				if (pszRecMode!=NULL)
					::lstrcpyn(pszText,pszRecMode,MaxLength);
			}
			break;

		case COLUMN_PRIORITY:
			FormatInt(m_pReserveData->recSetting.priority,pszText,MaxLength);
			break;

		default:
			return false;
		}

		return true;
	}


	int CReserveListView::CReserveListItem::Compare(int ID,const CItem *pItem) const
	{
		const CReserveListItem *pReserveItem=static_cast<const CReserveListItem*>(pItem);
		int Cmp=0;

		switch (ID) {
		case COLUMN_TITLE:
			//Cmp=m_pReserveData->title.compare(pReserveItem->m_pReserveData->title);
			Cmp=::lstrcmpW(m_pReserveData->title.c_str(),
						   pReserveItem->m_pReserveData->title.c_str());
			break;

		case COLUMN_TIME:
			Cmp=CompareSystemTime(m_pReserveData->startTime,
								  pReserveItem->m_pReserveData->startTime);
			if (Cmp==0)
				Cmp=CompareValue(m_pReserveData->durationSecond,
								 pReserveItem->m_pReserveData->durationSecond);
			break;

		case COLUMN_SERVICE:
			//Cmp=m_pReserveData->stationName.compare(pReserveItem->m_pReserveData->stationName);
			Cmp=::lstrcmpW(m_pReserveData->stationName.c_str(),
						   pReserveItem->m_pReserveData->stationName.c_str());
			break;

		case COLUMN_STATUS:
			if (m_pReserveData->recSetting.recMode==RECMODE_NO) {
				if (pReserveItem->m_pReserveData->recSetting.recMode==RECMODE_NO)
					Cmp=0;
				else
					Cmp=1;
			} else {
				if (pReserveItem->m_pReserveData->recSetting.recMode==RECMODE_NO)
					Cmp=-1;
				else
					Cmp=CompareValue(m_pReserveData->overlapMode,
									 pReserveItem->m_pReserveData->overlapMode);
			}
			break;

		case COLUMN_RECMODE:
			Cmp=CompareValue(m_pReserveData->recSetting.recMode,
							 pReserveItem->m_pReserveData->recSetting.recMode);
			break;

		case COLUMN_PRIORITY:
			Cmp=CompareValue(m_pReserveData->recSetting.priority,
							 pReserveItem->m_pReserveData->recSetting.priority);
			break;
		}

		if (Cmp==0) {
			if (ID!=COLUMN_TIME) {
				Cmp=CompareSystemTime(m_pReserveData->startTime,
									  pReserveItem->m_pReserveData->startTime);
				if (Cmp==0)
					Cmp=CompareValue(m_pReserveData->durationSecond,
									 pReserveItem->m_pReserveData->durationSecond);
			}
			if (Cmp==0)
				Cmp=CompareValue(m_pReserveData->reserveID,
								 pReserveItem->m_pReserveData->reserveID);
		}

		return Cmp;
	}

}
