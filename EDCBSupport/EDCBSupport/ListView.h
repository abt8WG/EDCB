#pragma once

#include "Widget.h"


namespace EDCBSupport
{

	// リスト表示クラス
	class CListView : public CWidget
	{
	public:
		class CColumn abstract
		{
		public:
			CColumn();
			virtual ~CColumn() {}
			virtual int GetID() const = 0;
			virtual LPCTSTR GetName() const = 0;
			virtual void GetText(LPTSTR pszText,int MaxLength) const = 0;
			virtual int GetFormat() const { return LVCFMT_LEFT; }
			int GetWidth() const { return m_Width; }
			void SetWidth(int Width) { m_Width=Width; }
			int GetOrder() const { return m_Order; }
			void SetOrder(int Order) { m_Order=Order; }

		protected:
			int m_Width;
			int m_Order;
		};

		struct ColumnInfo
		{
			int m_ID;
			LPCTSTR m_pszName;
			LPCTSTR m_pszText;
			int m_Format;
			int m_DefaultWidth;
		};

		class CBasicColumn : public CColumn, protected ColumnInfo
		{
		public:
			CBasicColumn(const ColumnInfo &Info);
			virtual ~CBasicColumn() {}
			int GetID() const override { return m_ID; }
			LPCTSTR GetName() const override { return m_pszName; }
			void GetText(LPTSTR pszText,int MaxLength) const override
				{ ::lstrcpyn(pszText,m_pszText,MaxLength); }
			int GetFormat() const override { return m_Format; }
		};

		class CItem abstract
		{
		public:
			virtual ~CItem() {}
			virtual bool GetText(int ID,LPTSTR pszText,int MaxLength) const = 0;
			virtual int Compare(int ID,const CItem *pItem) const = 0;
		};

		class CEventHandler abstract
		{
		public:
			virtual ~CEventHandler() {}
			virtual void OnRClick(CListView *pListView) {}
			virtual void OnDoubleClick(CListView *pListView,int Item) {}
			virtual void OnKeyDown(CListView *pListView,UINT Key) {}
		};

		CListView();
		virtual ~CListView();

		bool Create(HWND hwndParent,INT_PTR ID=0) override;

		int GetItemCount() const;
		int GetSelectedItemCount() const;
		int GetSelectedItem() const;
		bool GetSelectedItems(std::vector<int> *pItemList) const;
		bool SetSortColumn(int ColumnID,bool fAscending=true);
		int NumColumns() const;
		const CColumn *GetColumnByIndex(int Index) const;
		const CColumn *GetColumnByID(int ColumnID) const;
		int GetColumnWidth(int ColumnID) const;
		bool SetColumnWidth(int ColumnID,int Width);
		int GetColumnOrder(int ColumnID) const;
		bool SetColumnOrder(int ColumnID,int Order);
		void SetEventHandler(CEventHandler *pEventHandler) { m_pEventHandler=pEventHandler; }
		bool OnNotify(WPARAM wParam,LPARAM lParam,LRESULT *pResult);

	protected:
		std::vector<CColumn*> m_ColumnList;
		int m_SortColumn;
		bool m_fSortAscending;
		CEventHandler *m_pEventHandler;

		void OnDestroy() override;

		void InitColumnList(const ColumnInfo *pColumnList,size_t NumColumns);
		void CreateColumns();
		int GetColumnIndex(int ColumnID) const;
		bool InsertItem(int Index,CItem *pItem);
		bool AddItem(CItem *pItem) { return InsertItem(-1,pItem); }
		CItem *GetItem(int Index);
		const CItem *GetItem(int Index) const;
		bool UpdateItem(int Index) const;
		bool DeleteItem(int Index);
		void DeleteAllItems();
		void ReserveItemCount(int Items);
		void SortItems();
		static int CALLBACK ItemCompareFunc(LPARAM lParam1,LPARAM lParam2,LPARAM lParamSort);
		void SetColumnSortMark(int Index,bool fSet);

		virtual LRESULT OnCustomDraw(LPNMLVCUSTOMDRAW pnmcd) { return CDRF_DODEFAULT; }
	};

}
