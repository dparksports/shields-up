#nullable enable
using System;
using System.Linq;
using System.Runtime.InteropServices;
using ShieldsUp.Core.Services;

namespace ShieldsUp.Helpers;

/// <summary>
/// File-open dialog through the shell's own IFileOpenDialog. The WinRT
/// FileOpenPicker requires package identity and an unelevated picker broker,
/// so it throws COMException 0x80004005 here (unpackaged, requireAdministrator);
/// the Win32 dialog carries neither requirement.
/// </summary>
internal static class FileDialogs
{
    private const uint FOS_FORCEFILESYSTEM = 0x40;
    private const uint FOS_PATHMUSTEXIST = 0x800;
    private const uint FOS_FILEMUSTEXIST = 0x1000;
    private const uint SIGDN_FILESYSPATH = 0x80058000;
    private static readonly Guid ShellItemIid = new("43826D1E-E718-42EE-BC55-A1E261C37BFE");

    /// <summary>Modal pick-one-file dialog. Null on cancel, close, or dialog failure.</summary>
    public static string? PickOpenFile(IntPtr ownerHwnd, string title, string patternLabel, params string[] extensions)
    {
        AppLogger.LogVerify("file dialog: creating IFileOpenDialog…");
        var dialog = (IFileOpenDialog)new FileOpenDialogCom();
        try
        {
            dialog.GetOptions(out uint options);
            dialog.SetOptions(options | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST | FOS_FILEMUSTEXIST);
            dialog.SetTitle(title);

            string spec = string.Join(";", extensions.Select(e => "*" + e));
            dialog.SetFileTypes(1, new[] { new FilterSpec(patternLabel, spec) });
            dialog.SetFileTypeIndex(1);
            TrySetStartFolder(dialog, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads");

            AppLogger.LogVerify("file dialog: showing (modal)…");
            // S_OK only; cancel is 0x800704C7, any other failure is treated the same
            int hr = dialog.Show(ownerHwnd);
            AppLogger.LogVerify($"file dialog: Show returned 0x{hr:X8}");
            if (hr != 0)
                return null;

            dialog.GetResult(out IShellItem item);
            try
            {
                item.GetDisplayName(SIGDN_FILESYSPATH, out IntPtr pathPtr);
                try
                {
                    string? path = Marshal.PtrToStringUni(pathPtr);
                    return string.IsNullOrEmpty(path) ? null : path;
                }
                finally
                {
                    Marshal.FreeCoTaskMem(pathPtr);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(item);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log("file-dialog", $"open dialog failed: {ex.Message}");
            AppLogger.LogVerify($"file dialog FAILED: {ex}");
            return null;
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }

    private static void TrySetStartFolder(IFileOpenDialog dialog, string folder)
    {
        try
        {
            if (!System.IO.Directory.Exists(folder)) return;
            SHCreateItemFromParsingName(folder, IntPtr.Zero, ShellItemIid, out IShellItem item);
            try { dialog.SetFolder(item); }
            finally { Marshal.ReleaseComObject(item); }
        }
        catch { } // start folder is cosmetic
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        string pszPath, IntPtr pbc, in Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);

    [ComImport, Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
    private class FileOpenDialogCom { }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private readonly struct FilterSpec
    {
        public FilterSpec(string name, string spec) { pszName = name; pszSpec = spec; }
        public readonly string pszName;
        public readonly string pszSpec;
    }

    [ComImport, Guid("D57C7288-D4AD-4768-BE02-9D969532D960"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        // IModalWindow
        [PreserveSig] int Show(IntPtr hwndOwner);

        // IFileDialog — slot order must match the COM vtable exactly
        [PreserveSig] int SetFileTypes(uint cFileTypes, FilterSpec[] rgFilterSpec);
        [PreserveSig] int SetFileTypeIndex(uint iFileType);
        [PreserveSig] int GetFileTypeIndex(out uint piFileType);
        [PreserveSig] int Advise(IntPtr pfde, out uint pdwCookie);
        [PreserveSig] int Unadvise(uint dwCookie);
        [PreserveSig] int SetOptions(uint fos);
        [PreserveSig] int GetOptions(out uint pfos);
        [PreserveSig] int SetDefaultFolder(IShellItem psi);
        [PreserveSig] int SetFolder(IShellItem psi);
        [PreserveSig] int GetFolder(out IShellItem ppsi);
        [PreserveSig] int GetCurrentSelection(out IShellItem ppsi);
        [PreserveSig] int SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        [PreserveSig] int GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        [PreserveSig] int SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        [PreserveSig] int SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        [PreserveSig] int SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        [PreserveSig] int GetResult(out IShellItem ppsi);
        [PreserveSig] int AddPlace(IShellItem psi, int fdap);
        [PreserveSig] int SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        [PreserveSig] int Close(int hr);
        [PreserveSig] int SetClientGuid(in Guid guid);
        [PreserveSig] int ClearClientData();
        [PreserveSig] int SetFilter(IntPtr pFilter);

        // IFileOpenDialog
        [PreserveSig] int GetResults(out IntPtr ppenum);
        [PreserveSig] int GetSelectedItems(out IntPtr ppsai);
    }

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        [PreserveSig] int BindToHandler(IntPtr pbc, in Guid bhid, in Guid riid, out IntPtr ppv);
        [PreserveSig] int GetParent(out IShellItem ppsi);
        [PreserveSig] int GetDisplayName(uint sigdnName, out IntPtr ppszName);
    }
}
