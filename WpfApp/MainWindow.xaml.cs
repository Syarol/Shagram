using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.IO;
using System.Net;

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

using MimeTypes;

namespace WpfApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window
    {
        public string LinkPattern = @"^(http|https|ftp|)\://|[a-zA-Z0-9\-\.]+\.[a-zA-Z](:[a-zA-Z0-9]*)?/?([a-zA-Z0-9\-\._\?\,\'/\\\+&amp;%\$#\=~])*[^\.\,\)\(\s]$";

        private Session _session;
        private IEnumerable<TLUser> contacts;
        private dynamic openedDialog = null;
        private string pinnedFile = null;
        private bool isPhotoPinned;
        private TelegramClient client;
        private List<TLChannel> megagroupsList = new List<TLChannel>();
        private TLDialogs dialogsList = new TLDialogs();
        private string EnterYourMessageHere;
        private ImageSource nullPhoto;

        public MainWindow()
        {
            InitializeComponent();
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
                    nullPhoto = img_userPhoto.ImageSource;

                    string userName = _session.TLUser.FirstName + " " + _session.TLUser.LastName;
                    user_name.Content = userName;

                    var photo = await GetUserPhotoAsync(_session.TLUser);
                    img_userPhoto.ImageSource = ByteToImage(photo.Bytes);

                    await Task.WhenAll(GetDialogsOnStartAsync());
                }

                EnterYourMessageHere = new TextRange(message_enter_txb.Document.ContentStart, message_enter_txb.Document.ContentEnd).Text;
            } 
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                Application.Current.Shutdown();
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
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = null;
                image.StreamSource = mem;
                image.EndInit();
            }
            //image.Freeze();
            return image;
        }

        private async Task<TLFile> GetUserPhotoAsync(TLUser user)
        {
            var photo = ((TLUserProfilePhoto)user.Photo);
            var photoLocation = (TLFileLocation)photo.PhotoBig;

            var resFile = await client.GetFile(new TLInputFileLocation()
            {
                LocalId = photoLocation.LocalId,
                Secret = photoLocation.Secret,
                VolumeId = photoLocation.VolumeId
            }, 512 * 1024, 0);

            return resFile;
        }

        private async Task<TLFile> GetChannelPhotoAsync(TLChannel channel)
        {
            var photo = ((TLChatPhoto)channel.Photo);
            var photoLocation = (TLFileLocation)photo.PhotoSmall;

            var resFile = await client.GetFile(new TLInputFileLocation()
            {
                LocalId = photoLocation.LocalId,
                Secret = photoLocation.Secret,
                VolumeId = photoLocation.VolumeId
            }, 512 * 1024, 0);

            return resFile;
        }

        private async void OpenDialogAsync(dynamic dialog)
        {
            try
            {
                if (choose_dialog_label.Visibility == Visibility.Visible)
                {
                    choose_dialog_label.Visibility = Visibility.Hidden;
                }

                if (write_text_field.Height == 0)
                {
                    if (dialog.GetType() == typeof(TLChannel))
                    {
                        if (!dialog.Megagroup)
                        {
                            write_text_field.Height = 0;
                            Grid.SetRowSpan(messages_field_scroll, 3);
                        }
                        else if (dialog.Megagroup)
                        {
                            write_text_field.Height = 131;
                            Grid.SetRowSpan(messages_field_scroll, 1);
                        }
                    }
                    else
                    {
                        write_text_field.Height = 131;
                        Grid.SetRowSpan(messages_field_scroll, 1);
                    }
                } else
                {
                    if (dialog.GetType() == typeof(TLChannel))
                    {
                        if (!dialog.Megagroup)
                        {
                            write_text_field.Height = 0;
                            Grid.SetRowSpan(messages_field_scroll, 3);
                        }
                        else if (dialog.Megagroup)
                        {
                            write_text_field.Height = 131;
                            Grid.SetRowSpan(messages_field_scroll, 1);
                        }
                    }
                }              

                int start;
                int end;

                if (IsSameDialog(dialog))
                {
                    start = messages_field.Children.Count;
                    if (dialog.GetType() == typeof(TLChannel))
                    {
                        if (dialog.Megagroup) end = start + 50;
                        else end = start + 25;
                    } else end = start + 50;
                }
                else
                {
                    openedDialog = dialog;
                    messages_field.Children.Clear();
                    start = 0;
                    if (dialog.GetType() == typeof(TLChannel))
                    {
                        if (dialog.Megagroup) end = 50;
                        else end = 25;
                    }
                    else end = 25;
                    messages_field_scroll.ScrollToEnd();
                }

                switch (dialog)
                {
                    case TLChannel item:
                        var req = new TLRequestGetHistory
                        {
                            AddOffset = start,
                            Limit = end,
                            Peer = new TLInputPeerChannel { ChannelId = dialog.Id, AccessHash = dialog.AccessHash }
                        };

                        OpenMessagesAsync(await client.SendRequestAsync<TLChannelMessages>(req), item);
                        break;
                    case TLChat item:
                        req = new TLRequestGetHistory
                        {
                            AddOffset = start,
                            Limit = end,
                            Peer = new TLInputPeerChat { ChatId = item.Id }
                        };

                        try
                        {
                            OpenChatMessagesAsync(await client.SendRequestAsync<TLMessagesSlice>(req), item);
                        }
                        catch (InvalidCastException ex)
                        {
                            OpenChatMessagesAsync(await client.SendRequestAsync<TLMessages>(req), item);
                        }
                        break;
                    case TLUser item:
                        req = new TLRequestGetHistory
                        {
                            AddOffset = start,
                            Limit = end,
                            Peer = new TLInputPeerUser { UserId = item.Id, AccessHash = item.AccessHash.Value }
                        };

                        try
                        {
                            OpenMessagesAsync(await client.SendRequestAsync<TLMessagesSlice>(req), item);
                        }
                        catch (InvalidCastException)
                        {
                            OpenMessagesAsync(await client.SendRequestAsync<TLMessages>(req), item);
                        }
                        break;
                    default:
                        MessageBox.Show("Hmm🤔...new dialog type");
                        break;
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private bool IsSameDialog(dynamic dialog)
        {
            if (openedDialog == null)
            {
                openedDialog = dialog;
                return false;
            } else if (openedDialog.GetType() == dialog.GetType())
            {
                switch (openedDialog)
                {
                    case TLUser aVar:
                        if (openedDialog.FirstName == dialog.FirstName &&
                            openedDialog.LastName == dialog.LastName &&
                            openedDialog.Username == dialog.Username) return true;
                        else return false;
                    case TLChannel bVar:
                    case TLChat cVar:
                        if (openedDialog.Title == dialog.Title) return true;
                        else return false;
                    default:
                        MessageBox.Show("Hmm🤔...new dialog type");
                        return false;
                }
            }
            else return false;
        }

        private async void OpenMessagesAsync(TLMessagesSlice messages, TLUser user)
        {
            try
            {
                string mainUserMessageTitle = _session.TLUser.FirstName + " " + _session.TLUser.LastName;
                string userMessageTitle = user.FirstName + " " + user.LastName;
                ImageSource userPhotoFile;
                try
                {
                    TLFile tempPhotoFile = await GetUserPhotoAsync(user);
                    userPhotoFile = ByteToImage(tempPhotoFile.Bytes); 
                }
                catch (NullReferenceException ex)
                {
                    userPhotoFile = nullPhoto;
                }
                string messageSender = "";

                foreach (var chatMessage in messages.Messages)
                {
                    Border gridBorder = new Border();
                    Grid messageBlockWrapper = new Grid();
                    gridBorder.Child = messageBlockWrapper;
                    ColumnDefinition gridCol1 = new ColumnDefinition();
                    ColumnDefinition gridCol2 = new ColumnDefinition();
                    gridCol1.Width = new GridLength(50);
                    messageBlockWrapper.ColumnDefinitions.Add(gridCol1);
                    messageBlockWrapper.ColumnDefinitions.Add(gridCol2);

                    if (chatMessage.GetType() == typeof(TLMessage))
                    {
                        Ellipse userPhoto = new Ellipse();
                        userPhoto.Margin = new Thickness(0, 5, 0, 0);
                        userPhoto.VerticalAlignment = VerticalAlignment.Top;
                        userPhoto.SetValue(Grid.ColumnProperty, 0);
                        userPhoto.Height = 40;
                        userPhoto.Width = 40;
                        ImageBrush userMainPhoto = new ImageBrush();
                    
                        var message = (TLMessage)chatMessage;
                        if (user.Id != message.FromId.Value)
                        {
                            userMainPhoto.ImageSource = img_userPhoto.ImageSource;
                            messageSender = mainUserMessageTitle;
                        }
                        else
                        {
                            userMainPhoto.ImageSource = userPhotoFile;
                            messageSender = userMessageTitle;
                        }

                        userPhoto.SetValue(Grid.ColumnProperty, 1);
                        userPhoto.Fill = userMainPhoto;
                        Grid.SetColumn(userPhoto, 0);
                        messageBlockWrapper.Children.Add(userPhoto);
                    }

                    StackPanel messageBlock = new StackPanel();
                    messageBlock.Orientation = Orientation.Vertical;
                    messageBlockWrapper.Children.Add(messageBlock);

                    Label sender = new Label();
                    sender.FontWeight = FontWeights.Bold;
                    sender.Content = messageSender;
                    messageBlock.Children.Add(sender);

                    StackPanel txtBox = new StackPanel();
                    messageBlock.Children.Add(txtBox);

                    Label time = new Label();
                    time.FontSize = 10;
                    time.HorizontalContentAlignment = HorizontalAlignment.Right;
                    if (chatMessage.GetType() == typeof(TLMessage))
                    {
                        var message = (TLMessage)chatMessage;
                        DateTime messageTime = TimeUnixTOWindows(message.Date, true);
                        time.Content = messageTime.Hour + ":" + messageTime.Minute;
                    }
                    else if (chatMessage.GetType() == typeof(TLMessage))
                    {
                        var message = (TLMessageService)chatMessage;
                        DateTime messageTime = TimeUnixTOWindows(message.Date, true);
                        time.Content = messageTime.Hour + ":" + messageTime.Minute;
                    }
                    messageBlock.Children.Add(time);

                    
                    Grid.SetColumn(messageBlock, 1);

                    switch (chatMessage)
                    {
                        case TLMessage message:
                            {
                                if (message.Message != "")
                                {
                                    TextBlock txtBlock = new TextBlock();
                                    txtBlock.TextWrapping = TextWrapping.Wrap;
                                    txtBox.Children.Add(txtBlock);

                                    string startMessage = message.Message;
                                    Regex reg = new Regex(LinkPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                                    if (reg.IsMatch(startMessage))
                                    {
                                        foreach (Match match in reg.Matches(message.Message))
                                        {
                                            int i = match.Index;
                                            int f = match.Length;

                                            TextBlock text = new TextBlock();
                                            text.Text = startMessage.Substring(0, i);
                                            //txtBlock.Children.Add(text);

                                            Run linkText = new Run(match.ToString());
                                            Hyperlink link = new Hyperlink(linkText)
                                            {
                                                NavigateUri = new Uri("http://" + match.ToString())
                                            };
                                            link.RequestNavigate += new RequestNavigateEventHandler(delegate (object senderee, RequestNavigateEventArgs e)
                                            {
                                                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
                                                e.Handled = true;
                                            });

                                            txtBlock.Inlines.Add(link);
                                            startMessage.Replace(startMessage.Substring(0, f), "");
                                        }
                                    }
                                    else txtBlock.Text = message.Message;
                                    //txtBlockWrapper.Children.Add(messageBox);
                                }
                                switch (message.Media)
                                {
                                    case TLMessageMediaPhoto item:
                                        {
                                            System.Windows.Controls.Image photo = new System.Windows.Controls.Image();
                                            photo.Height = 200;
                                            photo.Width = 200;
                                            var messagePhoto = await GetMessagePhotoAsync(item);

                                            photo.Source = ByteToImage(messagePhoto.Bytes);
                                            txtBox.Children.Add(photo);

                                            if (item.Caption != "")
                                            {
                                                TextBlock txtBlock = new TextBlock();
                                                txtBlock.TextWrapping = TextWrapping.Wrap;
                                                txtBox.Children.Add(txtBlock);
                                                txtBlock.Text = item.Caption;
                                                //messageBlockWrapper.Children.Add(txtBlock);
                                            }
                                            break;
                                        }
                                    case TLMessageMediaDocument item:
                                        var doc = (TLDocument)item.Document;
                                        foreach (var att in doc.Attributes.ToList())
                                        {
                                            switch (att)
                                            {
                                                case TLDocumentAttributeVideo video:

                                                    break;
                                                case TLDocumentAttributeAudio audio:

                                                    break;
                                                case TLDocumentAttributeSticker sticker:
                                                    var inputStickerSet = (TLInputStickerSetID)sticker.Stickerset;
                                                    //var stickerSet = (TLStickerSet)sticker;

                                                    var req = new TLInputDocumentFileLocation()
                                                    {
                                                        Id = inputStickerSet.Id,
                                                        AccessHash = inputStickerSet.AccessHash,
                                                        Version = doc.Version
                                                    };
                                                    MessageBox.Show(req.ToString());
                                                    break;
                                            }
                                        }
                                        break;
                                }
                                break;
                            }
                        case TLMessageService message:
                            {
                                TextBlock txtBlock = new TextBlock();
                                txtBlock.TextWrapping = TextWrapping.Wrap;
                                txtBox.Children.Add(txtBlock);

                                txtBlock.TextAlignment = TextAlignment.Center;
                                dynamic action = message.Action;
                                txtBlock.Text = ServiceMessageHandler(action).ToString();
                                messageBlock.Children.Add(txtBlock);
                                break;
                            }
                    }
                    messages_field.Children.Insert(0, /*messageBlockWrapper*/gridBorder);
                }
            } catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private async void OpenMessagesAsync(TLChannelMessages messages, TLChannel channel)
        {
            try
            {
                TLFile chennelPhotoFile = await GetChannelPhotoAsync(channel);

                foreach (var chatMessage in messages.Messages)
                {
                    Border gridBorder = new Border();
                    gridBorder.Margin = new Thickness(10, 0, 10, 10);

                    Grid messageBlockWrapper = new Grid();
                    gridBorder.Child = messageBlockWrapper;
                    ColumnDefinition gridCol1 = new ColumnDefinition();
                    ColumnDefinition gridCol2 = new ColumnDefinition();
                    gridCol1.Width = new GridLength(50);
                    messageBlockWrapper.ColumnDefinitions.Add(gridCol1);
                    messageBlockWrapper.ColumnDefinitions.Add(gridCol2);

                    Ellipse userPhoto = new Ellipse();
                        userPhoto.Margin = new Thickness(0, 5, 0, 0);
                        userPhoto.VerticalAlignment = VerticalAlignment.Top;
                        userPhoto.SetValue(Grid.ColumnProperty, 0);
                        userPhoto.Height = 40;
                        userPhoto.Width = 40;
                        ImageBrush userMainPhoto = new ImageBrush();

                    if (!channel.Megagroup)
                    {
                        userMainPhoto.ImageSource = ByteToImage(chennelPhotoFile.Bytes);
                    } else
                    {
                        if (chatMessage.GetType() == typeof(TLMessage))
                        {
                            var message = (TLMessage)chatMessage;

                            /*var request = new TLRequestGetParticipants {
                                Offset = 0,
                                Limit = -1,
                                Channel = new TLInputChannel { AccessHash = channel.AccessHash.Value, ChannelId = channel.Id },
                                Filter = new TLChannelParticipants()
                            };
                            TLChannelParticipants found = await client.SendRequestAsync<TLChannelParticipants>(request);
                            MessageBox.Show(found.Users.Count.ToString());*/
                            
                            // contacts = channel.Username.ToList().Where(x => { MessageBox.Show(x.ToString()); return x == message.FromId.Value; }).Cast<TLUser>();
                            //MessageBox.Show(message.FromId.Value.ToString());
                            // MessageBox.Show(contacts.ToString());
                            //MessageBox.Show(channel.Username.Where(x > x));
                        }
                        else if (chatMessage.GetType() == typeof(TLMessage))
                        {
                            var message = (TLMessageService)chatMessage;
                        }
                    }
                        
                        userPhoto.Fill = userMainPhoto;
                        messageBlockWrapper.Children.Add(userPhoto);

                    StackPanel messageBlock = new StackPanel();
                        userPhoto.SetValue(Grid.ColumnProperty, 1);
                        messageBlock.Orientation = Orientation.Vertical;
                        messageBlockWrapper.Children.Add(messageBlock);

                    Label sender = new Label();
                        sender.FontWeight = FontWeights.Bold;
                        sender.Content = channel.Title;
                        messageBlock.Children.Add(sender);

                    StackPanel txtBox = new StackPanel();
                        messageBlock.Children.Add(txtBox);

                    Label time = new Label();
                    //time.Foreground = System.Windows.Media.Brushes.Red;
                    time.FontSize = 10;
                    time.HorizontalContentAlignment = HorizontalAlignment.Right;
                    if (chatMessage.GetType() == typeof(TLMessage))
                    {
                        var message = (TLMessage)chatMessage;
                        DateTime messageTime = TimeUnixTOWindows(message.Date, true);
                        time.Content = messageTime.Hour + ":" + messageTime.Minute;
                    }
                    else if (chatMessage.GetType() == typeof(TLMessage))
                    {
                        var message = (TLMessageService)chatMessage;
                        DateTime messageTime = TimeUnixTOWindows(message.Date, true);
                        time.Content = messageTime.Hour + ":" + messageTime.Minute;
                    }
                    messageBlock.Children.Add(time);

                    Grid.SetColumn(userPhoto, 0);
                    Grid.SetColumn(messageBlock, 1);

                    switch (chatMessage)
                    {
                        case TLMessage message:
                            {
                                if (message.Message != "")
                                {
                                    TextBlock txtBlock = new TextBlock();
                                    txtBlock.TextWrapping = TextWrapping.Wrap;
                                    txtBox.Children.Add(txtBlock);

                                    string startMessage = message.Message;
                                    Regex reg = new Regex(LinkPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                                    if (reg.IsMatch(startMessage))
                                    {
                                        foreach (Match match in reg.Matches(message.Message))
                                        {
                                            int i = match.Index;
                                            int f = match.Length;

                                            TextBlock text = new TextBlock();
                                            text.Text = startMessage.Substring(0, i);
                                            //txtBlock.Children.Add(text);

                                            Run linkText = new Run(match.ToString());
                                            Hyperlink link = new Hyperlink(linkText)
                                            {
                                                NavigateUri = new Uri("http://" + match.ToString())
                                            };
                                            link.RequestNavigate += new RequestNavigateEventHandler(delegate (object senderee, RequestNavigateEventArgs e)
                                            {
                                                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
                                                e.Handled = true;
                                            });

                                            txtBlock.Inlines.Add(link);
                                            startMessage.Replace(startMessage.Substring(0, f), "");
                                        }
                                    }
                                    else txtBlock.Text = message.Message;
                                    //txtBlockWrapper.Children.Add(messageBox);
                                }

                                switch (message.Media)
                                {
                                    case TLMessageMediaPhoto item:
                                        {
                                            System.Windows.Controls.Image photo = new System.Windows.Controls.Image();
                                            photo.Height = 200;
                                            photo.Width = 200;
                                            var messagePhoto = await GetMessagePhotoAsync(item);
                                            photo.Source = ByteToImage(messagePhoto.Bytes);
                                            txtBox.Children.Add(photo);

                                            if (item.Caption != "")
                                            {
                                                TextBlock txtBlock = new TextBlock();
                                                txtBlock.TextWrapping = TextWrapping.Wrap;
                                                txtBox.Children.Add(txtBlock);
                                                txtBlock.Text = item.Caption;
                                                //messageBlockWrapper.Children.Add(txtBlock);
                                            }
                                            break;
                                        }
                                    case TLMessageMediaDocument item:
                                        TLDocument doc = (TLDocument)item.Document;
                                        foreach (var att in doc.Attributes.ToList())
                                        {
                                            switch (att)
                                            {
                                                case TLDocumentAttributeVideo video:

                                                    break;
                                                case TLDocumentAttributeAudio audio:

                                                    break;
                                                case TLDocumentAttributeSticker sticker:

                                                    break;
                                            }
                                        }
                                        break;
                                }
                                break;
                            }
                        case TLMessageService message:
                            {
                                TextBlock txtBlock = new TextBlock();
                                txtBlock.TextWrapping = TextWrapping.Wrap;
                                txtBox.Children.Add(txtBlock);

                                txtBlock.TextAlignment = TextAlignment.Center;
                                dynamic action = message.Action;
                                txtBlock.Text = ServiceMessageHandler(action).ToString();
                                //messageBlock.Children.Add(txtBox);
                                break;
                            }
                    }
                    messages_field.Children.Insert(0, /*messageBlockWrapper*/gridBorder);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private static DateTime TimeUnixTOWindows(int TimestampToConvert, bool Local)
        {
            var mdt = new DateTime(1970, 1, 1, 0, 0, 0);
            if (Local)
            {
                return mdt.AddSeconds(TimestampToConvert).ToLocalTime();
            }
            else
            {
                return mdt.AddSeconds(TimestampToConvert);
            }
        }

        private async Task<TLFile> GetMessagePhotoAsync(TLMessageMediaPhoto message)
        {
            var photo = (TLPhoto)message.Photo;

            var photoSize = (TLPhotoSize)photo.Sizes.ToList().Last();
            var photoLocation = (TLFileLocation)photoSize.Location;
            var resFile = await client.GetFile(new TLInputFileLocation
            {
                LocalId = photoLocation.LocalId,
                Secret = photoLocation.Secret,
                VolumeId = photoLocation.VolumeId
            }, 512 * 1024);

            return resFile;
        }

        private string ServiceMessageHandler(dynamic action)
        {
            switch (action)
            {
                case TLMessageActionChannelCreate act:
                    return act.Title + " is created";
                case TLMessageActionChannelMigrateFrom act:
                    return act.Title + " is upgraded to supergroup";
                case TLMessageActionChatAddUser act:
                    return act.Users + " joined";
                case TLMessageActionChatCreate act:
                    return act.Title + " is created";
                case TLMessageActionChatDeletePhoto act:
                    return act.ToString();
                case TLMessageActionChatDeleteUser act:
                    return act.UserId.ToString() + " is deleted from group";
                case TLMessageActionChatEditPhoto act:
                    return act.Photo + " is changed";
                case TLMessageActionChatEditTitle act:
                    return "Title changed to " + act.Title;
                case TLMessageActionChatJoinedByLink act:
                    return act.InviterId + " joined by link";
                case TLMessageActionChatMigrateTo act:
                    return act.ChannelId + " is migrated";
                case TLMessageActionEmpty act:
                    return "Empty message";
                case TLMessageActionGameScore act:
                    return act.GameId + " gate result is " + act.Score;
                case TLMessageActionHistoryClear act:
                    return "History was cleared";
                case TLMessageActionPaymentSent act:
                    return "Some payment action";
                case TLMessageActionPaymentSentMe act:
                    return "Some payment action";
                case TLMessageActionPhoneCall act:
                    return act.CallId + " is called"; 
                case TLMessageActionPinMessage act:
                    return "Some message was pinned";
                default: return "Some action";
            }
        }

        private async void OpenMessagesAsync(TLMessages messages, TLUser user)
        {
            string mainUserMessageTitle = _session.TLUser.FirstName + " " + _session.TLUser.LastName;
            string userMessageTitle = user.FirstName + " " + user.LastName;
            ImageSource userPhotoFile;
            try
            {
                TLFile tempPhotoFile = await GetUserPhotoAsync(user);
                userPhotoFile = ByteToImage(tempPhotoFile.Bytes);
            }
            catch (NullReferenceException ex)
            {
                userPhotoFile = nullPhoto;
            }
            string messageSender = "";

            foreach (var chatMessage in messages.Messages)
            {
                Border gridBorder = new Border();
                //gridBorder.Margin = new Thickness(10, 0, 10, 10);
                //gridBorder.CornerRadius = new CornerRadius(10);
                //gridBorder.BorderThickness = new Thickness(1, 1, 1, 1);
                //gridBorder.BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#c3c3c3"));
                //gridBorder.Background = System.Windows.Media.Brushes.LightYellow;
                //gridBorder.Width = 400;
                //gridBorder.HorizontalAlignment = HorizontalAlignment.Left;
                //gridBorder.VerticalAlignment = VerticalAlignment.Top;

                Grid messageBlockWrapper = new Grid();
                gridBorder.Child = messageBlockWrapper;
                ColumnDefinition gridCol1 = new ColumnDefinition();
                ColumnDefinition gridCol2 = new ColumnDefinition();
                gridCol1.Width = new GridLength(50);
                messageBlockWrapper.ColumnDefinitions.Add(gridCol1);
                messageBlockWrapper.ColumnDefinitions.Add(gridCol2);

                if (chatMessage.GetType() == typeof(TLMessage))
                {
                    Ellipse userPhoto = new Ellipse();
                    userPhoto.Margin = new Thickness(0, 5, 0, 0);
                    userPhoto.VerticalAlignment = VerticalAlignment.Top;
                    userPhoto.SetValue(Grid.ColumnProperty, 0);
                    userPhoto.Height = 40;
                    userPhoto.Width = 40;
                    ImageBrush userMainPhoto = new ImageBrush();

                    var message = (TLMessage)chatMessage;
                    if (user.Id != message.FromId.Value)
                    {
                        userMainPhoto.ImageSource = img_userPhoto.ImageSource;
                        messageSender = mainUserMessageTitle;
                    }
                    else
                    {
                        userMainPhoto.ImageSource = userPhotoFile;
                        messageSender = userMessageTitle;
                    }

                    userPhoto.SetValue(Grid.ColumnProperty, 1);
                    userPhoto.Fill = userMainPhoto;
                    Grid.SetColumn(userPhoto, 0);
                    messageBlockWrapper.Children.Add(userPhoto);
                }

                StackPanel messageBlock = new StackPanel();
                messageBlock.Orientation = Orientation.Vertical;
                messageBlockWrapper.Children.Add(messageBlock);

                Label sender = new Label();
                sender.FontWeight = FontWeights.Bold;
                sender.Content = messageSender;
                messageBlock.Children.Add(sender);

                StackPanel txtBox = new StackPanel();
                messageBlock.Children.Add(txtBox);

                Label time = new Label();
                time.FontSize = 10;
                time.HorizontalContentAlignment = HorizontalAlignment.Right;
                if (chatMessage.GetType() == typeof(TLMessage))
                {
                    var messageT = (TLMessage)chatMessage;
                    DateTime messageTime = TimeUnixTOWindows(messageT.Date, true);
                    time.Content = messageTime.Hour + ":" + messageTime.Minute;
                }
                else if (chatMessage.GetType() == typeof(TLMessage))
                {
                    var message = (TLMessageService)chatMessage;
                    DateTime messageTime = TimeUnixTOWindows(message.Date, true);
                    time.Content = messageTime.Hour + ":" + messageTime.Minute;
                }
                messageBlock.Children.Add(time);

                Grid.SetColumn(messageBlock, 1);
                switch (chatMessage)
                {
                    case TLMessage message:
                    {
                        if (message.Message != "")
                        {
                            TextBlock txtBlock = new TextBlock();
                            txtBlock.TextWrapping = TextWrapping.Wrap;
                            txtBox.Children.Add(txtBlock);

                            string startMessage = message.Message;
                            Regex reg = new Regex(LinkPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                            if (reg.IsMatch(startMessage))
                            {
                                foreach (Match match in reg.Matches(message.Message))
                                {
                                    int i = match.Index;
                                    int f = match.Length;

                                    TextBlock text = new TextBlock();
                                    text.Text = startMessage.Substring(0, i);
                                    //txtBlock.Children.Add(text);

                                    Run linkText = new Run(match.ToString());
                                    Hyperlink link = new Hyperlink(linkText)
                                    {
                                        NavigateUri = new Uri("http://" + match.ToString())
                                    };
                                    link.RequestNavigate += new RequestNavigateEventHandler(delegate (object senderee, RequestNavigateEventArgs e)
                                    {
                                        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
                                        e.Handled = true;
                                    });

                                    txtBlock.Inlines.Add(link);
                                    startMessage.Replace(startMessage.Substring(0, f), "");
                                }
                            }
                            else txtBlock.Text = message.Message;
                            //txtBlockWrapper.Children.Add(messageBox);
                            switch (message.Media)
                            {
                                case TLMessageMediaPhoto item:
                                {
                                    System.Windows.Controls.Image photo = new System.Windows.Controls.Image();
                                    photo.Height = 200;
                                    photo.Width = 200;
                                    var messagePhoto = await GetMessagePhotoAsync(item);

                                    photo.Source = ByteToImage(messagePhoto.Bytes);
                                    txtBox.Children.Add(photo);

                                    if (item.Caption != "")
                                    {
                                        TextBlock textBlock = new TextBlock();
                                        textBlock.TextWrapping = TextWrapping.Wrap;
                                        txtBox.Children.Add(textBlock);
                                        textBlock.Text = item.Caption;
                                        //messageBlockWrapper.Children.Add(txtBlock);
                                    }
                                    break;
                                }
                                case TLMessageMediaDocument item:
                                    TLDocument doc = (TLDocument)item.Document;
                                    foreach (var att in doc.Attributes.ToList())
                                    {
                                        switch (att)
                                        {
                                            case TLDocumentAttributeVideo video:
                                                break;
                                            case TLDocumentAttributeAudio audio:
                                                break;
                                            case TLDocumentAttributeSticker sticker:
                                                break;
                                        }
                                    }
                                    break;
                                default:
                                {
                                    //MessageBox.Show(message.Media.GetType().ToString());
                                    break;
                                }
                            }
                            break;
                        }
                        break;
                    }
                    case TLMessageService message:
                    {
                        TextBlock txtBlock = new TextBlock();
                        txtBlock.TextWrapping = TextWrapping.Wrap;
                        txtBox.Children.Add(txtBlock);

                        txtBlock.TextAlignment = TextAlignment.Center;
                        dynamic action = message.Action;
                        txtBlock.Text = ServiceMessageHandler(action).ToString();
                        messageBlock.Children.Add(txtBlock);
                        break;
                    }
                }
                messages_field.Children.Insert(0, /*messageBlockWrapper*/gridBorder);
            }
        }

        private async Task GetDialogsOnStartAsync()
        {
            dialogsList = await client.GetUserDialogsAsync() as TLDialogs;

            if (!chats_list.HasContent)
            {
                TLContacts result = await client.GetContactsAsync();
                contacts = result.Users.ToList().Where(x => x.GetType() == typeof(TLUser)).Cast<TLUser>();

                ListBox usersList = new ListBox();

                TextBlock savedMessages = new TextBlock();
                savedMessages.Height = 20;
                savedMessages.Text = "Saved Messages";
                savedMessages.MouseDown += (sender, e) => OpenDialogAsync(_session.TLUser);
                usersList.Items.Add(savedMessages);

                foreach (var contact in contacts)
                {
                    TextBlock txtBlock = new TextBlock();
                    txtBlock.Height = 20;
                    txtBlock.Text = contact.FirstName + " " + contact.LastName;
                    txtBlock.MouseDown += (sender, e) => OpenDialogAsync(contact);
                    usersList.Items.Add(txtBlock);
                }

                contacts_list.Content = usersList;
            }
        }

        private async Task<bool> DidHaveMessagesAsync(TLUser contact)
        {
            var req = new TLRequestGetHistory
            {
                Peer = new TLInputPeerUser { UserId = contact.Id, AccessHash = contact.AccessHash.Value }
            };

            try
            {
                var messages = await client.SendRequestAsync<TLMessages>(req);

                if (messages.Messages.Count == 0) return false;
                else return true;
            } catch (InvalidCastException)
            {
                return true;
            }
        }

        private void message_enter_txb_GotFocus(object sender, RoutedEventArgs e)
        {
            string richTextBoxText = new TextRange(message_enter_txb.Document.ContentStart, message_enter_txb.Document.ContentEnd).Text;
            if (richTextBoxText.Equals(EnterYourMessageHere))
            {
                message_enter_txb.Document.Blocks.Clear();
            }
        }

        private void message_enter_txb_LostFocus(object sender, RoutedEventArgs e)
        {
            if (message_enter_txb.Document.Blocks.Count == 0)
            {
                message_enter_txb.Document.Blocks.Clear();
                message_enter_txb.Document.Blocks.Add(new Paragraph(new Run("Enter Your message here")));
            }
        }

        private void messages_field_scroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (messages_field.Children.Count >= 50 && messages_field_scroll.VerticalOffset == 0)
            {
                switch (openedDialog)
                {
                    case TLChat chat:
                        OpenDialogAsync(openedDialog);
                        break;
                    case TLChannel channel:
                        OpenDialogAsync(openedDialog);
                        break;
                    case TLUser user:
                        OpenDialogAsync(openedDialog);
                        break;
                }
            }
        }

        private void message_enter_txb_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                string richText = new TextRange(message_enter_txb.Document.ContentStart, message_enter_txb.Document.ContentEnd).Text;
                /*if (rgx.IsMatch(richText))
                {
                    new TextRange(message_enter_txb.Document.ContentStart, message_enter_txb.Document.ContentEnd).Text = rgx.Replace(richText, (matched) =>
                    {
                        Emoji.Wpf.Image img = new Emoji.Wpf.Image();
                        img.Text = matched.Value;
                        MessageBox.Show(img.Text);
                        return String.Format("{0}", img);
                    });
                }*/
            } catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private async void send_message_Click(object sender, RoutedEventArgs e)
        {
            try {
                string messageText = new TextRange(message_enter_txb.Document.ContentStart, message_enter_txb.Document.ContentEnd).Text;
                if (messageText.Length != 0 && openedDialog != null && isPhotoPinned == false)
                {
                    switch (openedDialog)
                    {
                        case TLChat chat:
                            await client.SendMessageAsync(new TLInputPeerChat()
                            {
                                ChatId = openedDialog.Id
                            }, messageText);
                            break;
                        case TLChannel channel:
                            await client.SendMessageAsync(new TLInputPeerChannel()
                            {
                                ChannelId = openedDialog.Id,
                                AccessHash = openedDialog.AccessHash
                            }, messageText);
                            break;
                        case TLUser user:
                            await client.SendMessageAsync(new TLInputPeerUser()
                            {
                                UserId = openedDialog.Id
                            }, messageText);
                            break;
                    }
                }

                if (pinnedFile != null)
                {
                    int pos = pinnedFile.LastIndexOf("\\") + 1;
                    string pinnedName = pinnedFile.Substring(pos, pinnedFile.Length - pos);

                    dynamic fileResult;
                    FileInfo fInfo = new FileInfo(pinnedFile);
                    if (fInfo.Length > 10485760)
                        fileResult = (TLInputFileBig)await client.UploadFile(pinnedFile, new StreamReader(pinnedFile));
                    else
                        fileResult = (TLInputFile)await client.UploadFile(pinnedFile, new StreamReader(pinnedFile));

                    var dotPos = pinnedFile.LastIndexOf(".") + 1;
                    string pinnedExtension = pinnedFile.Substring(pos, pinnedFile.Length - pos);
                    string fileMimeType =  MimeTypeMap.GetMimeType(pinnedExtension);


                    switch (openedDialog)
                    {
                        case TLChat chat:
                            if (isPhotoPinned == false)
                            {
                                await client.SendUploadedDocument(new TLInputPeerChat() { ChatId = openedDialog.Id },
                                    fileResult,
                                    pinnedName,
                                    fileMimeType,
                                    new TLVector<TLAbsDocumentAttribute>());
                            }
                            else
                            {
                                await client.SendUploadedPhoto(new TLInputPeerChat() { ChatId = openedDialog.Id }, fileResult, messageText);
                            }
                            break;
                        case TLChannel channel:
                            if (isPhotoPinned == false)
                            {
                                await client.SendUploadedDocument(new TLInputPeerChannel()
                                {
                                    ChannelId = openedDialog.Id,
                                    AccessHash = openedDialog.AccessHash
                                },
                                fileResult,
                                pinnedName,
                                fileMimeType,
                                new TLVector<TLAbsDocumentAttribute>());
                            }
                            else
                            {
                                await client.SendUploadedPhoto(new TLInputPeerChannel() { ChannelId = openedDialog.Id,
                                AccessHash = openedDialog.AccessHash}, fileResult, messageText);
                            }
                            break;
                        case TLUser user:
                            if (isPhotoPinned == false)
                            {
                                await client.SendUploadedDocument(new TLInputPeerUser() { UserId = openedDialog.Id },
                                fileResult,
                                pinnedName,
                                fileMimeType,
                                new TLVector<TLAbsDocumentAttribute>());
                            }
                            else
                            {
                                await client.SendUploadedPhoto(new TLInputPeerUser() { UserId = openedDialog.Id,
                                    AccessHash = openedDialog.AccessHash }, fileResult, messageText);
                            }
                            break;
                    }
                }

                message_enter_txb.Document.Blocks.Clear();
                message_enter_txb.Document.Blocks.Add(new Paragraph(new Run("Enter Your message here")));

                refreshDialog(openedDialog);
            } catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void pin_file_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog chooseFileDlg = new Microsoft.Win32.OpenFileDialog();
            chooseFileDlg.InitialDirectory = "c:\\";
            chooseFileDlg.Filter = "All files (*.*)|*.*";
            chooseFileDlg.RestoreDirectory = true;

            Nullable<bool> result = chooseFileDlg.ShowDialog();

            if (result == true)
            {
                isPhotoPinned = false;
                pinnedFile = chooseFileDlg.FileName;
                int pos = pinnedFile.LastIndexOf("\\") + 1;
                added_file_name.Content = pinnedFile.Substring(pos, pinnedFile.Length - pos);
            }
        }

        private void pin_photo_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog chooseFileDlg = new Microsoft.Win32.OpenFileDialog();
            chooseFileDlg.InitialDirectory = "c:\\";
            chooseFileDlg.Filter = "Image files (*.jpg, *.jpeg, *.jpe, *.jfif, *.png) | *.jpg; *.jpeg; *.jpe; *.jfif; *.png";
            chooseFileDlg.RestoreDirectory = true;

            Nullable<bool> result = chooseFileDlg.ShowDialog();

            if (result == true)
            {
                pinnedFile = chooseFileDlg.FileName;
                isPhotoPinned = true;
                int pos = pinnedFile.LastIndexOf("\\") + 1;
                added_file_name.Content = pinnedFile.Substring(pos, pinnedFile.Length - pos);
            }
        }

        private void contactsTab_Clicked(object sender, MouseButtonEventArgs e)
        {
            
        }

        private void channelsTab_Clicked(object sender, MouseButtonEventArgs e)
        {
            if (!chats_list.HasContent)
            {
                var chats = dialogsList.Chats.Where(x => x.GetType() == typeof(TLChannel)).Cast<TLChannel>();

                ListBox chatsPanel = new ListBox();
                foreach (var dialog in chats)
                {
                    if (!dialog.Megagroup)
                    {
                        TextBlock txtBlock = new TextBlock();
                        txtBlock.Height = 20;
                        txtBlock.Text = dialog.Title;
                        txtBlock.MouseDown += (sendered, ev) => OpenDialogAsync(dialog);
                        chatsPanel.Items.Add(txtBlock);
                    }
                    else
                    {
                        megagroupsList.Add(dialog);
                    }
                }
                channels_list.Content = chatsPanel;
            }
        }

        private void chatsTab_Clicked(object sender, MouseButtonEventArgs e)
        {
            if (!chats_list.HasContent)
            {
                var userChats = dialogsList.Chats.Where(x => x.GetType() == typeof(TLChat)).Cast<TLChat>();

                ListBox chatsPanel = new ListBox();
                foreach (var chat in userChats)
                {
                    if (chat.Deactivated) continue;
                    TextBlock txtBlock = new TextBlock();
                    txtBlock.Height = 20;
                    txtBlock.Text = chat.Title;

                    txtBlock.MouseDown += (senderer, ev) => OpenDialogAsync(chat);
                    chatsPanel.Items.Add(txtBlock);
                }
                if (megagroupsList.Count == 0)
                {
                    var chats = dialogsList.Chats.Where(x => x.GetType() == typeof(TLChannel)).Cast<TLChannel>();
                    foreach (var dialog in chats)
                    {
                        if (dialog.Megagroup)
                        {
                            megagroupsList.Add(dialog);
                        }
                    }
                }

                foreach (var dialog in megagroupsList.ToList())
                {
                    if (dialog.Megagroup)
                    {
                        TextBlock txtBlock = new TextBlock();
                        txtBlock.Height = 20;
                        txtBlock.Text = dialog.Title;
                        txtBlock.MouseDown += (sendered, ev) => OpenDialogAsync(dialog);
                        chatsPanel.Items.Add(txtBlock);
                        megagroupsList.Add(dialog);
                    }
                }

                chats_list.Content = chatsPanel;
            }
        }

        private async void OpenChatMessagesAsync(TLMessagesSlice messages, TLChat chat)
        {
            try
            {
                foreach (var chatMessage in messages.Messages)
                {
                    Border gridBorder = new Border();
                    //gridBorder.Margin = new Thickness(10, 0, 10, 10);
                    //gridBorder.CornerRadius = new CornerRadius(10);
                    //gridBorder.BorderThickness = new Thickness(1, 1, 1, 1);
                    //gridBorder.BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#c3c3c3"));
                    //gridBorder.Background = System.Windows.Media.Brushes.LightYellow;
                    //gridBorder.Width = 400;
                    //gridBorder.HorizontalAlignment = HorizontalAlignment.Left;
                    //gridBorder.VerticalAlignment = VerticalAlignment.Top;

                    Grid messageBlockWrapper = new Grid();
                    gridBorder.Child = messageBlockWrapper;
                    ColumnDefinition gridCol1 = new ColumnDefinition();
                    ColumnDefinition gridCol2 = new ColumnDefinition();
                    gridCol1.Width = new GridLength(50);
                    messageBlockWrapper.ColumnDefinitions.Add(gridCol1);
                    messageBlockWrapper.ColumnDefinitions.Add(gridCol2);

                    if (chatMessage.GetType() == typeof(TLMessage))
                    {
                        Ellipse userPhoto = new Ellipse();
                        userPhoto.Margin = new Thickness(0, 5, 0, 0);
                        userPhoto.VerticalAlignment = VerticalAlignment.Top;
                        userPhoto.SetValue(Grid.ColumnProperty, 0);
                        userPhoto.Height = 40;
                        userPhoto.Width = 40;
                        ImageBrush userMainPhoto = new ImageBrush();

                        var message = (TLMessage)chatMessage;



                        //contacts = chat..ToList().Where(x => { MessageBox.Show(x.ToString()); return x == message.FromId.Value; }).Cast<TLUser>();
                        //MessageBox.Show(message.FromId.Value.ToString());
                        //MessageBox.Show(contacts.ToString());
                        //MessageBox.Show(channel.Username.Where(x > x));
                        /* (user.Id != message.FromId.Value)
                        {
                            userMainPhoto.ImageSource = img_userPhoto.ImageSource;
                            userMessageTitle = _session.TLUser.FirstName + " " + _session.TLUser.LastName;
                        }
                        else
                        {
                            userMainPhoto.ImageSource = ByteToImage(userPhotoFile.Bytes);
                        }*/

                        userPhoto.SetValue(Grid.ColumnProperty, 1);
                        userPhoto.Fill = userMainPhoto;
                        Grid.SetColumn(userPhoto, 0);
                        messageBlockWrapper.Children.Add(userPhoto);
                    }

                    StackPanel messageBlock = new StackPanel();
                    messageBlock.Orientation = Orientation.Vertical;
                    messageBlockWrapper.Children.Add(messageBlock);

                    Label sender = new Label();
                    sender.FontWeight = FontWeights.Bold;
                    //sender.Content = userMessageTitle;
                    messageBlock.Children.Add(sender);

                    StackPanel txtBox = new StackPanel();
                    messageBlock.Children.Add(txtBox);

                    Label time = new Label();
                    time.FontSize = 10;
                    time.HorizontalContentAlignment = HorizontalAlignment.Right;
                    if (chatMessage.GetType() == typeof(TLMessage))
                    {
                        var message = (TLMessage)chatMessage;
                        DateTime messageTime = TimeUnixTOWindows(message.Date, true);
                        time.Content = messageTime.Hour + ":" + messageTime.Minute;
                    }
                    else if (chatMessage.GetType() == typeof(TLMessage))
                    {
                        var message = (TLMessageService)chatMessage;
                        DateTime messageTime = TimeUnixTOWindows(message.Date, true);
                        time.Content = messageTime.Hour + ":" + messageTime.Minute;
                    }
                    messageBlock.Children.Add(time);


                    Grid.SetColumn(messageBlock, 1);

                    switch (chatMessage)
                    {
                        case TLMessage message:
                            {
                                if (message.Message != "")
                                {
                                    TextBlock txtBlock = new TextBlock();
                                    txtBlock.TextWrapping = TextWrapping.Wrap;
                                    txtBox.Children.Add(txtBlock);

                                    string startMessage = message.Message;
                                    Regex reg = new Regex(LinkPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                                    if (reg.IsMatch(startMessage))
                                    {
                                        foreach (Match match in reg.Matches(message.Message))
                                        {
                                            int i = match.Index;
                                            int f = match.Length;

                                            TextBlock text = new TextBlock();
                                            text.Text = startMessage.Substring(0, i);
                                            //txtBlock.Children.Add(text);

                                            Run linkText = new Run(match.ToString());
                                            Hyperlink link = new Hyperlink(linkText)
                                            {
                                                NavigateUri = new Uri("http://" + match.ToString())
                                            };
                                            link.RequestNavigate += new RequestNavigateEventHandler(delegate (object senderee, RequestNavigateEventArgs e)
                                            {
                                                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
                                                e.Handled = true;
                                            });

                                            txtBlock.Inlines.Add(link);
                                            startMessage.Replace(startMessage.Substring(0, f), "");
                                        }
                                    }
                                    else txtBlock.Text = message.Message;
                                    //txtBlockWrapper.Children.Add(messageBox);
                                }
                                switch (message.Media)
                                {
                                    case TLMessageMediaPhoto item:
                                        {
                                            System.Windows.Controls.Image photo = new System.Windows.Controls.Image();
                                            photo.Height = 200;
                                            photo.Width = 200;
                                            var messagePhoto = await GetMessagePhotoAsync(item);

                                            photo.Source = ByteToImage(messagePhoto.Bytes);
                                            txtBox.Children.Add(photo);

                                            if (item.Caption != "")
                                            {
                                                TextBlock txtBlock = new TextBlock();
                                                txtBlock.TextWrapping = TextWrapping.Wrap;
                                                txtBox.Children.Add(txtBlock);
                                                txtBlock.Text = item.Caption;
                                                //messageBlockWrapper.Children.Add(txtBlock);
                                            }
                                            break;
                                        }
                                    case TLMessageMediaDocument item:
                                        var doc = (TLDocument)item.Document;
                                        foreach (var att in doc.Attributes.ToList())
                                        {
                                            switch (att)
                                            {
                                                case TLDocumentAttributeVideo video:

                                                    break;
                                                case TLDocumentAttributeAudio audio:

                                                    break;
                                                case TLDocumentAttributeSticker sticker:
                                                    var inputStickerSet = (TLInputStickerSetID)sticker.Stickerset;
                                                    //var stickerSet = (TLStickerSet)sticker;

                                                    var req = new TLInputDocumentFileLocation()
                                                    {
                                                        Id = inputStickerSet.Id,
                                                        AccessHash = inputStickerSet.AccessHash,
                                                        Version = doc.Version
                                                    };
                                                    MessageBox.Show(req.ToString());
                                                    break;
                                            }
                                        }
                                        break;
                                }
                                break;
                            }
                        case TLMessageService message:
                            {
                                TextBlock txtBlock = new TextBlock();
                                txtBlock.TextWrapping = TextWrapping.Wrap;
                                txtBox.Children.Add(txtBlock);

                                txtBlock.TextAlignment = TextAlignment.Center;
                                dynamic action = message.Action;
                                txtBlock.Text = ServiceMessageHandler(action).ToString();
                                //messageBlock.Children.Add(txtBox);
                                break;
                            }
                    }
                    messages_field.Children.Insert(0, /*messageBlockWrapper*/gridBorder);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private async void OpenChatMessagesAsync(TLMessages messages, TLChat user)
        {
           // string userMessageTitle = user.FirstName + " " + user.LastName;
            //TLFile userPhotoFile = await GetUserPhotoAsync(user);

            foreach (var chatMessage in messages.Messages)
            {
                Border gridBorder = new Border();
                //gridBorder.Margin = new Thickness(10, 0, 10, 10);
                //gridBorder.CornerRadius = new CornerRadius(10);
                //gridBorder.BorderThickness = new Thickness(1, 1, 1, 1);
                //gridBorder.BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#c3c3c3"));
                //gridBorder.Background = System.Windows.Media.Brushes.LightYellow;
                //gridBorder.Width = 400;
                //gridBorder.HorizontalAlignment = HorizontalAlignment.Left;
                //gridBorder.VerticalAlignment = VerticalAlignment.Top;

                Grid messageBlockWrapper = new Grid();
                gridBorder.Child = messageBlockWrapper;
                ColumnDefinition gridCol1 = new ColumnDefinition();
                ColumnDefinition gridCol2 = new ColumnDefinition();
                gridCol1.Width = new GridLength(50);
                messageBlockWrapper.ColumnDefinitions.Add(gridCol1);
                messageBlockWrapper.ColumnDefinitions.Add(gridCol2);

                if (chatMessage.GetType() == typeof(TLMessage))
                {
                    Ellipse userPhoto = new Ellipse();
                    userPhoto.Margin = new Thickness(0, 5, 0, 0);
                    userPhoto.VerticalAlignment = VerticalAlignment.Top;
                    userPhoto.SetValue(Grid.ColumnProperty, 0);
                    userPhoto.Height = 40;
                    userPhoto.Width = 40;
                    ImageBrush userMainPhoto = new ImageBrush();

                    var message = (TLMessage)chatMessage;
                    /*if (user.Id != message.FromId.Value)
                    {
                        userMainPhoto.ImageSource = img_userPhoto.ImageSource;
                        userMessageTitle = _session.TLUser.FirstName + " " + _session.TLUser.LastName;
                    }
                    else
                    {
                        userMainPhoto.ImageSource = ByteToImage(userPhotoFile.Bytes);
                    }*/

                    userPhoto.SetValue(Grid.ColumnProperty, 1);
                    userPhoto.Fill = userMainPhoto;
                    Grid.SetColumn(userPhoto, 0);
                    messageBlockWrapper.Children.Add(userPhoto);
                }

                StackPanel messageBlock = new StackPanel();
                messageBlock.Orientation = Orientation.Vertical;
                messageBlockWrapper.Children.Add(messageBlock);

                Label sender = new Label();
                sender.FontWeight = FontWeights.Bold;
                //sender.Content = userMessageTitle;
                messageBlock.Children.Add(sender);

                StackPanel txtBox = new StackPanel();
                messageBlock.Children.Add(txtBox);

                Label time = new Label();
                time.FontSize = 10;
                time.HorizontalContentAlignment = HorizontalAlignment.Right;
                if (chatMessage.GetType() == typeof(TLMessage))
                {
                    var messageT = (TLMessage)chatMessage;
                    DateTime messageTime = TimeUnixTOWindows(messageT.Date, true);
                    time.Content = messageTime.Hour + ":" + messageTime.Minute;
                }
                else if (chatMessage.GetType() == typeof(TLMessage))
                {
                    var message = (TLMessageService)chatMessage;
                    DateTime messageTime = TimeUnixTOWindows(message.Date, true);
                    time.Content = messageTime.Hour + ":" + messageTime.Minute;
                }
                messageBlock.Children.Add(time);

                Grid.SetColumn(messageBlock, 1);
                switch (chatMessage)
                {
                    case TLMessage message:
                        {
                            if (message.Message != "")
                            {
                                TextBlock txtBlock = new TextBlock();
                                txtBlock.TextWrapping = TextWrapping.Wrap;
                                txtBox.Children.Add(txtBlock);

                                string startMessage = message.Message;
                                Regex reg = new Regex(LinkPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                                if (reg.IsMatch(startMessage))
                                {
                                    foreach (Match match in reg.Matches(message.Message))
                                    {
                                        int i = match.Index;
                                        int f = match.Length;

                                        TextBlock text = new TextBlock();
                                        text.Text = startMessage.Substring(0, i);
                                        //txtBlock.Children.Add(text);

                                        Run linkText = new Run(match.ToString());
                                        Hyperlink link = new Hyperlink(linkText)
                                        {
                                            NavigateUri = new Uri("http://" + match.ToString())
                                        };
                                        link.RequestNavigate += new RequestNavigateEventHandler(delegate (object senderee, RequestNavigateEventArgs e)
                                        {
                                            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
                                            e.Handled = true;
                                        });

                                        txtBlock.Inlines.Add(link);
                                        startMessage.Replace(startMessage.Substring(0, f), "");
                                    }
                                }
                                else txtBlock.Text = message.Message;
                                //txtBlockWrapper.Children.Add(messageBox);
                                switch (message.Media)
                                {
                                    case TLMessageMediaPhoto item:
                                        {
                                            System.Windows.Controls.Image photo = new System.Windows.Controls.Image();
                                            photo.Height = 200;
                                            photo.Width = 200;
                                            var messagePhoto = await GetMessagePhotoAsync(item);

                                            photo.Source = ByteToImage(messagePhoto.Bytes);
                                            txtBox.Children.Add(photo);

                                            if (item.Caption != "")
                                            {
                                                TextBlock textBlock = new TextBlock();
                                                textBlock.TextWrapping = TextWrapping.Wrap;
                                                txtBox.Children.Add(textBlock);
                                                textBlock.Text = item.Caption;
                                                //messageBlockWrapper.Children.Add(txtBlock);
                                            }
                                            break;
                                        }
                                    case TLMessageMediaDocument item:
                                        TLDocument doc = (TLDocument)item.Document;
                                        foreach (var att in doc.Attributes.ToList())
                                        {
                                            switch (att)
                                            {
                                                case TLDocumentAttributeVideo video:
                                                    break;
                                                case TLDocumentAttributeAudio audio:
                                                    break;
                                                case TLDocumentAttributeSticker sticker:
                                                    break;
                                            }
                                        }
                                        break;
                                    default:
                                        {
                                            //MessageBox.Show(message.Media.GetType().ToString());
                                            break;
                                        }
                                }
                                break;
                            }
                            break;
                        }
                    case TLMessageService message:
                        {
                            TextBlock txtBlock = new TextBlock();
                            txtBlock.TextWrapping = TextWrapping.Wrap;
                            txtBox.Children.Add(txtBlock);

                            txtBlock.TextAlignment = TextAlignment.Center;
                            dynamic action = message.Action;
                            txtBlock.Text = ServiceMessageHandler(action).ToString();
                            messageBlock.Children.Add(txtBlock);
                            break;
                        }
                }
                messages_field.Children.Insert(0, /*messageBlockWrapper*/gridBorder);
            }
        }

        private void open_options_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PropertiesWindow propertiesWindow = new PropertiesWindow();
            propertiesWindow.Show();
        }

        private async void refreshDialog(dynamic dialog)
        {
            try
            {
                if (write_text_field.Height == 0)
                {
                    if (dialog.GetType() == typeof(TLChannel))
                    {
                        if (!dialog.Megagroup)
                        {
                            write_text_field.Height = 0;
                            //messages_field_scroll.Height = ;
                        }
                        else if (dialog.Megagroup)
                        {
                            write_text_field.Height = 131;
                        }
                    }
                    else
                    {
                        write_text_field.Height = 131;
                        //messages_field_scroll.Height = ;
                    }
                }

                int end;

                messages_field.Children.Clear();
                int start = 0;
                if (dialog.GetType() == typeof(TLChannel))
                {
                    if (dialog.Megagroup) end = 50;
                    else end = 25;
                }
                else end = 25;
                messages_field_scroll.ScrollToEnd();


                switch (dialog)
                {
                    case TLChannel item:
                        var req = new TLRequestGetHistory
                        {
                            AddOffset = start,
                            Limit = end,
                            Peer = new TLInputPeerChannel { ChannelId = dialog.Id, AccessHash = dialog.AccessHash }
                        };

                        OpenMessagesAsync(await client.SendRequestAsync<TLChannelMessages>(req), item);
                        break;
                    case TLChat item:
                        req = new TLRequestGetHistory
                        {
                            AddOffset = start,
                            Limit = end,
                            Peer = new TLInputPeerChat { ChatId = item.Id }
                        };

                        try
                        {
                            OpenChatMessagesAsync(await client.SendRequestAsync<TLMessagesSlice>(req), item);
                        }
                        catch (InvalidCastException ex)
                        {
                            OpenChatMessagesAsync(await client.SendRequestAsync<TLMessages>(req), item);
                        }
                        break;
                    case TLUser item:
                        req = new TLRequestGetHistory
                        {
                            AddOffset = start,
                            Limit = end,
                            Peer = new TLInputPeerUser { UserId = item.Id, AccessHash = item.AccessHash.Value }
                        };

                        try
                        {
                            OpenMessagesAsync(await client.SendRequestAsync<TLMessagesSlice>(req), item);
                        }
                        catch (InvalidCastException)
                        {
                            OpenMessagesAsync(await client.SendRequestAsync<TLMessages>(req), item);
                        }
                        break;
                    default:
                        MessageBox.Show("Hmm🤔...new dialog type");
                        break;
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void refresh_dialog_MouseDown(object sender, MouseButtonEventArgs e)
        {
            refreshDialog(openedDialog);
        }
    }
}
