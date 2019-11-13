using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1
{
    public class ProcessingService
    {
        #region Constants

        private const string FFMPEG_EXE_X86 = "ffmpeg_x86.exe";
        private const string FFMPEG_EXE_X64 = "ffmpeg_x64.exe";

        #endregion Constants

        private Action<string> StateHandler = null;
        private Action<string> TextHandler = null;

        #region Constructors

        public ProcessingService(Action<string> stateAction, Action<string> textAction)
        {
            string appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            FFMPEGExe = Path.Combine(appDir, Environment.Is64BitOperatingSystem ? FFMPEG_EXE_X64 : FFMPEG_EXE_X86);
            StateHandler = stateAction;
            TextHandler = textAction;
        }

        #endregion Constructors

        #region Properties

        public string FFMPEGExe { get; }

        #endregion Properties

        #region Methods

        public void ConcatParts(VodPlaylist vodPlaylist, string concatFile)
        {
            //setStatus("Merging files");
            //setProgress(0);

            //log(Environment.NewLine + Environment.NewLine + "Merging all VOD parts into '" + concatFile + "'...");

            using (FileStream outputStream = new FileStream(concatFile, FileMode.OpenOrCreate, FileAccess.Write))
            {
                int partsCount = vodPlaylist.Count;

                for (int i = 0; i < partsCount; i++)
                {
                    VodPlaylistPart part = vodPlaylist[i];

                    using (FileStream partStream = new FileStream(part.LocalFile, FileMode.Open, FileAccess.Read))
                    {
                        int maxBytes;
                        byte[] buffer = new byte[4096];

                        while ((maxBytes = partStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            outputStream.Write(buffer, 0, maxBytes);
                        }
                    }

                    FileSystem.DeleteFile(part.LocalFile);

                    StateHandler(string.Format("Merging... {0}/{1} {2} %", i + 1, partsCount, Math.Round(((i + 1) / ((float)partsCount)) * 100).ToString()));
                }

                StateHandler("Merging... 100 %");
            }
        }

        public void ConvertVideo(string sourceFile, string outputFile, TimeSpan duration)
        {
            //CheckOutputDirectory(Path.GetDirectoryName(outputFile));

            TextHandler("Executing '" + FFMPEGExe + "' on '" + sourceFile + "'...");
            //log(Environment.NewLine + Environment.NewLine + "Executing '" + FFMPEGExe + "' on '" + sourceFile + "'...");

            ProcessStartInfo psi = new ProcessStartInfo(FFMPEGExe)
            {
                Arguments = "-y -i \"" + sourceFile + "\" -analyzeduration " + int.MaxValue + " -probesize " + int.MaxValue + " -c:v copy -c:a copy -bsf:a aac_adtstoasc \"" + outputFile + "\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                StandardErrorEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            //log(Environment.NewLine + "Command line arguments: " + psi.Arguments + Environment.NewLine);

            using (Process p = new Process())
            {
                DataReceivedEventHandler outputDataReceived = new DataReceivedEventHandler((s, e) =>
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            //TextHandler(e.Data);

                            string dataTrimmed = e.Data.Trim();

                            if (dataTrimmed.StartsWith("frame", StringComparison.OrdinalIgnoreCase))
                            {
                                string timeStr = dataTrimmed.Substring(dataTrimmed.IndexOf("time") + 4).Trim();
                                timeStr = timeStr.Substring(timeStr.IndexOf("=") + 1).Trim();
                                timeStr = timeStr.Substring(0, timeStr.IndexOf(" ")).Trim();

                                if (TimeSpan.TryParse(timeStr, out TimeSpan current))
                                {
                                    StateHandler(Math.Round(current.TotalMilliseconds / duration.TotalMilliseconds * 100).ToString() + " %");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        //log(Environment.NewLine + "An error occured while reading '" + FFMPEGExe + "' output stream!" + Environment.NewLine + Environment.NewLine + ex.ToString());
                    }
                });

                p.OutputDataReceived += outputDataReceived;
                p.ErrorDataReceived += outputDataReceived;
                p.StartInfo = psi;
                p.Start();
                p.BeginErrorReadLine();
                p.BeginOutputReadLine();
                p.WaitForExit();

                if (p.ExitCode == 0)
                {
                    //log(Environment.NewLine + "Video conversion complete!");
                }
                else
                {
                    throw new ApplicationException("An error occured while converting the video!");
                }
            }
        }

        private void CheckOutputDirectory(string outputDir)
        {
            if (!Directory.Exists(outputDir))
            {
                //log(Environment.NewLine + Environment.NewLine + "Creating output directory '" + outputDir + "'...");
                FileSystem.CreateDirectory(outputDir);
                //log(" done!");
            }
        }

        #endregion Methods
    }
}
