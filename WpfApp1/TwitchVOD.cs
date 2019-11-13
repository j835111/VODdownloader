using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace WpfApp1
{
    public class TwitchVOD
    {
        #region Fields

        private Timer timer;

        private int vodCount = 0;

        private string directoryName;

        #region Properties

        public string Id { get; }

        public bool State { get; }

        public string Url { get; }

        #endregion

        #region Constructor

        public TwitchVOD()
        {
            timer = new Timer(300000)
            {
                AutoReset = true,
                Enabled = true
            };
            timer.Elapsed += TimerHandler;
        }

        #endregion

        #region Methods

        public void Stop()
        {

        }

        private void TimerHandler(object sender,ElapsedEventArgs e)
        {
            CheckOutputDirectory("temp" + Id);
            VodPlaylist vodPlaylists = TwitchService.RetrieveVodPlaylist("temp" + Id, playlistUrl);

        }

        private void CheckOutputDirectory(string outputDir)
        {
            if (!Directory.Exists(outputDir))
            {
                Console.WriteLine("Creating output directory...");
                FileSystem.CreateDirectory(outputDir);
                Console.WriteLine("Done!");
            }
        }
    }
}
