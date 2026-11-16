using System;
using System.Threading.Tasks;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace IslandZ;

public class MuzikaStanje
{
    public string Naslov = "";
    public string Izvodjac = "";
    public bool Svira;
    public IRandomAccessStreamReference? Slika;
}

// Ono sto Windows zna o plejeru koji trenutno svira (Spotify, pregledac...).
public static class Muzika
{
    private static GlobalSystemMediaTransportControlsSessionManager? menadzer;

    private static async Task<GlobalSystemMediaTransportControlsSession?> Sesija()
    {
        menadzer ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        return menadzer.GetCurrentSession();
    }

    public static async Task<MuzikaStanje?> Procitaj()
    {
        try
        {
            var s = await Sesija();
            if (s == null) return null;
            var info = s.GetPlaybackInfo();
            var p = await s.TryGetMediaPropertiesAsync();
            if (p == null || string.IsNullOrWhiteSpace(p.Title)) return null;
            return new MuzikaStanje
            {
                Naslov = p.Title,
                Izvodjac = p.Artist ?? "",
                Svira = info != null && info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                Slika = p.Thumbnail,
            };
        }
        catch
        {
            return null;
        }
    }

    public static async Task PustiPauziraj()
    {
        try { var s = await Sesija(); if (s != null) await s.TryTogglePlayPauseAsync(); } catch { }
    }

    public static async Task Sledeca()
    {
        try { var s = await Sesija(); if (s != null) await s.TrySkipNextAsync(); } catch { }
    }

    public static async Task Prethodna()
    {
        try { var s = await Sesija(); if (s != null) await s.TrySkipPreviousAsync(); } catch { }
    }
}
