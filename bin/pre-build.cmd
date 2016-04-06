@echo off
if (%2) == () goto :eof
setlocal
set gitcmd="%github_git%\cmd\git.exe"
set REV=""
if /i (%1) == (Release) if exist %gitcmd% for /f %%a in ('"%gitcmd% log -1 --format=%%h "%~f2.""') do set REV=" (%%a)"
if exist %2Revision.cs (
  echo update %2Revision.cs - %REV%
  echo namespace EpgTimer{class Revision{public const string GIT_REVISION=%REV%;}} > %2Revision.cs
) else (
  echo update %2gitrevision.h - %REV%
  echo #define GIT_REVISION %REV% > %2gitrevision.h
)
