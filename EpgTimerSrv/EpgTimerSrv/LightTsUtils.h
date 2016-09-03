// 軽量TSパーサ
// TvTestのコードを流用しているためこのコードのライセンスはGPLです。
//
#pragma once

#include <Windows.h>

#include "../../Common/StructDef.h"
#include "../../Common/StringUtil.h"

#if _MSC_VER < 1700
#define noexcept
#endif

#define BIT_SHIFT_MASK(value, shift, mask) (((value) >> (shift)) & ((1<<(mask))-1))

/////////////////////////////////////////////////////////////////////////////
// バッファ操作ユーティリティ
/////////////////////////////////////////////////////////////////////////////

class CMediaData
{
public:
	CMediaData();
	CMediaData(const DWORD dwBuffSize);
	CMediaData(const BYTE *pData, const DWORD dwDataSize);
	CMediaData(const BYTE byFiller, const DWORD dwDataSize);

	CMediaData(const CMediaData &Operand) : m_pData(NULL) { *this = Operand; }
	CMediaData(CMediaData &&Operand) noexcept : m_pData(NULL) { *this = std::move(Operand); }
	CMediaData & operator = (const CMediaData &Operand);
	CMediaData & operator = (CMediaData &&Operand) noexcept;
	virtual ~CMediaData() { free(m_pData); }

	CMediaData & operator += (const CMediaData &Operand);

	BYTE *GetData() { return m_dwDataSize > 0 ? m_pData : NULL; }
	const BYTE *GetData() const { return m_dwDataSize > 0 ? m_pData : NULL; }
	DWORD GetSize() const { return m_dwDataSize; }

	void SetAt(const DWORD dwPos, const BYTE byData);
	BYTE GetAt(const DWORD dwPos) const;

	DWORD SetData(const BYTE *pData, const DWORD dwDataSize);
	DWORD AddData(const BYTE *pData, const DWORD dwDataSize);
	DWORD AddData(const CMediaData& data);
	DWORD AddByte(const BYTE byData);
	template <typename T> DWORD Add(T data) { return AddData((BYTE*)&data, sizeof(T)); }
	DWORD TrimHead(const DWORD dwTrimSize = 1UL);
	DWORD TrimTail(const DWORD dwTrimSize = 1UL);

	DWORD Reserve(const DWORD dwGetSize);

	DWORD SetSize(const DWORD dwSetSize);
	DWORD SetSize(const DWORD dwSetSize, const BYTE byFiller);

	void ClearSize(void);
	void ClearBuffer(void);

protected:
	DWORD m_dwDataSize;
	DWORD m_dwBuffSize;
	BYTE *m_pData;
};

template <typename T>
bool ReadFromMemory(T* data, BYTE*& cur, const BYTE* end) {
	int size = sizeof(T);
	if (cur + size > end) {
		return false;
	}
	*data = *(T*)cur;
	cur += size;
	return true;
}

/////////////////////////////////////////////////////////////////////////////
// ARIB STD-B10 Part2 Annex C MJD+JTC 処理クラス
/////////////////////////////////////////////////////////////////////////////

class CAribTime
{
public:
	static const bool AribToSystemTime(const BYTE *pHexData, SYSTEMTIME *pSysTime);
	static const bool AribToInt64(const BYTE *pHexData, __int64* pTime);
	static const bool AribMjdToInt64(const BYTE *pHexData, __int64* pTime);
	static void SplitAribMjd(const WORD wAribMjd, WORD *pwYear, WORD *pwMonth, WORD *pwDay, WORD *pwDayOfWeek = NULL);
	static void SplitAribBcd(const BYTE *pAribBcd, WORD *pwHour, WORD *pwMinute, WORD *pwSecond = NULL);
	static const DWORD AribBcdToSecond(const BYTE *pAribBcd);
};

/////////////////////////////////////////////////////////////////////////////
// TS Stream 各種パラメータ
/////////////////////////////////////////////////////////////////////////////

typedef struct ProgramTable
{
	int Version;
	int TSID;
} ProgramTable;


DWORD inline ReadBigEndian(DWORD big)
{
	return ((big & 0xFF000000) >> 24) |
		((big & 0x00FF0000) >> 8) |
		((big & 0x0000FF00) << 8) |
		((big & 0x000000FF) << 24);
}

DWORD inline Read24(const BYTE* pPos)
{
	return ((DWORD)pPos[0] << 16) | ((DWORD)pPos[1] << 8) | (DWORD)pPos[0];
}

template <int bits>
WORD inline Readin16(const BYTE* pPos)
{
	return (((WORD)pPos[0] << 8) | (WORD)pPos[1]) & ((1 << bits) - 1);
}

template <>
WORD inline Readin16<16>(const BYTE* pPos)
{
	return (((WORD)pPos[0] << 8) | (WORD)pPos[1]);
}

/*
コンパイラの最適化により
1.  (ptr[0] >> 4)*100000 + (ptr[0] & 0x0F)*10000 +
(ptr[1] >> 4)*1000 + (ptr[1] & 0x0F)*100 +
(ptr[2] >> 4)*10 + (ptr[2] & 0x0F)*1;
2. ReadBcd<6>(ptr);
上記2つは全く同じ命令が生成される
*/
template <int symbols>
DWORD inline ReadBcd(const BYTE* ptr, DWORD dwNum = 0)
{
	return ReadBcd<symbols - 2>(ptr + 1, dwNum * 100 + (ptr[0] >> 4) * 10 + (ptr[0] & 0x0F));
}

template <>
DWORD inline ReadBcd<2>(const BYTE* ptr, DWORD dwNum)
{
	return dwNum * 100 + (ptr[0] >> 4) * 10 + (ptr[0] & 0x0F);
}

template <>
DWORD inline ReadBcd<1>(const BYTE* ptr, DWORD dwNum)
{
	return dwNum * 10 + (ptr[0] >> 4);
}

namespace TSPARAM
{
	enum TSStreamParameter {
		PID_COUNT = 0x2000,
		PID_EMPTY = 0x1FFF,
		TS_PACKETSIZE = 188,
		SHREAD_GROUP_MAX = 4,
	};
}

/////////////////////////////////////////////////////////////////////////////
// CRC計算クラス
/////////////////////////////////////////////////////////////////////////////

class CCrcCalculator
{
public:
	static WORD CalcCrc16(const BYTE *pData, DWORD DataSize, WORD wCurCrc = 0xFFFF);
	static DWORD CalcCrc32(const BYTE *pData, DWORD DataSize, DWORD dwCurCrc = 0xFFFFFFFFUL);
};

/////////////////////////////////////////////////////////////////////////////
// TSパケット抽象化クラス
/////////////////////////////////////////////////////////////////////////////

class CTsPacket
{
public:
	CTsPacket();
	CTsPacket(const BYTE *pHexData, DWORD* pdwRet = NULL);

	enum	// ParsePacket() エラーコード
	{
		EC_VALID = 0x00000000UL,		// 正常パケット
		EC_FORMAT = 0x00000001UL,		// フォーマットエラー
		EC_TRANSPORT = 0x00000002UL,		// トランスポートエラー(ビットエラー)
		EC_CONTINUITY = 0x00000003UL		// 連続性カウンタエラー(ドロップ)
	};
	DWORD ParsePacket(BYTE *pContinuityCounter = NULL);

	BYTE* GetData() { return m_pData; }
	DWORD GetSize() { return TSPARAM::TS_PACKETSIZE; }

	// よく使うものだけメンバ変数にする

	// ヘッダー
	BYTE GetSyncByte() const { return m_pData[0]; }
	bool GetTransportErrorIndicator() const { return (m_pData[1] & 0x80U) != 0; }
	bool GetPayloadUnitStartIndicator() const { return (m_pData[1] & 0x40U) != 0; }
	bool GetTransportPriority() const { return (m_pData[1] & 0x20U) != 0; }
	WORD GetPID() const { return ((WORD)(m_pData[1] & 0x1F) << 8) | (WORD)m_pData[2]; }
	BYTE GetTransportScramblingCtrl() const { return (m_pData[3] & 0xC0U) >> 6; }
	BYTE GetAdaptationFieldCtrl() const { return (m_pData[3] & 0x30U) >> 4; }
	BYTE GetContinuityCounter() const { return m_pData[3] & 0x0FU; }

	// アダプテーションフィールド
	BYTE GetAdaptationFieldLength() const { return m_pData[4]; }
	bool GetDiscontinuityIndicator() const { return (m_pData[5] & 0x80U) != 0; }
	bool GetRamdomAccessIndicator() const { return (m_pData[5] & 0x40U) != 0; }
	bool GetEsPriorityIndicator() const { return (m_pData[5] & 0x20U) != 0; }
	bool GetPcrFlag() const { return (m_pData[5] & 0x10U) != 0; }
	bool GetOpcrFlag() const { return (m_pData[5] & 0x08U) != 0; }
	bool GetSplicingPointFlag() const { return (m_pData[5] & 0x04U) != 0; }
	bool GetTransportPrivateDataFlag() const { return(m_pData[5] & 0x02U) != 0; }
	bool GetAdaptationFieldExtFlag() const { return (m_pData[5] & 0x01U) != 0; }

	// ヘルパ
	BYTE * GetPayloadData(void);
	const BYTE * GetPayloadData(void) const;
	const BYTE GetPayloadSize(void) const;

	bool HaveAdaptationField(void) const { return (((m_pData[3] & 0x30U) >> 4) & 0x02U) != 0; }
	bool HavePayload(void) const { return (((m_pData[3] & 0x30U) >> 4) & 0x01U) != 0; }
	bool IsScrambled(void) const { return (((m_pData[3] & 0xC0U) >> 6) & 0x02U) != 0; }
	/*
	struct TAG_TSPACKETHEADER {
	BYTE bySyncByte;					// Sync Byte
	bool bTransportErrorIndicator;		// Transport Error Indicator
	bool bPayloadUnitStartIndicator;	// Payload Unit Start Indicator
	bool TransportPriority;				// Transport Priority
	WORD wPID;							// PID
	BYTE byTransportScramblingCtrl;		// Transport Scrambling Control
	BYTE byAdaptationFieldCtrl;			// Adaptation Field Control
	BYTE byContinuityCounter;			// Continuity Counter
	} m_Header;

	struct TAG_ADAPTFIELDHEADER {
	BYTE byAdaptationFieldLength;		// Adaptation Field Length
	bool bDiscontinuityIndicator;		// Discontinuity Indicator
	bool bRamdomAccessIndicator;		// Random Access Indicator
	bool bEsPriorityIndicator;			// Elementary Stream Priority Indicator
	bool bPcrFlag;						// PCR Flag
	bool bOpcrFlag;						// OPCR Flag
	bool bSplicingPointFlag;			// Splicing Point Flag
	bool bTransportPrivateDataFlag;		// Transport Private Data Flag
	bool bAdaptationFieldExtFlag;		// Adaptation Field Extension Flag
	const BYTE *pOptionData;			// オプションフィールドデータ
	BYTE byOptionSize;					// オプションフィールド長
	} m_AdaptationField;*/

protected:
	BYTE m_pData[TSPARAM::TS_PACKETSIZE];
};

/////////////////////////////////////////////////////////////////////////////
// PSIセクションクラス
/////////////////////////////////////////////////////////////////////////////

#pragma warning(push)
#pragma warning(disable:4127) // 条件式が定数です。

template<bool m_bTargetExt, bool m_bTR>
class CSiSection
{
public:
	CSiSection(CMediaData* Sec_)
		: m_pData(Sec_->GetData())
		, m_dwDataSize(Sec_->GetSize())
	{
	}

	CSiSection(const BYTE* pData, DWORD dwDataSize)
		: m_pData(pData)
		, m_dwDataSize(dwDataSize)
	{
	}

	bool CheckHeader();

	const BYTE * GetPayloadData(void) const
	{
		const DWORD dwHeaderSize = (m_bTargetExt) ? 8UL : 3UL;
		return m_dwDataSize > dwHeaderSize ? &m_pData[dwHeaderSize] : NULL;
	}
	WORD GetPayloadSize(void) const
	{
		// ペイロードサイズを返す(実際に保持してる　※セクション長より少なくなることもある)
		const DWORD dwHeaderSize = (m_bTargetExt) ? 8UL : 3UL;
		const WORD wSectionLength = GetSectionLength();

		if (m_dwDataSize <= dwHeaderSize)return 0U;
		else if (m_bTargetExt) {
			// 拡張セクション // CRCも入れたサイズを返すので、-5U（元は-9Uになっていた）
			return (m_dwDataSize >= (wSectionLength + 3UL)) ? (wSectionLength - 5U) : ((WORD)m_dwDataSize - 8U);
		}
		else {
			// 標準セクション
			return (m_dwDataSize >= (wSectionLength + 3UL)) ? wSectionLength : ((WORD)m_dwDataSize - 3U);
		}
	}

	BYTE GetTableID(void) const { return m_pData[0]; }
	bool IsExtendedSection(void) const { return BIT_SHIFT_MASK(m_pData[1], 7, 1); }
	bool IsPrivate(void) const { return BIT_SHIFT_MASK(m_pData[1], 6, 1); }
	WORD GetSectionLength(void) const { return Readin16<12>(&m_pData[1]); }
	WORD GetTableIdExtension(void) const { return Readin16<16>(&m_pData[3]); }
	BYTE GetVersion(void) const { return BIT_SHIFT_MASK(m_pData[5], 1, 5); }
	bool IsCurrentNext(void) const { return BIT_SHIFT_MASK(m_pData[5], 0, 1); }
	BYTE GetSectionNumber(void) const { return m_pData[6]; }
	BYTE GetLastSectionNumber(void) const { return m_pData[7]; }

	const BYTE* GetSectionEnd(void) const { return &m_pData[GetSectionLength() + 3]; }

	DWORD CalcCRC(void) const;

protected:
	const BYTE* m_pData;
	DWORD m_dwDataSize;
};

#pragma warning(pop)

/////////////////////////////////////////////////////////////////////////////
// PSIセクション抽出クラス
/////////////////////////////////////////////////////////////////////////////

class CSiSectionParser
{
public:
	typedef void(*PSISECTION_CALLBACK)
		(void* context, CSiSectionParser *pSiSectionParser, CMediaData *pSection);

	CSiSectionParser(PSISECTION_CALLBACK cbFunc = NULL, void* context = NULL, bool bAutoDelete = false);
	CSiSectionParser(const CSiSectionParser &Operand);
	CSiSectionParser & operator = (const CSiSectionParser &Operand);

	void SetHandler(PSISECTION_CALLBACK cbFunc, void* context);

	void Reset(void);

	const DWORD GetCrcErrorCount(void);

protected:
	const BYTE StorePayload(const BYTE *pPayload, const BYTE byRemain);
	/*
	const BYTE StartUnit(const BYTE *pPayload, const BYTE byRemain);
	const BYTE StoreUnit(const BYTE *pPayload, const BYTE byRemain);
	*/
	PSISECTION_CALLBACK m_cbFunc;
	void* m_Context;
	CMediaData m_SiSection;

	bool m_bIsStoring;
	WORD m_wStoreSize;
	DWORD m_dwCrcErrorCount;
};

// m_bTargetExt : ターゲットのsection_syntax_indicator
// section_syntax_indicatorはPIDごとに0 or 1が静的に判定可能なので
// m_bTR : ARIB TR-B14, TR-B15 に準拠するかどうか
template<bool m_bTargetExt, bool m_bTR> // 明示的なインスタンス化が必要
class CSiSectionParserImpl : public CSiSectionParser
{
public:
	CSiSectionParserImpl(PSISECTION_CALLBACK cbFunc = NULL, void* context = NULL, bool bAutoDelete = false);

	bool StorePacket(CTsPacket *pPacket);
private:
	const BYTE StoreHeader(const BYTE *pPayload, const BYTE byRemain);
};


namespace DESC_TAG
{
	enum TAG
	{
		COND_ACCESS_METHOD = 0x09, // 限定受信方式記述子
		x0D = 0x0D, // 著作権記述子
		x13 = 0x13, // カルーセル識別記述子
		x14 = 0x14, // アソシエーションタグ記述子
		x15 = 0x15, // 拡張アソシエーションタグ記述子
		x28 = 0x28, // AVC ビデオ記述子
		x2A = 0x2A, // AVC タイミングHRD 記述子
		x40 = 0x40, // ネットワーク名記述子
		SERVICE_LIST = 0x41, // サービスリスト記述子
		x42 = 0x42, // スタッフ記述子
		SATELITE_SYSTEM = 0x43, // 衛星分配システム記述子
		x44 = 0x44, // 有線分配システム記述子
		BOUQUET_NAME = 0x47, // ブーケ名記述子
		SERVICE = 0x48, // サービス記述子
		x49 = 0x49, // 国別受信可否記述子
		x4A = 0x4A, // リンク記述子
		x4B = 0x4B, // NVOD 基準サービス記述子
		x4C = 0x4C, // タイムシフトサービス記述子
		SHORT_EVENT = 0x4D, // 短形式イベント記述子
		EXT_EVENT = 0x4E, // 拡張形式イベント記述子
		x4F = 0x4F, // タイムシフトイベント記述子
		COMPONENT = 0x50, // コンポーネント記述子
		x51 = 0x51, // モザイク記述子
		STREAM_IDENTIFIER = 0x52, // ストリーム識別記述子
		CA_IDENTIFIER = 0x53, // CA 識別記述子
		CONTENT = 0x54, // コンテント記述子
		x55 = 0x55, // パレンタルレート記述子
		x58 = 0x58, // ローカル時間オフセット記述子
		x63 = 0x63, // パーシャルトランスポートストリーム記述子
		xC0 = 0xC0, // 階層伝送記述子
		DIGITAL_COPY_CONTROL = 0xC1, // デジタルコピー制御記述子
		xC2 = 0xC2, // ネットワーク識別記述子
		xC3 = 0xC3, // パーシャルトランスポートストリームタイム記述子
		AUDIO_COMPONENT = 0xC4, // 音声コンポーネント記述子
		xC5 = 0xC5, // ハイパーリンク記述子
		xC6 = 0xC6, // 対象地域記述子
		xC7 = 0xC7, // データコンテンツ記述子
		xC8 = 0xC8, // ビデオデコードコントロール記述子
		xC9 = 0xC9, // ダウンロードコンテンツ記述子
		xCA = 0xCA, // CA_EMM_TS 記述子
		xCB = 0xCB, // CA 契約情報記述子
		xCC = 0xCC, // CA サービス記述子
		TS_INFORMATION = 0xCD, // TS 情報記述子
		xCE = 0xCE, // 拡張ブロードキャスタ記述子
		LOGO_TRANS = 0xCF, // ロゴ伝送記述子
		xD0 = 0xD0, // 基本ローカルイベント記述子
		xD1 = 0xD1, // リファレンス記述子
		xD2 = 0xD2, // ノード関係記述子
		xD3 = 0xD3, // 短形式ノード情報記述子
		xD4 = 0xD4, // STC 参照記述子
		xD5 = 0xD5, // シリーズ記述子
		EVENT_GROUP = 0xD6, // イベントグループ記述子
		SI_PARAM = 0xD7, // SI 伝送パラメータ記述子
		xD8 = 0xD8, // ブロードキャスタ名記述子
		xD9 = 0xD9, // コンポーネントグループ記述子
		xDA = 0xDA, // SI プライムTS 記述子
		xDB = 0xDB, // 掲示板情報記述子
		xDC = 0xDC, // LDT リンク記述子
		xDD = 0xDD, // 連結送信記述子
		xDE = 0xDE, // コンテント利用記述子
		xE0 = 0xE0, // サービスグループ記述子
		xF7 = 0xF7, // カルーセル互換複合記述子
		xF8 = 0xF8, // 限定再生方式記述子
		xF9 = 0xF9, // 有線TS 分割システム記述子
		xFA = 0xFA, // 地上分配システム記述子
		xFB = 0xFB, // 部分受信記述子
		xFC = 0xFC, // 緊急情報記述子
		xFD = 0xFD, // データ符号化方式記述子
		xFE = 0xFE, // システム管理記述子
	};
}

class CDescBase
{
protected:
	const BYTE* ptr;
public:
	CDescBase(const BYTE* p_) : ptr(p_) { }
protected:
	BYTE tag() const { return ptr[0]; }
	BYTE length() const { return ptr[1]; }
};

// [0x09] 限定受信方式記述子
class CDescCAMethod : public CDescBase
{
public:
	enum { TAG = DESC_TAG::COND_ACCESS_METHOD };
	CDescCAMethod(const BYTE* p_) : CDescBase(p_) { }

	bool Check() const { return true; } // TODO: チェックメソッド

	WORD GetMethodId() const { return Readin16<16>(&ptr[2]); } // Method
															   // CATの記述子だったらEMM_PID, PMTの記述子だったらECM_PID
	WORD GetPID() const { return Readin16<13>(&ptr[4]); }
};

// [0x41] サービスリスト記述子
class CDescServiceList : public CDescBase
{
public:
	enum { TAG = DESC_TAG::SERVICE_LIST };
	CDescServiceList(const BYTE* p_) : CDescBase(p_) { }

	struct DATA {
		DATA(const BYTE* p) : wSID(Readin16<16>(p)), byServiceType(p[2]) { }

		WORD wSID;
		BYTE byServiceType;
	};

	int GetDataCount() const { return length() / 2; }
	DATA GetData(int n) const { return DATA(&ptr[2 + n * 2]); }
};

// [0x43] 衛星分配システム記述子
class CDescSateliteDeliverySystem : public CDescBase
{
public:
	enum { TAG = DESC_TAG::SATELITE_SYSTEM };
	CDescSateliteDeliverySystem(const BYTE* p_) : CDescBase(p_) { }

	DWORD GetFrequency() const;
	WORD GetOrbitalPosition() const { return Readin16<16>(&ptr[6]); }
	BYTE GetWestEastFlag() const { return BIT_SHIFT_MASK(ptr[8], 7, 1); }
	BYTE GetPolaritation() const { return BIT_SHIFT_MASK(ptr[8], 5, 2); }
	BYTE GetModulation() const { return BIT_SHIFT_MASK(ptr[8], 0, 5); }
	DWORD GetSymbolRate() const;
	BYTE GetEFCInner() const { return BIT_SHIFT_MASK(ptr[12], 0, 4); }
};

// [0x48] サービス記述子
class CDescService : public CDescBase
{
public:
	enum { TAG = DESC_TAG::SERVICE };
	CDescService(const BYTE* p_) : CDescBase(p_) { }

	bool Check() const { return true; } // TODO: チェックメソッド

	BYTE GetServiceType() const { return ptr[2]; }
	std::pair<const BYTE*, int> GetProviderName() const;
	std::pair<const BYTE*, int> GetServiceName() const;
};

// [0x4D] 短形式イベント記述子
class CDescShortEvent : public CDescBase
{
public:
	enum { TAG = DESC_TAG::SHORT_EVENT };
	CDescShortEvent(const BYTE* p_) : CDescBase(p_) { }

	bool Check() const { return true; } // TODO: チェックメソッド

	DWORD GetLanguageCode() const { return Read24(&ptr[2]); }
	std::pair<const BYTE*, int> GetName() const;
	std::pair<const BYTE*, int> GetText() const;
};

// [0x4E] 拡張形式イベント記述子
class CDescExtEvent : public CDescBase
{
public:
	enum { TAG = DESC_TAG::EXT_EVENT };
	CDescExtEvent(const BYTE* p_) : CDescBase(p_) { }

	bool Check() const { return true; } // TODO: チェックメソッド

	BYTE GetDescriptorNumber() const { return BIT_SHIFT_MASK(ptr[2], 4, 4); }
	BYTE GetLastDescriptorNumber() const { return BIT_SHIFT_MASK(ptr[2], 0, 4); }
	DWORD GetLanguageCode() const { return Read24(&ptr[3]); }

	// これは形式的に定義しているだけ。使わなくてもいい
	class TextHandler
	{
	public:
		virtual void OnItemDescription(wchar_t* str, int len) = 0;
		virtual void OnItemText(wchar_t* str, int len) = 0;
		virtual void OnText(wchar_t* str, int len) = 0;
	};

	template<class HandlerType>
	void ParseText(HandlerType& Handler)
	{
		const BYTE* itemPtr = &ptr[7];
		const BYTE* itemEnd = itemPtr + ptr[6];

		while (itemPtr < itemEnd) {
			if (itemPtr[0]) {
				Handler.OnItemDescription(std::pair<const BYTE*, int>(&itemPtr[1], itemPtr[0]));
			}
			itemPtr += itemPtr[0] + 1;
			if (itemPtr[0]) {
				Handler.OnItemText(std::pair<const BYTE*, int>(&itemPtr[1], itemPtr[0]));
			}
			itemPtr += itemPtr[0] + 1;
		}
		if (itemPtr[0]) {
			Handler.OnText(std::pair<const BYTE*, int>(&itemPtr[1], itemPtr[0]));
		}
	}
};

// [0x50] コンポーネント記述子
class CDescComponent : public CDescBase
{
public:
	enum { TAG = DESC_TAG::COMPONENT };
	CDescComponent(const BYTE* p_) : CDescBase(p_) { }

	bool Check() const { return true; } // TODO: チェックメソッド

	BYTE GetStreamContent() const { return BIT_SHIFT_MASK(ptr[2], 0, 4); }
	BYTE GetComponentType() const { return ptr[3]; }
	BYTE GetComponentTag() const { return ptr[4]; }
	DWORD GetLanguageCode() const { return Read24(&ptr[5]); }
	std::pair<const BYTE*, int> GetText() const;
};

// [0x52] ストリーム識別記述子
class CDescStreamIdentifier : public CDescBase
{
public:
	enum { TAG = DESC_TAG::STREAM_IDENTIFIER };
	CDescStreamIdentifier(const BYTE* p_) : CDescBase(p_) { }

	bool Check() const { return true; } // TODO: チェックメソッド

	BYTE GetComponentTag() const { return ptr[2]; }
};

// [0x54] コンテント記述子
class CDescContent : public CDescBase
{
public:
	enum { TAG = DESC_TAG::CONTENT };
	CDescContent(const BYTE* p_) : CDescBase(p_) { }

	bool Check() const { return true; } // TODO: チェックメソッド

	typedef struct DATA {
		union {
			struct nibble_ {
				BYTE content_nibble_level_1;
				BYTE content_nibble_level_2;
				BYTE user_nibble1;
				BYTE user_nibble2;
			} nibble;
			DWORD data;
		};

		DATA(const BYTE* ptr) {
			nibble.content_nibble_level_1 = BIT_SHIFT_MASK(ptr[0], 4, 4);
			nibble.content_nibble_level_2 = BIT_SHIFT_MASK(ptr[0], 0, 4);
			nibble.user_nibble1 = BIT_SHIFT_MASK(ptr[1], 4, 4);
			nibble.user_nibble2 = BIT_SHIFT_MASK(ptr[1], 0, 4);
		 }
	} DATA;

	int GetDataCount() const { return length() / 2; }
	DATA GetData(int n) const { return DATA(&ptr[2 + n * 2]); }
};

// [0xC1] デジタルコピー制御記述子
class CDescDigitalCopyControl : public CDescBase
{
public:
	enum { TAG = DESC_TAG::DIGITAL_COPY_CONTROL };
	CDescDigitalCopyControl(const BYTE* p_) : CDescBase(p_) { }

	bool Check() const { return true; } // TODO: チェックメソッド

	BYTE GetRecodeControlData() const { return BIT_SHIFT_MASK(ptr[2], 6, 2); }
	bool GetMaxBitrateFlag() const { return BIT_SHIFT_MASK(ptr[2], 5, 1) != 0; }
	bool GetComponentControlFlag() const { return BIT_SHIFT_MASK(ptr[2], 4, 1); }
	BYTE GetUserDefinedData() const { return BIT_SHIFT_MASK(ptr[2], 0, 4); }

	// MaxBitrateFlagがtrueのときだけ
	BYTE GetMaxBitrate() const { return ptr[3]; }

	typedef struct COMPONENT_CONTROL
	{
		BYTE ComponentTag;
		BYTE RecodeControl;
		bool MaxBitrateFlag;
		BYTE UserDefined;
		BYTE MaxBitrate;
	} COMPONENT_CONTROL;

	template <class cbType>
	void EnumComponentControl(cbType& cbFunc)
	{
		if (!GetComponentControlFlag()) return;
		bool bMaxBitrate = GetMaxBitrateFlag();
		BYTE ControlLength = bMaxBitrate ? ptr[4] : ptr[3];
		const BYTE* ptrItem = bMaxBitrate ? &ptr[5] : &ptr[4];
		const BYTE* ptrEnd = ptrItem + ControlLength;
		while (ptrItem < ptrEnd) {
			COMPONENT_CONTROL cc;
			cc.ComponentTag = ptrItem[0];
			cc.RecodeControl = BIT_SHIFT_MASK(ptrItem[1], 6, 2);
			cc.MaxBitrateFlag = BIT_SHIFT_MASK(ptrItem[1], 5, 1) != 0;
			cc.UserDefined = BIT_SHIFT_MASK(ptrItem[1], 0, 4);
			if (cc.MaxBitrateFlag) {
				cc.MaxBitrate = ptr[2];
				ptrItem += 3;
			}
			else {
				ptrItem += 2;
			}
			cbFunc(cc);
		}
	}
};

// [0xC4] 音声コンポーネント記述子
class CDescAudioComponent : public CDescBase
{
public:
	enum { TAG = DESC_TAG::AUDIO_COMPONENT };
	CDescAudioComponent(const BYTE* p_) : CDescBase(p_) { }

	bool Check() const { return true; } // TODO: チェックメソッド

	BYTE GetStreamContent() const { return BIT_SHIFT_MASK(ptr[2], 0, 4); }
	BYTE GetComponentType() const { return ptr[3]; }
	BYTE GetComponentTag() const { return ptr[4]; }
	BYTE GetStreamType() const { return ptr[5]; }
	BYTE GetSimulcastGroupTag() const { return ptr[6]; }
	BOOL GetESMultiLingualFlag() const { return BIT_SHIFT_MASK(ptr[7], 7, 1); }
	BOOL GetMainComponentFlag() const { return BIT_SHIFT_MASK(ptr[7], 6, 1); }
	BYTE GetQualityIndicator() const { return BIT_SHIFT_MASK(ptr[7], 4, 2); }
	BYTE GetSamplingRate() const { return BIT_SHIFT_MASK(ptr[7], 1, 3); }
	DWORD GetLanguageCode() const { return Read24(&ptr[8]); }
	// ESMultiLingualFlag == 1 のときだけ
	DWORD GetLanguageCode2() const { return Read24(&ptr[11]); }
	std::pair<const BYTE*, int> GetText() const;

};

// [0xCD] TS 情報記述子
class CDescTsInfomation : public CDescBase
{
public:
	enum { TAG = DESC_TAG::TS_INFORMATION };
	CDescTsInfomation(const BYTE* p_) : CDescBase(p_) { }

	bool Check() const { return true; } // TODO: チェックメソッド

	BYTE GetRemoconKey() const { return ptr[2]; }
	std::pair<const BYTE*, int> GetTsName() const;
	BYTE GetTransmissionTypeCount() const { return BIT_SHIFT_MASK(ptr[3], 0, 2); }

	class TransType
	{
		const BYTE* ptr;
	public:
		explicit TransType(const BYTE* p_) : ptr(p_) { }

		BYTE GetTransmissionInfoType() const { return ptr[0]; }
		BYTE GetNumServide() const { return ptr[1]; }
		WORD GetServiceId(int n) const { return Readin16<16>(&ptr[2 + n * 2]); }
	};

	class TransTypeHandler
	{
	public:
		virtual void OnTransType(TransType& data) = 0;
	};

	template <class HandlerType>
	void EnumTransmissionType(HandlerType& Handler)
	{
		const BYTE* ptrItem = &ptr[4 + BIT_SHIFT_MASK(ptr[3], 2, 6)];
		int Cnt = GetTransmissionTypeCount();
		for (int i = 0; i < Cnt; i++) {
			Handler.OnTransType(TransType(ptrItem));
			ptrItem += 2 + ptrItem[1] * 2;
		}
	}

};

// [0xD6] イベントグループ記述子
class CDescEventGroup : public CDescBase
{
public:
	enum { TAG = DESC_TAG::EVENT_GROUP };
	CDescEventGroup(const BYTE* p_) : CDescBase(p_) { }

	bool Check() const { return true; } // TODO: チェックメソッド

	BYTE GetGroupType() const { return BIT_SHIFT_MASK(ptr[2], 4, 4); }

	typedef struct EVENT {
		union {
			struct word_data_ {
				WORD service_id, event_id;
			} word_data;
			DWORD data;
		};

		EVENT(WORD sid, WORD eid) {
			word_data.service_id = sid;
			word_data.event_id = eid;
		}
	} EVENT;

	BYTE GetEventCount() const { return BIT_SHIFT_MASK(ptr[2], 0, 4); }
	EVENT GetEvent(int n) const { return EVENT(Readin16<16>(&ptr[3 + n * 4]), Readin16<16>(&ptr[5 + n * 4])); }

	// ここに他ネットワークからの場合、そのネットワークに関する情報がある

};

// [0xD7] SI 伝送パラメータ記述子

class CDescSIParam : public CDescBase
{
public:
	enum { TAG = DESC_TAG::SI_PARAM };
	CDescSIParam(const BYTE* p_) : CDescBase(p_) { }

	bool Check() const { return true; } // TODO: チェックメソッド

	BYTE GetParamVersion() const { return ptr[2]; }
	// 現在より過去の最も最近の記述子が現在有効である
	__int64 GetUpdateTime() const;

	class CPfParam
	{
		const BYTE* ptr;
	public:
		CPfParam(const BYTE* ptr_) : ptr(ptr_) { }

		// 単位:秒
		BYTE GetTableCyclePFEIT() { return (BYTE)ReadBcd<2>(&ptr[0]); }
		BYTE GetTableCycleMEIT() { return (BYTE)ReadBcd<2>(&ptr[1]); }
		BYTE GetTableCycleLEIT() { return (BYTE)ReadBcd<2>(&ptr[2]); }
		// 伝送番組数
		BYTE GetNumOfMEitEvent() { return BIT_SHIFT_MASK(ptr[3], 4, 4); }
		BYTE GetNumOfLEitEvent() { return BIT_SHIFT_MASK(ptr[3], 0, 4); }
	};

	class CScheduleEitParam
	{
		const BYTE* ptr;
	public:
		CScheduleEitParam(const BYTE* ptr_) : ptr(ptr_) { }

		// ARIB TR-B14 表9-1 参照
		BYTE GetMediaType() { return BIT_SHIFT_MASK(ptr[0], 6, 2); }
		// pattern は意味がない（参照してはならない）
		BYTE GetPattern() { return BIT_SHIFT_MASK(ptr[0], 4, 2); }
		// 単位:日(0-32の値を取る。それ以外だと異常状態）
		BYTE GetScheduleRange() { return (BYTE)ReadBcd<2>(&ptr[1]); }
		// 単位:秒
		WORD GetBaseCycle() { return (WORD)ReadBcd<3>(&ptr[2]); }
		BYTE GetCycleGroupCount() { return BIT_SHIFT_MASK(ptr[3], 0, 2); }
		BYTE GetNumOfSegment(int n) { return (BYTE)ReadBcd<2>(&ptr[4 + n * 2]); }
		BYTE GetCycle(int n) { return (BYTE)ReadBcd<2>(&ptr[5 + n * 2]); }
	};

	class SIParamDescHandler
	{
	public:
		// 第一ループの場合 NIT, BIT, SDT (BS,CSのみ + EIT[p/f actual/other], SDTT) の周期
		// 第二ループの場合 地上のみ SDTT, CDT の周期
		virtual void OnTableCycle(BYTE tableId, BYTE tableCycle) = 0;

		virtual void OnPfEitParamT(BYTE tableId, CPfParam prm) = 0;
		virtual void OnPfEitParamS(BYTE tableId, CPfParam prm) = 0; // 有効なのはGetTableCyclePFEITだけ

		virtual void OnScheduleEitParam(BYTE tableId, CScheduleEitParam prm) = 0;
	};

	template <class HandlerType>
	bool Parse(HandlerType& DescHandler)
	{
		utl::vmem<const BYTE*> ptrList;
		const BYTE* pos = ptr + 5;
		// -4 は CRC分
		const BYTE* DescEnd = ptr + 2 + length();
		while (pos < DescEnd) {
			ptrList.push_back(pos);
			if (pos + 2 >= DescEnd) return false;
			WORD blen = 2 + pos[1];
			pos += blen;
		}
		if (pos != DescEnd) return false;

		for (int i = 0; i < ptrList.size(); i++) {
			pos = ptrList[i];
			switch (pos[0]) {
				// table_id
				// 
			case 0x40: // NIT cycle : terre 1, BS 1
			case 0xC4: // BIT cycle : terre 1, BS 1
			case 0x42: // SDT cycle : terre 1, BS(actual) 1
			case 0x46: // SDT cycle : BS(other) 1
			case 0xC3: // SDTT cycle : terre 2, BS 1
			case 0xC8: // CDT cycle : terre 2
				DescHandler.OnTableCycle(pos[0], pos[2]);
				break;
			case 0x4E: // pf : terre 1, 2, BS 2
			case 0x4F: // pf : BS(other) 1
				if (pos[1] >= 4) { // terre ちょっと危ないけどこの方法でいいよね
					DescHandler.OnPfEitParamT(pos[0], CPfParam(pos + 2));
				}
				else { // BS
					DescHandler.OnPfEitParamS(pos[0], CPfParam(pos + 2));
				}
				break;
			case 0x50: // schedule : terre 1, BS(actual) 1
			case 0x58: // schedule : terre 2, BS 2
			case 0x60: // schedule : BS(other) 1
				const BYTE* item_ptr = pos + 2;
				const BYTE* item_end = item_ptr + pos[1];
				while (item_ptr < item_end) {
					CScheduleEitParam EitParam(item_ptr);
					DescHandler.OnScheduleEitParam(pos[0], EitParam);
					item_ptr += 4 + EitParam.GetCycleGroupCount() * 2;
				}
				if (item_ptr != item_ptr) return false;
				break;
			}
		}

		return true;
	}
};

class CDescHandler
{
public:
	virtual void OnCAMethod(CDescCAMethod& desc) { }
	virtual void OnServiceList(CDescServiceList& desc) { }
	virtual void OnService(CDescService& desc) { }
	virtual void OnShortEvent(CDescShortEvent& desc) { }
	virtual void OnExtEvent(CDescExtEvent& desc) { }
	virtual void OnComponent(CDescComponent& desc) { }
	virtual void OnStreamIdentifier(CDescStreamIdentifier& desc) { }
	virtual void OnContent(CDescContent& desc) { }
	virtual void OnDigitalCopyControl(CDescDigitalCopyControl& desc) { }
	virtual void OnAudioComponent(CDescAudioComponent& desc) { }
	virtual void OnTsInfomation(CDescTsInfomation& desc) { }
	virtual void OnEventGroup(CDescEventGroup& desc) { }
	virtual void OnSIParam(CDescSIParam& desc) { }
};

class CDescDispatch
{
public:
	// 各ディスクリプタのCheckは呼ばないので、各自必要に応じて呼ぶように
	static void DispatchBlock(CDescHandler* pHandler, const BYTE* pDesc, DWORD dwDescLen);
private:
	typedef void(*DISPATCH_FUNCTION)(CDescHandler* pHandler, const BYTE* pDesc);
	//
	static DISPATCH_FUNCTION DispatchTable[0x100];

	static void OnCAMethod(CDescHandler* pHandler, const BYTE* pDesc);
	static void OnServiceList(CDescHandler* pHandler, const BYTE* pDescc);
	static void OnService(CDescHandler* pHandler, const BYTE* pDesc);
	static void OnShortEvent(CDescHandler* pHandler, const BYTE* pDesc);
	static void OnExtEvent(CDescHandler* pHandler, const BYTE* pDesc);
	static void OnComponent(CDescHandler* pHandler, const BYTE* pDesc);
	static void OnStreamIdentifier(CDescHandler* pHandler, const BYTE* pDesc);
	static void OnContent(CDescHandler* pHandler, const BYTE* pDesc);
	static void OnDigitalCopyControl(CDescHandler* pHandler, const BYTE* pDesc);
	static void OnAudioComponent(CDescHandler* pHandler, const BYTE* pDesc);
	static void OnTsInfomation(CDescHandler* pHandler, const BYTE* pDesc);
	static void OnEventGroup(CDescHandler* pHandler, const BYTE* pDesc);
	static void OnSIParam(CDescHandler* pHandler, const BYTE* pDesc);

	class CInitializer { public: CInitializer(); };
	static const CInitializer Initializer;
};

/////////////////////////////////////////////////////////////////////////////
// PAT テーブル
/////////////////////////////////////////////////////////////////////////////

class CSiSectionPAT : public CSiSection<true, true>
{
public:
	CSiSectionPAT(CMediaData* Sec_);

	bool Check();
	bool Parse();

	WORD GetTSID() const { return GetTableIdExtension(); }

	class CProg // == Service
	{
	public:
		explicit CProg(const BYTE* ptr)
			: ProgramNumver(Readin16<16>(ptr))
			, NITorPMTPID(Readin16<13>(&ptr[2]))
		{
		}

		WORD GetProgramNumber() const { return ProgramNumver; }
		// program_number == 0 ? NIT : PID
		WORD GetNITorPMTPID() const { return NITorPMTPID; }

	private:
		WORD ProgramNumver;
		WORD NITorPMTPID;
	};

	int GetProgramListCount() const { return ProgramCount; }
	CProg GetProgramInfo(int n) const;

protected:
	int ProgramCount;
};

/////////////////////////////////////////////////////////////////////////////
// EIT テーブル
/////////////////////////////////////////////////////////////////////////////

class CSiSectionEIT : public CSiSection<true, true>
{
public:
	CSiSectionEIT(CMediaData* Sec_);

	bool Check();
	bool Parse();

	WORD GetServiceID() { return GetTableIdExtension(); }

	WORD GetTSID() { return Readin16<16>(m_pData + 8); }
	WORD GetOriginalNID() { return Readin16<16>(&m_pData[8 + 2]); }
	BYTE GetSegmentLastSectionNumber() { return m_pData[8 + 4]; }
	BYTE GetLastTableID() { return m_pData[8 + 5]; }

	class CEvent
	{
	public:
		explicit CEvent(const BYTE* ptr_)
			: ptr(ptr_)
		{
		}

		WORD GetEventID() { return Readin16<16>(ptr); }
		__int64 GetStartTime(); // FILETIMEと同じ数値
		DWORD GetDuration(); // 秒
		//STDRUN::STATUS GetRunningStatus() { return (STDRUN::STATUS)BIT_SHIFT_MASK(ptr[10], 5, 3); }
		BOOL GetFreeCAMode() { return BIT_SHIFT_MASK(ptr[10], 4, 1); }

		WORD GetDescSize() { return Readin16<12>(&ptr[10]); }
		const BYTE* GetDescData() { return &ptr[12]; }

	private:
		const BYTE* ptr;
	};

	int GetEventListCount() { return (int)EventList.size(); }
	CEvent GetEventInfo(int n);
protected:
	std::vector<CEvent> EventList;
};

class EP3AribStrinbDecoder {
	//ARIB文字列を複合
	typedef int (WINAPI *DecodeARIBCharactersEP3)(
		const BYTE *pSrcData, const DWORD dwSrcLen, void(*pfn)(const WCHAR*, void*), void* ctx
		);

public:
	EP3AribStrinbDecoder() {
		module = ::LoadLibrary(L"EpgDataCap3.dll");
		if (module != NULL) {
			pfnDecodeARIBCharactersEP3 = (DecodeARIBCharactersEP3) ::GetProcAddress(module, "DecodeARIBCharacters");
		}

		if (pfnDecodeARIBCharactersEP3 == NULL) {
			OutputDebugString(L"pfnDecodeARIBCharactersEP3取得に失敗");
		}
	}
	~EP3AribStrinbDecoder() {
		if (module != NULL) {
			FreeLibrary(module);
			module = NULL;
		}
	}

	bool Initialize() {
		return pfnDecodeARIBCharactersEP3 != NULL;
	}

	std::wstring DecodeString(const std::pair<const BYTE*, int>& input);

private:
	DecodeARIBCharactersEP3 pfnDecodeARIBCharactersEP3;
	HMODULE module;
};

class CTsFilter
{
public:
	CTsFilter() : m_Out(NULL) { }

	virtual void StorePacket(CTsPacket* pPacket) = 0;

	void SetOutputFilter(CTsFilter* filter) {
		m_Out = filter;
	}

protected:
	void OutputPacket(CTsPacket* pPacket) {
		if (m_Out != NULL) {
			m_Out->StorePacket(pPacket);
		}
	}

private:
	CTsFilter* m_Out;
};

class CCocatMediaData {
public:
	void Put(const BYTE* ptr_, size_t length) {
		this->ptr = ptr_;
		this->len1 = buffer.GetSize();
		this->len2 = length;
		this->offset = 0;
	}

	size_t GetSize() {
		return len1 + len2 - offset;
	}

	BYTE Get(size_t idx) {
		idx += offset;
		if (idx < len1) {
			return buffer.GetAt(static_cast<DWORD>(idx));
		}
		return ptr[idx - len1];
	}

	const BYTE* Pop(size_t bytes) {
		const BYTE* ret;
		if (offset + bytes < len1) {
			ret = buffer.GetData() + offset;
		}
		else if(offset < len1) {
			buffer.AddData(ptr, static_cast<DWORD>(offset + bytes - len1));
			ret = buffer.GetData() + offset;
		}
		else {
			ret = ptr + offset - len1;
		}
		offset += bytes;
		return ret;
	}

	void Commit() {
		if (offset >= len1) {
			buffer.ClearSize();
			buffer.AddData(ptr + offset - len1, static_cast<DWORD>(len1 + len2 - offset));
		}
		else {
			buffer.TrimHead(static_cast<DWORD>(offset));
			buffer.AddData(ptr, static_cast<DWORD>(len2));
		}
	}

	void Clear() {
		buffer.ClearSize();
	}

private:
	CMediaData buffer;
	const BYTE* ptr;
	size_t len1;
	size_t len2;
	size_t offset;
};

class CTsPacketParser : public CTsFilter
{
	enum {
		SYNC_CHECK_LENGTH = 16 * TSPARAM::TS_PACKETSIZE
	};
public:
	virtual void StorePacket(CTsPacket* pPacket) {
		throw "Not Suported";
	}

	void InputRawData(BYTE* pData, size_t dwLength) {
		buffer.Put(pData, dwLength);

		while (buffer.GetSize() >= TSPARAM::TS_PACKETSIZE) {
			if (buffer.Get(0) == 0x47) {
				InputPacket(buffer.Pop(TSPARAM::TS_PACKETSIZE));
			}
			else {
				if (buffer.GetSize() > SYNC_CHECK_LENGTH) {
					if (checkSyncByte(SYNC_CHECK_LENGTH)) {
						InputPacketData(buffer.Pop(SYNC_CHECK_LENGTH), SYNC_CHECK_LENGTH);
					}
					else {
						buffer.Pop(1);
					}
				}
				else {
					break;
				}
			}
		}

		buffer.Commit();
	}

	void Reset() {
		buffer.Clear();
	}

private:
	CCocatMediaData buffer;

	bool checkSyncByte(int dwLength) {
		for (int idx = 0; idx < dwLength; idx += TSPARAM::TS_PACKETSIZE) {
			if (buffer.Get(idx) != 0x47) {
				return false;
			}
		}
		return true;
	}

	void InputPacket(const BYTE* pData) {
		DWORD dwRet;
		CTsPacket cPacket(pData, &dwRet);

		if (dwRet != CTsPacket::EC_VALID) {
			if (dwRet != CTsPacket::EC_CONTINUITY) {
				// 不正パケットは破棄
				return;
			}
		}

		OutputPacket(&cPacket);
	}

	void InputPacketData(const BYTE* pData, size_t dwLength) {
		for (size_t idx = 0; idx < dwLength; idx += TSPARAM::TS_PACKETSIZE) {
			InputPacket(pData + idx);
		}
	}
};

/////////////////////////////////////////////////////////////////////////////
// CEitDetector
/////////////////////////////////////////////////////////////////////////////

class CEitConverter : protected CDescHandler {
public:
	bool Initialize() { return arib.Initialize(); }
	void Feed(CSiSectionEIT& eit, int idx, EPGDB_EVENT_INFO* dest);

private:
	EP3AribStrinbDecoder arib;
	EPGDB_EVENT_INFO* dest;

	// DescHandler
	virtual void OnCAMethod(CDescCAMethod& desc) { }
	virtual void OnService(CDescService& desc) { }
	virtual void OnShortEvent(CDescShortEvent& desc);
	virtual void OnExtEvent(CDescExtEvent& desc);
	virtual void OnComponent(CDescComponent& desc);
	virtual void OnStreamIdentifier(CDescStreamIdentifier& desc) { }
	virtual void OnContent(CDescContent& desc);
	virtual void OnDigitalCopyControl(CDescDigitalCopyControl& desc) { }
	virtual void OnAudioComponent(CDescAudioComponent& desc);
	virtual void OnTsInfomation(CDescTsInfomation& desc) { }
	virtual void OnEventGroup(CDescEventGroup& desc);
};

class CEitDetector : public CTsFilter
{
public:
	CEitDetector();
	virtual ~CEitDetector();

	bool Initialize() { return m_EITConverter.Initialize(); }

	void Reset();

	virtual void StorePacket(CTsPacket* pPacket);

	void SetTarget(EPGDB_EVENT_INFO* eventInfo, int serviceId, int eventId) {
		m_EventInfo = eventInfo;
		m_TargetServiceId = serviceId;
		m_TargetEventId = eventId;
	}
	int GetTSID() { return m_ProgTbl.TSID; }
	bool IsDetected() { return m_Detected; }

	CMediaData* GetPATData() { return &m_PATData; }
	CMediaData* GetEITData() { return &m_EITData; }
	CEitConverter* GetEITConverter() { return &m_EITConverter; }

protected:
	// ハンドラ
	static void OnPAT_(void* context, CSiSectionParser *pSiSectionParser, CMediaData *pSection);
	void OnPAT(CSiSectionParser *pSiSectionParser, CMediaData *pSection);
	static void OnEIT_(void* context, CSiSectionParser *pSiSectionParser, CMediaData *pSection);
	void OnEIT(CSiSectionParser *pSiSectionParser, CMediaData *pSection);

	CEitConverter m_EITConverter;

	bool m_Detected;
	EPGDB_EVENT_INFO* m_EventInfo;
	int m_TargetServiceId;
	int m_TargetEventId;
	ProgramTable m_ProgTbl;

	CMediaData m_PATData;
	CMediaData m_EITData;

	// PATを処理する
	CSiSectionParserImpl<true, true> m_PATParser;
	// EITを処理する
	CSiSectionParserImpl<true, true> m_EITParser[3];
};
