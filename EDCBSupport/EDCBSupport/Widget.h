#pragma once


namespace EDCBSupport
{

	class CWidget abstract
	{
	public:
		CWidget();
		virtual ~CWidget();
		virtual bool Create(HWND hwndParent,INT_PTR ID=0) = 0;
		void Destroy();
		bool IsCreated() const { return m_hwnd!=NULL; }
		void SetPosition(int Left,int Top,int Width,int Height);
		void SetPosition(const RECT &Position);
		bool GetPosition(RECT *pPosition) const;
		int GetWidth() const;
		int GetHeight() const;
		void SetVisible(bool fVisible);

	protected:
		HWND m_hwnd;

		virtual void OnDestroy() {}
	};

}
