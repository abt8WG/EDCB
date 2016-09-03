// 軽量TSパーサ
// TvTestのコードを流用しているためこのコードのライセンスはGPLです。
//
#include "stdafx.h"

#include <crtdbg.h>
#include <stdint.h>

#include "LightTsUtils.h"

#pragma region CCrcCalculator

WORD CCrcCalculator::CalcCrc16(const BYTE *pData, DWORD DataSize, WORD wCurCrc)
{
	// CRC16計算(ISO/IEC 13818-1 準拠)
	static const WORD CrcTable[256] = {
		0x0000U, 0x8005U, 0x800FU, 0x000AU, 0x801BU, 0x001EU, 0x0014U, 0x8011U, 0x8033U, 0x0036U, 0x003CU, 0x8039U, 0x0028U, 0x802DU, 0x8027U, 0x0022U,
		0x8063U, 0x0066U, 0x006CU, 0x8069U, 0x0078U, 0x807DU, 0x8077U, 0x0072U, 0x0050U, 0x8055U, 0x805FU, 0x005AU, 0x804BU, 0x004EU, 0x0044U, 0x8041U,
		0x80C3U, 0x00C6U, 0x00CCU, 0x80C9U, 0x00D8U, 0x80DDU, 0x80D7U, 0x00D2U, 0x00F0U, 0x80F5U, 0x80FFU, 0x00FAU, 0x80EBU, 0x00EEU, 0x00E4U, 0x80E1U,
		0x00A0U, 0x80A5U, 0x80AFU, 0x00AAU, 0x80BBU, 0x00BEU, 0x00B4U, 0x80B1U, 0x8093U, 0x0096U, 0x009CU, 0x8099U, 0x0088U, 0x808DU, 0x8087U, 0x0082U,
		0x8183U, 0x0186U, 0x018CU, 0x8189U, 0x0198U, 0x819DU, 0x8197U, 0x0192U, 0x01B0U, 0x81B5U, 0x81BFU, 0x01BAU, 0x81ABU, 0x01AEU, 0x01A4U, 0x81A1U,
		0x01E0U, 0x81E5U, 0x81EFU, 0x01EAU, 0x81FBU, 0x01FEU, 0x01F4U, 0x81F1U, 0x81D3U, 0x01D6U, 0x01DCU, 0x81D9U, 0x01C8U, 0x81CDU, 0x81C7U, 0x01C2U,
		0x0140U, 0x8145U, 0x814FU, 0x014AU, 0x815BU, 0x015EU, 0x0154U, 0x8151U, 0x8173U, 0x0176U, 0x017CU, 0x8179U, 0x0168U, 0x816DU, 0x8167U, 0x0162U,
		0x8123U, 0x0126U, 0x012CU, 0x8129U, 0x0138U, 0x813DU, 0x8137U, 0x0132U, 0x0110U, 0x8115U, 0x811FU, 0x011AU, 0x810BU, 0x010EU, 0x0104U, 0x8101U,
		0x8303U, 0x0306U, 0x030CU, 0x8309U, 0x0318U, 0x831DU, 0x8317U, 0x0312U, 0x0330U, 0x8335U, 0x833FU, 0x033AU, 0x832BU, 0x032EU, 0x0324U, 0x8321U,
		0x0360U, 0x8365U, 0x836FU, 0x036AU, 0x837BU, 0x037EU, 0x0374U, 0x8371U, 0x8353U, 0x0356U, 0x035CU, 0x8359U, 0x0348U, 0x834DU, 0x8347U, 0x0342U,
		0x03C0U, 0x83C5U, 0x83CFU, 0x03CAU, 0x83DBU, 0x03DEU, 0x03D4U, 0x83D1U, 0x83F3U, 0x03F6U, 0x03FCU, 0x83F9U, 0x03E8U, 0x83EDU, 0x83E7U, 0x03E2U,
		0x83A3U, 0x03A6U, 0x03ACU, 0x83A9U, 0x03B8U, 0x83BDU, 0x83B7U, 0x03B2U, 0x0390U, 0x8395U, 0x839FU, 0x039AU, 0x838BU, 0x038EU, 0x0384U, 0x8381U,
		0x0280U, 0x8285U, 0x828FU, 0x028AU, 0x829BU, 0x029EU, 0x0294U, 0x8291U, 0x82B3U, 0x02B6U, 0x02BCU, 0x82B9U, 0x02A8U, 0x82ADU, 0x82A7U, 0x02A2U,
		0x82E3U, 0x02E6U, 0x02ECU, 0x82E9U, 0x02F8U, 0x82FDU, 0x82F7U, 0x02F2U, 0x02D0U, 0x82D5U, 0x82DFU, 0x02DAU, 0x82CBU, 0x02CEU, 0x02C4U, 0x82C1U,
		0x8243U, 0x0246U, 0x024CU, 0x8249U, 0x0258U, 0x825DU, 0x8257U, 0x0252U, 0x0270U, 0x8275U, 0x827FU, 0x027AU, 0x826BU, 0x026EU, 0x0264U, 0x8261U,
		0x0220U, 0x8225U, 0x822FU, 0x022AU, 0x823BU, 0x023EU, 0x0234U, 0x8231U, 0x8213U, 0x0216U, 0x021CU, 0x8219U, 0x0208U, 0x820DU, 0x8207U, 0x0202U
	};

	for (DWORD i = 0; i < DataSize; i++) {
		wCurCrc = (wCurCrc << 8) ^ CrcTable[(wCurCrc >> 8) ^ pData[i]];
	}

	return wCurCrc;
}

DWORD CCrcCalculator::CalcCrc32(const BYTE *pData, DWORD DataSize, DWORD dwCurCrc)
{
	// CRC32計算(「Mpeg2-TSのストリームからデータ放送情報を抽出するテスト」からコードを流用、計算を分割できるように拡張)
	static const DWORD CrcTable[256] = {
		0x00000000UL, 0x04C11DB7UL, 0x09823B6EUL, 0x0D4326D9UL,	0x130476DCUL, 0x17C56B6BUL, 0x1A864DB2UL, 0x1E475005UL,	0x2608EDB8UL, 0x22C9F00FUL, 0x2F8AD6D6UL, 0x2B4BCB61UL,	0x350C9B64UL, 0x31CD86D3UL, 0x3C8EA00AUL, 0x384FBDBDUL,
		0x4C11DB70UL, 0x48D0C6C7UL, 0x4593E01EUL, 0x4152FDA9UL,	0x5F15ADACUL, 0x5BD4B01BUL, 0x569796C2UL, 0x52568B75UL,	0x6A1936C8UL, 0x6ED82B7FUL, 0x639B0DA6UL, 0x675A1011UL,	0x791D4014UL, 0x7DDC5DA3UL, 0x709F7B7AUL, 0x745E66CDUL,
		0x9823B6E0UL, 0x9CE2AB57UL, 0x91A18D8EUL, 0x95609039UL,	0x8B27C03CUL, 0x8FE6DD8BUL, 0x82A5FB52UL, 0x8664E6E5UL,	0xBE2B5B58UL, 0xBAEA46EFUL, 0xB7A96036UL, 0xB3687D81UL,	0xAD2F2D84UL, 0xA9EE3033UL, 0xA4AD16EAUL, 0xA06C0B5DUL,
		0xD4326D90UL, 0xD0F37027UL, 0xDDB056FEUL, 0xD9714B49UL,	0xC7361B4CUL, 0xC3F706FBUL, 0xCEB42022UL, 0xCA753D95UL,	0xF23A8028UL, 0xF6FB9D9FUL, 0xFBB8BB46UL, 0xFF79A6F1UL,	0xE13EF6F4UL, 0xE5FFEB43UL, 0xE8BCCD9AUL, 0xEC7DD02DUL,
		0x34867077UL, 0x30476DC0UL, 0x3D044B19UL, 0x39C556AEUL,	0x278206ABUL, 0x23431B1CUL, 0x2E003DC5UL, 0x2AC12072UL,	0x128E9DCFUL, 0x164F8078UL, 0x1B0CA6A1UL, 0x1FCDBB16UL,	0x018AEB13UL, 0x054BF6A4UL, 0x0808D07DUL, 0x0CC9CDCAUL,
		0x7897AB07UL, 0x7C56B6B0UL, 0x71159069UL, 0x75D48DDEUL,	0x6B93DDDBUL, 0x6F52C06CUL, 0x6211E6B5UL, 0x66D0FB02UL,	0x5E9F46BFUL, 0x5A5E5B08UL, 0x571D7DD1UL, 0x53DC6066UL,	0x4D9B3063UL, 0x495A2DD4UL, 0x44190B0DUL, 0x40D816BAUL,
		0xACA5C697UL, 0xA864DB20UL, 0xA527FDF9UL, 0xA1E6E04EUL,	0xBFA1B04BUL, 0xBB60ADFCUL, 0xB6238B25UL, 0xB2E29692UL,	0x8AAD2B2FUL, 0x8E6C3698UL, 0x832F1041UL, 0x87EE0DF6UL,	0x99A95DF3UL, 0x9D684044UL, 0x902B669DUL, 0x94EA7B2AUL,
		0xE0B41DE7UL, 0xE4750050UL, 0xE9362689UL, 0xEDF73B3EUL,	0xF3B06B3BUL, 0xF771768CUL, 0xFA325055UL, 0xFEF34DE2UL,	0xC6BCF05FUL, 0xC27DEDE8UL, 0xCF3ECB31UL, 0xCBFFD686UL,	0xD5B88683UL, 0xD1799B34UL, 0xDC3ABDEDUL, 0xD8FBA05AUL,
		0x690CE0EEUL, 0x6DCDFD59UL, 0x608EDB80UL, 0x644FC637UL,	0x7A089632UL, 0x7EC98B85UL, 0x738AAD5CUL, 0x774BB0EBUL,	0x4F040D56UL, 0x4BC510E1UL, 0x46863638UL, 0x42472B8FUL,	0x5C007B8AUL, 0x58C1663DUL, 0x558240E4UL, 0x51435D53UL,
		0x251D3B9EUL, 0x21DC2629UL, 0x2C9F00F0UL, 0x285E1D47UL,	0x36194D42UL, 0x32D850F5UL, 0x3F9B762CUL, 0x3B5A6B9BUL,	0x0315D626UL, 0x07D4CB91UL, 0x0A97ED48UL, 0x0E56F0FFUL,	0x1011A0FAUL, 0x14D0BD4DUL, 0x19939B94UL, 0x1D528623UL,
		0xF12F560EUL, 0xF5EE4BB9UL, 0xF8AD6D60UL, 0xFC6C70D7UL,	0xE22B20D2UL, 0xE6EA3D65UL, 0xEBA91BBCUL, 0xEF68060BUL,	0xD727BBB6UL, 0xD3E6A601UL, 0xDEA580D8UL, 0xDA649D6FUL,	0xC423CD6AUL, 0xC0E2D0DDUL, 0xCDA1F604UL, 0xC960EBB3UL,
		0xBD3E8D7EUL, 0xB9FF90C9UL, 0xB4BCB610UL, 0xB07DABA7UL,	0xAE3AFBA2UL, 0xAAFBE615UL, 0xA7B8C0CCUL, 0xA379DD7BUL,	0x9B3660C6UL, 0x9FF77D71UL, 0x92B45BA8UL, 0x9675461FUL,	0x8832161AUL, 0x8CF30BADUL, 0x81B02D74UL, 0x857130C3UL,
		0x5D8A9099UL, 0x594B8D2EUL, 0x5408ABF7UL, 0x50C9B640UL,	0x4E8EE645UL, 0x4A4FFBF2UL, 0x470CDD2BUL, 0x43CDC09CUL,	0x7B827D21UL, 0x7F436096UL, 0x7200464FUL, 0x76C15BF8UL,	0x68860BFDUL, 0x6C47164AUL, 0x61043093UL, 0x65C52D24UL,
		0x119B4BE9UL, 0x155A565EUL, 0x18197087UL, 0x1CD86D30UL,	0x029F3D35UL, 0x065E2082UL, 0x0B1D065BUL, 0x0FDC1BECUL,	0x3793A651UL, 0x3352BBE6UL, 0x3E119D3FUL, 0x3AD08088UL,	0x2497D08DUL, 0x2056CD3AUL, 0x2D15EBE3UL, 0x29D4F654UL,
		0xC5A92679UL, 0xC1683BCEUL, 0xCC2B1D17UL, 0xC8EA00A0UL,	0xD6AD50A5UL, 0xD26C4D12UL, 0xDF2F6BCBUL, 0xDBEE767CUL,	0xE3A1CBC1UL, 0xE760D676UL, 0xEA23F0AFUL, 0xEEE2ED18UL,	0xF0A5BD1DUL, 0xF464A0AAUL, 0xF9278673UL, 0xFDE69BC4UL,
		0x89B8FD09UL, 0x8D79E0BEUL, 0x803AC667UL, 0x84FBDBD0UL,	0x9ABC8BD5UL, 0x9E7D9662UL, 0x933EB0BBUL, 0x97FFAD0CUL,	0xAFB010B1UL, 0xAB710D06UL, 0xA6322BDFUL, 0xA2F33668UL,	0xBCB4666DUL, 0xB8757BDAUL, 0xB5365D03UL, 0xB1F740B4UL
	};

	for (DWORD i = 0; i < DataSize; i++) {
		dwCurCrc = (dwCurCrc << 8) ^ CrcTable[(dwCurCrc >> 24) ^ pData[i]];
	}

	return dwCurCrc;
}
#pragma endregion

#pragma region CAribTime

/////////////////////////////////////////////////////////////////////////////
// ARIB STD-B10 Part2 Annex C MJD+JTC 処理クラス
/////////////////////////////////////////////////////////////////////////////

const bool CAribTime::AribToSystemTime(const BYTE *pHexData, SYSTEMTIME *pSysTime)
{
	// 全ビットが1のときは未定義
	if ((*((DWORD *)pHexData) == 0xFFFFFFFFUL) && (pHexData[4] == 0xFFU))return false;

	// MJD形式の日付を解析
	SplitAribMjd(((WORD)pHexData[0] << 8) | (WORD)pHexData[1], &pSysTime->wYear, &pSysTime->wMonth, &pSysTime->wDay, &pSysTime->wDayOfWeek);

	// BCD形式の時刻を解析
	SplitAribBcd(&pHexData[2], &pSysTime->wHour, &pSysTime->wMinute, &pSysTime->wSecond);

	// ミリ秒は常に0
	pSysTime->wMilliseconds = 0U;

	return true;
}

const bool CAribTime::AribToInt64(const BYTE *pHexData, __int64* pTime)
{
	static const __int64 SecontTick = 1000 * 1000 * 10;
	static const __int64 MinuteTick = SecontTick * 60;
	static const __int64 HourTick = MinuteTick * 60;
	static const __int64 DayTick = HourTick * 24;

	// 全てのビットが１なら未定義
	if ((*((DWORD *)pHexData) == 0xFFFFFFFFUL) && (pHexData[4] == 0xFFU)) {
		return false;
	}

	DWORD dwMJD = Readin16<16>(pHexData);
	const BYTE* pAribBcd = &pHexData[2];
	WORD wHour = (WORD)(pAribBcd[0] >> 4) * 10U + (WORD)(pAribBcd[0] & 0x0FU);
	WORD wMinute = (WORD)(pAribBcd[1] >> 4) * 10U + (WORD)(pAribBcd[1] & 0x0FU);
	WORD wSecond = (WORD)(pAribBcd[2] >> 4) * 10U + (WORD)(pAribBcd[2] & 0x0FU);

	// 2038年以降も対応
	//（2003年12月1日以前だったら17ビット目が立っているとする）
	// →BITで2003年11月1日が来たので、2000年1月1日を基準とする
	if (dwMJD < 51544) {
		dwMJD |= 0x10000;
	}

	// 94187 を加算するとFILETIMEになる
	*pTime = DayTick * (dwMJD + 94187L) + HourTick * wHour + MinuteTick * wMinute + SecontTick * wSecond;
	return true;
}

const bool CAribTime::AribMjdToInt64(const BYTE *pHexData, __int64* pTime)
{
	static const __int64 DayTick = 24LL * 3600 * 1000 * 1000 * 10;

	// 全てのビットが１なら未定義
	if ((*((WORD *)pHexData) == 0xFFFFUL)) {
		return false;
	}

	DWORD dwMJD = Readin16<16>(pHexData);

	// 2038年以降も対応
	//（2003年12月1日以前だったら17ビット目が立っているとする）
	// →BITで2003年11月1日が来たので、2000年1月1日を基準とする
	if (dwMJD < 51544) {
		dwMJD |= 0x10000;
	}

	// 94187 を加算するとFILETIMEになる
	*pTime = DayTick * (dwMJD + 94187L);
	return true;
}

void CAribTime::SplitAribMjd(const WORD wAribMjd, WORD *pwYear, WORD *pwMonth, WORD *pwDay, WORD *pwDayOfWeek)
{
	// MJD形式の日付を解析する
	const DWORD dwYd = (DWORD)(((double)wAribMjd - 15078.2) / 365.25);
	const DWORD dwMd = (DWORD)(((double)wAribMjd - 14956.1 - (double)((int)((double)dwYd * 365.25))) / 30.6001);
	const DWORD dwK = ((dwMd == 14UL) || (dwMd == 15UL)) ? 1U : 0U;

	if (pwDay)*pwDay = wAribMjd - 14956U - (WORD)((double)dwYd * 365.25) - (WORD)((double)dwMd * 30.6001);
	if (pwYear)*pwYear = (WORD)(dwYd + dwK) + 1900U;
	if (pwMonth)*pwMonth = (WORD)(dwMd - 1UL - dwK * 12UL);
	if (pwDayOfWeek)*pwDayOfWeek = (wAribMjd + 2U) % 7U;
}

void CAribTime::SplitAribBcd(const BYTE *pAribBcd, WORD *pwHour, WORD *pwMinute, WORD *pwSecond)
{
	// BCD形式の時刻を解析する
	if (pwHour)*pwHour = (WORD)(pAribBcd[0] >> 4) * 10U + (WORD)(pAribBcd[0] & 0x0FU);
	if (pwMinute)*pwMinute = (WORD)(pAribBcd[1] >> 4) * 10U + (WORD)(pAribBcd[1] & 0x0FU);
	if (pwSecond)*pwSecond = (WORD)(pAribBcd[2] >> 4) * 10U + (WORD)(pAribBcd[2] & 0x0FU);
}

const DWORD CAribTime::AribBcdToSecond(const BYTE *pAribBcd)
{
	// 全ビットが1のときは未定義
	if (pAribBcd[0] == 0xFF && pAribBcd[1] == 0xFF && pAribBcd[2] == 0xFF)
		return 0;

	// BCD形式の時刻を秒に変換する
	const DWORD dwSecond = (((DWORD)(pAribBcd[0] >> 4) * 10U + (DWORD)(pAribBcd[0] & 0x0FU)) * 3600UL)
		+ (((DWORD)(pAribBcd[1] >> 4) * 10U + (DWORD)(pAribBcd[1] & 0x0FU)) * 60UL)
		+ ((DWORD)(pAribBcd[2] >> 4) * 10U + (DWORD)(pAribBcd[2] & 0x0FU));

	return dwSecond;
}

#pragma endregion

#pragma region CMediaData

#define MINBUFSIZE	256UL		// 最小バッファサイズ
//#define MINADDSIZE	256UL		// 最小追加確保サイズ


CMediaData::CMediaData()
	: m_dwDataSize(0UL)
	, m_dwBuffSize(0UL)
	, m_pData(NULL)
{
	// 空のバッファを生成する
}

CMediaData::CMediaData(const DWORD dwBuffSize)
	: m_dwDataSize(0UL)
	, m_dwBuffSize(0UL)
	, m_pData(NULL)
{
	// バッファサイズを指定してバッファを生成する
	Reserve(dwBuffSize);
}

CMediaData::CMediaData(const BYTE *pData, const DWORD dwDataSize)
	: m_dwDataSize(0UL)
	, m_dwBuffSize(0UL)
	, m_pData(NULL)
{
	// データ初期値を指定してバッファを生成する
	SetData(pData, dwDataSize);
}

CMediaData::CMediaData(const BYTE byFiller, const DWORD dwDataSize)
	: m_dwDataSize(0UL)
	, m_dwBuffSize(0UL)
	, m_pData(NULL)
{
	// フィルデータを指定してバッファを生成する
	SetSize(dwDataSize, byFiller);
}

CMediaData & CMediaData::operator = (const CMediaData &Operand)
{
	if (&Operand != this) {
		// バッファサイズの情報まではコピーしない
		SetData(Operand.m_pData, Operand.m_dwDataSize);
	}
	return *this;
}

CMediaData & CMediaData::operator = (CMediaData &&Operand) noexcept
{
	if (&Operand != this) {
		m_dwDataSize = Operand.m_dwDataSize;
		m_dwBuffSize = Operand.m_dwBuffSize;
		m_pData = Operand.m_pData;
		Operand.m_dwDataSize = 0;
		Operand.m_dwBuffSize = 0;
		Operand.m_pData = NULL;
	}
	return *this;
}

CMediaData & CMediaData::operator += (const CMediaData &Operand)
{
	AddData(Operand);
	return *this;
}

void CMediaData::SetAt(const DWORD dwPos, const BYTE byData)
{
	// 1バイトセットする
	if (dwPos < m_dwDataSize)
		m_pData[dwPos] = byData;
}

BYTE CMediaData::GetAt(const DWORD dwPos) const
{
	// 1バイト取得する
	return dwPos < m_dwDataSize ? m_pData[dwPos] : 0x00U;
}

DWORD CMediaData::SetData(const BYTE *pData, const DWORD dwDataSize)
{
	if (dwDataSize > 0) {
		// バッファ確保
		Reserve(dwDataSize);

		// データセット
		::CopyMemory(m_pData, pData, dwDataSize);
	}

	// サイズセット
	m_dwDataSize = dwDataSize;

	return m_dwDataSize;
}

DWORD CMediaData::AddData(const BYTE *pData, const DWORD dwDataSize)
{
	if (dwDataSize > 0) {
		// バッファ確保
		Reserve(m_dwDataSize + dwDataSize);

		// データ追加
		::CopyMemory(&m_pData[m_dwDataSize], pData, dwDataSize);

		// サイズセット
		m_dwDataSize += dwDataSize;
	}
	return m_dwDataSize;
}

DWORD CMediaData::AddData(const CMediaData& data)
{
	return AddData(data.m_pData, data.m_dwDataSize);
}

DWORD CMediaData::AddByte(const BYTE byData)
{
	// バッファ確保
	Reserve(m_dwDataSize + 1UL);

	// データ追加
	m_pData[m_dwDataSize] = byData;

	// サイズ更新
	m_dwDataSize++;

	return m_dwDataSize;
}

DWORD CMediaData::TrimHead(const DWORD dwTrimSize)
{
	// データ先頭を切り詰める
	if (m_dwDataSize == 0 || dwTrimSize == 0) {
		// 何もしない
	}
	else if (dwTrimSize >= m_dwDataSize) {
		// 全体を切り詰める
		m_dwDataSize = 0UL;
	}
	else {
		// データを移動する
		::MoveMemory(m_pData, m_pData + dwTrimSize, m_dwDataSize - dwTrimSize);
		m_dwDataSize -= dwTrimSize;
	}

	return m_dwDataSize;
}

DWORD CMediaData::TrimTail(const DWORD dwTrimSize)
{
	// データ末尾を切り詰める
	if (dwTrimSize >= m_dwDataSize) {
		// 全体を切り詰める
		m_dwDataSize = 0UL;
	}
	else {
		// データ末尾を切り詰める
		m_dwDataSize -= dwTrimSize;
	}

	return m_dwDataSize;
}

DWORD CMediaData::Reserve(const DWORD dwGetSize)
{
	if (dwGetSize <= m_dwBuffSize)
		return m_dwBuffSize;

	// 少なくとも指定サイズを格納できるバッファを確保する
	if (!m_pData) {
		// バッファ確保まだ
		DWORD dwBuffSize = max(dwGetSize, MINBUFSIZE);

		m_pData = static_cast<BYTE*>(malloc(dwBuffSize));
		if (m_pData)
			m_dwBuffSize = dwBuffSize;
	}
	else if (dwGetSize > m_dwBuffSize) {
		// 要求サイズはバッファサイズを超える
		DWORD dwBuffSize = dwGetSize;

		if (dwBuffSize < 0x100000UL) {
			if (dwBuffSize < m_dwDataSize * 2)
				dwBuffSize = m_dwDataSize * 2;
		}
		else {
			dwBuffSize = (dwBuffSize / 0x100000UL + 1) * 0x100000UL;
		}

		BYTE *pNewBuffer = static_cast<BYTE*>(realloc(m_pData, dwBuffSize));

		if (pNewBuffer != NULL) {
			m_dwBuffSize = dwBuffSize;
			m_pData = pNewBuffer;
		}
	}

	return m_dwBuffSize;
}

DWORD CMediaData::SetSize(const DWORD dwSetSize)
{
	if (dwSetSize > 0) {
		// バッファ確保
		Reserve(dwSetSize);
	}

	// サイズセット
	m_dwDataSize = dwSetSize;

	return m_dwDataSize;
}

DWORD CMediaData::SetSize(const DWORD dwSetSize, const BYTE byFiller)
{
	// サイズセット
	SetSize(dwSetSize);

	// データセット
	if (dwSetSize) {
		::FillMemory(m_pData, dwSetSize, byFiller);
	}

	return m_dwDataSize;
}

void CMediaData::ClearSize(void)
{
	// データサイズをクリアする
	m_dwDataSize = 0UL;
}

void CMediaData::ClearBuffer(void)
{
	m_dwDataSize = 0UL;
	m_dwBuffSize = 0UL;
	if (m_pData) {
		free(m_pData);
		m_pData = NULL;
	}
}
#pragma endregion

#pragma region CTsPacket

CTsPacket::CTsPacket()
{
	// 空のパケットを生成する
	memset(m_pData, 0x00, TSPARAM::TS_PACKETSIZE);
}

CTsPacket::CTsPacket(const BYTE *pHexData, DWORD* pdwRet)
{
	memcpy(m_pData, pHexData, TSPARAM::TS_PACKETSIZE);
	// バイナリデータからパケットを生成する
	DWORD dwRet = ParsePacket();
	if (pdwRet) {
		*pdwRet = dwRet;
	}
}

DWORD CTsPacket::ParsePacket(BYTE *pContinuityCounter)
{
	/*
	// TSパケットヘッダ解析
	m_Header.bySyncByte					= m_pData[0];							// +0
	m_Header.bTransportErrorIndicator	= (m_pData[1] & 0x80U) != 0;	// +1 bit7
	m_Header.bPayloadUnitStartIndicator	= (m_pData[1] & 0x40U) != 0;	// +1 bit6
	m_Header.TransportPriority			= (m_pData[1] & 0x20U) != 0;	// +1 bit5
	m_Header.wPID = ((WORD)(m_pData[1] & 0x1F) << 8) | (WORD)m_pData[2];		// +1 bit4-0, +2
	m_Header.byTransportScramblingCtrl	= (m_pData[3] & 0xC0U) >> 6;			// +3 bit7-6
	m_Header.byAdaptationFieldCtrl		= (m_pData[3] & 0x30U) >> 4;			// +3 bit5-4
	m_Header.byContinuityCounter		= m_pData[3] & 0x0FU;					// +3 bit3-0

	// アダプテーションフィールド解析
	::ZeroMemory(&m_AdaptationField, sizeof(m_AdaptationField));

	if (m_Header.byAdaptationFieldCtrl & 0x02) {
	// アダプテーションフィールドあり
	if (m_AdaptationField.byAdaptationFieldLength = m_pData[4]) {								// +4
	// フィールド長以降あり
	m_AdaptationField.bDiscontinuityIndicator	= (m_pData[5] & 0x80U) != 0;	// +5 bit7
	m_AdaptationField.bRamdomAccessIndicator	= (m_pData[5] & 0x40U) != 0;	// +5 bit6
	m_AdaptationField.bEsPriorityIndicator		= (m_pData[5] & 0x20U) != 0;	// +5 bit5
	m_AdaptationField.bPcrFlag					= (m_pData[5] & 0x10U) != 0;	// +5 bit4
	m_AdaptationField.bOpcrFlag					= (m_pData[5] & 0x08U) != 0;	// +5 bit3
	m_AdaptationField.bSplicingPointFlag		= (m_pData[5] & 0x04U) != 0;	// +5 bit2
	m_AdaptationField.bTransportPrivateDataFlag	= (m_pData[5] & 0x02U) != 0;	// +5 bit1
	m_AdaptationField.bAdaptationFieldExtFlag	= (m_pData[5] & 0x01U) != 0;	// +5 bit0

	if (m_pData[4] > 1U) {
	m_AdaptationField.pOptionData			= &m_pData[6];
	m_AdaptationField.byOptionSize			= m_pData[4] - 1U;
	}
	}
	}
	*/

	// ここで新規追加メソッドが正しいかチェックするコードを入れるよい

	int AdaptationCtrl = GetAdaptationFieldCtrl();

	// パケットのフォーマット適合性をチェックする
	if (GetSyncByte() != 0x47U)return EC_FORMAT;								// 同期バイト不正
	if (GetTransportErrorIndicator())return EC_TRANSPORT;						// ビット誤りあり
	if ((GetPID() >= 0x0002U) && (GetPID() <= 0x000FU))return EC_FORMAT;	// 未定義PID範囲
	if (GetTransportScramblingCtrl() == 0x01U)return EC_FORMAT;				// 未定義スクランブル制御値
	if (AdaptationCtrl == 0x00U)return EC_FORMAT;					// 未定義アダプテーションフィールド制御値
	if ((AdaptationCtrl == 0x02U) && (GetAdaptationFieldLength() > 183U))return EC_FORMAT;	// アダプテーションフィールド長異常
	if ((AdaptationCtrl == 0x03U) && (GetAdaptationFieldLength() > 182U))return EC_FORMAT;	// アダプテーションフィールド長異常

	if (!pContinuityCounter || GetPID() == 0x1FFFU)return EC_VALID;

	// 連続性チェック
	const BYTE byOldCounter = pContinuityCounter[GetPID()];
	const BYTE byNewCounter = (GetAdaptationFieldCtrl() & 0x01U) ? GetContinuityCounter() : 0x10U;
	pContinuityCounter[GetPID()] = byNewCounter;

	if (!GetDiscontinuityIndicator()) {
		if ((byOldCounter < 0x10U) && (byNewCounter < 0x10U)) {
			if (((byOldCounter + 1U) & 0x0FU) != byNewCounter) {
				return EC_CONTINUITY;
			}
		}
	}

	return EC_VALID;
}

BYTE * CTsPacket::GetPayloadData(void)
{
	// ペイロードポインタを返す
	switch (GetAdaptationFieldCtrl()) {
	case 1U:	// ペイロードのみ
		return &m_pData[4];

	case 3U:	// アダプテーションフィールド、ペイロードあり
		return &m_pData[GetAdaptationFieldLength() + 5U];

	default:	// アダプテーションフィールドのみ or 例外
		return NULL;
	}
}

const BYTE * CTsPacket::GetPayloadData(void) const
{
	// ペイロードポインタを返す
	switch (GetAdaptationFieldCtrl()) {
	case 1U:	// ペイロードのみ
		return &m_pData[4];

	case 3U:	// アダプテーションフィールド、ペイロードあり
		return &m_pData[GetAdaptationFieldLength() + 5U];

	default:	// アダプテーションフィールドのみ or 例外
		return NULL;
	}
}

const BYTE CTsPacket::GetPayloadSize(void) const
{
	// ペイロードサイズを返す
	switch (GetAdaptationFieldCtrl()) {
	case 1U:	// ペイロードのみ
		return (TSPARAM::TS_PACKETSIZE - 4U);

	case 3U:	// アダプテーションフィールド、ペイロードあり
		return (TSPARAM::TS_PACKETSIZE - GetAdaptationFieldLength() - 5U);

	default:	// アダプテーションフィールドのみ or 例外
		return 0U;
	}
}

#pragma endregion

std::pair<const BYTE*, int> CDescComponent::GetText() const
{
	return std::pair<const BYTE*, int>(&ptr[8], length() - 6);
}

std::pair<const BYTE*, int> CDescShortEvent::GetName() const
{
	return std::pair<const BYTE*, int>(&ptr[6], ptr[5]);
}

std::pair<const BYTE*, int> CDescShortEvent::GetText() const
{
	return std::pair<const BYTE*, int>(&ptr[7 + ptr[5]], ptr[6 + ptr[5]]);
}

std::pair<const BYTE*, int> CDescAudioComponent::GetText() const
{
	DWORD len;
	const BYTE* pos;
	if (GetESMultiLingualFlag()) {
		len = length() - 12;
		pos = &ptr[14];
	}
	else {
		len = length() - 9;
		pos = &ptr[11];
	}
	return std::pair<const BYTE*, int>(pos, len);
}

std::pair<const BYTE*, int> CDescService::GetProviderName() const
{
	return std::pair<const BYTE*, int>(&ptr[4], ptr[3]);
}

std::pair<const BYTE*, int> CDescService::GetServiceName() const
{
	return std::pair<const BYTE*, int>(&ptr[5 + ptr[3]], ptr[4 + ptr[3]]);
}

std::pair<const BYTE*, int> CDescTsInfomation::GetTsName() const
{
	return std::pair<const BYTE*, int>(&ptr[4], BIT_SHIFT_MASK(ptr[3], 2, 6));
}

DWORD CDescSateliteDeliverySystem::GetFrequency() const
{
	/*return  (ptr[2] >> 4)*10000000 + (ptr[2] & 0x0F)*1000000 +
	(ptr[3] >> 4)*100000 + (ptr[3] & 0x0F)*10000 +
	(ptr[4] >> 4)*1000 + (ptr[4] & 0x0F)*100 +
	(ptr[5] >> 4)*10 + (ptr[5] & 0x0F);*/
	return ReadBcd<8>(&ptr[2]);
}

DWORD CDescSateliteDeliverySystem::GetSymbolRate() const
{
	/*return  (ptr[9] >> 4)*1000000 + (ptr[9] & 0x0F)*100000 +
	(ptr[10] >> 4)*10000 + (ptr[10] & 0x0F)*1000 +
	(ptr[11] >> 4)*100 + (ptr[11] & 0x0F)*10 +
	(ptr[12] >> 4);*/
	return ReadBcd<7>(&ptr[9]);
}

__int64 CDescSIParam::GetUpdateTime() const
{
	__int64 time;
	if (!CAribTime::AribMjdToInt64(&ptr[3], &time)) {
		return 0;
	}
	return time;
}

// CDescDispatch

CDescDispatch::DISPATCH_FUNCTION CDescDispatch::DispatchTable[0x100];
const CDescDispatch::CInitializer CDescDispatch::Initializer;

void CDescDispatch::DispatchBlock(CDescHandler *pHandler, const BYTE *pDesc, DWORD dwDescLen)
{
	const BYTE* ptr = pDesc;
	const BYTE* ptrEnd = pDesc + dwDescLen;
	// descriptor_length分はデータがあることを保証する
	while (ptr + 2 <= ptrEnd && ptr + 2 + ptr[1] <= ptrEnd) {
		// ディスパッチ
		if (DispatchTable[ptr[0]]) {
			(DispatchTable[ptr[0]])(pHandler, ptr);
		}
		ptr += 2 + ptr[1];
	}

	if (ptr != ptrEnd) {
		OutputDebugString(L"ディスクリプタの長さが不正");
	}
}

CDescDispatch::CInitializer::CInitializer()
{
	memset(DispatchTable, 0x00, sizeof(DispatchTable));

	DispatchTable[CDescCAMethod::TAG] = OnCAMethod;
	DispatchTable[CDescServiceList::TAG] = OnServiceList;
	DispatchTable[CDescService::TAG] = OnService;
	DispatchTable[CDescShortEvent::TAG] = OnShortEvent;
	DispatchTable[CDescExtEvent::TAG] = OnExtEvent;
	DispatchTable[CDescComponent::TAG] = OnComponent;
	DispatchTable[CDescStreamIdentifier::TAG] = OnStreamIdentifier;
	DispatchTable[CDescContent::TAG] = OnContent;
	DispatchTable[CDescDigitalCopyControl::TAG] = OnDigitalCopyControl;
	DispatchTable[CDescAudioComponent::TAG] = OnAudioComponent;
	DispatchTable[CDescTsInfomation::TAG] = OnTsInfomation;
	DispatchTable[CDescEventGroup::TAG] = OnEventGroup;
	DispatchTable[CDescSIParam::TAG] = OnSIParam;
}

void CDescDispatch::OnCAMethod(CDescHandler* pHandler, const BYTE* pDesc)
{
	CDescCAMethod Desc(pDesc);
	//if( Desc.Check() ){
	pHandler->OnCAMethod(Desc);
	//}
}

void CDescDispatch::OnServiceList(CDescHandler* pHandler, const BYTE* pDesc)
{
	CDescServiceList Desc(pDesc);
	//if( Desc.Check() ){
	pHandler->OnServiceList(Desc);
	//}
}

void CDescDispatch::OnService(CDescHandler* pHandler, const BYTE* pDesc)
{
	CDescService Desc(pDesc);
	//if( Desc.Check() ){
	pHandler->OnService(Desc);
	//}
}

void CDescDispatch::OnShortEvent(CDescHandler* pHandler, const BYTE* pDesc)
{
	CDescShortEvent Desc(pDesc);
	//if( Desc.Check() ){
	pHandler->OnShortEvent(Desc);
	//}
}

void CDescDispatch::OnExtEvent(CDescHandler* pHandler, const BYTE* pDesc)
{
	CDescExtEvent Desc(pDesc);
	//if( Desc.Check() ){
	pHandler->OnExtEvent(Desc);
	//}
}

void CDescDispatch::OnComponent(CDescHandler* pHandler, const BYTE* pDesc)
{
	CDescComponent Desc(pDesc);
	//if( Desc.Check() ){
	pHandler->OnComponent(Desc);
	//}
}

void CDescDispatch::OnStreamIdentifier(CDescHandler* pHandler, const BYTE* pDesc)
{
	CDescStreamIdentifier Desc(pDesc);
	//if( Desc.Check() ){
	pHandler->OnStreamIdentifier(Desc);
	//}
}

void CDescDispatch::OnContent(CDescHandler* pHandler, const BYTE* pDesc)
{
	CDescContent Desc(pDesc);
	//if( Desc.Check() ){
	pHandler->OnContent(Desc);
	//}
}

void CDescDispatch::OnDigitalCopyControl(CDescHandler* pHandler, const BYTE* pDesc)
{
	CDescDigitalCopyControl Desc(pDesc);
	//if( Desc.Check() ){
	pHandler->OnDigitalCopyControl(Desc);
	//}
}

void CDescDispatch::OnAudioComponent(CDescHandler* pHandler, const BYTE* pDesc)
{
	CDescAudioComponent Desc(pDesc);
	//if( Desc.Check() ){
	pHandler->OnAudioComponent(Desc);
	//}
}

void CDescDispatch::OnTsInfomation(CDescHandler* pHandler, const BYTE* pDesc)
{
	CDescTsInfomation Desc(pDesc);
	//if( Desc.Check() ){
	pHandler->OnTsInfomation(Desc);
	//}
}

void CDescDispatch::OnEventGroup(CDescHandler* pHandler, const BYTE* pDesc)
{
	CDescEventGroup Desc(pDesc);
	//if( Desc.Check() ){
	pHandler->OnEventGroup(Desc);
	//}
}

void CDescDispatch::OnSIParam(CDescHandler* pHandler, const BYTE* pDesc)
{
	CDescSIParam Desc(pDesc);
	//if( Desc.Check() ){
	pHandler->OnSIParam(Desc);
	//}
}


#pragma region TSTable

/////////////////////////////////////////////////////////////////////////////
// PAT テーブル
/////////////////////////////////////////////////////////////////////////////

CSiSectionPAT::CSiSectionPAT(CMediaData* Sec_)
	: CSiSection(Sec_)
{
}

bool CSiSectionPAT::Check()
{
	if (GetSectionNumber() == 0 && GetLastSectionNumber() == 0 && CalcCRC() == 0) {
		// 4の倍数？
		if ((m_dwDataSize - 8) & 0x03) return false;
		ProgramCount = (int)(m_dwDataSize - 12) / 4;
		return true;
	}
	return false;
}

bool CSiSectionPAT::Parse()
{
	return true;
}

CSiSectionPAT::CProg CSiSectionPAT::GetProgramInfo(int n) const
{
	if (n < 0 || n >= ProgramCount) {
		_OutputDebugString(L"Error: Index out of range: %s:%d", __FILE__, __LINE__);
	}
	return CProg(&m_pData[8 + n * 4]);
}

/////////////////////////////////////////////////////////////////////////////
// EIT テーブル
/////////////////////////////////////////////////////////////////////////////

CSiSectionEIT::CSiSectionEIT(CMediaData* Sec_)
	: CSiSection(Sec_)
{
}

bool CSiSectionEIT::Check()
{
	return CalcCRC() == 0;
}

bool CSiSectionEIT::Parse()
{
	const BYTE* ptr = m_pData + 8 + 6;
	// -4 は CRC分
	const BYTE* SecitonEnd = GetSectionEnd();
	const BYTE* ptrEnd = SecitonEnd - 4;
	while (ptr < ptrEnd) {
		EventList.push_back(CEvent(ptr));
		if (ptr + 12 >= SecitonEnd) return false;
		WORD blen = 12 + Readin16<12>(&ptr[10]);
		ptr += blen;
	}
	if (ptr + 4 != SecitonEnd) return false;
	return true;
}

CSiSectionEIT::CEvent CSiSectionEIT::GetEventInfo(int n)
{
	if (n < 0 || n >= (int)EventList.size()) {
		_OutputDebugString(L"Error: Index out of range: %s:%d", __FILE__, __LINE__);
	}
	return EventList[n];
}

__int64 CSiSectionEIT::CEvent::GetStartTime()
{
	__int64 time;
	if (!CAribTime::AribToInt64(&ptr[2], &time)) {
		return 0;
	}
	return time;
}

DWORD CSiSectionEIT::CEvent::GetDuration()
{
	return CAribTime::AribBcdToSecond(&ptr[7]);
}

#pragma endregion

#pragma region CEitDetector

/////////////////////////////////////////////////////////////////////////////
// CEitConverter
/////////////////////////////////////////////////////////////////////////////

SYSTEMTIME GetSystemTimeFromInt64(__int64 i64) {
	FILETIME ftime;
	ftime.dwHighDateTime = i64 >> 32;
	ftime.dwLowDateTime = (DWORD)i64;
	SYSTEMTIME systime;
	FileTimeToSystemTime(&ftime, &systime);
	return systime;
}

void CEitConverter::Feed(CSiSectionEIT& eit, int idx, EPGDB_EVENT_INFO* dest_) {
	this->dest = dest_;

	CSiSectionEIT::CEvent ev = eit.GetEventInfo(idx);

	dest->original_network_id = eit.GetOriginalNID();
	dest->transport_stream_id = eit.GetTSID();
	dest->service_id = eit.GetServiceID();
	dest->event_id = ev.GetEventID();

	int64_t startTime = ev.GetStartTime();
	if( (dest->StartTimeFlag = (startTime > 0)) != 0 ){
		dest->start_time = GetSystemTimeFromInt64(startTime);
	}

	DWORD duration = ev.GetDuration();
	if( (dest->DurationFlag = (duration > 0)) != 0 ){
		dest->durationSec = duration;
	}

	dest->freeCAFlag = static_cast<BYTE>(ev.GetFreeCAMode());

	// 記述子読み込み
	CDescDispatch::DispatchBlock(this, ev.GetDescData(), ev.GetDescSize());
}

void CEitConverter::OnShortEvent(CDescShortEvent& desc)
{
	if (dest->shortInfo) return;
	dest->shortInfo.reset(new EPGDB_SHORT_EVENT_INFO);
	dest->search_event_name.clear();
	dest->shortInfo->event_name = arib.DecodeString(desc.GetName());
	//ごく稀にAPR(改行)を含むため
	Replace(dest->shortInfo->event_name, L"\r\n", L"");
	dest->shortInfo->text_char = arib.DecodeString(desc.GetText());
}

void CEitConverter::OnExtEvent(CDescExtEvent& desc)
{
	class ExtEventHandler
	{
	public:
		ExtEventHandler(std::wstring& text, EP3AribStrinbDecoder& arib) : text(text), arib(arib) { }
		std::wstring& text;
		EP3AribStrinbDecoder& arib;

		void OnItemDescription(const std::pair<const BYTE*, int>& input)
		{
			text += arib.DecodeString(input);
			text += L"\r\n";
		}
		void OnItemText(const std::pair<const BYTE*, int>& input)
		{
			text += arib.DecodeString(input);
			text += L"\r\n";
		}
		void OnText(const std::pair<const BYTE*, int>& input)
		{
			text += arib.DecodeString(input);
			text += L"\r\n";
		}
	};

	//DebugPrintln(L"Ext Num:%d(Last:%d)",
	//	desc.GetDescriptorNumber(),
	//	desc.GetLastDescriptorNumber());

	if (dest->extInfo) return;
	dest->extInfo.reset(new EPGDB_EXTENDED_EVENT_INFO);
	dest->search_event_name.clear();
	ExtEventHandler Handler(dest->extInfo->text_char, arib);
	desc.ParseText(Handler);
}

void CEitConverter::OnComponent(CDescComponent& desc)
{
	if (dest->componentInfo) return;
	dest->componentInfo.reset(new EPGDB_COMPONENT_INFO);
	dest->componentInfo->stream_content = desc.GetStreamContent();
	dest->componentInfo->component_type = desc.GetComponentType();
	dest->componentInfo->component_tag = desc.GetComponentTag();
	dest->componentInfo->text_char = arib.DecodeString(desc.GetText());
}

void CEitConverter::OnContent(CDescContent& desc)
{
	if (dest->contentInfo) return;
	dest->contentInfo.reset(new EPGDB_CONTEN_INFO);
	int DataCnt = desc.GetDataCount();
	for (int i = 0; i < DataCnt; i++) {
		EPGDB_CONTENT_DATA item;
		const auto& data = desc.GetData(i);
		item.content_nibble_level_1 = data.nibble.content_nibble_level_1;
		item.content_nibble_level_2 = data.nibble.content_nibble_level_2;
		item.user_nibble_1 = data.nibble.user_nibble1;
		item.user_nibble_2 = data.nibble.user_nibble2;
		dest->contentInfo->nibbleList.push_back(item);
	}
}

void CEitConverter::OnAudioComponent(CDescAudioComponent& desc)
{
	//if (dest->audioInfo == NULL) {
	//	dest->audioInfo = new EPGDB_AUDIO_COMPONENT_INFO;
	//}
	dest->audioInfo.reset(new EPGDB_AUDIO_COMPONENT_INFO);
	EPGDB_AUDIO_COMPONENT_INFO_DATA item;
	item.stream_content = desc.GetStreamContent();
	item.component_type = desc.GetComponentType();
	item.component_tag = desc.GetComponentTag();
	item.stream_type = desc.GetStreamType();
	item.simulcast_group_tag = desc.GetSimulcastGroupTag();
	item.ES_multi_lingual_flag = static_cast<BYTE>(desc.GetESMultiLingualFlag());
	item.main_component_flag = static_cast<BYTE>(desc.GetMainComponentFlag());
	item.quality_indicator = desc.GetQualityIndicator();
	item.sampling_rate = desc.GetSamplingRate();
	item.text_char = arib.DecodeString(desc.GetText());
	dest->audioInfo->componentList.push_back(item);
}

void CEitConverter::OnEventGroup(CDescEventGroup& desc)
{
	if (dest->eventGroupInfo) return;
	dest->eventGroupInfo.reset(new EPGDB_EVENTGROUP_INFO);
	dest->eventGroupInfo->group_type = desc.GetGroupType();
	int DataCnt = desc.GetEventCount();
	for (int i = 0; i < DataCnt; i++) {
		EPGDB_EVENT_DATA item;
		const auto& data = desc.GetEvent(i);
		item.original_network_id = dest->original_network_id;
		item.transport_stream_id = dest->transport_stream_id;
		item.service_id = data.word_data.service_id;
		item.event_id = data.word_data.event_id;
		dest->eventGroupInfo->eventDataList.push_back(item);
	}
}

/////////////////////////////////////////////////////////////////////////////
// CEitDetector
/////////////////////////////////////////////////////////////////////////////

CEitDetector::CEitDetector()
	: m_Detected(false)
	, m_EventInfo(NULL)
{
	m_PATParser.SetHandler(OnPAT_, this);
	for (int i = 0; i < 3; ++i) {
		m_EITParser[i].SetHandler(OnEIT_, this);
	}
}

CEitDetector::~CEitDetector()
{
}

void CEitDetector::Reset()
{
	m_Detected = false;
	m_ProgTbl.Version = -1;
	m_PATParser.Reset();
	m_EITParser[0].Reset();
	m_EITParser[1].Reset();
	m_EITParser[2].Reset();
}

void CEitDetector::StorePacket(CTsPacket* pPacket)
{
	int pid = pPacket->GetPID();
	CSiSectionParserImpl<true, true>* parser = NULL;

	switch (pid) {
	case 0x0000:
		parser = &m_PATParser;
		break;
	case 0x0012:
		parser = &m_EITParser[0];
		break;
	case 0x0026:
		parser = &m_EITParser[1];
		break;
	case 0x0027:
		parser = &m_EITParser[2];
		break;
	}

	if (parser != NULL) {
		parser->StorePacket(pPacket);

		OutputPacket(pPacket);
	}
}

void CEitDetector::OnPAT_(void* context, CSiSectionParser *pSiSectionParser, CMediaData *pSection)
{
	CEitDetector* pThis = static_cast<CEitDetector*>(context);
	pThis->OnPAT(pSiSectionParser, pSection);
}

void CEitDetector::OnPAT(CSiSectionParser *pSiSectionParser, CMediaData *pSection)
{
	if (m_EventInfo == NULL || m_Detected) return;

	if (CSiSection<true, true>(pSection).GetTableID() != 0x00) return;
	CSiSectionPAT pat(pSection);
	if (pat.Check()
		&& m_ProgTbl.Version != pat.GetVersion()
		&& pat.Parse())
	{
		m_PATData.SetData(pSection->GetData(), pSection->GetSize());

		m_ProgTbl.TSID = pat.GetTSID();
		m_ProgTbl.Version = pat.GetVersion();

		//DebugPrintln(L"PAT Recieved. Program Count:%d", m_NextProgTbl.ProgramItems.size());
	}
}

void CEitDetector::OnEIT_(void* context, CSiSectionParser *pSiSectionParser, CMediaData *pSection)
{
	CEitDetector* pThis = static_cast<CEitDetector*>(context);
	pThis->OnEIT(pSiSectionParser, pSection);
}

static void DecodeStringCB(const WCHAR* ptr, void* ctx) {
	std::wstring* dst = (std::wstring*)ctx;
	*dst = ptr;
}

std::wstring EP3AribStrinbDecoder::DecodeString(const std::pair<const BYTE*, int>& input) {
	std::wstring ret;
	pfnDecodeARIBCharactersEP3(input.first, input.second, DecodeStringCB, &ret);
	return ret;
}

void CEitDetector::OnEIT(CSiSectionParser *pSiSectionParser, CMediaData *pSection)
{
	// まだPATを受信してない
	if (m_ProgTbl.Version == -1) return;

	if (m_EventInfo == NULL || m_Detected) return;

	CSiSectionEIT eit(pSection);
	if (eit.Check() && eit.Parse()) {
		if (eit.GetTableID() == 0x4E) { // 自ストリームの現在と次の番組
			int serviceId = eit.GetServiceID();
			if (serviceId == m_TargetServiceId) {
				for (int i = 0; i < eit.GetEventListCount(); ++i) {
					if (m_TargetEventId == eit.GetEventInfo(i).GetEventID()) {
						m_EITData.SetData(pSection->GetData(), pSection->GetSize());
						m_EITConverter.Feed(eit, i, m_EventInfo);
						m_Detected = true;
					}
				}
			}
		}
	}
}

#pragma endregion

#pragma region CSiSection

#pragma warning(push)
#pragma warning(disable:4127) // 条件式が定数です。

template<bool m_bTargetExt, bool m_bTR>
bool CSiSection<m_bTargetExt, m_bTR>::CheckHeader()
{
	const DWORD dwHeaderSize = (m_bTargetExt) ? 8UL : 3UL;

	// ヘッダサイズチェック
	if (m_dwDataSize < dwHeaderSize) {
		//DebugPrintln(L"ヘッダサイズエラー");
		return false;
	}

	// ヘッダのフォーマット適合性をチェックする
	// reserved は 1を送出するようになっているが、受信機は無視しなければならない！
	const WORD wSectionLength = GetSectionLength();
	//if((m_pData[1] & 0x30U) != 0x30U)return false;										// 固定ビット異常
	if (wSectionLength > 4093U) {
		//DebugPrintln(L"セクション長異常");
		return false;
	}
	if (IsExtendedSection()) {
		// 拡張形式のエラーチェック
		if (!m_bTargetExt) {
			//DebugPrintln(L"目的のヘッダではない");
			return false;
		}
		//else if((m_pData[5] & 0xC0U) != 0xC0U)return false;								// 固定ビット異常
		else if (GetSectionNumber() > GetLastSectionNumber()) {
			//DebugPrintln(L"セクション番号異常");
			return false;
		}
		else if (wSectionLength < 9U) {
			//DebugPrintln(L"セクション長異常");
			return false;
		}
		if (m_bTR) {
			if (!IsCurrentNext()) {
				//DebugPrintln(L"current_next_indicator が1でない");
				return false;
			}
		}
	}
	else {
		// 標準形式のエラーチェック	
		if (m_bTargetExt) {
			//DebugPrintln(L"目的のヘッダではない");
			return false;
		}
		else if (wSectionLength < 4U) {
			//DebugPrintln(L"セクション長異常");
			return false;
		}
	}

	return true;
}

template<bool m_bTargetExt, bool m_bTR>
DWORD CSiSection<m_bTargetExt, m_bTR>::CalcCRC(void) const
{
	// CRCを計算
	DWORD DataLength = GetSectionLength() + 3UL;
	if (DataLength > m_dwDataSize) {
		DataLength = m_dwDataSize;
	}
	return CCrcCalculator::CalcCrc32(m_pData, DataLength);
}

#pragma warning(pop)

// 明示的なインスタンス化
template CSiSection<true, true>;
template CSiSection<false, true>;

#pragma endregion	

#pragma region CSiSectionParser

//////////////////////////////////////////////////////////////////////
// CSiSectionParserクラス
//////////////////////////////////////////////////////////////////////

CSiSectionParser::CSiSectionParser(PSISECTION_CALLBACK cbFunc, void* context, bool bAutoDelete)
	: m_Context(context)
	, m_cbFunc(cbFunc)
	, m_SiSection(0x10002UL)		// PSIセクション最大サイズのバッファ確保
	, m_bIsStoring(false)
	, m_dwCrcErrorCount(0UL)
{

}

template<bool m_bTargetExt, bool m_bTR>
CSiSectionParserImpl<m_bTargetExt, m_bTR>::CSiSectionParserImpl(PSISECTION_CALLBACK cbFunc, void* context, bool bAutoDelete)
	: CSiSectionParser(cbFunc, context, bAutoDelete)
{

}

CSiSectionParser::CSiSectionParser(const CSiSectionParser &Operand)
{
	*this = Operand;
}

CSiSectionParser & CSiSectionParser::operator = (const CSiSectionParser &Operand)
{
	if (&Operand != this) {
		// インスタンスのコピー
		m_cbFunc = Operand.m_cbFunc;
		m_Context = Operand.m_Context;
		m_SiSection = Operand.m_SiSection;
		m_bIsStoring = Operand.m_bIsStoring;
		m_wStoreSize = Operand.m_wStoreSize;
		m_dwCrcErrorCount = Operand.m_dwCrcErrorCount;
	}

	return *this;
}

void CSiSectionParser::SetHandler(PSISECTION_CALLBACK cbFunc, void* context)
{
	m_cbFunc = cbFunc;
	m_Context = context;
}

template<bool m_bTargetExt, bool m_bTR>
bool CSiSectionParserImpl<m_bTargetExt, m_bTR>::StorePacket(CTsPacket *pPacket)
{
	const BYTE *pData = pPacket->GetPayloadData();
	const BYTE bySize = pPacket->GetPayloadSize();
	if (!bySize || !pData)
		return false;

	const BYTE byUnitStartPos = (pPacket->GetPayloadUnitStartIndicator()) ? (pData[0] + 1U) : 0U;

	if (byUnitStartPos) {
		// [ヘッダ断片 | ペイロード断片] + [スタッフィングバイト] + ヘッダ先頭 + [ヘッダ断片] + [ペイロード断片] + [スタッフィングバイト]
		BYTE byPos = 1U;

		if (byUnitStartPos > 1U) {
			// ユニット開始位置が先頭ではない場合(断片がある場合)
			byPos += StoreHeader(&pData[byPos], bySize - byPos);
			byPos += StorePayload(&pData[byPos], bySize - byPos);
		}

		// ユニット開始位置から新規セクションのストアを開始する
		m_bIsStoring = false;
		m_SiSection.ClearSize();

		byPos = byUnitStartPos;
		byPos += StoreHeader(&pData[byPos], bySize - byPos);
		byPos += StorePayload(&pData[byPos], bySize - byPos);
	}
	else {
		// [ヘッダ断片] + ペイロード + [スタッフィングバイト]
		BYTE byPos = 0U;
		byPos += StoreHeader(&pData[byPos], bySize - byPos);
		byPos += StorePayload(&pData[byPos], bySize - byPos);
	}

	return true;
}

void CSiSectionParser::Reset(void)
{
	// 状態を初期化する
	m_bIsStoring = false;
	m_wStoreSize = 0U;
	m_dwCrcErrorCount = 0UL;
	m_SiSection.ClearSize();
}

const DWORD CSiSectionParser::GetCrcErrorCount(void)
{
	// CRCエラー回数を返す
	return m_dwCrcErrorCount;
}

// m_bTargetExt, m_bTR
template<bool m_bTargetExt, bool m_bTR>
const BYTE CSiSectionParserImpl<m_bTargetExt, m_bTR>::StoreHeader(const BYTE *pPayload, const BYTE byRemain)
{
	// ヘッダを解析してセクションのストアを開始する
	if (m_bIsStoring)
		return 0U;

	const BYTE byHeaderSize = (m_bTargetExt) ? 8U : 3U;
	const BYTE byHeaderRemain = byHeaderSize - (BYTE)m_SiSection.GetSize();

	if (byRemain >= byHeaderRemain) {
		// ヘッダストア完了、ヘッダを解析してペイロードのストアを開始する
		m_SiSection.AddData(pPayload, byHeaderRemain);
		if (CSiSection<m_bTargetExt, m_bTR>(&m_SiSection).CheckHeader()) {
			// ヘッダフォーマットOK
			m_wStoreSize = CSiSection<m_bTargetExt, m_bTR>(&m_SiSection).GetSectionLength() + 3U;
			m_bIsStoring = true;
			return byHeaderRemain;
		}
		else {
			// ヘッダエラー
			m_SiSection.ClearSize();
			return byRemain;
		}
	}
	else {
		// ヘッダストア未完了、次のデータを待つ
		m_SiSection.AddData(pPayload, byRemain);
		return byRemain;
	}
}

const BYTE CSiSectionParser::StorePayload(const BYTE *pPayload, const BYTE byRemain)
{
	// セクションのストアを完了する
	if (!m_bIsStoring)
		return 0U;

	const WORD wStoreRemain = m_wStoreSize - (WORD)m_SiSection.GetSize();

	if (wStoreRemain <= (WORD)byRemain) {
		// ストア完了
		m_SiSection.AddData(pPayload, wStoreRemain);

		if (m_cbFunc) {
			m_cbFunc(m_Context, this, &m_SiSection);
		}

		// 状態を初期化し、次のセクション受信に備える
		m_SiSection.ClearSize();
		m_bIsStoring = false;

		return (BYTE)wStoreRemain;
	}
	else {
		// ストア未完了、次のペイロードを待つ
		m_SiSection.AddData(pPayload, byRemain);
		return byRemain;
	}
}

// 明示的なインスタンス化
template CSiSectionParserImpl<true, true>;
template CSiSectionParserImpl<false, true>;
// 以下必要ない
// template CSiSectionParserImpl<true, false>;
// template CSiSectionParserImpl<false, false>;

#pragma endregion
