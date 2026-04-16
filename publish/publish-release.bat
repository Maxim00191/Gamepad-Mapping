@echo off
setlocal
cd /d "%~dp0"
rem Match .github/workflows/build.yml (shell: pwsh): gzip + XOR uses modern .NET; Windows PowerShell 5.1 yields a different compressed size.
set "PSCLI=pwsh.exe"
where %PSCLI% >nul 2>nul
if errorlevel 1 (
  set "PSCLI=powershell.exe"
  echo WARNING: pwsh.exe not found; using Windows PowerShell. For the same upload_text_policy.txt.gz as CI, install PowerShell 7+ and ensure pwsh is on PATH. >&2
)
%PSCLI% -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish-release.ps1" %*
exit /b %ERRORLEVEL%
