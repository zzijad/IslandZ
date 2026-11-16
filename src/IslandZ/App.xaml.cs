using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace IslandZ;

public partial class App : Application
{
    public static DispatcherQueue? UiQueue;
    public static IslandWindow? Island;
    private static PodesavanjaWindow? prozorPodesavanja;

    public App()
    {
        InitializeComponent();
        // Program zivi dok ga korisnik ne ugasi iz menija, bez obzira na prozore.
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;
        UnhandledException += (s, e) =>
        {
            e.Handled = true;
            Pomocno.Log(e.Exception);
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        UiQueue = DispatcherQueue.GetForCurrentThread();
        Podesavanja.Ucitaj();
        Programi.UcitajStartMeni();

        Island = new IslandWindow();
        Island.Activate();
    }

    public static void PrikaziPodesavanja()
    {
        if (prozorPodesavanja == null)
        {
            prozorPodesavanja = new PodesavanjaWindow();
            prozorPodesavanja.Closed += (s, e) => prozorPodesavanja = null;
        }
        prozorPodesavanja.Activate();
        Pomocno.UPrviPlan(prozorPodesavanja);
    }

    public static void Ugasi()
    {
        Island?.Ugasi();
        prozorPodesavanja?.Close();
        Island?.Close();
        Current.Exit();
    }
}
