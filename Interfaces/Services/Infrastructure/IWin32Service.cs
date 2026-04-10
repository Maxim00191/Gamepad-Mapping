using System.Text;

namespace GamepadMapperGUI.Interfaces.Services.Infrastructure;

/// <summary>
/// Low-level abstraction for Win32 API calls to enable testing and robust error handling.
/// </summary>
public interface IWin32Service
{
    IntPtr GetForegroundWindow();
    uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);
    bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);
    bool GetTokenInformation(IntPtr tokenHandle, int tokenInformationClass, IntPtr tokenInformation, int tokenInformationLength, out int returnLength);
    bool CloseHandle(IntPtr hObject);
    uint SendInput(uint nInputs, IntPtr pInputs, int cbSize);
    uint MapVirtualKey(uint uCode, uint uMapType);
    string GetProcessName(int processId);
}

