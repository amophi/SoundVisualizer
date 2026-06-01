using System.Configuration;
using System.Data;
using System.Windows;

namespace SoundVisualizer
{

    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            AppSettings.Load();
        }
    }
}
