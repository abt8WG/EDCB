#pragma once

#ifdef UNICODE
typedef wstring tstring;
#else
typedef string tstring;
#endif

#define __NAME2__(x,y)  x##y
#define __NAME__(x,y)   __NAME2__(x,y)
#define HEADER          _T("%s(%d) : %s")

#ifdef _CRTDBG_MAP_ALLOC
class MemoryMeasure
{
public:
	MemoryMeasure(LPCTSTR file, size_t line, LPCTSTR func) : m_file(file), m_line(line), m_func(func)
	{
		_CrtMemCheckpoint(&m_old);
	}
	~MemoryMeasure()
	{
#ifdef DEBUG
		_CrtMemState m_diff;
		_CrtMemState m_new;
		_CrtMemCheckpoint(&m_new);
		_CrtMemDifference(&m_diff, &m_old, &m_new);

		_OutputDebugString(HEADER L"\r\n", m_file.c_str(), m_line, m_func.c_str());
		_CrtMemDumpStatistics(&m_diff);
#endif
	}
private:
	tstring       m_file;
	size_t        m_line;
	tstring       m_func;
	_CrtMemState  m_old;
};
#define MEMORY_MEASURE()	MemoryMeasure __NAME__(__MemoryMeasure__,__COUNTER__)(_T(__FILE__), __LINE__, _T(__FUNCTION__))
#define MEMORY_MEASURE_1(x)	MemoryMeasure __NAME__(__MemoryMeasure__,__COUNTER__)(_T(__FILE__), __LINE__, x)
#else
#define MEMORY_MEASURE()
#define MEMORY_MEASURE_1(x)
#endif

class TimeMeasure
{
public:
	TimeMeasure(LPCTSTR file, size_t line, LPCTSTR func) : m_file(file), m_line(line), m_func(func)
	{
		QueryPerformanceFrequency(&m_Freq);
		QueryPerformanceCounter(&m_Start);
	}
	~TimeMeasure()
	{
		LARGE_INTEGER m_End;
		QueryPerformanceCounter(&m_End);

		if (m_Freq.QuadPart == 0)
		{
			_OutputDebugString(HEADER _T(" : Time = %ld [tick]\r\n"), m_file.c_str(), m_line, m_func.c_str(), m_End.QuadPart - m_Start.QuadPart);
		}
		else
		{
			LPCTSTR units[] = { _T("sec"), _T("msec"), _T("nsec"), };
			size_t index;
			float fTime;
			for (index = 0, fTime = (m_End.QuadPart - m_Start.QuadPart) * 1.0f / m_Freq.QuadPart;
				index < _countof(units) - 1 && fTime < 1.0f;
				index++, fTime *= 1000.0f)
				;
			_OutputDebugString(HEADER _T(" : Time = %.2f [%s]\r\n"), m_file.c_str(), m_line, m_func.c_str(), fTime, units[index]);
		}
	}
private:
	tstring       m_file;
	size_t        m_line;
	tstring       m_func;
	LARGE_INTEGER m_Freq;
	LARGE_INTEGER m_Start;
};
#define TIME_MEASURE()		TimeMeasure __NAME__(__TimeMeasure__,__COUNTER__)(_T(__FILE__), __LINE__, _T(__FUNCTION__))
#define TIME_MEASURE_1(x)	TimeMeasure __NAME__(__TimeMeasure__,__COUNTER__)(_T(__FILE__), __LINE__, x)
