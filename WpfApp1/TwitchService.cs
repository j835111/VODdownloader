using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Timers;
using System.Windows.Controls;

namespace WpfApp1
{
    public class TwitchService
    {
        private const string CHANNEL_VIDEOS_URL = "https://api.twitch.tv/kraken/channels/{0}/videos";
        private const string CHANNEL_URL = "https://api.twitch.tv/kraken/channels/{0}";
        private const string USERS_URL = "https://api.twitch.tv/kraken/users";
        private const string ALL_PLAYLISTS_URL = "https://usher.twitch.tv/vod/{0}?nauthsig={1}&nauth={2}&allow_source=true&player=twitchweb&allow_spectre=true&allow_audio_only=true";
        private const string ACCESS_TOKEN_URL = "https://api.twitch.tv/api/vods/{0}/access_token";
        private const string STREAMS_URL = "https://api.twitch.tv/helix/streams?user_id={0}";
        private const string VIDEO_URL = "https://api.twitch.tv/kraken/videos/{0}";
        //private const string TWITCH_CLIENT_ID = "37v97169hnj8kaoq8fs3hzz8v6jezdj";
        private const string TWITCH_CLIENT_ID = "2b978gf2i6x3j9eeozx42cueu8o7pf";
        private const string TWITCH_CLIENT_ID_HEADER = "Client-ID";
        private const string TWITCH_V5_ACCEPT = "application/vnd.twitchtv.v5+json";
        private const string TWITCH_V5_ACCEPT_HEADER = "Accept";

        private const int DOWNLOAD_RETRIES = 3;
        private const int DOWNLOAD_RETRY_TIME = 20;

        private Action<string> TextHandler = null;
        private Action<string> StateHandler = null;
        private TwitchVideo twitchVideo = null;
        private string playlistUrl = null;
        private string vodId = null;
        private int vodCount = 0;
        private int downloadCount = 0;
        private string channelName = null;

        public TwitchService(string channelName, Action<string> textAction, Action<string> stateAction)
        {
            this.channelName = channelName;
            TextHandler = textAction;
            StateHandler = stateAction;
            vodId = SearchVODId(channelName);
            playlistUrl = RetrievePlaylistUrl(vodId, RetrieveVodAuthInfo(vodId));
        }

        //public TwitchService(TextBlock tb)
        //{
        //    this.tb = tb;
        //}



        public TwitchVideo GetTwitchVideoFromId(int id)
        {
            using (WebClient webClient = CreateTwitchWebClient())
            {
                try
                {
                    string result = webClient.DownloadString(string.Format(VIDEO_URL, id));

                    JObject videoJson = JObject.Parse(result);

                    if (videoJson != null)
                    {
                        return ParseVideo(videoJson);
                    }
                }
                catch (WebException ex)
                {
                    if (ex.Response is HttpWebResponse resp && resp.StatusCode == HttpStatusCode.NotFound)
                    {
                        return null;
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return null;
        }

        public static TwitchVideo ParseVideo(JObject videoJson)
        {
            string channel = videoJson.Value<JObject>("channel").Value<string>("display_name");
            string title = videoJson.Value<string>("title");
            string id = videoJson.Value<string>("_id");
            string game = videoJson.Value<string>("game");
            int views = videoJson.Value<int>("views");
            TimeSpan length = new TimeSpan(0, 0, videoJson.Value<int>("length"));
            DateTime recordedDate = DateTime.ParseExact(videoJson.Value<string>("published_at"), "MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
            Uri url = new Uri(videoJson.Value<string>("url"));

            if (id.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                id = id.Substring(1);
            }

            return new TwitchVideo(channel, title, id, game, views, length, recordedDate, url);
        }

        public string SearchVODId(string channel)
        {
            if (string.IsNullOrWhiteSpace(channel))
            {
                throw new ArgumentNullException(nameof(channel));
            }

            string channelId = GetChannelIdByName(channel);
            TextHandler("Find Channel ID:" + channelId);
            Console.WriteLine("Find Channel ID:" + channelId);

            string channelVideosUrl = string.Format(CHANNEL_VIDEOS_URL, channelId);

            using (WebClient webClient = CreateTwitchWebClient())
            {
                webClient.QueryString.Add("broadcast_type", "archive");
                webClient.QueryString.Add("limit", "100");
                webClient.QueryString.Add("offset", "0");

                string result = webClient.DownloadString(channelVideosUrl);

                JObject videosResponseJson = JObject.Parse(result);

                if (videosResponseJson != null)
                {
                    //JArray videoJson = videosResponseJson.Value<JArray>("videos");
                    JObject videoJson = (JObject)videosResponseJson.Value<JArray>("videos")[0];
                    string id = videoJson.Value<string>("_id");

                    if (id.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                    {
                        id = id.Substring(1);
                    }

                    return id;
                }

                return null;
            }
        }

        private static WebClient CreateTwitchWebClient()
        {
            WebClient wc = new WebClient();
            wc.Headers.Add(TWITCH_CLIENT_ID_HEADER, TWITCH_CLIENT_ID);
            wc.Headers.Add(TWITCH_V5_ACCEPT_HEADER, TWITCH_V5_ACCEPT);
            wc.Encoding = Encoding.UTF8;
            return wc;
        }

        public static string GetChannelIdByName(string channel)
        {
            if (string.IsNullOrWhiteSpace(channel))
            {
                throw new ArgumentNullException(nameof(channel));
            }

            using (WebClient webClient = CreateTwitchWebClient())
            {
                webClient.QueryString.Add("login", channel);

                string result = null;

                try
                {
                    result = webClient.DownloadString(USERS_URL);
                }
                catch (WebException)
                {
                    return null;
                }

                if (!string.IsNullOrWhiteSpace(result))
                {
                    JObject searchResultJson = JObject.Parse(result);

                    JArray usersJson = searchResultJson.Value<JArray>("users");

                    if (usersJson != null && usersJson.HasValues)
                    {
                        JToken userJson = usersJson.FirstOrDefault();

                        if (userJson != null)
                        {
                            string id = userJson.Value<string>("_id");

                            if (!string.IsNullOrWhiteSpace(id))
                            {
                                using (WebClient webClientChannel = CreateTwitchWebClient())
                                {
                                    try
                                    {
                                        webClientChannel.DownloadString(string.Format(CHANNEL_URL, id));

                                        return id;
                                    }
                                    catch (WebException)
                                    {
                                        return null;
                                    }
                                    catch (Exception)
                                    {
                                        throw;
                                    }
                                }
                            }
                        }
                    }
                }

                return null;
            }
        }

        public string RetrievePlaylistUrl(string vodId, VodAuthInfo vodAuthInfo)
        {
            using (WebClient webClient = CreateTwitchWebClient())
            {
                //log(Environment.NewLine + Environment.NewLine + "Retrieving m3u8 playlist urls for all VOD qualities...");
                string allPlaylistsStr = webClient.DownloadString(string.Format(ALL_PLAYLISTS_URL, vodId, vodAuthInfo.Signature, vodAuthInfo.Token));
                //log(" done!");

                List<string> allPlaylistsList = allPlaylistsStr.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).Where(s => !s.StartsWith("#")).ToList();

                Console.WriteLine("All Playlist url:");
                TextHandler("All Playlist url:");
                allPlaylistsList.ForEach(url =>
                {
                    Console.WriteLine(url);
                    TextHandler(url);
                });

                string playlistUrl = allPlaylistsList.Where(s => s.ToLowerInvariant().Contains("/chunked/")).First();
                TextHandler("Playlist url for selected:");
                Console.WriteLine("Playlist url for selected:");
                TextHandler(playlistUrl);
                Console.WriteLine(playlistUrl);

                return playlistUrl;
            }
        }

        public VodAuthInfo RetrieveVodAuthInfo(string vodId)
        {
            if (string.IsNullOrWhiteSpace(vodId))
            {
                throw new ArgumentNullException(nameof(vodId));
            }

            using (WebClient webClient = CreateTwitchWebClient())
            {
                string accessTokenStr = webClient.DownloadString(string.Format(ACCESS_TOKEN_URL, vodId));

                JObject accessTokenJson = JObject.Parse(accessTokenStr);

                string token = Uri.EscapeDataString(accessTokenJson.Value<string>("token"));
                string signature = accessTokenJson.Value<string>("sig");

                if (string.IsNullOrWhiteSpace(token))
                {
                    throw new ApplicationException("VOD access token is null!");
                }

                if (string.IsNullOrWhiteSpace(signature))
                {
                    throw new ApplicationException("VOD signature is null!");
                }

                bool privileged = false;
                bool subOnly = false;

                JObject tokenJson = JObject.Parse(HttpUtility.UrlDecode(token));

                if (tokenJson == null)
                {
                    throw new ApplicationException("Decoded VOD access token is null!");
                }

                privileged = tokenJson.Value<bool>("privileged");

                if (privileged)
                {
                    subOnly = true;
                }
                else
                {
                    JObject chansubJson = tokenJson.Value<JObject>("chansub");

                    if (chansubJson == null)
                    {
                        throw new ApplicationException("Token property 'chansub' is null!");
                    }

                    JArray restrictedQualitiesJson = chansubJson.Value<JArray>("restricted_bitrates");

                    if (restrictedQualitiesJson == null)
                    {
                        throw new ApplicationException("Token property 'chansub -> restricted_bitrates' is null!");
                    }

                    if (restrictedQualitiesJson.Count > 0)
                    {
                        subOnly = true;
                    }
                }

                return new VodAuthInfo(token, signature, privileged, subOnly);
            }
        }

        public VodPlaylist RetrieveVodPlaylist(string tempDir, string playlistUrl)
        {
            using (WebClient webClient = new WebClient())
            {
                TextHandler("Retrieving playlist...");
                Console.WriteLine("Retrieving playlist...");
                string playlistStr = webClient.DownloadString(playlistUrl);
                TextHandler("Done!");
                Console.WriteLine("Done!");

                if (string.IsNullOrWhiteSpace(playlistStr))
                {
                    throw new ApplicationException("The playlist is empty!");
                }

                string urlPrefix = playlistUrl.Substring(0, playlistUrl.LastIndexOf("/") + 1);

                TextHandler("Parsing playlist...");
                Console.WriteLine("Parsing playlist...");
                VodPlaylist vodPlaylist = VodPlaylist.Parse(tempDir, playlistStr, urlPrefix);
                TextHandler("Done!");
                Console.WriteLine("Done!");

                TextHandler(Environment.NewLine + "Number of video chunks: " + vodPlaylist.Count());
                Console.WriteLine(Environment.NewLine + "Number of video chunks: " + vodPlaylist.Count());

                return vodPlaylist;
            }

        }

        public void DownloadParts(VodPlaylist vodPlaylist)
        {
            int partsCount = vodPlaylist.Count;

            TextHandler("Starting parallel video chunk download");
            TextHandler("Number of video chunks to download: " + partsCount);
            TextHandler("Parallel video chunk download is running...");
            Console.WriteLine("Starting parallel video chunk download");
            Console.WriteLine("Number of video chunks to download: " + partsCount);
            Console.WriteLine("Parallel video chunk download is running...");

            long completedPartDownloads = 0;

            Parallel.ForEach(vodPlaylist, new ParallelOptions() { MaxDegreeOfParallelism = ServicePointManager.DefaultConnectionLimit - 1 }, (part, loopState) =>
               {
                   int retryCounter = 0;

                   bool success = false;

                   do
                   {
                       try
                       {
                           using (WebClient downloadClient = new WebClient())
                           {
                               byte[] bytes = downloadClient.DownloadData(part.RemoteFile);

                               Interlocked.Increment(ref completedPartDownloads);

                               FileSystem.DeleteFile(part.LocalFile);

                               File.WriteAllBytes(part.LocalFile, bytes);

                               long completed = Interlocked.Read(ref completedPartDownloads);

                               success = true;
                           }
                       }
                       catch (WebException ex)
                       {
                           if (retryCounter < DOWNLOAD_RETRIES)
                           {
                               retryCounter++;
                               TextHandler("Downloading file '" + part.RemoteFile + "' failed! Trying again in " + DOWNLOAD_RETRY_TIME + "s");
                               TextHandler(ex.ToString());
                               Console.WriteLine("Downloading file '" + part.RemoteFile + "' failed! Trying again in " + DOWNLOAD_RETRY_TIME + "s");
                               Console.WriteLine(ex.ToString());
                               Thread.Sleep(DOWNLOAD_RETRY_TIME * 1000);
                           }
                           else
                           {
                               throw new ApplicationException("Could not download file '" + part.RemoteFile + "' after " + DOWNLOAD_RETRIES + " retries!");
                           }
                       }
                   }
                   while (!success);
               });
            TextHandler("Download of all video chunks complete!");
            Console.WriteLine("Download of all video chunks complete!");
            //log(Environment.NewLine + Environment.NewLine + "Download of all video chunks complete!");
        }

        public static bool GetStreamState(string userId)
        {
            using (WebClient webClient = CreateTwitchWebClient())
            {
                string streamStateStr = webClient.DownloadString(string.Format(STREAMS_URL, userId));

                if (string.IsNullOrWhiteSpace(streamStateStr))
                {
                    throw new ApplicationException("The Stream State is error!");
                }

                JObject streamStateJson = JObject.Parse(streamStateStr);

                if (streamStateJson.Value<JArray>("data").Count == 0)
                    return false;
                else
                    return true;
            }
        }

        public void TimerHandler(object sender, ElapsedEventArgs e)
        {
            if (!GetStreamState(GetChannelIdByName(channelName)))
            {
                MainWindow.timer.Stop();

                Task task = new Task(() =>
                  {
                      DownloadWrapper();
                  });
                task.Start();
                MainWindow.tasks.Add(task);

                Task.WaitAll(MainWindow.tasks.ToArray());

                ProcessingService processingService = new ProcessingService(StateHandler, TextHandler);

                twitchVideo = GetTwitchVideoFromId(Convert.ToInt32(vodId));

                string concatFile = twitchVideo.RecordedDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "_" + twitchVideo.Title + "_" + twitchVideo.Game + ".ts";
                string outputFile = twitchVideo.RecordedDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "_" + twitchVideo.Title + "_" + twitchVideo.Game + ".mp4";

                foreach (var c in Path.GetInvalidFileNameChars())
                {
                    concatFile = concatFile.Replace(c, '_');
                    outputFile = outputFile.Replace(c, '_');
                }

                TextHandler("Start Merge...");

                processingService.ConcatParts(RetrieveVodPlaylist("temp", playlistUrl), concatFile);

                TextHandler("Start Convert...");
                processingService.ConvertVideo(concatFile, outputFile, twitchVideo.Length);

                FileSystem.DeleteFile(concatFile);
                TextHandler("Done!!");
            }

            if (downloadCount == 15)
            {
                Task task = new Task(() =>
                  {
                      DownloadWrapper();
                  });
                task.Start();
                MainWindow.tasks.Add(task);
                downloadCount = 0;
            }

            downloadCount++;
        }

        public void CheckOutputDirectory(string outputDir)
        {
            if (!Directory.Exists(outputDir))
            {
                Console.WriteLine("Creating output directory...");
                FileSystem.CreateDirectory(outputDir);
                Console.WriteLine("Done!");
            }
        }

        public void DownloadWrapper()
        {
            CheckOutputDirectory("temp");
            VodPlaylist vodPlaylists = RetrieveVodPlaylist("temp", playlistUrl);
            int newCount = vodPlaylists.Count;
            vodPlaylists.RemoveRange(0, vodCount);
            vodCount = newCount;
            DownloadParts(vodPlaylists);
        }
    }
}