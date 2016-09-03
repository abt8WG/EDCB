#pragma once

#include "ListView.h"


namespace EDCBSupport
{

	// 録画済みファイルリスト
	class CFileListView : public CListView
	{
	public:
		enum {
			COLUMN_TIME,
			COLUMN_TITLE,
			COLUMN_SERVICE,
			COLUMN_RESULT,
			COLUMN_FILE
		};

		class CFileListItem : public CListView::CItem
		{
		public:
			CFileListItem(const REC_FILE_INFO &RecFileInfo);
			bool GetText(int ID,LPTSTR pszText,int MaxLength) const override;
			int Compare(int ID,const CItem *pItem) const override;
			REC_FILE_INFO *GetRecFileInfo() { return &m_RecFileInfo; }
			void SetRecFileInfo(const REC_FILE_INFO &RecFileInfo) { m_RecFileInfo=RecFileInfo; }

		private:
			REC_FILE_INFO m_RecFileInfo;
		};

		enum {
			COLOR_ERROR,
			NUM_COLORS
		};

		CFileListView();
		~CFileListView();
		void SetRecFileList(const std::vector<REC_FILE_INFO> &RecFileList);
		REC_FILE_INFO *GetItemRecFileInfo(int Index);
		bool SetColor(int Type,COLORREF Color);

	private:
		COLORREF m_ColorList[NUM_COLORS];

		LRESULT OnCustomDraw(LPNMLVCUSTOMDRAW pnmlvcd) override;
	};

}
