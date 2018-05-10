using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.IO;
using System.Net;
using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;

using TeleSharp.TL;
using TeleSharp.TL.Messages;
using TeleSharp.TL.Contacts;
using TeleSharp.TL.Upload;
using TeleSharp.TL.Users;
using TLSharp.Core;
using TLSharp.Core.Utils;
using TeleSharp.TL.Updates;
using TeleSharp.TL.Channels;
using TeleSharp.TL.Auth;
using TeleSharp.TL.Help;
using TLSharp.Core.Auth;
using TLSharp.Core.MTProto.Crypto;
using TLSharp.Core.Network;

using TLAuthorization = TeleSharp.TL.Auth.TLAuthorization;



namespace WpfApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window
    {
        delegate void openDialogHandler(TLChannel dialog);

        private MtProtoSender _sender;
        private AuthKey _key;
        private TcpTransport _transport;
        private string _apiHash = "";
        private int _apiId = 0;
        private Session _session;
        private List<TLDcOption> dcOptions;
        private TcpClientConnectionHandler _handler;
        private IEnumerable<TLUser> contacts;

        private TelegramClient client;
        TLUser user = null;

        public MainWindow()
        {
            InitializeComponent();
        }

        public MainWindow(TLUser getUser)
        {
            InitializeComponent();

            user = getUser;
        }

        private void window_close_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void window_maximize_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (this.WindowState == WindowState.Normal)
            {
                this.WindowState = WindowState.Maximized;
                window_maximize.Content = "";
                window_maximize.ToolTip = "Normalize";
            }
            else
            {
                this.WindowState = WindowState.Normal;
                window_maximize.Content = "";
                window_maximize.ToolTip = "Maximize";
            }
        }

        private void window_hide_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
       
                var session = new FileSessionStore();
                client = Login.NewClient(session);

                _session = Session.TryLoadOrCreateNew(session, "session");

                await client.ConnectAsync();
                if (!client.IsUserAuthorized())//if user not authorised than open login form
                {
                    Login loginWindow = new Login(); // Inicialize login window
                    loginWindow.Show();
                    this.Close();
                }
                else
                {
                    string user = _session.TLUser.FirstName + " " + _session.TLUser.LastName;
                    user_name.Content = user;

                    //GetContactsAsync(client);

                    GetDialogsAsync();

                    



                }
            } 
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void top_bar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            main_window.DragMove();
        }

        public BitmapImage ByteToImage(byte[] bytes)
        {
            var image = new BitmapImage();
            using (var mem = new MemoryStream(bytes))
            {
                mem.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = null;
                image.StreamSource = mem;
                image.EndInit();
            }
            image.Freeze();
            return image;
        }

        private async void OpenDialogAsync(TLChannel dialog)
        {
            try
            {
                messages_field_scroll.ScrollToEnd();

                int total = 1;
                int start = 0;
                int end = 100;
                //while (start <= total)
                //{
                    var req = new TLRequestGetHistory
                    {
                        AddOffset = start,
                        Limit = end,
                        Peer = new TLInputPeerChannel { ChannelId = dialog.Id, AccessHash = dialog.AccessHash.Value }
                    };
                    var messages = await client.SendRequestAsync<TLChannelMessages>(req);

                    foreach (var channelMessage in messages.Messages)
                    {
                        TextBlock txtBlock = new TextBlock();
                        txtBlock.TextWrapping = TextWrapping.Wrap;
                        txtBlock.Margin = new Thickness(10, 0, 0, 0);
                        txtBlock.VerticalAlignment = VerticalAlignment.Center;
                        if (channelMessage.GetType() == typeof(TLMessage))
                        {
                            var message = (TLMessage)channelMessage;
                            txtBlock.Text = message.Message.ToString();
                            messages_field.Children.Insert(0, txtBlock);
                        }
                        else if (channelMessage.GetType() == typeof(TLMessageService))
                        {
                            var message = (TLMessageService)channelMessage;
                            txtBlock.Text = message.Post.ToString();
                            messages_field.Children.Insert(0, txtBlock);
                        }
                    }
                    //start += 101;  TLMessageService  TLMessage
                //}
            } catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            //messages_field
        }

        private async void GetDialogsAsync()
        {
            var dialogs = await client.GetUserDialogsAsync() as TLDialogs;
            var chats = dialogs.Chats.Where(x => x.GetType() == typeof(TLChannel)).Cast<TLChannel>();
            var userChats = dialogs.Chats.Where(x => x.GetType() == typeof(TLChat)).Cast<TLChat>();

            foreach (var chat in userChats)
            {
                TextBlock txtBlock = new TextBlock();
                txtBlock.TextWrapping = TextWrapping.Wrap;
                txtBlock.Margin = new Thickness(10, 0, 0, 0);
                txtBlock.Height = 20;
                txtBlock.VerticalAlignment = VerticalAlignment.Center;
                txtBlock.Text = (chat.Title).ToString();
                //txtBlock.PreviewMouseDown += new RoutedEventHandler(/**/);//add event handler that opening a dialog
                contacts_list.Children.Add(txtBlock);
            }

            foreach (var dialog in chats)
            {
                TextBlock txtBlock = new TextBlock();
                txtBlock.TextWrapping = TextWrapping.Wrap;
                txtBlock.Margin = new Thickness(10, 0, 0, 0);
                txtBlock.Height = 20;
                txtBlock.VerticalAlignment = VerticalAlignment.Center;
                txtBlock.Text = dialog.Title.ToString();

                txtBlock.MouseDown += (sender, e) => OpenDialogAsync(dialog);
                contacts_list.Children.Add(txtBlock);
            }
        }

        private async void GetContactsAsync(TelegramClient client)
        {
            TLContacts result;
            //get available contacts
            try
            {
                result = await client.GetContactsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n" + ex.InnerException);
                return;
            }
            contacts = result.Users.ToList().Where(x => x.GetType() == typeof(TLUser)).Cast<TLUser>();
            foreach (var contact in contacts)
            {
                //MessageBox.Show(contact.FirstName);
                TextBlock txtBlock = new TextBlock();
                txtBlock.TextWrapping = TextWrapping.Wrap;
                txtBlock.Text = contact.FirstName;
                contacts_list.Children.Add(txtBlock);
            }
        }

        private void message_enter_txb_GotFocus(object sender, RoutedEventArgs e)
        {
            message_enter_txb.Text = "";
        }

        private void message_enter_txb_LostFocus(object sender, RoutedEventArgs e)
        {
            if (message_enter_txb.Text == "") message_enter_txb.Text = "Enter Your messasge here";
        }
    }
}
