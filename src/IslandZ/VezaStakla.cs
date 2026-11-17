using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Composition;
using WinRT;

namespace IslandZ;

// Rucni most do Microsoft.UI.Content.ContentExternalBackdropLink.
// Ta klasa postoji u Windows App SDK-u, ali je nema u javnim C# deklaracijama,
// pa se poziva direktno preko COM tabele. Redosled u tabeli (posle 6 IInspectable):
// fabrika: 6 Create; veza: 6 DispatcherQueue, 7/8 BorderMode, 9 PlacementVisual.
internal sealed unsafe class VezaStakla
{
    private const string Klasa = "Microsoft.UI.Content.ContentExternalBackdropLink";
    private static readonly Guid FabrikaIid = new("46cac6fb-bb51-510a-958d-e0eb4160f678");
    private static readonly Guid KompozitorIid = new("95213c13-c4cb-57de-b267-d21ab901ae38");

    [DllImport("combase.dll", CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString(string s, int duzina, out IntPtr hstring);

    [DllImport("combase.dll")]
    private static extern int WindowsDeleteString(IntPtr hstring);

    [DllImport("combase.dll")]
    private static extern int RoGetActivationFactory(IntPtr klasa, in Guid iid, out IntPtr fabrika);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryW(string ime);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr modul, string ime);

    private readonly IntPtr veza; // drzi vezu zivom dok program radi

    public ICompositionSupportsSystemBackdrop Meta { get; }
    public Visual Vizual { get; }

    private static IntPtr* Tabela(IntPtr o) => *(IntPtr**)o;

    private static void Proveri(int hr)
    {
        if (hr < 0) Marshal.ThrowExceptionForHR(hr);
    }

    public VezaStakla(Compositor kompozitor)
    {
        IntPtr fabrika = Fabrika();
        IntPtr kInsp = IntPtr.Zero, kAbi = IntPtr.Zero;
        try
        {
            kInsp = MarshalInspectable<Compositor>.FromManaged(kompozitor);
            Proveri(Marshal.QueryInterface(kInsp, in KompozitorIid, out kAbi));
            IntPtr nova;
            Proveri(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr*, int>)Tabela(fabrika)[6])(fabrika, kAbi, &nova));
            veza = nova;
        }
        finally
        {
            if (kAbi != IntPtr.Zero) Marshal.Release(kAbi);
            if (kInsp != IntPtr.Zero) Marshal.Release(kInsp);
            Marshal.Release(fabrika);
        }

        // Meke (glatke) ivice: CompositionBorderMode.Soft = 1
        ((delegate* unmanaged[Stdcall]<IntPtr, int, int>)Tabela(veza)[8])(veza, 1);

        IntPtr vizual;
        Proveri(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)Tabela(veza)[9])(veza, &vizual));
        try { Vizual = MarshalInspectable<Visual>.FromAbi(vizual); }
        finally { if (vizual != IntPtr.Zero) Marshal.Release(vizual); }

        Guid metaIid = GuidGenerator.GetIID(typeof(ICompositionSupportsSystemBackdrop));
        Proveri(Marshal.QueryInterface(veza, in metaIid, out IntPtr meta));
        try { Meta = MarshalInterface<ICompositionSupportsSystemBackdrop>.FromAbi(meta); }
        finally { Marshal.Release(meta); }
    }

    private static IntPtr Fabrika()
    {
        Proveri(WindowsCreateString(Klasa, Klasa.Length, out IntPtr ime));
        try
        {
            int hr = RoGetActivationFactory(ime, in FabrikaIid, out IntPtr fabrika);
            if (hr >= 0) return fabrika;

            // Klasa nije prijavljena u manifestu: pitaj direktno biblioteku koja je sadrzi.
            IntPtr modul = LoadLibraryW("Microsoft.UI.Input.dll");
            if (modul == IntPtr.Zero) Proveri(hr);
            IntPtr izvoz = GetProcAddress(modul, "DllGetActivationFactory");
            if (izvoz == IntPtr.Zero) Proveri(hr);
            IntPtr opsta;
            Proveri(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)izvoz)(ime, &opsta));
            int hr2 = Marshal.QueryInterface(opsta, in FabrikaIid, out fabrika);
            Marshal.Release(opsta);
            Proveri(hr2);
            return fabrika;
        }
        finally
        {
            WindowsDeleteString(ime);
        }
    }
}
