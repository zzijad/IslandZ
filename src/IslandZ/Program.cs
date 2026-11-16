using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace IslandZ;

public static class Program
{
    [DllImport("Microsoft.ui.xaml.dll")]
    private static extern void XamlCheckProcessRequirements();

    [STAThread]
    private static int Main(string[] args)
    {
        XamlCheckProcessRequirements();
        WinRT.ComWrappersSupport.InitializeComWrappers();

        // Dozvoljena je samo jedna instanca programa.
        if (PreusmeriAkoVecRadi())
            return 0;

        Application.Start(p =>
        {
            var ctx = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(ctx);
            _ = new App();
        });
        return 0;
    }

    private static bool PreusmeriAkoVecRadi()
    {
        var mojiArgumenti = AppInstance.GetCurrent().GetActivatedEventArgs();
        var glavna = AppInstance.FindOrRegisterForKey("IslandZ-glavna-instanca");

        if (glavna.IsCurrent)
        {
            glavna.Activated += Glavna_Activated;
            return false;
        }

        // Program vec radi: javi postojecoj instanci da otvori podesavanja.
        Task.Run(() => glavna.RedirectActivationToAsync(mojiArgumenti).AsTask().Wait()).Wait();
        return true;
    }

    private static void Glavna_Activated(object? sender, AppActivationArguments e)
    {
        App.UiQueue?.TryEnqueue(App.PrikaziPodesavanja);
    }
}
