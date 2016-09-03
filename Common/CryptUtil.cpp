#include "stdafx.h"

#include "CryptUtil.h"
#include "StringUtil.h"
#include <wincrypt.h>

#pragma comment(lib, "advapi32.lib")
#pragma comment(lib, "crypt32.lib")

// template<class T>Base64Encode用テーブル
const char CCryptUtil::base64_encode[66] = {
	'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
	'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
	'U', 'V', 'W', 'X', 'Y', 'Z', 'a', 'b', 'c', 'd',
	'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n',
	'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x',
	'y', 'z', '0', '1', '2', '3', '4', '5', '6', '7',
	'8', '9', '+', '/', '='
};

// template<class T>Base64Decode用テーブル
const char CCryptUtil::base64_decode[256] = {
	-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
	-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
	-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,62,-1,-1,-1,63,		// +, /
	52,53,54,55,56,57,58,59, 60,61,-1,-1,-1,64,-1,-1,		// 0-9, =
	-1, 0, 1, 2, 3, 4, 5, 6,  7, 8, 9,10,11,12,13,14,		// A-Z
	15,16,17,18,19,20,21,22, 23,24,25,-1,-1,-1,-1,-1,
	-1,26,27,28,29,30,31,32, 33,34,35,36,37,38,39,40,		// a-z
	41,42,43,44,45,46,47,48, 49,50,51,-1,-1,-1,-1,-1,
	-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
	-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
	-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
	-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
	-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
	-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
	-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
	-1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1
};

BOOL CCryptUtil::Create(const wstring& base64string)
{
	string utf8;
	WtoUTF8(base64string, utf8);
	return Create(utf8);
}

BOOL CCryptUtil::Create(const string& base64string)
{
	if (base64string.empty())
		return FALSE;

	BOOL ret = CryptAcquireContext(&m_hProv, NULL, NULL, PROV_RSA_AES, CRYPT_VERIFYCONTEXT);
	if (ret) {
		// パスワード長が64バイトを超えると HMAC の初期値の事前計算が出来なくなるので、
		// SHA-256 でハッシュした32バイトを使うことにする。
		// ipad & opad にパスワードが保存されるのを防ぐ効果もある。
		HCRYPTHASH  hHash = NULL;
		string password;
		BYTE tmp[32] = { 0 };
		DWORD size = sizeof(tmp);
		ret = CryptCreateHash(m_hProv, CALG_SHA_256, 0, 0, &hHash) &&
			Decrypt(base64string, password) &&
			CryptHashData(hHash, (const BYTE*)password.data(), (DWORD)password.size(), 0) &&
			CryptGetHashParam(hHash, HP_HASHVAL, tmp, &size, 0);
		if (hHash) {
			CryptDestroyHash(hHash);
		}
		ret = ret && CreateHmac(tmp, size);
	}
	return ret;
}

BOOL CCryptUtil::CreateHmac(const BYTE *pKey, const DWORD cbKey)
{
	BYTE tmp[64] = { 0 }; // SHA512(64バイト)まで対応
	DWORD size = sizeof(tmp);

	BOOL ret = FALSE;

	if (m_hProv != NULL && pKey != NULL && cbKey != 0) {
		if (cbKey > 64) {
			// key が64バイトを超える場合は key の hash 値を使う
			// hash 値を計算するために SelectHash しておく必要がある。
			HCRYPTHASH  hHash = NULL;
			ret = (CryptCreateHash(m_hProv, m_algid, 0, 0, &hHash) &&
				CryptHashData(hHash, pKey, cbKey, 0) &&
				CryptGetHashParam(hHash, HP_HASHVAL, tmp, &size, 0));
			if (hHash) {
				CryptDestroyHash(hHash);
			}
		}
		else
		{
			// key が64バイト未満の場合は64バイトまで 0 fill した値を使う
			memcpy(tmp, pKey, cbKey);
			ret = TRUE;
		}
	}
	if (ret) {
		// ipad & opad を計算する
		C_ASSERT(_countof(m_ipad) == _countof(tmp));
		C_ASSERT(_countof(m_opad) == _countof(tmp));
		for (int i = 0; i < sizeof(tmp); i++) {
			m_ipad[i] = tmp[i] ^ 0x36;
			m_opad[i] = tmp[i] ^ 0x5c;
		}
	}
	return ret;
}

VOID CCryptUtil::Close()
{
	if (m_hHash){
		CryptDestroyHash(m_hHash);
		m_hHash = NULL;
	}
	if (m_hProv) {
		CryptReleaseContext(m_hProv, 0);
		m_hProv = NULL;
	}
}

BOOL CCryptUtil::SelectHash(ALG_ID id)
{
	m_hashSize = 0;
	if (m_hHash != NULL)
		return FALSE;

	switch (id) {
	case CALG_MD5: m_hashSize = 128 / 8; break;
	case CALG_SHA1: m_hashSize = 160 / 8; break;
	case CALG_SHA_256: m_hashSize = 256 / 8; break;
	case CALG_SHA_384: m_hashSize = 384 / 8; break;
	case CALG_SHA_512: m_hashSize = 512 / 8; break;
	default: return FALSE;
	}
	m_algid = id;
	return TRUE;
}

BOOL CCryptUtil::SelectHash(DWORD hashSize)
{
	m_hashSize = 0;
	if (m_hHash != NULL)
		return FALSE;

	switch (hashSize) {
	case 128 / 8: m_algid = CALG_MD5; break;
	case 160 / 8: m_algid = CALG_SHA1; break;
	case 256 / 8: m_algid = CALG_SHA_256; break;
	case 384 / 8: m_algid = CALG_SHA_384; break;
	case 512 / 8: m_algid = CALG_SHA_512; break;
	default: return FALSE;
	}
	m_hashSize = hashSize;
	return TRUE;
}

BOOL CCryptUtil::CalcHmac(const BYTE *pbData, DWORD cbData)
{
	if (m_hProv == NULL || m_hashSize == 0)
		return FALSE;

	BOOL ret = TRUE;

	// ipad + data の hash の計算
	if (m_hHash == NULL) {
		ret = CryptCreateHash(m_hProv, m_algid, 0, 0, &m_hHash) &&
			CryptHashData(m_hHash, m_ipad, sizeof(m_ipad), 0);
	}
	if (m_hHash != NULL && ret == TRUE) {
		ret = CryptHashData(m_hHash, pbData, cbData, 0);
	}
	if (m_hHash != NULL && ret == FALSE) {
		CryptDestroyHash(m_hHash);
		m_hHash = NULL;
	}
	return ret;
}

BOOL CCryptUtil::GetHmac(BYTE *pbData, DWORD cbData)
{
	if (m_hProv == NULL || m_hHash == NULL || m_hashSize == 0 || cbData < m_hashSize) {
		return FALSE;
	}

	// ipad + data の hash を取得する
	BOOL ret = CryptGetHashParam(m_hHash, HP_HASHVAL, pbData, &cbData, 0);
	CryptDestroyHash(m_hHash);
	m_hHash = NULL;

	// opad + (ipad + data の hash) の hash(HMAC) を求める 
	HCRYPTHASH  hHash = NULL;
	ret = ret && (CryptCreateHash(m_hProv, m_algid, 0, 0, &hHash) &&
		CryptHashData(hHash, m_opad, sizeof(m_opad), 0) &&
		CryptHashData(hHash, pbData, cbData, 0) &&
		CryptGetHashParam(hHash, HP_HASHVAL, pbData, &cbData, 0));
	if (hHash) {
		CryptDestroyHash(hHash);
	}
	return ret;
}

BOOL CCryptUtil::CompareHmac(const BYTE *pbData)
{
	BYTE tmp[64] = { 0 }; // SHA512(64バイト)まで対応
	return GetHmac(tmp, sizeof(tmp)) && memcmp(pbData, tmp, m_hashSize) == 0;
}

BOOL CCryptUtil::Encrypt(const wstring& input_string, wstring& output_string, DWORD flags)
{
	string utf8;
	WtoUTF8(input_string, utf8);
	return Encrypt(utf8, output_string, flags);
}

BOOL CCryptUtil::Decrypt(const wstring& input_string, wstring& output_string, DWORD flags)
{
	string utf8;
	BOOL ret = Decrypt(input_string, utf8, flags);
	if (ret) {
		UTF8toW(utf8, output_string);
	}
	return ret;
}
