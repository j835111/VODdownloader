﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Timer = System.Timers.Timer;
using System.ComponentModel;

namespace WpfApp1
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        public TwitchService twitchService = null;
        public static int vodCount = 0;
        public static int downloadCount = 0;
        public static List<Task> tasks = null;
        public static Timer timer = null;
        public Action<string> TextAction;
        public Action<string> StateAction;
        private string channelName = null;
        public static bool IsClick = false;
        private NotifyIcon notify = null;

        public MainWindow()
        {
            InitializeComponent();
            TextAction += TextHandler;
            StateAction += StateHandler;
            this.StateChanged += MainWindow_StateChanged;

            notify = new NotifyIcon
            {
                Icon = new Icon("notifyicon.ico"),
                Visible = true
            };
            notify.Click += Notify_Click;
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
                this.Hide();
        }

        private void Notify_Click(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = WindowState.Normal;
        }

        private void TextHandler(string text)
        {
            this.Dispatcher.BeginInvoke((Action)delegate ()
            {
                tb.Text += Environment.NewLine + text;
            });
        }

        private void StateHandler(string text)
        {
            this.Dispatcher.BeginInvoke((Action)delegate ()
            {
                state.Content = text;
            });
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (IsClick)
                return;

            IsClick = true;
            tb.Text += "按了";
            channelName = name.Text;

            tasks = new List<Task>();
            //忽略https憑證問題
            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(ValidateServerCertificate);

            tb.Text += "頻道名稱:" + channelName;
            tb.Text += Environment.NewLine + "正在建立執行個體...";
            tb.Text += Environment.NewLine + "建立成功";
            tb.Text += Environment.NewLine + "正在查詢是否有開台...";

            //try
            //{
            bool state = TwitchService.GetStreamState(TwitchService.GetChannelIdByName(channelName));

            if (!state)
            {
                throw new ApplicationException("沒有開台");
            }

            twitchService = new TwitchService(channelName, TextAction, StateAction);

            Task task = new Task(() =>
            {
                twitchService.DownloadWrapper();
            });
            tasks.Add(task);

            tb.Text += Environment.NewLine + "開始下載已直播之部分...";
            task.Start();

            tb.Text += Environment.NewLine + "開始30秒一次的開台狀態偵測...";


            timer = new Timer(15000)
            {
                AutoReset = true,
                Enabled = true
            };
            timer.Elapsed += twitchService.TimerHandler;
            timer.Start();
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show("錯誤!!" + Environment.NewLine + ex.ToString());
            //}
        }

        public bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}
