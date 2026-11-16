using System.Runtime.InteropServices;
using Windows.Storage;

namespace WasteVision.Desktop.Common;

public static class NativeFileDialog
{
    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class OpenFileName
    {
        public int structSize = 0;
        public IntPtr dlgOwner = IntPtr.Zero;
        public IntPtr instance = IntPtr.Zero;
        public string? filter = null;
        public string? customFilter = null;
        public int maxCustFilter = 0;
        public int filterIndex = 0;
        public string? file = null;
        public int maxFile = 0;
        public string? fileTitle = null;
        public int maxFileTitle = 0;
        public string? initialDir = null;
        public string? title = null;
        public int flags = 0;
        public short fileOffset = 0;
        public short fileExtension = 0;
        public string? defExt = null;
        public IntPtr custData = IntPtr.Zero;
        public IntPtr hook = IntPtr.Zero;
        public string? templateName = null;
        public IntPtr reservedPtr = IntPtr.Zero;
        public int reservedInt = 0;
        public int flagsEx = 0;
    }

    private const int OFN_FILEMUSTEXIST = 0x00001000;
    private const int OFN_PATHMUSTEXIST = 0x00000800;
    private const int OFN_NOCHANGEDIR = 0x00000008;
    private const int OFN_EXPLORER = 0x00080000;

    /// <summary>
    /// Opens a native Win32 OpenFileDialog that works 100% reliably in unpackaged WinUI 3 apps.
    /// </summary>
    public static string? PickFile(IntPtr hWnd, string title, string filter, string? initialDir = null)
    {
        var ofn = new OpenFileName
        {
            structSize = Marshal.SizeOf(typeof(OpenFileName)),
            dlgOwner = hWnd,
            filter = filter.Replace('|', '\0') + "\0\0",
            file = new string(new char[1024]),
            maxFile = 1024,
            fileTitle = new string(new char[260]),
            maxFileTitle = 260,
            initialDir = initialDir,
            title = title,
            flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR | OFN_EXPLORER
        };

        if (GetOpenFileName(ofn))
        {
            return ofn.file?.TrimEnd('\0');
        }
        return null;
    }

    /// <summary>
    /// Convenience helper for picking image files.
    /// </summary>
    public static string? PickImage(IntPtr hWnd, string? initialDir = null)
    {
        const string filter = "Image Files (*.jpg;*.jpeg;*.png;*.webp;*.bmp)|*.jpg;*.jpeg;*.png;*.webp;*.bmp|All Files (*.*)|*.*";
        return PickFile(hWnd, "اختر صورة نفاية للفحص والاستدلال", filter, initialDir);
    }

    /// <summary>
    /// Convenience helper for picking ZIP dataset files.
    /// </summary>
    public static string? PickZip(IntPtr hWnd, string? initialDir = null)
    {
        const string filter = "ZIP Archive (*.zip)|*.zip|All Files (*.*)|*.*";
        return PickFile(hWnd, "اختر ملف ZIP لمجموعة صور النفايات", filter, initialDir);
    }
}
