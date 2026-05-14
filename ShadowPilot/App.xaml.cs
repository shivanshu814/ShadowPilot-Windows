using System.Windows;
using ShadowPilot.Services;

namespace ShadowPilot;

public partial class App : Application
{
    private OverlayWindow?  _overlay;
    private SetupWindow?    _setup;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        bool hasKey = !string.IsNullOrEmpty(EnvConfig.OpenAIKey) ||
                      !string.IsNullOrEmpty(EnvConfig.OpenRouterKey);

        _overlay = new OverlayWindow();

        if (!hasKey)
        {
            // Show setup first so user can check key status; overlay opens after
            _setup = new SetupWindow(_overlay.ViewModel, onDone: () =>
            {
                _setup?.Close();
                _overlay.Show();
            });
            _setup.Show();
        }
        else
        {
            // Show setup briefly to let user paste JD / resume, then continue
            _setup = new SetupWindow(_overlay.ViewModel, onDone: () =>
            {
                _setup?.Close();
                _overlay.Show();
            });
            _setup.Show();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _overlay?.ViewModel.SaveSettings();
        base.OnExit(e);
    }
}
