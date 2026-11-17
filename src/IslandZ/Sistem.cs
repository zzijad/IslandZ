using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace IslandZ;

public static class Sistem
{
    private const string RunKljuc = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Naziv = "IslandZ";

    public static readonly string[] Precice = { "Ctrl+Razmak", "Alt+Razmak", "Ctrl+Alt+Razmak", "Isključeno" };

    private static string Exe =>
        Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "IslandZ.exe");

    public static bool AutostartUkljucen()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKljuc);
        return k?.GetValue(Naziv) != null;
    }

    public static void PostaviAutostart(bool ukljuci)
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKljuc, true) ?? Registry.CurrentUser.CreateSubKey(RunKljuc);
        if (ukljuci)
            k.SetValue(Naziv, $"\"{Exe}\"");
        else
            k.DeleteValue(Naziv, false);
    }

    // Modifikatori za RegisterHotKey; 0 znaci da je precica iskljucena.
    public static uint ModifikatoriPrecice(string precica) => precica switch
    {
        "Ctrl+Razmak" => Win.MOD_CONTROL,
        "Alt+Razmak" => Win.MOD_ALT,
        "Ctrl+Alt+Razmak" => Win.MOD_CONTROL | Win.MOD_ALT,
        _ => 0,
    };

    // Iz .lnk precice: program na koji pokazuje, fajl sa ikonicom i redni broj ikonice u njemu.
    // Prazno ako ne moze da se procita. Poziva se sa UI niti.
    public static (string cilj, string ikona, int indeks) CitajPrecicu(string lnk)
    {
        try
        {
            var tip = Type.GetTypeFromProgID("WScript.Shell");
            if (tip == null) return ("", "", 0);
            dynamic shell = Activator.CreateInstance(tip)!;
            dynamic precica = shell.CreateShortcut(lnk);
            string cilj = precica.TargetPath ?? "";
            string lok = precica.IconLocation ?? "";
            string ikona = lok;
            int indeks = 0;
            int zarez = lok.LastIndexOf(',');
            if (zarez >= 0)
            {
                ikona = lok.Substring(0, zarez);
                int.TryParse(lok.Substring(zarez + 1).Trim(), out indeks);
            }
            ikona = Environment.ExpandEnvironmentVariables(ikona.Trim().Trim('"'));
            return (Environment.ExpandEnvironmentVariables(cilj), ikona, indeks);
        }
        catch
        {
            return ("", "", 0);
        }
    }

    public static string CiljPrecice(string lnk) => CitajPrecicu(lnk).cilj;

    // Podsetnik pravi precicu na desktopu, a ako radi sa Windowsom i unos u Run kljucu.
    public static void OtvoriPodsetnik()
    {
        try
        {
            string lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Podsetnik.lnk");
            string? putanja = File.Exists(lnk) ? lnk : null;
            if (putanja == null)
            {
                using var k = Registry.CurrentUser.OpenSubKey(RunKljuc);
                if (k?.GetValue("Podsetnik") is string v)
                {
                    int a = v.IndexOf('"'), b = a >= 0 ? v.IndexOf('"', a + 1) : -1;
                    string exe = b > a ? v.Substring(a + 1, b - a - 1) : v.Trim();
                    if (File.Exists(exe)) putanja = exe;
                }
            }
            if (putanja != null)
                Process.Start(new ProcessStartInfo(putanja) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Pomocno.Log(ex);
        }
    }
}
