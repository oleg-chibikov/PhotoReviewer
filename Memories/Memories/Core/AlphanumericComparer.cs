using System.Runtime.InteropServices;

namespace PhotoReviewer.Memories.Core;

sealed class AlphanumericComparer : IComparer<string>
{
    public int Compare(string? x, string? y) => StrCmpLogicalW(x, y);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
#pragma warning disable CA5392 // Use DefaultDllImportSearchPaths attribute for P/Invokes
    static extern int StrCmpLogicalW(string? s1, string? s2);
#pragma warning restore CA5392 // Use DefaultDllImportSearchPaths attribute for P/Invokes
}