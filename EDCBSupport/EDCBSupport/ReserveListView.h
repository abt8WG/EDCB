#pragma once

#include "ListView.h"


namespace EDCBSupport
{

	class CReserveListView : public CListView
	{
	public:
		enum {
			COLUMN_TIME,
			COLUMN_TITLE,
			COLUMN_SERVICE,
			COLUMN_STATUS,
			COLUMN_RECMODE,
			COLUMN_PRIORITY,
			COLUMN_SERVICEMODE
		};

		class CReserveListItem : public CListView::CItem
		{
		public:
			CReserveListItem(RESERVE_DATA *pReserveData);
			bool GetText(int ID,LPTSTR pszText,int MaxLength) const override;
			int Compare(int ID,const CItem *pItem) const override;
			RESERVE_DATA *GetReserveData() const { return m_pReserveData; }
			void SetReserveData(RESERVE_DATA *pReserveData) { m_pReserveData=pReserveData; }
			const DWORD GetReserveID() const { return m_ReserveID; }

		private:
			RESERVE_DATA *m_pReserveData;
			DWORD m_ReserveID;
		};

		enum {
			COLOR_DISABLED,
			COLOR_CONFLICT,
			COLOR_NOTUNER,
			NUM_COLORS
		};

		CReserveListView();
		~CReserveListView();
		void SetReserveList(std::vector<RESERVE_DATA> &ReserveList);
		RESERVE_DATA *GetItemReserveData(int Index);
		bool SetColor(int Type,COLORREF Color);

	private:
		COLORREF m_ColorList[NUM_COLORS];

		LRESULT OnCustomDraw(LPNMLVCUSTOMDRAW pnmlvcd) override;
	};

}
