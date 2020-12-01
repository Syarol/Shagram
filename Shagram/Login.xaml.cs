using System;
using System.Windows;
using System.Windows.Input;
using System.IO;
using System.Collections.Generic;

using dotenv.net; //get evnvironment variables

using TeleSharp.TL;
using TLSharp.Core;

using Newtonsoft.Json;
using TLSharp.Core.Exceptions;

namespace Shagram
{
    /// <summary>
    /// Interaction logic for Login.xaml
    /// </summary>
    public partial class Login : Window
    {
        private string NumberToSendMessage;
        private string hash;
        private TelegramClient client;

        public Login()
        {
            InitializeComponent();

            List<Country> countriesList = LoadJson(Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName + "./countries.json");

            countriesCodesList.ItemsSource = countriesList;
            countriesCodesList.DisplayMemberPath = "NameAndCode";
            countriesCodesList.SelectedValuePath = "Dial_code";
            countriesCodesList.SelectedIndex = 0;
        }

        private void window_close_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void window_hide_MouseDown(object sender, MouseButtonEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FileSessionStore session = new FileSessionStore();
                        
            client = NewClient(session);
            await client.ConnectAsync();
        }

        public static TelegramClient NewClient(FileSessionStore session)
        {
            try
            {
                DotEnv.AutoConfig(); //loads environment variables

                int API_ID = Int32.Parse(Environment.GetEnvironmentVariable("API_ID"));
                string API_HASH = Environment.GetEnvironmentVariable("API_HASH");
                return new TelegramClient(API_ID, API_HASH, session, "session");
            }
            catch (MissingApiConfigurationException ex)
            {
                throw new Exception($"Please add your API settings to the `app.config` file. (More info: {MissingApiConfigurationException.InfoUrl})", ex);
            }
        }

        private async void btn_setPhoneNumber_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txt_phone_number.Text)) return;

                NumberToSendMessage = countriesCodesList.SelectedValue.ToString() + txt_phone_number.Text;

                hash = await client.SendCodeRequestAsync(NumberToSendMessage);

                lbl_phone_number.Visibility = Visibility.Hidden;
                txt_phone_number.Visibility = Visibility.Hidden;
                btn_setPhoneNumber.Visibility = Visibility.Hidden;
                countriesCodesList.Visibility = Visibility.Hidden;

                lbl_received_code.Visibility = Visibility.Visible;
                txt_received_code.Visibility = Visibility.Visible;
                btn_setReceivedCode.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                Application.Current.Shutdown();
            }
        }

        private async void btn_setReceivedCode_Click(object sender, RoutedEventArgs e)
        {
            string code = txt_received_code.Text; //get entered code

            if (string.IsNullOrWhiteSpace(code)) return;

            try
            {
                await client.MakeAuthAsync(NumberToSendMessage, hash, code);

                MainWindow mainWindow = new MainWindow(); // Inicialize main window
                mainWindow.Show();
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public List<Country> LoadJson(string jsonAdress)
        {
            using (StreamReader r = new StreamReader(jsonAdress))
            {
                string json = r.ReadToEnd();
                List<Country> countriesList = JsonConvert.DeserializeObject<List<Country>>(json);
                return countriesList;
            }        
        }

        public class Country
        {
            public string Name {get; set; }
            public string Dial_code { get; set; }
            public string Code { get; set; }
            public string Flag { get; set; }

            public string NameAndCode => $"{Name} {Dial_code}";
        }
    }
}
