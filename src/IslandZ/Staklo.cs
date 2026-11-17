using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;

namespace IslandZ;

// Cini prozor potpuno providnim: iza XAML sadrzaja nema nikakve pozadine.
// Sam oblik ostrva (pilula) crta XAML, sa glatkim ivicama.
public sealed class ProvidanBackdrop : SystemBackdrop
{
    private static IntPtr kontrolerReda;
    private static Windows.UI.Composition.Compositor? kompozitor;

    [StructLayout(LayoutKind.Sequential)]
    private struct DispatcherQueueOptions
    {
        public int dwSize;
        public int threadType;
        public int apartmentType;
    }

    [DllImport("CoreMessaging.dll")]
    private static extern int CreateDispatcherQueueController([In] DispatcherQueueOptions options, out IntPtr controller);

    // Sistemski kompozitor trazi sistemski red poruka na ovoj niti.
    private static Windows.UI.Composition.Compositor Kompozitor
    {
        get
        {
            if (kompozitor == null)
            {
                if (Windows.System.DispatcherQueue.GetForCurrentThread() == null && kontrolerReda == IntPtr.Zero)
                {
                    DispatcherQueueOptions o;
                    o.dwSize = Marshal.SizeOf<DispatcherQueueOptions>();
                    o.threadType = 2;     // DQTYPE_THREAD_CURRENT
                    o.apartmentType = 2;  // DQTAT_COM_STA
                    CreateDispatcherQueueController(o, out kontrolerReda);
                }
                kompozitor = new Windows.UI.Composition.Compositor();
            }
            return kompozitor;
        }
    }

    protected override void OnDefaultSystemBackdropConfigurationChanged(ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
    {
        if (target != null)
            base.OnDefaultSystemBackdropConfigurationChanged(target, xamlRoot);
    }

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        connectedTarget.SystemBackdrop = Kompozitor.CreateColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        base.OnTargetConnected(connectedTarget, xamlRoot);
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        var cetka = disconnectedTarget.SystemBackdrop;
        disconnectedTarget.SystemBackdrop = null;
        cetka?.Dispose();
        base.OnTargetDisconnected(disconnectedTarget);
    }
}

// Zamuceno staklo iseceno u oblik ostrva. Sistemski akril se ne vezuje za ceo prozor
// nego za jedan vizual, koji moze da se zaobli i menja velicinu.
public sealed class Staklo
{
    private readonly VezaStakla veza;
    private readonly DesktopAcrylicController kontroler;
    private readonly Visual vizual;
    private readonly RectangleClip isecak;

    public Staklo(UIElement domacin, int providnost)
    {
        var k = ElementCompositionPreview.GetElementVisual(domacin).Compositor;
        veza = new VezaStakla(k);
        vizual = veza.Vizual;
        isecak = k.CreateRectangleClip();
        vizual.Clip = isecak;

        kontroler = new DesktopAcrylicController();
        Postavi(providnost);
        // Ostrvo skoro nikad nije aktivan prozor, a staklo treba da radi stalno.
        kontroler.SetSystemBackdropConfiguration(new SystemBackdropConfiguration
        {
            IsInputActive = true,
            Theme = SystemBackdropTheme.Dark,
        });
        kontroler.AddSystemBackdropTarget(veza.Meta);
        ElementCompositionPreview.SetElementChildVisual(domacin, vizual);
    }

    public void Postavi(int providnost)
    {
        double p = providnost / 100.0;
        kontroler.TintColor = Windows.UI.Color.FromArgb(255, 22, 28, 44);
        kontroler.TintOpacity = (float)(0.75 - 0.70 * p);
        kontroler.LuminosityOpacity = (float)(0.90 - 0.80 * p);
        kontroler.FallbackColor = Windows.UI.Color.FromArgb(230, 22, 28, 44);
    }

    // Velicina i zaobljenje u XAML jedinicama (ne u pikselima).
    public void Oblik(double w, double h, double r)
    {
        vizual.Size = new Vector2((float)w, (float)h);
        Zaobli(isecak, w, h, r);
    }

    public bool Vidljivo
    {
        set => vizual.IsVisible = value;
    }

    public static void Zaobli(RectangleClip c, double w, double h, double r)
    {
        var ugao = new Vector2((float)r, (float)r);
        c.Left = 0;
        c.Top = 0;
        c.Right = (float)w;
        c.Bottom = (float)h;
        c.TopLeftRadius = ugao;
        c.TopRightRadius = ugao;
        c.BottomLeftRadius = ugao;
        c.BottomRightRadius = ugao;
    }
}
