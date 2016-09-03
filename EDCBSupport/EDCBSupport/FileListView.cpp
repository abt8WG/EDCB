#include "stdafx.h"
#include "EDCBSupport.h"
#include "FileListView.h"


namespace EDCBSupport
{

	CFileListView::CFileListView()
	{
		static const ColumnInfo ColumnList[] = {
			{COLUMN_TIME,		TEXT("Time"),		TEXT("日時"),		LVCFMT_LEFT,	20},
			{COLUMN_SERVICE,	TEXT("Service"),	TEXT("サービス名"),	LVCFMT_LEFT,	16},
			{COLUMN_TITLE,		TEXT("Title"),		TEXT("番組名"),		LVCFMT_LEFT,	20},
			{COLUMN_RESULT,		TEXT("Result"),		TEXT("結果"),		LVCFMT_LEFT,	12},
			{COLUMN_FILE,		TEXT("File"),		TEXT("ファイル"),	LVCFMT_LEFT,	32},
		};
		InitColumnList(ColumnList,_countof(ColumnList));

		m_SortColumn=COLUMN_TIME;

		m_ColorList[COLOR_ERROR]=RGB(255,192,0);
	}


	CFileListView::~CFileListView()
	{
		Destroy();
	}


	void CFileListView::SetRecFileList(const std::vector<REC_FILE_INFO> &RecFileList)
	{
		if (RecFileList.size()==0) {
			DeleteAllItems();
			return;
		}

		std::vector<bool> ExistsFlags;
		ExistsFlags.resize(RecFileList.size());
		int ItemCount=GetItemCount();

		for (int i=0;i<ItemCount;) {
			CFileListItem *pItem=dynamic_cast<CFileListItem*>(GetItem(i));
			bool fFound=false;

			for (size_t j=0;j<RecFileList.size();j++) {
				const REC_FILE_INFO &RecFileInfo=RecFileList[j];

				if (!ExistsFlags[j]
						&& RecFileInfo.id==pItem->GetRecFileInfo()->id) {
					pItem->SetRecFileInfo(RecFileInfo);
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

		for (size_t i=0;i<RecFileList.size();i++) {
			if (!ExistsFlags[i]) {
				AddItem(new CFileListItem(RecFileList[i]));
			}
		}

		SortItems();
	}


	REC_FILE_INFO *CFileListView::GetItemRecFileInfo(int Index)
	{
		CFileListItem *pItem=dynamic_cast<CFileListItem*>(GetItem(Index));

		if (pItem==NULL)
			return NULL;
		return pItem->GetRecFileInfo();
	}


	bool CFileListView::SetColor(int Type,COLORREF Color)
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


	LRESULT CFileListView::OnCustomDraw(LPNMLVCUSTOMDRAW pnmlvcd)
	{
		switch (pnmlvcd->nmcd.dwDrawStage) {
		case CDDS_PREPAINT:
			return CDRF_NOTIFYITEMDRAW;

		case CDDS_ITEMPREPAINT:
			return CDRF_NOTIFYSUBITEMDRAW;

		case CDDS_ITEMPREPAINT | CDDS_SUBITEM:
			pnmlvcd->clrText=ListView_GetTextColor(m_hwnd);
			pnmlvcd->clrTextBk=ListView_GetTextBkColor(m_hwnd);
			if (m_ColumnList[pnmlvcd->iSubItem]->GetID()==COLUMN_RESULT) {
				CFileListItem *pItem=
					reinterpret_cast<CFileListItem*>(pnmlvcd->nmcd.lItemlParam);
				const REC_FILE_INFO *pRecFileInfo=pItem->GetRecFileInfo();
				COLORREF BackColor;

				if ((pRecFileInfo->recStatus &
						(REC_STATUS_ERR_WAKE | REC_STATUS_ERR_OPEN | REC_STATUS_ERR_FIND))!=0) {
					BackColor=m_ColorList[COLOR_ERROR];
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




	CFileListView::CFileListItem::CFileListItem(const REC_FILE_INFO &RecFileInfo)
		: m_RecFileInfo(RecFileInfo)
	{
	}


	bool CFileListView::CFileListItem::GetText(int ID,LPTSTR pszText,int MaxLength) const
	{
		pszText[0]=_T('\0');

		switch (ID) {
		case COLUMN_TIME:
			{
				SYSTEMTIME EndTime;
				TCHAR szStartTime[64],szEndTime[64];

				GetEndTime(m_RecFileInfo.startTime,m_RecFileInfo.durationSecond,&EndTime);
				FormatSystemTime(m_RecFileInfo.startTime,szStartTime,_countof(szStartTime),
								 SYSTEMTIME_FORMAT_TIME | SYSTEMTIME_FORMAT_SECONDS);
				FormatSystemTime(EndTime,szEndTime,_countof(szEndTime),
								 SYSTEMTIME_FORMAT_TIMEONLY | SYSTEMTIME_FORMAT_SECONDS);
				FormatString(pszText,MaxLength,TEXT("%s ～ %s"),szStartTime,szEndTime);
			}
			break;

		case COLUMN_TITLE:
			if (!m_RecFileInfo.title.empty())
				::lstrcpyn(pszText,m_RecFileInfo.title.c_str(),MaxLength);
			break;

		case COLUMN_SERVICE:
			if (!m_RecFileInfo.serviceName.empty())
				::lstrcpyn(pszText,m_RecFileInfo.serviceName.c_str(),MaxLength);
			break;

		case COLUMN_RESULT:
			if (!m_RecFileInfo.comment.empty()) {
				::lstrcpyn(pszText,m_RecFileInfo.comment.c_str(),MaxLength);
			} else {
				LPCTSTR pszStatus=NULL;

				if ((m_RecFileInfo.recStatus&REC_STATUS_END)!=0) {
					pszStatus=TEXT("正常終了");
				}
				if ((m_RecFileInfo.recStatus&REC_STATUS_ERR_WAKE)!=0) {
					pszStatus=TEXT("録画時刻超過エラー");
				} else if ((m_RecFileInfo.recStatus&REC_STATUS_ERR_OPEN)!=0) {
					pszStatus=TEXT("起動エラー");
				} else if ((m_RecFileInfo.recStatus&REC_STATUS_ERR_FIND)!=0) {
					pszStatus=TEXT("EPG情報エラー");
				} else if ((m_RecFileInfo.recStatus&REC_STATUS_END_PG)!=0) {
					pszStatus=TEXT("プログラム録画に変更して終了");
				} else if ((m_RecFileInfo.recStatus&REC_STATUS_CHG_TIME)!=0) {
					pszStatus=TEXT("開始時刻を変更して録画終了");
				}
				/*
				else if ((m_RecFileInfo.recStatus&REC_STATUS_RELAY)!=0) {
					pszStatus=TEXT("イベントリレー");
				}
				*/
				if (pszStatus!=NULL)
					::lstrcpyn(pszText,pszStatus,MaxLength);
			}
			break;

		case COLUMN_FILE:
			if (!m_RecFileInfo.recFilePath.empty())
				::lstrcpyn(pszText,m_RecFileInfo.recFilePath.c_str(),MaxLength);
			break;

		default:
			return false;
		}

		return true;
	}


	int CFileListView::CFileListItem::Compare(int ID,const CItem *pItem) const
	{
		const CFileListItem *pFileItem=static_cast<const CFileListItem*>(pItem);
		int Cmp=0;

		switch (ID) {
		case COLUMN_TIME:
			Cmp=CompareSystemTime(m_RecFileInfo.startTime,
								  pFileItem->m_RecFileInfo.startTime);
			if (Cmp==0)
				CompareValue(m_RecFileInfo.durationSecond,
							 pFileItem->m_RecFileInfo.durationSecond);
			break;

		case COLUMN_TITLE:
			//Cmp=m_RecFileInfo.title.compare(pFileItem->m_RecFileInfo.title);
			Cmp=::lstrcmpW(m_RecFileInfo.title.c_str(),
						   pFileItem->m_RecFileInfo.title.c_str());
			break;

		case COLUMN_SERVICE:
			//Cmp=m_RecFileInfo.serviceName.compare(pFileItem->m_RecFileInfo.serviceName);
			Cmp=::lstrcmpW(m_RecFileInfo.serviceName.c_str(),
						   pFileItem->m_RecFileInfo.serviceName.c_str());
			break;

		case COLUMN_RESULT:
			Cmp=CompareValue(m_RecFileInfo.recStatus,
							 pFileItem->m_RecFileInfo.recStatus);
			break;

		case COLUMN_FILE:
			/*
			Cmp=::lstrcmpiW(m_RecFileInfo.recFilePath.c_str(),
							pFileItem->m_RecFileInfo.recFilePath.c_str());
			*/
			Cmp=::StrCmpLogicalW(m_RecFileInfo.recFilePath.c_str(),
								 pFileItem->m_RecFileInfo.recFilePath.c_str());
			break;
		}

		if (Cmp==0) {
			if (ID!=COLUMN_TIME) {
				Cmp=CompareSystemTime(m_RecFileInfo.startTime,
									  pFileItem->m_RecFileInfo.startTime);
				if (Cmp==0)
					CompareValue(m_RecFileInfo.durationSecond,
								 pFileItem->m_RecFileInfo.durationSecond);
			}
			if (Cmp==0)
				Cmp=CompareValue(m_RecFileInfo.id,
								 pFileItem->m_RecFileInfo.id);
		}

		return Cmp;
	}

}
