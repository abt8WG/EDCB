@echo off
setlocal
if (%2) == () goto :eof
if not exist %2 goto :eof

@rem git.exe ‚ðŒ©‚Â‚¯‚é
set gitcmd=
if /i (%1) == (Release) for /f %%a in ('where /f git.exe') do set gitcmd=%%a

@rem revision ‚ðŽæ“¾‚·‚é
set REV=""
pushd %2
if not (%gitcmd%) == () for /f %%a in ('"%gitcmd% log -1 --format=%%h ."') do set REV=" (%%a)"
popd

@rem revision header file ‚ð¶¬‚·‚é
if exist %2\Revision.cs (
  echo update %2\Revision.cs - %REV%
  echo namespace EpgTimer{class Revision{public const string GIT_REVISION=%REV%;}} > %2\Revision.cs
) else (
  echo update %2\gitrevision.h - %REV%
  echo #define GIT_REVISION %REV% > %2\gitrevision.h
)
