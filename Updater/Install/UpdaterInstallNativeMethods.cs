using System;
using System.Runtime.InteropServices;

namespace Updater.Install;

internal static class UpdaterInstallNativeMethods
{
    internal const int TOKEN_ASSIGN_PRIMARY = 0x0001;
    internal const int TOKEN_DUPLICATE = 0x0002;
    internal const int TOKEN_QUERY = 0x0008;
    internal const int TOKEN_ADJUST_DEFAULT = 0x0080;
    internal const int TOKEN_ADJUST_SESSIONID = 0x0100;
    internal const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;

    [DllImport("kernel32.dll")]
    internal static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WTSQueryUserToken(uint sessionId, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DuplicateTokenEx(
        IntPtr existingTokenHandle,
        int desiredAccess,
        IntPtr tokenAttributes,
        SECURITY_IMPERSONATION_LEVEL impersonationLevel,
        TOKEN_TYPE tokenType,
        out IntPtr duplicateTokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CreateProcessAsUser(
        IntPtr token,
        string? applicationName,
        string? commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string? currentDirectory,
        ref STARTUPINFO startupInfo,
        out PROCESS_INFORMATION processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr handle);

    #region WinTrust API (Digital Signature Verification)

    internal static readonly Guid WINTRUST_ACTION_GENERIC_VERIFY_V2 = new Guid("{00AAC56B-CD44-11d0-8CC2-00C04FC295EE}");

    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern int WinVerifyTrust(
        IntPtr hwnd,
        [MarshalAs(UnmanagedType.LPStruct)] Guid pgActionID,
        IntPtr pWVTData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WINTRUST_FILE_INFO
    {
        public int cbStruct;
        public string pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;

        public WINTRUST_FILE_INFO(string filePath)
        {
            cbStruct = Marshal.SizeOf<WINTRUST_FILE_INFO>();
            pcwszFilePath = filePath;
            hFile = IntPtr.Zero;
            pgKnownSubject = IntPtr.Zero;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WINTRUST_DATA
    {
        public int cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public string? pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
        public IntPtr pSignatureSettings;

        public WINTRUST_DATA(IntPtr pFilePtr)
        {
            cbStruct = Marshal.SizeOf<WINTRUST_DATA>();
            pPolicyCallbackData = IntPtr.Zero;
            pSIPClientData = IntPtr.Zero;
            dwUIChoice = 2; // WTD_UI_NONE
            fdwRevocationChecks = 0; // WTD_REVOKE_NONE
            dwUnionChoice = 1; // WTD_CHOICE_FILE
            pFile = pFilePtr;
            dwStateAction = 0; // WTD_STATEACTION_IGNORE
            hWVTStateData = IntPtr.Zero;
            pwszURLReference = null;
            dwProvFlags = 0x00000040; // WTD_SAFER_FLAG
            dwUIContext = 0;
            pSignatureSettings = IntPtr.Zero;
        }
    }

    #endregion

    internal enum TOKEN_TYPE
    {
        TokenPrimary = 1,
        TokenImpersonation = 2
    }

    internal enum SECURITY_IMPERSONATION_LEVEL
    {
        SecurityAnonymous = 0,
        SecurityIdentification = 1,
        SecurityImpersonation = 2,
        SecurityDelegation = 3
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct STARTUPINFO
    {
        internal int cb;
        internal string? lpReserved;
        internal string? lpDesktop;
        internal string? lpTitle;
        internal int dwX;
        internal int dwY;
        internal int dwXSize;
        internal int dwYSize;
        internal int dwXCountChars;
        internal int dwYCountChars;
        internal int dwFillAttribute;
        internal int dwFlags;
        internal short wShowWindow;
        internal short cbReserved2;
        internal IntPtr lpReserved2;
        internal IntPtr hStdInput;
        internal IntPtr hStdOutput;
        internal IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROCESS_INFORMATION
    {
        internal IntPtr hProcess;
        internal IntPtr hThread;
        internal int dwProcessId;
        internal int dwThreadId;
    }
}
