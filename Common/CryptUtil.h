#pragma once

#include <wincrypt.h>

#define MAX_PASSWORD_LENGTH  64

class CCryptUtil
{
	static const char base64_encode[66];
	static const char base64_decode[256];

	HCRYPTPROV m_hProv;
	HCRYPTHASH m_hHash;
	ALG_ID m_algid;
	DWORD  m_hashSize;
	BYTE m_ipad[64];
	BYTE m_opad[64];

public:
	CCryptUtil() : m_hProv(NULL), m_hHash(NULL), m_hashSize(0) {}
	~CCryptUtil() { Close(); }

	BOOL IsInitialized() { return m_hProv != NULL; }

	BOOL Create(const string& base64string);
	BOOL Create(const wstring& base64string);
	BOOL CreateHmac(const BYTE *pKey, const DWORD cbKey);
	VOID Close();

	BOOL SelectHash(ALG_ID id);
	BOOL SelectHash(DWORD hashSize);
	DWORD GetHashSize() { return m_hashSize; }

	BOOL CalcHmac(const BYTE *pbData, DWORD cbData);
	BOOL GetHmac(BYTE *pbData, DWORD cbData);
	BOOL CompareHmac(const BYTE *pbData);

	BOOL GetRandom(BYTE *pOut, DWORD cbOut) { return m_hProv != NULL && CryptGenRandom(m_hProv, cbOut, pOut); }

	static BOOL Encrypt(const wstring& input_string, wstring& output_string, DWORD flags = CRYPTPROTECT_LOCAL_MACHINE | CRYPTPROTECT_UI_FORBIDDEN | CRYPTPROTECT_AUDIT);
	static BOOL Decrypt(const wstring& input_string, wstring& output_string, DWORD flags = CRYPTPROTECT_LOCAL_MACHINE | CRYPTPROTECT_UI_FORBIDDEN | CRYPTPROTECT_AUDIT);

	template<class T>
	static BOOL Encrypt(const string& input_string, T& output_string, DWORD flags = CRYPTPROTECT_LOCAL_MACHINE | CRYPTPROTECT_UI_FORBIDDEN | CRYPTPROTECT_AUDIT)
	{
		BOOL ret = TRUE;
		if (input_string.empty()) {
			output_string.clear();
		}
		else {
			DATA_BLOB input;
			DATA_BLOB output;
			input.pbData = (BYTE*)input_string.data();
			input.cbData = (DWORD)input_string.size();
			ret = CryptProtectData(&input, NULL, NULL, NULL, NULL, flags, &output);
			if (ret) {
				ret = Base64Encode(output.pbData, output.cbData, output_string);
				LocalFree(output.pbData);
			}
		}
		return ret;
	}

	template<class T>
	static BOOL Decrypt(const T& input_string, string& output_string, DWORD flags = CRYPTPROTECT_LOCAL_MACHINE | CRYPTPROTECT_UI_FORBIDDEN | CRYPTPROTECT_AUDIT)
	{
		BOOL ret = TRUE;
		if (input_string.empty()) {
			output_string.clear();
		}
		else {
			BYTE *pData = NULL;
			DWORD cbData;
			ret = Base64Decode(input_string, &pData, &cbData);
			if (ret) {
				DATA_BLOB input;
				DATA_BLOB output;
				input.pbData = pData;
				input.cbData = cbData;
				ret = CryptUnprotectData(&input, NULL, NULL, NULL, NULL, flags, &output);
				if (ret) {
					output_string.assign((char*)output.pbData, output.cbData);
					LocalFree(output.pbData);
				}
				delete[] pData;
			}
		}
		return ret;
	}

private:
	template<class T>
	static BOOL Base64Encode(BYTE *pBinary, DWORD cbBinary, T& base64_string)
	{
		base64_string.clear();
		for (DWORD i = 0; i < cbBinary; i += 3)
		{
			bool b1 = i + 1 < cbBinary;
			bool b2 = i + 2 < cbBinary;

			int i0 = pBinary[i] >> 2;
			int i1 = (pBinary[i] & 0x03) << 4 | (b1 ? (pBinary[i + 1] & 0xF0) >> 4 : 0);
			int i2 = b1 ? (pBinary[i + 1] & 0x0F) << 2 | (b2 ? (pBinary[i + 2] & 0xC0) >> 6 : 0) : 64;
			int i3 = b2 ? pBinary[i + 2] & 0x3F : 64;
			base64_string.push_back(base64_encode[i0]);
			base64_string.push_back(base64_encode[i1]);
			base64_string.push_back(base64_encode[i2]);
			base64_string.push_back(base64_encode[i3]);
		}
		return TRUE;
	}

	template<class T>
	static BOOL Base64Decode(const T& base64_string, BYTE **ppOut, DWORD *pcbOut)
	{
		if (base64_string.size() % 4 != 0) {
			return FALSE;
		}

		size_t length = 0;
		for (auto itr = base64_string.begin(); itr != base64_string.end(); itr++) {
			if (base64_decode[*itr & 0xFF] >= 0)
				length++;
		}
		if (length != base64_string.size()) {
			return FALSE;
		}

		length = (length >> 2) * 3;
		BYTE *buf = new BYTE[length];
		*ppOut = buf;

		for (auto itr = base64_string.begin(); itr != base64_string.end();) {
			int i0 = *itr++ & 0xFF;
			int i1 = *itr++ & 0xFF;
			int i2 = *itr++ & 0xFF;
			int i3 = *itr++ & 0xFF;
			*buf++ = base64_decode[i0] << 2 | base64_decode[i1] >> 4;
			if (i2 == '=')
				break;
			*buf++ = base64_decode[i1] << 4 | base64_decode[i2] >> 2;
			if (i3 == '=')
				break;
			*buf++ = base64_decode[i2] << 6 | base64_decode[i3];
		}
		*pcbOut = static_cast<DWORD>(buf - *ppOut);
		return TRUE;
	}
};
