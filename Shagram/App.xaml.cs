using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

using TeleSharp.TL;
using TLSharp.Core;

namespace Shagram
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void App_Startup(object sender, StartupEventArgs e)
        {
            FileSessionStore session = new FileSessionStore();
            TelegramClient client = Login.NewClient(session);

            if (client.IsUserAuthorized())//if user authorised than open main window
            {
                MainWindow mainWindow = new MainWindow(); // Inicialize main window
                mainWindow.Show();
            }
            else 
            {
                Login loginWindow = new Login(); // Inicialize login window
                loginWindow.Show();
            }
        }
    }
}
