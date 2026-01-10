using System.Configuration;
using System.Data;
using System.Windows;
using LangVPlayer.Resources;
using LangVPlayer.Services;

namespace LangVPlayer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Initialize localization / Инициализация локализации
        var settings = SettingsService.Load();
        Strings.Init(settings.Language);
    }
}

