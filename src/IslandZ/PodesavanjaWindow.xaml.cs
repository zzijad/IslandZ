using System;
using System.IO;
using System.Reflection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Graphics;
using WinRT.Interop;

namespace IslandZ;

public sealed partial class PodesavanjaWindow : Window
{
    private bool ucitavam = true;

    public PodesavanjaWindow()
    {
        InitializeComponent();
        Title = "IslandZ — podešavanja";
        PodesiProzor(560, 820);

        var pod = Podesavanja.Trenutna;
        Stil.SelectedIndex = pod.Stil == "crno" ? 1 : 0;
        Velicina.Value = pod.Velicina;
        Providnost.Value = pod.Providnost;
        Otvaranje.SelectedIndex = pod.OtvaranjeNaKlik ? 1 : 0;
        Zadrska.Value = pod.ZadrskaMs;

        Monitor.Items.Add("Glavni monitor");
        int broj = 0;
        try { broj = DisplayArea.FindAll().Count; } catch { }
        for (int i = 1; i <= broj; i++) Monitor.Items.Add("Monitor " + i);
        Monitor.SelectedIndex = pod.Monitor >= 0 && pod.Monitor <= broj ? pod.Monitor : 0;

        CeoEkran.IsOn = pod.SakrijPrekoCelogEkrana;
        MuzikaP.IsOn = pod.PrikaziMuziku;
        PodsetnikP.IsOn = pod.PrikaziPodsetnik;
        TajmerP.IsOn = pod.PrikaziTajmer;
        TackiceP.IsOn = pod.PrikaziTackice;

        try { Autostart.IsOn = Sistem.AutostartUkljucen(); } catch { }
        foreach (string p in Sistem.Precice) Precica.Items.Add(p);
        int ip = Array.IndexOf(Sistem.Precice, pod.Precica);
        Precica.SelectedIndex = ip >= 0 ? ip : Sistem.Precice.Length - 1;

        var v = Assembly.GetExecutingAssembly().GetName().Version;
        Verzija.Text = v != null ? $"IslandZ {v.Major}.{v.Minor}.{v.Build}" : "IslandZ";

        ucitavam = false;
    }

    private void PodesiProzor(int sirina, int visina)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        double skala = Win.GetDpiForWindow(hwnd) / 96.0;
        if (skala <= 0) skala = 1;
        int sw = (int)(sirina * skala), sh = (int)(visina * skala);
        var oblast = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        sw = Math.Min(sw, oblast.Width);
        sh = Math.Min(sh, oblast.Height);
        AppWindow.MoveAndResize(new RectInt32(
            oblast.X + (oblast.Width - sw) / 2,
            oblast.Y + (oblast.Height - sh) / 2,
            sw, sh));
        AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;
        string ikona = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(ikona)) AppWindow.SetIcon(ikona);
    }

    // Svaka promena se odmah cuva i primenjuje na ostrvo.
    private void Sacuvaj()
    {
        if (ucitavam) return;
        var pod = Podesavanja.Trenutna;
        pod.Stil = Stil.SelectedIndex == 1 ? "crno" : "staklo";
        pod.Velicina = (int)Velicina.Value;
        pod.Providnost = (int)Providnost.Value;
        pod.OtvaranjeNaKlik = Otvaranje.SelectedIndex == 1;
        pod.ZadrskaMs = (int)Zadrska.Value;
        pod.Monitor = Math.Max(0, Monitor.SelectedIndex);
        pod.SakrijPrekoCelogEkrana = CeoEkran.IsOn;
        pod.PrikaziMuziku = MuzikaP.IsOn;
        pod.PrikaziPodsetnik = PodsetnikP.IsOn;
        pod.PrikaziTajmer = TajmerP.IsOn;
        pod.PrikaziTackice = TackiceP.IsOn;
        if (Precica.SelectedIndex >= 0) pod.Precica = Sistem.Precice[Precica.SelectedIndex];
        Podesavanja.Sacuvaj();
        try { App.Island?.PrimeniPodesavanja(); }
        catch (Exception ex) { Pomocno.Log(ex); }
    }

    private void Promena_Izbor(object sender, SelectionChangedEventArgs e) => Sacuvaj();

    private void Promena_Klizac(object sender, RangeBaseValueChangedEventArgs e) => Sacuvaj();

    private void Promena_Prekidac(object sender, RoutedEventArgs e) => Sacuvaj();

    private void Autostart_Toggled(object sender, RoutedEventArgs e)
    {
        if (ucitavam) return;
        try { Sistem.PostaviAutostart(Autostart.IsOn); }
        catch (Exception ex) { Pomocno.Log(ex); }
    }
}
