using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace IslandZ;

// Cita ikonicu programa onako kako je vidi Windows: iz same precice (.lnk),
// iz .exe fajla, ili kao poslednju mogucnost ono sto prikazuje Explorer.
public static class Ikone
{
    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public int fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint PrivateExtractIconsW(string fajl, int indeks, int cx, int cy,
        [Out] IntPtr[] ikone, [Out] uint[] idovi, uint broj, uint flags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfoW(string putanja, uint atributi, ref SHFILEINFO info, uint velicina, uint flags);

    [DllImport("user32.dll")] private static extern bool GetIconInfo(IntPtr ikona, out ICONINFO info);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr ikona);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr o);
    [DllImport("gdi32.dll")] private static extern int GetObjectW(IntPtr o, int velicina, out BITMAP b);
    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint linija, [Out] byte[] bitovi,
        ref BITMAPINFOHEADER info, uint upotreba);

    private const int Velicina = 96;

    // Mora da se zove sa UI niti.
    public static ImageSource? Ucitaj(string putanja)
    {
        try
        {
            string izvor = putanja;
            int indeks = 0;
            if (putanja.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var (cilj, ikona, ind) = Sistem.CitajPrecicu(putanja);
                if (ikona.Length > 0 && File.Exists(ikona)) { izvor = ikona; indeks = ind; }
                else if (cilj.Length > 0 && File.Exists(cilj)) { izvor = cilj; }
                else izvor = "";
            }

            IntPtr h = IntPtr.Zero;
            if (izvor.Length > 0)
            {
                var ikone = new IntPtr[1];
                var idovi = new uint[1];
                uint n = PrivateExtractIconsW(izvor, indeks, Velicina, Velicina, ikone, idovi, 1, 0);
                if (n >= 1 && n != 0xFFFFFFFF) h = ikone[0];
            }
            if (h == IntPtr.Zero)
            {
                // Ono sto prikazuje Explorer (32 px).
                var info = new SHFILEINFO();
                SHGetFileInfoW(putanja, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), 0x100);
                h = info.hIcon;
            }
            if (h == IntPtr.Zero) return null;
            try { return UBitmapu(h); }
            finally { DestroyIcon(h); }
        }
        catch (Exception ex)
        {
            Pomocno.Log(ex);
            return null;
        }
    }

    private static ImageSource? UBitmapu(IntPtr ikona)
    {
        if (!GetIconInfo(ikona, out var ii)) return null;
        try
        {
            if (ii.hbmColor == IntPtr.Zero) return null;
            if (GetObjectW(ii.hbmColor, Marshal.SizeOf<BITMAP>(), out var bm) == 0) return null;
            int w = bm.bmWidth, h = bm.bmHeight;
            if (w <= 0 || h <= 0 || w > 1024 || h > 1024) return null;

            var zaglavlje = new BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = w,
                biHeight = -h, // odozgo nadole
                biPlanes = 1,
                biBitCount = 32,
            };
            var px = new byte[w * h * 4];
            IntPtr hdc = GetDC(IntPtr.Zero);
            int linija;
            try { linija = GetDIBits(hdc, ii.hbmColor, 0, (uint)h, px, ref zaglavlje, 0); }
            finally { ReleaseDC(IntPtr.Zero, hdc); }
            if (linija == 0) return null;

            // Stare ikonice nemaju providnost u bojama: tada je sve neprovidno.
            bool imaAlfu = false;
            for (int i = 3; i < px.Length; i += 4)
                if (px[i] != 0) { imaAlfu = true; break; }

            // WinUI ocekuje boje vec pomnozene providnoscu.
            for (int i = 0; i < px.Length; i += 4)
            {
                if (!imaAlfu) { px[i + 3] = 255; continue; }
                int a = px[i + 3];
                if (a == 255) continue;
                px[i] = (byte)(px[i] * a / 255);
                px[i + 1] = (byte)(px[i + 1] * a / 255);
                px[i + 2] = (byte)(px[i + 2] * a / 255);
            }

            var bmp = new WriteableBitmap(w, h);
            using (var tok = bmp.PixelBuffer.AsStream())
                tok.Write(px, 0, px.Length);
            bmp.Invalidate();
            return bmp;
        }
        finally
        {
            if (ii.hbmColor != IntPtr.Zero) DeleteObject(ii.hbmColor);
            if (ii.hbmMask != IntPtr.Zero) DeleteObject(ii.hbmMask);
        }
    }
}
