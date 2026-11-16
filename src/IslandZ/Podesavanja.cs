using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace IslandZ;

public class Podesavanja
{
    public string Stil { get; set; } = "staklo";          // "staklo" ili "crno"
    public int Velicina { get; set; } = 100;              // procenat, 80-140
    public int Providnost { get; set; } = 64;             // 0-100
    public bool OtvaranjeNaKlik { get; set; }
    public int ZadrskaMs { get; set; } = 250;
    public int Monitor { get; set; }                      // 0 = glavni
    public bool SakrijPrekoCelogEkrana { get; set; } = true;
    public bool PrikaziMuziku { get; set; } = true;
    public bool PrikaziPodsetnik { get; set; } = true;
    public bool PrikaziTajmer { get; set; } = true;
    public bool PrikaziTackice { get; set; } = true;
    public string Precica { get; set; } = "Ctrl+Razmak";  // vidi Sistem.Precice
    public List<string> Zakaceni { get; set; } = new();

    public static Podesavanja Trenutna { get; private set; } = new();

    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IslandZ");

    private static string Putanja => Path.Combine(Folder, "podesavanja.json");

    public static void Ucitaj()
    {
        try
        {
            if (File.Exists(Putanja))
                Trenutna = JsonSerializer.Deserialize<Podesavanja>(File.ReadAllText(Putanja)) ?? new();
        }
        catch
        {
            Trenutna = new();
        }
        Trenutna.Velicina = Math.Clamp(Trenutna.Velicina, 80, 140);
        Trenutna.Providnost = Math.Clamp(Trenutna.Providnost, 0, 100);
        Trenutna.ZadrskaMs = Math.Clamp(Trenutna.ZadrskaMs, 0, 1000);
        Trenutna.Zakaceni ??= new();
    }

    public static void Sacuvaj()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Putanja, JsonSerializer.Serialize(Trenutna, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Pomocno.Log(ex);
        }
    }
}
