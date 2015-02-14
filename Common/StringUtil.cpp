#include "stdafx.h"
#include "StringUtil.h"

void Format(string& strBuff, const char *format, ...)
{
	va_list params;

	va_start(params, format);
	int iResult;
	char *buff;
	int length = _vscprintf(format, params);
	buff = new char [length + 1];
	iResult = vsprintf_s(buff, length + 1, format, params);
	buff[length] = '\0';
	if (buff != NULL) {
		strBuff = buff;
		delete[] buff;
	}

	va_end(params);
}

void Format(wstring& strBuff, const WCHAR *format, ...)
{
	va_list params;

	va_start(params, format);

	int iResult;
	WCHAR *buff;
	int length = _vscwprintf(format, params);
	buff = new WCHAR [length + 1];
	iResult = vswprintf_s(buff, length + 1, format, params);
	buff[length] = '\0';
	if (buff != NULL) {
		strBuff = buff;
		delete[] buff;
	}

    va_end(params);
}

void Replace(string& strBuff, const string& strOld, const string& strNew)
{
	string::size_type Pos = 0;
	string* strWork = &strBuff;
	string strForAlias;

	if( strWork == &strOld || strWork == &strNew ){
		strForAlias = strBuff;
		strWork = &strForAlias;
	}
	while ((Pos = strWork->find(strOld,Pos)) != string::npos)
	{
		strWork->replace(Pos,strOld.size(),strNew);
		Pos += strNew.size();
	}
	if( strWork == &strForAlias ){
		strBuff = strForAlias;
	}
}

void Replace(wstring& strBuff, const wstring& strOld, const wstring& strNew)
{
	string::size_type Pos = 0;
	wstring* strWork = &strBuff;
	wstring strForAlias;

	if( strWork == &strOld || strWork == &strNew ){
		strForAlias = strBuff;
		strWork = &strForAlias;
	}
	while ((Pos = strWork->find(strOld,Pos)) != string::npos)
	{
		strWork->replace(Pos,strOld.size(),strNew);
		Pos += strNew.size();
	}
	if( strWork == &strForAlias ){
		strBuff = strForAlias;
	}
}

void WtoA(const wstring& strIn, string& strOut)
{
	strOut.clear();
	int iLen = (int)strIn.size() * 2 + 1;
	char* pszBuff = new char[iLen];
	if( WideCharToMultiByte( 932, 0, strIn.c_str(), -1, pszBuff, iLen, NULL, NULL ) != 0 ){
		strOut = pszBuff;
		delete[] pszBuff;
	}else{
		//rare case
		delete[] pszBuff;
		iLen = WideCharToMultiByte( 932, 0, strIn.c_str(), -1, NULL, 0, NULL, NULL );
		if( iLen > 0 ){
			pszBuff = new char[iLen];
			if( WideCharToMultiByte( 932, 0, strIn.c_str(), -1, pszBuff, iLen, NULL, NULL ) != 0 ){
				strOut = pszBuff;
			}
			delete[] pszBuff;
		}
	}
}

void WtoUTF8(const wstring& strIn, string& strOut)
{
	strOut.clear();
	int iLen = (int)strIn.size() * 3 + 1;
	char* pszBuff = new char[iLen];
	if( WideCharToMultiByte( CP_UTF8, 0, strIn.c_str(), -1, pszBuff, iLen, NULL, NULL ) != 0 ){
		strOut = pszBuff;
		delete[] pszBuff;
	}else{
		//rare case
		delete[] pszBuff;
		iLen = WideCharToMultiByte( CP_UTF8, 0, strIn.c_str(), -1, NULL, 0, NULL, NULL );
		if( iLen > 0 ){
			pszBuff = new char[iLen];
			if( WideCharToMultiByte( CP_UTF8, 0, strIn.c_str(), -1, pszBuff, iLen, NULL, NULL ) != 0 ){
				strOut = pszBuff;
			}
			delete[] pszBuff;
		}
	}
}

void AtoW(const string& strIn, wstring& strOut)
{
	strOut.clear();
	int iLen = (int)strIn.size() + 1;
	WCHAR* pwszBuff = new WCHAR[iLen];
	if( MultiByteToWideChar( 932, 0, strIn.c_str(), -1, pwszBuff, iLen ) != 0 ){
		strOut = pwszBuff;
		delete[] pwszBuff;
	}else{
		//rare case
		delete[] pwszBuff;
		iLen = MultiByteToWideChar( 932, 0, strIn.c_str(), -1, NULL, 0 );
		if( iLen > 0 ){
			pwszBuff = new WCHAR[iLen];
			if( MultiByteToWideChar( 932, 0, strIn.c_str(), -1, pwszBuff, iLen ) != 0 ){
				strOut = pwszBuff;
			}
			delete[] pwszBuff;
		}
	}
}

void UTF8toW(const string& strIn, wstring& strOut)
{
	strOut.clear();
	int iLen = (int)strIn.size() + 1;
	WCHAR* pwszBuff = new WCHAR[iLen];
	if( MultiByteToWideChar( CP_UTF8, 0, strIn.c_str(), -1, pwszBuff, iLen ) != 0 ){
		strOut = pwszBuff;
		delete[] pwszBuff;
	}else{
		//rare case
		delete[] pwszBuff;
		iLen = MultiByteToWideChar( CP_UTF8, 0, strIn.c_str(), -1, NULL, 0 );
		if( iLen > 0 ){
			pwszBuff = new WCHAR[iLen];
			if( MultiByteToWideChar( CP_UTF8, 0, strIn.c_str(), -1, pwszBuff, iLen ) != 0 ){
				strOut = pwszBuff;
			}
			delete[] pwszBuff;
		}
	}
}

BOOL Separate(const string& strIn, const char *sep, string& strLeft, string& strRight)
{
	string::size_type Pos = strIn.find(sep);
	string strL(strIn, 0, Pos);
	if( Pos == string::npos ){
		strRight = "";
		strLeft = strL;
		return FALSE;
	}
	strRight = strIn.substr(Pos+strlen(sep));
	strLeft = strL;
	
	return TRUE;
}

BOOL Separate(const wstring& strIn, const WCHAR *sep, wstring& strLeft, wstring& strRight)
{
	wstring::size_type Pos = strIn.find(sep);
	wstring strL(strIn, 0, Pos);
	if( Pos == string::npos ){
		strRight = L"";
		strLeft = strL;
		return FALSE;
	}
	strRight = strIn.substr(Pos+wcslen(sep));
	strLeft = strL;
	
	return TRUE;
}

void ChkFolderPath(string& strPath)
{
	if( strPath.empty() == true ){
		return ;
	}
	string strChk = "\\";
	if( strChk.compare(strPath.substr(strPath.length()-1, 1)) == 0 ){
		strPath.erase(strPath.length()-1, 1);
	}
}

void ChkFolderPath(wstring& strPath)
{
	if( strPath.empty() == true ){
		return ;
	}
	wstring strChk = L"\\";
	if( strChk.compare(strPath.substr(strPath.length()-1, 1)) == 0 ){
		strPath.erase(strPath.length()-1, 1);
	}
}

void ChkFileName(string& strPath)
{
	if( strPath.empty() == true ){
		return ;
	}
	Replace(strPath, "\\","��");
	Replace(strPath, "/","�^");
	Replace(strPath, ":","�F");
	Replace(strPath, "*","��");
	Replace(strPath, "?","�H");
	Replace(strPath, "\"","�h");
	Replace(strPath, "<","��");
	Replace(strPath, ">","��");
	Replace(strPath, "|","�b");
}

void ChkFileName(wstring& strPath)
{
	if( strPath.empty() == true ){
		return ;
	}
	Replace(strPath, L"\\",L"��");
	Replace(strPath, L"/",L"�^");
	Replace(strPath, L":",L"�F");
	Replace(strPath, L"*",L"��");
	Replace(strPath, L"?",L"�H");
	Replace(strPath, L"\"",L"�h");
	Replace(strPath, L"<",L"��");
	Replace(strPath, L">",L"��");
	Replace(strPath, L"|",L"�b");
}

int CompareNoCase(const string& str1, const string& str2)
{
	DWORD dwSize1 = (DWORD)str1.length()+1;
	DWORD dwSize2 = (DWORD)str2.length()+1;

	char* szBuff1 = new char[dwSize1];
	char* szBuff2 = new char[dwSize2];

	strcpy_s(szBuff1, dwSize1, str1.c_str());
	strcpy_s(szBuff2, dwSize2, str2.c_str());

	_strlwr_s(szBuff1, dwSize1);
	_strlwr_s(szBuff2, dwSize2);

	int iRet = string(szBuff1).compare(szBuff2);

	delete[] szBuff1;
	delete[] szBuff2;

	return iRet;
}

int CompareNoCase(const wstring& str1, const wstring& str2)
{
	DWORD dwSize1 = (DWORD)str1.length()+1;
	DWORD dwSize2 = (DWORD)str2.length()+1;

	WCHAR* szBuff1 = new WCHAR[dwSize1];
	WCHAR* szBuff2 = new WCHAR[dwSize2];

	wcscpy_s(szBuff1, dwSize1, str1.c_str());
	wcscpy_s(szBuff2, dwSize2, str2.c_str());

	_wcslwr_s(szBuff1, dwSize1);
	_wcslwr_s(szBuff2, dwSize2);

	int iRet = wstring(szBuff1).compare(szBuff2);

	delete[] szBuff1;
	delete[] szBuff2;

	return iRet;
}

BOOL UrlDecode(LPCSTR src, DWORD srcSize, string& dest)
{
	if( src == NULL ){
		return FALSE;
	}

	string sjis;
	for( DWORD i=0; i<srcSize; i++ ){
		if( src[i] == '%' ){
			if( i+2 > srcSize ){
				break;
			}
			char tmp[3]="";
			tmp[0] = src[i+1];
			tmp[1] = src[i+2];
			CHAR *endstr;
			char tmp2[2]="";
			tmp2[0] = (CHAR)strtol(tmp, &endstr, 16);
			sjis += tmp2;

			i+=2;
		}else if( src[i] == '+' ){
			sjis += " ";
		}else if( src[i] == '\0' ){
			break;
		}else{
			char tmp[2]="";
			tmp[0] = src[i];
			sjis += tmp;
		}
	}

	dest = sjis;
	
	return TRUE;
}

BOOL UrlDecode(LPCWSTR src, DWORD srcSize, wstring& dest)
{
	if( src == NULL ){
		return FALSE;
	}

	string sjis;
	for( DWORD i=0; i<srcSize; i++ ){
		if( src[i] == '%' ){
			if( i+2 > srcSize ){
				break;
			}
			WCHAR tmp[3]=L"";
			tmp[0] = (char)src[i+1];
			tmp[1] = (char)src[i+2];

			WCHAR *endstr;
			char tmp2[2]="";
			tmp2[0] = (char)wcstol(tmp, &endstr, 16);
			sjis += tmp2;

			i+=2;
		}else if( src[i] == '+' ){
			sjis += " ";
		}else if( src[i] == '\0' ){
			break;
		}else{
			char tmp[2]="";
			tmp[0] = (char)src[i];
			sjis += tmp;
		}
	}

	int iLen = MultiByteToWideChar( 932, 0, sjis.c_str(), -1, NULL, 0 );
	WCHAR* pwszBuff = new WCHAR[iLen+1];
	ZeroMemory(pwszBuff, sizeof(WCHAR)*(iLen+1));
	MultiByteToWideChar( 932, 0, sjis.c_str(), -1, pwszBuff, iLen );

	dest = pwszBuff;
	
	delete[] pwszBuff;

	return TRUE;
}

void Trim(string& strBuff)
{
	while(1){
		int iPos = (int)strBuff.find(" ");
		if( iPos != 0 ){
			break;
		}
		strBuff.erase(0, 1);
	}
	while(1){
		int iPos = (int)strBuff.rfind(" ");
		if( iPos != strBuff.size()-1 || iPos < 0){
			break;
		}
		strBuff.erase(iPos, 1);
	}
}

void Trim(wstring& strBuff)
{
	while(1){
		int iPos = (int)strBuff.find(L" ");
		if( iPos != 0 ){
			break;
		}
		strBuff.erase(0, 1);
	}
	while(1){
		int iPos = (int)strBuff.rfind(L" ");
		if( iPos != strBuff.size()-1 || iPos < 0){
			break;
		}
		strBuff.erase(iPos, 1);
	}
}

string Tolower(const string& src)
{
	DWORD dwSize1 = (DWORD)src.length()+1;

	char* szBuff1 = new char[dwSize1];

	strcpy_s(szBuff1, dwSize1, src.c_str());

	_strlwr_s(szBuff1, dwSize1);

	string strBuff1 = szBuff1;

	delete[] szBuff1;

	return strBuff1;
}

wstring Tolower(const wstring& src)
{
	DWORD dwSize1 = (DWORD)src.length()+1;

	WCHAR* szBuff1 = new WCHAR[dwSize1];

	wcscpy_s(szBuff1, dwSize1, src.c_str());

	_wcslwr_s(szBuff1, dwSize1);

	wstring strBuff1 = szBuff1;

	delete[] szBuff1;

	return strBuff1;
}

void EscapeXmlString(wstring& src)
{
	Replace(src, L"&", L"&amp;");
	Replace(src, L"<", L"&lt;");
	Replace(src, L">", L"&gt;");
	Replace(src, L"\"", L"&quot;");
	Replace(src, L"'", L"&apos;");
}
