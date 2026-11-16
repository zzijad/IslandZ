using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace IslandZ;

// Zamuceno staklo iza ostrva. Ostaje ukljuceno i kad prozor nije aktivan,
// jer ostrvo skoro nikad nije aktivan prozor.
public sealed class StakloBackdrop : SystemBackdrop
{
    private DesktopAcrylicController? kontroler;
    private int providnost;

    public StakloBackdrop(int providnost)
    {
        this.providnost = providnost;
    }

    public void Postavi(int novaProvidnost)
    {
        providnost = novaProvidnost;
        Primeni();
    }

    private void Primeni()
    {
        if (kontroler == null) return;
        double p = providnost / 100.0;
        kontroler.TintColor = Color.FromArgb(255, 22, 28, 44);
        kontroler.TintOpacity = (float)(0.75 - 0.70 * p);
        kontroler.LuminosityOpacity = (float)(0.90 - 0.80 * p);
        kontroler.FallbackColor = Color.FromArgb(255, 22, 28, 44);
    }

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);
        if (kontroler != null) return;
        kontroler = new DesktopAcrylicController();
        Primeni();
        kontroler.AddSystemBackdropTarget(connectedTarget);
        kontroler.SetSystemBackdropConfiguration(new SystemBackdropConfiguration
        {
            IsInputActive = true,
            Theme = SystemBackdropTheme.Dark,
        });
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        base.OnTargetDisconnected(disconnectedTarget);
        if (kontroler == null) return;
        kontroler.RemoveSystemBackdropTarget(disconnectedTarget);
        kontroler.Dispose();
        kontroler = null;
    }
}
