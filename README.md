# IslandZ

Stakleno „ostrvo" na vrhu ekrana za Windows 10/11: pokretač programa, muzika, rokovi iz Podsetnika i tajmer (WinUI 3, .NET 10).

## Kako se dobija gotov program
Svaki push pokreće „Build IslandZ" u kartici **Actions**. Gotov `IslandZ.zip` je u **Artifacts** tog pokretanja.
Sa grane `main` se automatski pravi i Release, kad se poveća `<Version>` u `src/IslandZ/IslandZ.csproj`.

## Instalacija
Raspakuj u stalni folder (npr. `C:\Programi\IslandZ`) i pokreni `IslandZ.exe`.
SmartScreen: „More info" → „Run anyway" (program nije digitalno potpisan).

## Korišćenje
- **Pilula** na vrhu ekrana pokazuje sat, ikonice pokrenutih zakačenih programa, muziku koja svira i tajmer.
- **Priđi kursorom** (ili klikni, zavisno od podešavanja) i ostrvo se raširi.
- **Kačenje programa:** prevuci prečicu ili `.exe` na ostrvo, ili klikni **+**. Desni klik na ikonicu: otkači, pomeri levo/desno.
- **Pretraga:** kucaj ime programa iz Start menija, Enter pokreće prvi rezultat. Desni klik na rezultat: zakači.
- **Muzika:** kontrole za plejer koji se prijavljuje Windowsu (Spotify, pregledači...).
- **Rokovi:** čitaju se iz baze programa Podsetnik (`%LOCALAPPDATA%\Podsetnik\podsetnik.db`), samo za čitanje.
- **Tajmer:** upiši minute i klikni „Pokreni".
- **Desni klik na ostrvo:** Podešavanja, Izlaz. Esc skuplja ostrvo.

## Podaci
`%LOCALAPPDATA%\IslandZ\podesavanja.json`, `greske.log` (ako nešto pukne).
