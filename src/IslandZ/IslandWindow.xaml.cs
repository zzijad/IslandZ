using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Storage.Pickers;
using Windows.UI;
using WinRT.Interop;
using VirtualKey = Windows.System.VirtualKey;

namespace IslandZ;

// Ostrvo. Nije obican WinUI prozor: XAML sadrzaj zivi u golom Win32 prozoru bez
// sopstvene povrsine (WS_EX_NOREDIRECTIONBITMAP), jer je samo takav prozor zaista
// providan i samo kroz njega staklo vidi ono sto je iza.
public sealed partial class IslandWindow : UserControl
{
    private enum Stanje { Pilula, Rasireno, Obavestenje }

    private const int UKoloni = 6;

    private readonly IntPtr hwnd, xamlHwnd;
    private readonly Win.WndProc procedura; // drzi se u polju da ga GC ne pokupi
    private readonly DesktopWindowXamlSource izvor;
    private readonly DispatcherQueueTimer animTajmer, pollTajmer, sekTajmer;
    private readonly Stopwatch anim = new();
    private readonly Random rnd = new();
    private readonly CultureInfo kultura;
    private readonly RectangleClip isecakSadrzaja;
    private Staklo? staklo;
    private bool stakloNeuspelo;

    private Stanje stanje = Stanje.Pilula;
    private double dpi = 1, skala = 1;
    private RectInt32 oblast;          // radna povrsina izabranog monitora
    private RectInt32 prozor;          // gde je prozor sada (pikseli)
    private double dizajnW = 100;      // sirina trenutnog sadrzaja (pre skale)
    private double sadW, sadH, sadR;   // trenutni oblik (pikseli)
    private double odW, odH, odR, doW, doH, doR;

    private bool sakriven, meniOtvoren, drziOtvoreno, bioAktivan, cekajIzlazak, ugasen, precicaRegistrovana;
    private DateTime? hoverOd, vanOd;
    private DateTime obavOd;
    private Action? obavAkcija;
    private int brojac, sekunde;

    private List<Stavka> prikazani = new();
    private readonly List<(Stavka s, Ellipse tacka)> tacke = new();
    private Dictionary<string, IntPtr> pokrenuti = new();
    private string miniKljuc = "";
    private MuzikaStanje? muzika;
    private string muzikaKljuc = "";
    private bool citamMuziku;
    private List<Rok> rokovi = new();
    private readonly HashSet<string> obavesteni = new();
    private DateTime? tajmerKraj;

    public IslandWindow()
    {
        InitializeComponent();

        procedura = ProzorskaProcedura;
        IntPtr instanca = Win.GetModuleHandleW(null);
        var klasa = new Win.WNDCLASSW
        {
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(procedura),
            hInstance = instanca,
            hCursor = Win.LoadCursorW(IntPtr.Zero, (IntPtr)32512),
            lpszClassName = "IslandZHost",
        };
        Win.RegisterClassW(ref klasa);
        hwnd = Win.CreateWindowExW(
            Win.WS_EX_NOREDIRECTIONBITMAP | Win.WS_EX_TOOLWINDOW | Win.WS_EX_TOPMOST,
            "IslandZHost", "IslandZ", Win.WS_POPUP, 0, 0, 0, 0,
            IntPtr.Zero, IntPtr.Zero, instanca, IntPtr.Zero);
        if (hwnd == IntPtr.Zero)
            throw new InvalidOperationException("Prozor ostrva nije napravljen, greska " + Marshal.GetLastWin32Error());

        izvor = new DesktopWindowXamlSource();
        izvor.Initialize(Win32Interop.GetWindowIdFromWindow(hwnd));
        izvor.Content = this;
        xamlHwnd = Win32Interop.GetWindowFromWindowId(izvor.SiteBridge.WindowId);

        try { kultura = new CultureInfo("sr-Latn-RS"); }
        catch { kultura = CultureInfo.CurrentCulture; }

        // Sadrzaj se sece na isti zaobljeni oblik kao ostrvo.
        var platnoVizual = ElementCompositionPreview.GetElementVisual(Platno);
        isecakSadrzaja = platnoVizual.Compositor.CreateRectangleClip();
        platnoVizual.Clip = isecakSadrzaja;

        animTajmer = DispatcherQueue.CreateTimer();
        animTajmer.Interval = TimeSpan.FromMilliseconds(15);
        animTajmer.Tick += (s, e) => AnimTick();

        pollTajmer = DispatcherQueue.CreateTimer();
        pollTajmer.Interval = TimeSpan.FromMilliseconds(100);
        pollTajmer.Tick += (s, e) => Bezbedno(Poll);

        sekTajmer = DispatcherQueue.CreateTimer();
        sekTajmer.Interval = TimeSpan.FromSeconds(1);
        sekTajmer.Tick += (s, e) => Bezbedno(Sekunda);

        // Pre prvog prikaza prozor dobija oblik pilule, da ne blesne veliki prazan prozor.
        PripremiMonitor();
        PrimeniOblik(110 * dpi, 40 * dpi, 20 * dpi);

        Koren.Loaded += (s, e) => Bezbedno(() =>
        {
            PrimeniPodesavanja();
            Sekunda();
            pollTajmer.Start();
            sekTajmer.Start();
        });
    }

    private static void Bezbedno(Action a)
    {
        try { a(); }
        catch (Exception ex) { Pomocno.Log(ex); }
    }

    private static SolidColorBrush Boja(byte a, byte r, byte g, byte b) =>
        new(Color.FromArgb(a, r, g, b));

    // ---------- Oblik i polozaj prozora ----------

    private void PripremiMonitor()
    {
        DisplayArea da = DisplayArea.Primary;
        try
        {
            int i = Podesavanja.Trenutna.Monitor;
            var sve = DisplayArea.FindAll();
            if (i > 0 && i <= sve.Count) da = sve[i - 1];
        }
        catch { }
        oblast = da.WorkArea;
        Win.SetWindowPos(hwnd, IntPtr.Zero, oblast.X + oblast.Width / 2, oblast.Y, 1, 1, Win.SWP_NOZORDER | Win.SWP_NOACTIVATE);
        double d = Win.GetDpiForWindow(hwnd) / 96.0;
        dpi = d > 0 ? d : 1;
    }

    private void PrimeniOblik(double w, double h, double r)
    {
        sadW = w; sadH = h; sadR = r;
        int iw = Math.Max(1, (int)Math.Round(w)), ih = Math.Max(1, (int)Math.Round(h));
        int x = oblast.X + (oblast.Width - iw) / 2;
        int y = oblast.Y + (int)Math.Round(8 * dpi);
        prozor = new RectInt32(x, y, iw, ih);
        Win.SetWindowPos(hwnd, IntPtr.Zero, x, y, iw, ih, Win.SWP_NOZORDER | Win.SWP_NOACTIVATE);
        Win.SetWindowPos(xamlHwnd, IntPtr.Zero, 0, 0, iw, ih, Win.SWP_NOZORDER | Win.SWP_NOACTIVATE);
        Okvir.CornerRadius = new CornerRadius(r / dpi);
        Staklo.Zaobli(isecakSadrzaja, iw / dpi, ih / dpi, r / dpi);
        staklo?.Oblik(iw / dpi, ih / dpi, r / dpi);
    }

    private (double w, double h, double r) Dizajn()
    {
        switch (stanje)
        {
            case Stanje.Rasireno:
                RasirenoPanel.Measure(new Size(600, double.PositiveInfinity));
                return (600, Math.Max(120, RasirenoPanel.DesiredSize.Height), 40);
            case Stanje.Obavestenje:
                return (470, 80, 40);
            default:
                PilulaPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                return (Math.Max(90, PilulaPanel.DesiredSize.Width), 40, 20);
        }
    }

    // Izracunava koliki prozor treba za trenutni sadrzaj i dovodi ga na tu velicinu.
    private void Oblikuj(bool animiraj)
    {
        var (w, h, r) = Dizajn();
        dizajnW = w;
        double k = skala * dpi;
        double nw = w * k, nh = h * k, nr = r * k;
        Centriraj();

        bool istiCilj = Math.Abs(nw - doW) < 1 && Math.Abs(nh - doH) < 1;
        doW = nw; doH = nh; doR = nr;

        if (!animiraj)
        {
            animTajmer.Stop();
            PrimeniOblik(doW, doH, doR);
            return;
        }
        if (istiCilj && (animTajmer.IsRunning || (Math.Abs(sadW - doW) < 1 && Math.Abs(sadH - doH) < 1)))
            return;

        odW = sadW; odH = sadH; odR = sadR;
        anim.Restart();
        animTajmer.Start();
    }

    private void AnimTick()
    {
        try
        {
            double t = Math.Min(1, anim.ElapsedMilliseconds / 220.0);
            double e = 1 - Math.Pow(1 - t, 3);
            PrimeniOblik(odW + (doW - odW) * e, odH + (doH - odH) * e, odR + (doR - odR) * e);
            if (t >= 1) animTajmer.Stop();
        }
        catch (Exception ex)
        {
            animTajmer.Stop();
            Pomocno.Log(ex);
        }
    }

    private void Centriraj()
    {
        Canvas.SetLeft(Sadrzaj, (Koren.ActualWidth - dizajnW * skala) / 2);
        Canvas.SetTop(Sadrzaj, 0);
    }

    private void Koren_SizeChanged(object sender, SizeChangedEventArgs e) => Centriraj();

    // ---------- Stanja ----------

    private void PostaviStanje(Stanje s)
    {
        stanje = s;
        PilulaPanel.Visibility = s == Stanje.Pilula ? Visibility.Visible : Visibility.Collapsed;
        RasirenoPanel.Visibility = s == Stanje.Rasireno ? Visibility.Visible : Visibility.Collapsed;
        ObavestenjePanel.Visibility = s == Stanje.Obavestenje ? Visibility.Visible : Visibility.Collapsed;
        hoverOd = null;
        vanOd = null;
        if (s != Stanje.Rasireno) drziOtvoreno = false;
        Oblikuj(true);
    }

    private void Rasiri(bool saTastature)
    {
        if (Pretraga.Text.Length > 0) Pretraga.Text = "";
        OsveziPrograme();
        PostaviStanje(Stanje.Rasireno);
        if (saTastature)
        {
            drziOtvoreno = true;
            bioAktivan = false;
            Win.SetForegroundWindow(hwnd);
            Win.SetFocus(xamlHwnd);
            Pretraga.Focus(FocusState.Programmatic);
        }
    }

    private void Skupi()
    {
        PostaviStanje(Stanje.Pilula);
        if (Pretraga.Text.Length > 0) Pretraga.Text = "";
    }

    private void Prebaci()
    {
        if (sakriven) return;
        if (stanje == Stanje.Rasireno)
        {
            cekajIzlazak = true;
            Skupi();
        }
        else
        {
            Rasiri(true);
        }
    }

    public void Obavesti(string naslov, string tekst, string glif, Action? akcija)
    {
        if (sakriven) return;
        ObavNaslov.Text = naslov;
        ObavTekst.Text = tekst;
        ObavIkona.Glyph = glif;
        obavAkcija = akcija;
        ObavDugme.Visibility = akcija != null ? Visibility.Visible : Visibility.Collapsed;
        obavOd = DateTime.Now;
        PostaviStanje(Stanje.Obavestenje);
    }

    // ---------- Tajmeri ----------

    // Na 100 ms: prati kursor i odlucuje kad se ostrvo siri i skuplja.
    private void Poll()
    {
        if (ugasen || sakriven) return;
        Win.GetCursorPos(out var c);
        bool unutra = c.X >= prozor.X && c.X < prozor.X + prozor.Width
                      && c.Y >= oblast.Y && c.Y < prozor.Y + prozor.Height;
        if (!unutra) cekajIzlazak = false;

        var sad = DateTime.Now;
        var pod = Podesavanja.Trenutna;

        switch (stanje)
        {
            case Stanje.Pilula:
                if (unutra && !pod.OtvaranjeNaKlik && !cekajIzlazak)
                {
                    hoverOd ??= sad;
                    if ((sad - hoverOd.Value).TotalMilliseconds >= pod.ZadrskaMs) Rasiri(false);
                }
                else
                {
                    hoverOd = null;
                }
                break;

            case Stanje.Rasireno:
                IntPtr fg = Win.GetForegroundWindow();
                if (drziOtvoreno)
                {
                    if (fg == hwnd) bioAktivan = true;
                    else if (bioAktivan) drziOtvoreno = false;
                    if (unutra) drziOtvoreno = false;
                }
                bool kuca = fg == hwnd && Pretraga.Text.Length > 0;
                if (unutra || meniOtvoren || drziOtvoreno || kuca)
                {
                    vanOd = null;
                }
                else
                {
                    vanOd ??= sad;
                    if ((sad - vanOd.Value).TotalMilliseconds >= 500) Skupi();
                }
                break;

            case Stanje.Obavestenje:
                if ((sad - obavOd).TotalSeconds >= 6 && !unutra) Skupi();
                break;
        }

        // Trake u piluli se micu dok muzika svira.
        brojac++;
        if (stanje == Stanje.Pilula && muzika != null && muzika.Svira && brojac % 2 == 0)
        {
            Traka1.Height = 5 + rnd.Next(14);
            Traka2.Height = 5 + rnd.Next(14);
            Traka3.Height = 5 + rnd.Next(14);
            Traka4.Height = 5 + rnd.Next(14);
        }
    }

    // Na 1 s: sat, tajmer, pokrenuti programi, muzika, rokovi, ceo ekran.
    private void Sekunda()
    {
        if (ugasen) return;
        var sad = DateTime.Now;
        string sat = sad.ToString("HH:mm", CultureInfo.InvariantCulture);
        PilulaSat.Text = sat;
        Sat.Text = sat;
        Datum.Text = sad.ToString("ddd d. MMM", kultura);

        if (tajmerKraj is DateTime kraj && kraj <= sad)
        {
            tajmerKraj = null;
            Win.MessageBeep(0x40);
            OsveziTajmer();
            Obavesti("Tajmer", "Vreme je isteklo", "", null);
        }
        else
        {
            OsveziTajmer();
        }

        if (sekunde % 2 == 0) OsveziPokrenute();
        if (Podesavanja.Trenutna.PrikaziMuziku) _ = OsveziMuziku();
        if (++sekunde % 300 == 0) OsveziRokove();
        ProveriCeoEkran();
        if (!sakriven) Oblikuj(true);
    }

    // ---------- Programi ----------

    private void OsveziPrograme()
    {
        ProgramiPanel.Children.Clear();
        tacke.Clear();
        string upit = Pretraga.Text.Trim();
        bool pretraga = upit.Length > 0;
        prikazani = pretraga ? Programi.Pretrazi(upit, UKoloni * 2) : Programi.Zakaceni();

        StackPanel? red = null;
        for (int i = 0; i < prikazani.Count; i++)
        {
            if (i % UKoloni == 0)
            {
                red = new StackPanel { Orientation = Orientation.Horizontal };
                ProgramiPanel.Children.Add(red);
            }
            red!.Children.Add(NapraviDugme(prikazani[i], !pretraga));
        }

        PrazanTekst.Text = pretraga
            ? "Nema programa sa tim imenom."
            : "Prevuci prečicu programa ovde ili klikni + da je zakačiš.";
        PrazanTekst.Visibility = prikazani.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        OsveziTacke();
        if (stanje == Stanje.Rasireno) Oblikuj(true);
    }

    private Button NapraviDugme(Stavka s, bool zakacen)
    {
        var slika = new Image { Width = 40, Height = 40 };
        UcitajIkonu(slika, s);

        var kutija = new Border
        {
            Width = 60,
            Height = 60,
            CornerRadius = new CornerRadius(17),
            Background = Boja(0x24, 255, 255, 255),
            BorderBrush = Boja(0x40, 255, 255, 255),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = slika,
        };
        var ime = new TextBlock
        {
            Text = s.Naziv,
            FontSize = 12,
            MaxWidth = 84,
            TextTrimming = TextTrimming.CharacterEllipsis,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var tacka = new Ellipse
        {
            Width = 4,
            Height = 4,
            Fill = Boja(255, 255, 255, 255),
            Opacity = 0,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var sp = new StackPanel { Spacing = 5 };
        sp.Children.Add(kutija);
        sp.Children.Add(ime);
        sp.Children.Add(tacka);

        var dugme = new Button
        {
            Content = sp,
            Width = 92,
            Padding = new Thickness(0, 6, 0, 4),
            Background = Boja(0, 0, 0, 0),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(14),
        };
        ToolTipService.SetToolTip(dugme, s.Naziv);
        dugme.Click += (a, b) => PokreniStavku(s);

        var meni = new MenuFlyout();
        meni.Opened += (a, b) => meniOtvoren = true;
        meni.Closed += (a, b) => meniOtvoren = false;
        if (zakacen)
        {
            var otkaci = new MenuFlyoutItem { Text = "Otkači" };
            otkaci.Click += (a, b) => { Programi.Otkaci(s.Putanja); OsveziPrograme(); };
            var levo = new MenuFlyoutItem { Text = "Pomeri levo" };
            levo.Click += (a, b) => { Programi.Pomeri(s.Putanja, -1); OsveziPrograme(); };
            var desno = new MenuFlyoutItem { Text = "Pomeri desno" };
            desno.Click += (a, b) => { Programi.Pomeri(s.Putanja, 1); OsveziPrograme(); };
            meni.Items.Add(levo);
            meni.Items.Add(desno);
            meni.Items.Add(otkaci);
            tacke.Add((s, tacka));
        }
        else
        {
            var zakaci = new MenuFlyoutItem { Text = "Zakači" };
            zakaci.Click += (a, b) => { Programi.Zakaci(s.Putanja); Pretraga.Text = ""; OsveziPrograme(); };
            meni.Items.Add(zakaci);
        }
        dugme.ContextFlyout = meni;
        return dugme;
    }

    private void UcitajIkonu(Image slika, Stavka s)
    {
        var izvor = Programi.Ikona(s);
        if (izvor != null) slika.Source = izvor;
    }

    private void PokreniStavku(Stavka s)
    {
        Programi.Pokreni(s);
        cekajIzlazak = true;
        Skupi();
    }

    private void OsveziPokrenute()
    {
        pokrenuti = Programi.Pokrenuti();
        OsveziTacke();

        // Male ikonice pokrenutih zakacenih programa u piluli (najvise 4).
        var rade = Programi.Zakaceni().Where(Radi).Take(4).ToList();
        string kljuc = string.Join("|", rade.Select(x => x.Putanja));
        if (kljuc == miniKljuc) return;
        miniKljuc = kljuc;
        MiniIkone.Children.Clear();
        foreach (var s in rade)
        {
            var slika = new Image { Width = 20, Height = 20 };
            UcitajIkonu(slika, s);
            MiniIkone.Children.Add(slika);
        }
        MiniIkone.Visibility = rade.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool Radi(Stavka s) => s.Cilj.Length > 0 && pokrenuti.ContainsKey(s.Cilj);

    private void OsveziTacke()
    {
        bool prikazi = Podesavanja.Trenutna.PrikaziTackice;
        foreach (var (s, tacka) in tacke)
            tacka.Opacity = prikazi && Radi(s) ? 1 : 0;
    }

    // ---------- Muzika ----------

    private async Task OsveziMuziku()
    {
        if (citamMuziku) return;
        citamMuziku = true;
        try
        {
            var m = await Muzika.Procitaj();
            if (!Podesavanja.Trenutna.PrikaziMuziku) m = null;
            muzika = m;
            MuzikaPlocica.Visibility = m != null ? Visibility.Visible : Visibility.Collapsed;
            PilulaMuzika.Visibility = m != null && m.Svira ? Visibility.Visible : Visibility.Collapsed;
            if (m == null)
            {
                muzikaKljuc = "";
                return;
            }

            MuzikaNaslov.Text = m.Naslov;
            MuzikaIzvodjac.Text = m.Izvodjac;
            PustiIkona.Glyph = m.Svira ? "" : "";

            string kljuc = m.Naslov + "|" + m.Izvodjac;
            if (kljuc != muzikaKljuc)
            {
                muzikaKljuc = kljuc;
                MuzikaSlika.Source = null;
                if (m.Slika != null)
                {
                    using var tok = await m.Slika.OpenReadAsync();
                    var bmp = new BitmapImage();
                    await bmp.SetSourceAsync(tok);
                    MuzikaSlika.Source = bmp;
                }
            }
        }
        catch (Exception ex)
        {
            Pomocno.Log(ex);
        }
        finally
        {
            citamMuziku = false;
        }
    }

    private async void Pusti_Click(object sender, RoutedEventArgs e)
    {
        await Muzika.PustiPauziraj();
        await OsveziMuziku();
    }

    private async void Sledeca_Click(object sender, RoutedEventArgs e) => await Muzika.Sledeca();

    private async void Prethodna_Click(object sender, RoutedEventArgs e) => await Muzika.Prethodna();

    // ---------- Rokovi iz Podsetnika ----------

    private void OsveziRokove()
    {
        if (!Podesavanja.Trenutna.PrikaziPodsetnik)
        {
            rokovi = new();
            return;
        }
        rokovi = Rokovi.Hitni();
        if (rokovi.Count > 0)
        {
            var r = rokovi[0];
            RokNaslov.Text = r.Naslov;
            RokTekst.Text = r.Ime + " · " + r.Datum.ToString("dd.MM.", CultureInfo.InvariantCulture)
                            + (rokovi.Count > 1 ? "  (+" + (rokovi.Count - 1) + ")" : "");
        }
        else
        {
            RokNaslov.Text = "Nema hitnih rokova";
            RokTekst.Text = "Podsetnik";
        }

        // Obavestenje jednom dnevno za svaki rok.
        string dan = DateTime.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var novi = rokovi.Where(r => obavesteni.Add(dan + ":" + r.Id)).ToList();
        if (novi.Count > 0)
            Obavesti("Podsetnik", novi[0].Naslov + " — " + novi[0].Ime, "", Sistem.OtvoriPodsetnik);
    }

    private void Rok_Tapped(object sender, TappedRoutedEventArgs e)
    {
        Sistem.OtvoriPodsetnik();
        cekajIzlazak = true;
        Skupi();
    }

    private void ObavDugme_Click(object sender, RoutedEventArgs e)
    {
        var a = obavAkcija;
        cekajIzlazak = true;
        Skupi();
        a?.Invoke();
    }

    // ---------- Tajmer ----------

    private void TajmerDugme_Click(object sender, RoutedEventArgs e) => PrebaciTajmer();

    private void TajmerMinuti_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            PrebaciTajmer();
        }
    }

    private void PrebaciTajmer()
    {
        if (tajmerKraj != null)
        {
            tajmerKraj = null;
        }
        else if (double.TryParse(TajmerMinuti.Text.Trim().Replace(',', '.'), NumberStyles.Float,
                     CultureInfo.InvariantCulture, out double min) && min > 0 && min <= 1440)
        {
            tajmerKraj = DateTime.Now.AddMinutes(min);
        }
        OsveziTajmer();
    }

    private void OsveziTajmer()
    {
        bool radi = tajmerKraj != null && Podesavanja.Trenutna.PrikaziTajmer;
        TajmerUnos.Visibility = radi ? Visibility.Collapsed : Visibility.Visible;
        TajmerPreostalo.Visibility = radi ? Visibility.Visible : Visibility.Collapsed;
        PilulaTajmer.Visibility = radi ? Visibility.Visible : Visibility.Collapsed;
        TajmerDugme.Content = tajmerKraj != null ? "Zaustavi" : "Pokreni";
        if (tajmerKraj is DateTime kraj)
        {
            var ost = TimeSpan.FromSeconds(Math.Max(0, Math.Ceiling((kraj - DateTime.Now).TotalSeconds)));
            string t = ost.TotalHours >= 1
                ? $"{(int)ost.TotalHours}:{ost.Minutes:00}:{ost.Seconds:00}"
                : $"{ost.Minutes}:{ost.Seconds:00}";
            TajmerPreostalo.Text = t;
            PilulaTajmerTekst.Text = t;
        }
    }

    // ---------- Ceo ekran ----------

    private void ProveriCeoEkran()
    {
        bool ceo = false;
        if (Podesavanja.Trenutna.SakrijPrekoCelogEkrana)
        {
            IntPtr fg = Win.GetForegroundWindow();
            if (fg != IntPtr.Zero && fg != hwnd)
            {
                var sb = new StringBuilder(64);
                Win.GetClassName(fg, sb, 64);
                string klasa = sb.ToString();
                if (klasa != "Progman" && klasa != "WorkerW" && klasa != "Shell_TrayWnd"
                    && !Win.IsZoomed(fg) && Win.GetWindowRect(fg, out var r))
                {
                    var mi = new Win.MONITORINFO { cbSize = Marshal.SizeOf<Win.MONITORINFO>() };
                    if (Win.GetMonitorInfo(Win.MonitorFromWindow(fg, 2), ref mi))
                    {
                        var m = mi.rcMonitor;
                        bool pokriva = r.Left <= m.Left && r.Top <= m.Top && r.Right >= m.Right && r.Bottom >= m.Bottom;
                        int cx = prozor.X + prozor.Width / 2;
                        bool nasMonitor = cx >= m.Left && cx < m.Right && prozor.Y >= m.Top && prozor.Y < m.Bottom;
                        ceo = pokriva && nasMonitor;
                    }
                }
            }
        }
        if (ceo == sakriven) return;
        sakriven = ceo;
        if (ceo) Win.ShowWindow(hwnd, Win.SW_HIDE);
        else Prikazi();
    }

    // ---------- Precica na tastaturi ----------

    private void RegistrujPrecicu()
    {
        if (precicaRegistrovana)
        {
            Win.UnregisterHotKey(hwnd, 1);
            precicaRegistrovana = false;
        }
        uint mod = Sistem.ModifikatoriPrecice(Podesavanja.Trenutna.Precica);
        if (mod != 0)
            precicaRegistrovana = Win.RegisterHotKey(hwnd, 1, mod | Win.MOD_NOREPEAT, Win.VK_SPACE);
    }

    private IntPtr ProzorskaProcedura(IntPtr h, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == Win.WM_HOTKEY)
        {
            Bezbedno(Prebaci);
            return IntPtr.Zero;
        }
        return Win.DefWindowProcW(h, msg, wParam, lParam);
    }

    // ---------- Podesavanja ----------

    // Poziva se pri pokretanju i posle svake promene u prozoru podesavanja.
    public void PrimeniPodesavanja()
    {
        var pod = Podesavanja.Trenutna;
        PripremiMonitor();

        skala = pod.Velicina / 100.0;
        Skala.ScaleX = skala;
        Skala.ScaleY = skala;

        if (pod.Stil == "crno")
        {
            if (staklo != null) staklo.Vidljivo = false;
            Okvir.Background = Boja(255, 0, 0, 0);
            Okvir.BorderBrush = Boja(0x33, 255, 255, 255);
        }
        else
        {
            if (staklo == null && !stakloNeuspelo)
            {
                try
                {
                    staklo = new Staklo(StakloDomacin, pod.Providnost);
                }
                catch (Exception ex)
                {
                    stakloNeuspelo = true;
                    Pomocno.Log(ex);
                }
            }
            if (staklo != null)
            {
                staklo.Postavi(pod.Providnost);
                staklo.Vidljivo = true;
                Okvir.Background = Boja(0, 0, 0, 0);
            }
            else
            {
                // Zamucenje nije dostupno na ovom sistemu: providna tamna boja bez zamucenja.
                byte a = (byte)(235 - pod.Providnost);
                Okvir.Background = Boja(a, 22, 28, 44);
            }
            Okvir.BorderBrush = Boja(0x66, 255, 255, 255);
        }

        // Donji red: rok i tajmer jedan pored drugog, ili jedan preko cele sirine.
        bool rok = pod.PrikaziPodsetnik, taj = pod.PrikaziTajmer;
        DonjiRed.Visibility = rok || taj ? Visibility.Visible : Visibility.Collapsed;
        RokPlocica.Visibility = rok ? Visibility.Visible : Visibility.Collapsed;
        TajmerPlocica.Visibility = taj ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumn(RokPlocica, 0);
        Grid.SetColumnSpan(RokPlocica, taj ? 1 : 2);
        Grid.SetColumn(TajmerPlocica, rok ? 1 : 0);
        Grid.SetColumnSpan(TajmerPlocica, rok ? 1 : 2);

        if (!pod.PrikaziMuziku)
        {
            muzika = null;
            MuzikaPlocica.Visibility = Visibility.Collapsed;
            PilulaMuzika.Visibility = Visibility.Collapsed;
        }

        OsveziTajmer();
        OsveziRokove();
        OsveziPrograme();
        OsveziPokrenute();
        RegistrujPrecicu();
        Oblikuj(false);
    }

    public void Ugasi()
    {
        if (ugasen) return;
        ugasen = true;
        animTajmer.Stop();
        pollTajmer.Stop();
        sekTajmer.Stop();
        if (precicaRegistrovana) Win.UnregisterHotKey(hwnd, 1);
        try
        {
            Win.ShowWindow(hwnd, Win.SW_HIDE);
            izvor.Dispose();
            Win.DestroyWindow(hwnd);
        }
        catch (Exception ex)
        {
            Pomocno.Log(ex);
        }
    }

    // Prikazuje ostrvo bez uzimanja fokusa od programa u kome korisnik radi.
    public void Prikazi()
    {
        Win.ShowWindow(hwnd, Win.SW_SHOWNOACTIVATE);
        izvor.SiteBridge.Show();
    }

    // ---------- Dogadjaji iz XAML-a ----------

    private void Pilula_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed && stanje == Stanje.Pilula)
            Rasiri(false);
    }

    private void Koren_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape && stanje != Stanje.Pilula)
        {
            e.Handled = true;
            cekajIzlazak = true;
            Skupi();
        }
    }

    private void Pretraga_TextChanged(object sender, TextChangedEventArgs e) => OsveziPrograme();

    private void Pretraga_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && Pretraga.Text.Trim().Length > 0 && prikazani.Count > 0)
        {
            e.Handled = true;
            PokreniStavku(prikazani[0]);
        }
    }

    private void Meni_Opened(object sender, object e) => meniOtvoren = true;

    private void Meni_Closed(object sender, object e) => meniOtvoren = false;

    private void Podesavanja_Click(object sender, RoutedEventArgs e) => App.PrikaziPodesavanja();

    private void Izlaz_Click(object sender, RoutedEventArgs e) => App.Ugasi();

    private async void Dodaj_Click(object sender, RoutedEventArgs e)
    {
        meniOtvoren = true;
        try
        {
            var birac = new FileOpenPicker();
            InitializeWithWindow.Initialize(birac, hwnd);
            birac.SuggestedStartLocation = PickerLocationId.Desktop;
            birac.FileTypeFilter.Add(".exe");
            birac.FileTypeFilter.Add(".lnk");
            var fajl = await birac.PickSingleFileAsync();
            if (fajl != null && Programi.Zakaci(fajl.Path)) OsveziPrograme();
        }
        catch (Exception ex)
        {
            Pomocno.Log(ex);
        }
        finally
        {
            meniOtvoren = false;
        }
    }

    private void Koren_DragOver(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        e.AcceptedOperation = DataPackageOperation.Link;
        if (stanje == Stanje.Pilula) Rasiri(false);
    }

    private async void Koren_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
            var stavke = await e.DataView.GetStorageItemsAsync();
            bool dodato = false;
            foreach (var s in stavke)
                if (Programi.Zakaci(s.Path)) dodato = true;
            if (dodato) OsveziPrograme();
        }
        catch (Exception ex)
        {
            Pomocno.Log(ex);
        }
    }
}
