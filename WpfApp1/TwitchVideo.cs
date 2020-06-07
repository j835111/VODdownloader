using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1
{
    public class TwitchVideo
    {
        #region Constatnts

        private const string UNTITLED_BROADCAST = "Untitled Broadcast";
        private const string UNKNOWN_GAME = "Unknown";

        #endregion Constatnts

        #region Constructors

        public TwitchVideo(string channel, string title, string id, string game, int views, TimeSpan length,
             DateTime recordedDate, Uri url)
        {
            if (string.IsNullOrWhiteSpace(channel))
            {
                throw new ArgumentNullException(nameof(channel));
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                title = UNTITLED_BROADCAST;
            }

            Channel = channel;
            Title = title;
            Id = id;

            if (string.IsNullOrWhiteSpace(game))
            {
                Game = UNKNOWN_GAME;
            }
            else
            {
                Game = game;
            }

            Views = views;
            Length = length;
            RecordedDate = recordedDate;
            Url = url ?? throw new ArgumentNullException(nameof(url));
        }

        #endregion Constructors

        #region Properties

        public string Channel { get; }

        public string Title { get; }

        public string Id { get; }

        public string Game { get; }

        public TimeSpan Length { get; }

        public string LengthStr
        {
            get
            {
                return Length.ToDaylessString();
            }
        }

        public int Views { get; }

        public List<TwitchVideoQuality> Qualities { get; }

        public string BestQuality
        {
            get
            {
                if (Qualities == null || Qualities.Count == 0)
                {
                    return TwitchVideoQuality.UNKNOWN;
                }

                return Qualities.First().ResFpsString;
            }
        }

        public DateTime RecordedDate { get; }

        public Uri Url { get; }

        #endregion Properties
    }
}
