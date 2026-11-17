using System;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;

namespace IslandZ;

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
