using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;

namespace IslandZ;

public class Stavka
{
    public string Naziv { get; set; } = "";
    public string Putanja { get; set; } = "";   // .exe ili .lnk
    public string Cilj { get; set; } = "";      // .exe na koji pokazuje, malim slovima (za proveru da li radi)
}

public static class Programi
{
    private static List<Stavka> startMeni = new();
    private static readonly Dictionary<string, ImageSource> ikone = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Stavka> zakaceniKes = new(StringComparer.OrdinalIgnoreCase);

    // Spisak programa iz Start menija, za pretragu. Cita se u pozadini pri pokretanju.
    public static void UcitajStartMeni()
    {
        Task.Run(() =>
        {
            var lista = new List<Stavka>();
            var vidjeni = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] folderi =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            };
            foreach (string folder in folderi)
            {
                try
                {
                    if (!Directory.Exists(folder)) continue;
                    foreach (string f in Directory.EnumerateFiles(folder, "*.lnk", SearchOption.AllDirectories))
                    {
                        string naziv = Path.GetFileNameWithoutExtension(f);
                        string n = naziv.ToLowerInvariant();
                        if (n.Contains("uninstall") || n.Contains("deinstal")) continue;
                        if (vidjeni.Add(naziv))
                            lista.Add(new Stavka { Naziv = naziv, Putanja = f });
                    }
                }
                catch { }
            }
            startMeni = lista.OrderBy(s => s.Naziv, StringComparer.CurrentCultureIgnoreCase).ToList();
        });
    }

    public static List<Stavka> Pretrazi(string upit, int najvise)
    {
        string u = Pomocno.BezKvacica(upit.Trim());
        if (u.Length == 0) return new();
        var sve = startMeni;
        var pocinje = sve.Where(s => Pomocno.BezKvacica(s.Naziv).StartsWith(u));
        var sadrzi = sve.Where(s => !Pomocno.BezKvacica(s.Naziv).StartsWith(u) && Pomocno.BezKvacica(s.Naziv).Contains(u));
        return pocinje.Concat(sadrzi).Take(najvise).ToList();
    }

    // Zakaceni programi iz podesavanja. Poziva se sa UI niti (citanje .lnk ide preko COM-a).
    public static List<Stavka> Zakaceni()
    {
        var lista = new List<Stavka>();
        foreach (string p in Podesavanja.Trenutna.Zakaceni)
        {
            if (!zakaceniKes.TryGetValue(p, out var s))
            {
                string cilj = p.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? Sistem.CiljPrecice(p) : p;
                s = new Stavka
                {
                    Naziv = Path.GetFileNameWithoutExtension(p),
                    Putanja = p,
                    Cilj = cilj.ToLowerInvariant(),
                };
                zakaceniKes[p] = s;
            }
            lista.Add(s);
        }
        return lista;
    }

    public static bool Zakaci(string putanja)
    {
        var z = Podesavanja.Trenutna.Zakaceni;
        if (string.IsNullOrWhiteSpace(putanja) || z.Contains(putanja, StringComparer.OrdinalIgnoreCase)) return false;
        string ext = Path.GetExtension(putanja).ToLowerInvariant();
        if (ext != ".exe" && ext != ".lnk") return false;
        z.Add(putanja);
        Podesavanja.Sacuvaj();
        return true;
    }

    public static void Otkaci(string putanja)
    {
        Podesavanja.Trenutna.Zakaceni.RemoveAll(p => p.Equals(putanja, StringComparison.OrdinalIgnoreCase));
        Podesavanja.Sacuvaj();
    }

    public static void Pomeri(string putanja, int za)
    {
        var z = Podesavanja.Trenutna.Zakaceni;
        int i = z.FindIndex(p => p.Equals(putanja, StringComparison.OrdinalIgnoreCase));
        int j = i + za;
        if (i < 0 || j < 0 || j >= z.Count) return;
        (z[i], z[j]) = (z[j], z[i]);
        Podesavanja.Sacuvaj();
    }

    // Ikonica programa. Poziva se sa UI niti.
    public static ImageSource? Ikona(Stavka s)
    {
        if (ikone.TryGetValue(s.Putanja, out var gotova)) return gotova;
        var nova = Ikone.Ucitaj(s.Putanja);
        if (nova != null) ikone[s.Putanja] = nova;
        return nova;
    }

    // Programi koji trenutno imaju prozor: putanja .exe (malim slovima) -> prozor.
    public static Dictionary<string, IntPtr> Pokrenuti()
    {
        var rez = new Dictionary<string, IntPtr>();
        var sb = new StringBuilder(1024);
        Win.EnumWindows((h, l) =>
        {
            try
            {
                if (!Win.IsWindowVisible(h) || Win.GetWindowTextLength(h) == 0) return true;
                if (Win.GetWindow(h, 4) != IntPtr.Zero) return true; // ima vlasnika (dijalog)
                if (((long)Win.GetWindowLongPtr(h, Win.GWL_EXSTYLE) & Win.WS_EX_TOOLWINDOW) != 0) return true;
                Win.GetWindowThreadProcessId(h, out uint pid);
                IntPtr p = Win.OpenProcess(0x1000, false, pid);
                if (p == IntPtr.Zero) return true;
                try
                {
                    int n = sb.Capacity;
                    sb.Clear();
                    if (Win.QueryFullProcessImageName(p, 0, sb, ref n))
                    {
                        string exe = sb.ToString(0, n).ToLowerInvariant();
                        if (!rez.ContainsKey(exe)) rez[exe] = h;
                    }
                }
                finally { Win.CloseHandle(p); }
            }
            catch { }
            return true;
        }, IntPtr.Zero);
        return rez;
    }

    // Ako program vec radi prebacuje na njega, inace ga pokrece.
    public static void Pokreni(Stavka s)
    {
        try
        {
            if (s.Cilj.Length > 0 && Pokrenuti().TryGetValue(s.Cilj, out IntPtr h))
            {
                if (Win.IsIconic(h)) Win.ShowWindow(h, Win.SW_RESTORE);
                Win.SetForegroundWindow(h);
                return;
            }
            Process.Start(new ProcessStartInfo(s.Putanja) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Pomocno.Log(ex);
        }
    }
}
