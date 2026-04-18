@echo off
setlocal
cd /d "%~dp0"
rem Match .github/workflows/build.yml (shell: pwsh): gzip + AES-GCM uses modern .NET; Windows PowerShell 5.1 may differ for gzip.
set "PSCLI=pwsh.exe"
where %PSCLI% >nul 2>nul
if errorlevel 1 (
  set "PSCLI=powershell.exe"
  echo WARNING: pwsh.exe not found; using Windows PowerShell. For the same upload_text_policy.payload as CI, install PowerShell 7+ and ensure pwsh is on PATH. >&2
)
%PSCLI% -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish-release.ps1" %*
exit /b %ERRORLEVEL%
