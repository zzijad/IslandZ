using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace IslandZ;

public class Rok
{
    public long Id;
    public string Ime = "";
    public DateTime Datum;
    public int Dana;   // koliko dana do roka (negativno = istekao)

    public string Naslov => Dana < 0 ? "Rok je istekao" : Dana == 0 ? "Rok ističe danas" : "Rok ističe sutra";
}

// Cita rokove iz baze programa Podsetnik (samo citanje, nista se ne menja).
public static class Rokovi
{
    private static string Baza =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Podsetnik", "podsetnik.db");

    // Nezavrseni rokovi koji su istekli ili isticu danas ili sutra, najhitniji prvi.
    public static List<Rok> Hitni()
    {
        var lista = new List<Rok>();
        try
        {
            if (!File.Exists(Baza)) return lista;
            var b = new SqliteConnectionStringBuilder
            {
                DataSource = Baza,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
            };
            using var c = new SqliteConnection(b.ToString());
            c.Open();

            bool imaNeogranicen = false;
            using (var info = c.CreateCommand())
            {
                info.CommandText = "PRAGMA table_info(osobe)";
                using var ri = info.ExecuteReader();
                while (ri.Read())
                    if (ri.GetString(1) == "neogranicen") imaNeogranicen = true;
            }

            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT id, ime, prezime, rok FROM osobe WHERE zavrseno = 0"
                              + (imaNeogranicen ? " AND neogranicen = 0" : "");
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                string rok = r.IsDBNull(3) ? "" : r.GetString(3);
                if (!DateTime.TryParseExact(rok, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    continue;
                int dana = (d.Date - DateTime.Today).Days;
                if (dana > 1) continue;
                lista.Add(new Rok
                {
                    Id = r.GetInt64(0),
                    Ime = (r.GetString(1) + " " + r.GetString(2)).Trim(),
                    Datum = d,
                    Dana = dana,
                });
            }
        }
        catch (Exception ex)
        {
            Pomocno.Log(ex);
        }
        return lista.OrderBy(x => x.Datum).ThenBy(x => x.Ime).ToList();
    }
}
