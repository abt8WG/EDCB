#include "StdAfx.h"
#include "ConvertMacro2.h"

#include "../../Common/StringUtil.h"
#include "../../Common/TimeUtil.h"
#include "../../Common/EpgTimerUtil.h"


CConvertMacro2::CConvertMacro2(void)
{
}


CConvertMacro2::~CConvertMacro2(void)
{
}

static BOOL ExpandMacro(wstring var, PLUGIN_RESERVE_INFO* info, wstring& convert);

BOOL CConvertMacro2::Convert(wstring macro, PLUGIN_RESERVE_INFO* info, wstring& convert)
{
	convert = L"";

	for( size_t pos = 0;; ){
		size_t next = macro.find(L'$', pos);
		if( next == wstring::npos ){
			convert.append(macro, pos, wstring::npos);
			break;
		}
		convert.append(macro, pos, next - pos);
		pos = next;

		next = macro.find(L'$', pos + 1);
		if( next == wstring::npos ){
			convert.append(macro, pos, wstring::npos);
			break;
		}
		if( ExpandMacro(macro.substr(pos + 1, next - pos - 1), info, convert) == FALSE ){
			convert += L'$';
			pos++;
		}else{
			pos = next + 1;
		}
	}
	Replace(convert, L"\r", L"");
	Replace(convert, L"\n", L"");

	return TRUE;
}

static BOOL ExpandMacro(wstring var, PLUGIN_RESERVE_INFO* info, wstring& convert)
{
	//ΦπΟή
	vector<wstring> funcStack;
	while( !var.empty() && var.back() == L')' ){
		size_t n = var.find(L'(');
		if( n == wstring::npos ){
			return FALSE;
		}
		funcStack.push_back(var.substr(0, n));
		var = var.substr(n + 1, var.size() - 1 - (n + 1));
	}

	wstring ret;
	BOOL found = FALSE;
	if( var.compare(0, 1, L"S") == 0 || var.compare(0, 1, L"E") == 0 ){
		for( int i = 0; GetTimeMacroName(i); i++ ){
			wstring name;
			AtoW(GetTimeMacroName(i), name);
			if( var.compare(1, wstring::npos, name) == 0 ){
				if( var[0] == L'S' ){
					ret = GetTimeMacroValue(i, info->startTime);
				}else{
					SYSTEMTIME tEnd;
					ConvertSystemTime(ConvertI64Time(info->startTime) + info->durationSec * I64_1SEC, &tEnd);
					ret = GetTimeMacroValue(i, tEnd);
				}
				found = TRUE;
				break;
			}
		}
	}

	EPG_EVENT_INFO* epgInfo = info->epgInfo;
	if( found )	{}
	else if( var == L"Title" )	ret = info->eventName;
	else if( var == L"ONID10" )	Format(ret, L"%d", info->ONID);
	else if( var == L"TSID10" )	Format(ret, L"%d", info->TSID);
	else if( var == L"SID10" )	Format(ret, L"%d", info->SID);
	else if( var == L"EID10" )	Format(ret, L"%d", info->EventID);
	else if( var == L"ONID16" )	Format(ret, L"%04X", info->ONID);
	else if( var == L"TSID16" )	Format(ret, L"%04X", info->TSID);
	else if( var == L"SID16" )	Format(ret, L"%04X", info->SID);
	else if( var == L"EID16" )	Format(ret, L"%04X", info->EventID);
	else if( var == L"ServiceName" )	ret = info->serviceName;
	else if( var == L"DUHH" )	Format(ret, L"%02d", info->durationSec/(60*60));
	else if( var == L"DUH" )	Format(ret, L"%d", info->durationSec/(60*60));
	else if( var == L"DUMM" )	Format(ret, L"%02d", (info->durationSec%(60*60))/60);
	else if( var == L"DUM" )	Format(ret, L"%d", (info->durationSec%(60*60))/60);
	else if( var == L"DUSS" )	Format(ret, L"%02d", info->durationSec%60);
	else if( var == L"DUS" )	Format(ret, L"%d", info->durationSec%60);
	else if( var == L"Title2" ){
		ret = info->eventName;
		while( ret.find(L"[") != wstring::npos && ret.find(L"]") != wstring::npos ){
			wstring strSep1;
			wstring strSep2;
			Separate(ret, L"[", ret, strSep1);
			Separate(strSep1, L"]", strSep2, strSep1);
			ret += strSep1;
		}
	}else if( var == L"Genre" ){
		if( epgInfo != NULL && epgInfo->contentInfo != NULL && epgInfo->contentInfo->listSize > 0 ){
			BYTE nibble1 = epgInfo->contentInfo->nibbleList[0].content_nibble_level_1;
			BYTE nibble2 = epgInfo->contentInfo->nibbleList[0].content_nibble_level_2;
			if( nibble1 == 0x0E && nibble2 == 0x01 ){
				//CSg£pξρ
				nibble1 = epgInfo->contentInfo->nibbleList[0].user_nibble_1 | 0x70;
			}
			GetGenreName(nibble1, 0xFF, ret);
			if( ret.empty() ){
				Format(ret, L"(0x%02X)", nibble1);
			}
		}
	}else if( var == L"Genre2" ){
		if( epgInfo != NULL && epgInfo->contentInfo != NULL && epgInfo->contentInfo->listSize > 0 ){
			BYTE nibble1 = epgInfo->contentInfo->nibbleList[0].content_nibble_level_1;
			BYTE nibble2 = epgInfo->contentInfo->nibbleList[0].content_nibble_level_2;
			if( nibble1 == 0x0E && nibble2 == 0x01 ){
				//CSg£pξρ
				nibble1 = epgInfo->contentInfo->nibbleList[0].user_nibble_1 | 0x70;
				nibble2 = epgInfo->contentInfo->nibbleList[0].user_nibble_2;
			}
			GetGenreName(nibble1, nibble2, ret);
			if( ret.empty() && nibble1 != 0x0F ){
				Format(ret, L"(0x%02X)", nibble2);
			}
		}
	}else if( var == L"SubTitle" ){
		if( epgInfo != NULL && epgInfo->shortInfo != NULL ){
			ret = epgInfo->shortInfo->text_char;
		}
	}else if( var == L"SubTitle2" ){
		if( epgInfo != NULL && epgInfo->shortInfo != NULL ){
			wstring strSubTitle2 = epgInfo->shortInfo->text_char;
			strSubTitle2 = strSubTitle2.substr(0, strSubTitle2.find(L"\r\n"));
			LPCWSTR startsWith[] = { L"#ζ", L"0123456789OPQRSTUVWX", NULL };
			for( size_t j, i = 0; i < strSubTitle2.size(); i++ ){
				for( j = 0; startsWith[i][j] && startsWith[i][j] != strSubTitle2[i]; j++ );
				if( startsWith[i][j] == L'\0' ){
					break;
				}
				if( startsWith[i+1] == NULL ){
					ret = strSubTitle2;
					break;
				}
			}
		}
	}else{
		return FALSE;
	}

	//ΦπKp
	while( !funcStack.empty() ){
		wstring func = funcStack.back();
		funcStack.pop_back();
		for( size_t i = 0; i < func.size(); i++ ){
			//lΆQΖ(&ΆR[h;)πWJ
			if( func[i] == L'&' ){
				wchar_t* p;
				wchar_t c = (wchar_t)wcstol(&func.c_str()[i + 1], &p, 10);
				func.replace(i, p - func.c_str() - i + (*p ? 1 : 0), 1, c);
			}
		}
		if( func == L"HtoZ" ){
			funcStack.push_back(L"Tr_ !\"#&36;%&38;'&40;)*+,-./:;<=>?@[\\]^_`{|}~_@Ihfij{C|D^FGHmnOQeobpP_");
			funcStack.push_back(L"HtoZ<alnum>");
		}else if( func == L"ZtoH" ){
			funcStack.push_back(L"Tr_@Ihfij{C|D^FGHmnOQeobpP_ !\"#&36;%&38;'&40;)*+,-./:;<=>?@[\\]^_`{|}~_");
			funcStack.push_back(L"ZtoH<alnum>");
		}else if( func == L"HtoZ<alnum>" ){
			funcStack.push_back(L"Tr/0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz/OPQRSTUVWX`abcdefghijklmnopqrstuvwxy/");
		}else if( func == L"ZtoH<alnum>" ){
			funcStack.push_back(L"Tr/OPQRSTUVWX`abcdefghijklmnopqrstuvwxy/0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz/");
		}else if( func.compare(0, 2, L"Tr") == 0 && func.size() >= 3 ){
			//Άu·(Tr/u·ΆXg/u·γ/)
			size_t n = func.find(func[2], 3);
			if( n == wstring::npos ){
				return FALSE;
			}
			if( func.find(func[2], n + 1) != 4 + (n - 3) * 2 ){
				return FALSE;
			}
			wstring cmp(func, 3, n - 3);
			for( wstring::iterator itr = ret.begin(); itr != ret.end(); itr++ ){
				size_t m = cmp.find(*itr);
				if( m != wstring::npos ){
					*itr = func[n + 1 + m];
				}
			}
		}else if( func.compare(0, 1, L"S") == 0 && func.size() >= 2 ){
			//Άρu·(S/u·Άρ/u·γ/)
			size_t n = func.find(func[1], 2);
			if( n == wstring::npos ){
				return FALSE;
			}
			size_t m = func.find(func[1], n + 1);
			if( m == wstring::npos ){
				return FALSE;
			}
			if( n > 2 ){
				Replace(ret, func.substr(2, n - 2), func.substr(n + 1, m - n - 1));
			}
		}else if( func.compare(0, 2, L"Rm") == 0 && func.size() >= 3 ){
			//Άν(Rm/νΆXg/)
			size_t n = func.find(func[2], 3);
			if( n == wstring::npos ){
				return FALSE;
			}
			wstring cmp(func, 3, n - 3);
			for( wstring::iterator itr = ret.begin(); itr != ret.end(); ){
				if( cmp.find(*itr) != wstring::npos ){
					itr = ret.erase(itr);
				}else{
					itr++;
				}
			}
		}else if( func.compare(0, 2, L"Head") && func.size() >= 5 ){
			//«Ψθ(HeadΆ[ΘͺL])
			size_t n = ret.size();
			wchar_t* p;
			ret = ret.substr(0, wcstol(&func.c_str()[4], &p, 10));
			if( *p && !ret.empty() && ret.size() < n ){
				ret.back() = *p;
			}
		}else{
			return FALSE;
		}
	}

	convert += ret;

	return TRUE;
}
