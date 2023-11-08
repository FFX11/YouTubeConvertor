using Microsoft.Win32;
using NAudio.Wave;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using System.Diagnostics;
using NReco.VideoConverter;
using AngleSharp.Text;
using NAudio.Lame;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Linq;
using System.IO.Packaging;
using System.Drawing;
using System.Windows.Media;
using System.Collections.Generic;

namespace YouTubeConvertor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private CancellationTokenSource _cancellationTokenSource;
        private IStreamInfo _audioStreamInfo;
        private string _outputFilePath;
        string _tempFolderPath;
        private string _tempName;
        private string _ext;
        private List<string> _links = new();
        public MainWindow()
        {
            InitializeComponent();
            
        }

        private async void ConvertVideo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string inputFilePath = youtubeUrlTextBox.Text;

                _ext = extensionComboBox.Text;

                _tempName = string.Empty;
                videoButton.IsEnabled = false;

                string videoId = GetVideoIdFromUrl(inputFilePath);
                if (string.IsNullOrEmpty(videoId))
                {
                    MessageBox.Show("Invalid YouTube URL.");
                    return;
                }

                statusTextBlock.Text = "";
                // Initialize YouTube client
                var youtube = new YoutubeClient();

                // Get the video info
                var video = await youtube.Videos.GetAsync(videoId);

                // Get the media streams
                var mediaStreamInfos = await youtube.Videos.Streams.GetManifestAsync(videoId);

                string quality = ((ComboBoxItem)qualityComboBox.SelectedItem).Tag.ToString();
                string videoQuality;

                switch (quality)
                {
                    case "high":
                        videoQuality = "high";
                        break;
                    case "medium":
                        videoQuality = "medium";
                        break;
                    case "low":
                        videoQuality = "low";
                        break;
                    default:
                        videoQuality = "high";
                        break;
                }


                // Get the best audio stream
                if (_ext == "MP3")
                {
                    _audioStreamInfo = mediaStreamInfos.GetAudioOnlyStreams().TryGetWithHighestBitrate();
                }
                else
                {
                    
                    if (videoQuality == "high")
                    {
                        _audioStreamInfo = mediaStreamInfos.GetMuxedStreams().Where(x => x.VideoQuality.IsHighDefinition).FirstOrDefault();
                        if (_audioStreamInfo == null)
                        {
                            _audioStreamInfo = mediaStreamInfos.GetMuxedStreams().TryGetWithHighestVideoQuality();
                        }
                    }
                    if (videoQuality == "medium")
                    {
                        _audioStreamInfo = mediaStreamInfos.GetMuxedStreams().Where(x => !x.VideoQuality.IsHighDefinition).FirstOrDefault();
                    }
                }

                // Create a temporary file path
                _tempFolderPath = Path.GetTempPath();
                string tempFileName = Path.GetRandomFileName();
                string tempFilePath = Path.Combine(_tempFolderPath, tempFileName);
                _tempName = video.Title;
                _tempName = Regex.Replace(_tempName, @"[^\p{L}\s]", "");

                _cancellationTokenSource = new CancellationTokenSource();
                // Disable UI elements
                //convertButton.IsEnabled = false;
                videoButton.IsEnabled = false;
                //outputButton.IsEnabled = false;
                cancelButton.IsEnabled = true;
                // Download the audio stream to the temporary file
                

                // Convert the audio file to MP3
                if (_ext == "MP3")
                {
                    await youtube.Videos.Streams.DownloadAsync(_audioStreamInfo, tempFilePath, new Progress<double>(ReportProgress), _cancellationTokenSource.Token);
                    if (_outputFilePath != null && _ext == "MP3")
                    {
                        _tempFolderPath = Path.Combine(_outputFilePath, $@"{_tempName}.{_ext.ToLower()}");
                    }
                    else
                    {
                        _tempFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), $"{_tempName}.{_ext.ToLower()}");
                    }
                    await ConvertToMp3(tempFilePath, _tempFolderPath);
                    
                }
                else if (_ext == "MP4") 
                {
                    if (_outputFilePath == null)
                    {
                        _tempFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), $"{_tempName}.{_ext.ToLower()}");
                    }
                    else
                    {
                        _tempFolderPath = Path.Combine(_outputFilePath, $@"{_tempName}.{_ext.ToLower()}");
                    }
                    await youtube.Videos.Streams.DownloadAsync(_audioStreamInfo, _tempFolderPath, new Progress<double>(ReportProgress), _cancellationTokenSource.Token);
                    
                }
                else if (_ext != "MP4")
                {
                    if (_outputFilePath == null)
                    {
                        _tempFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), $"{_tempName}.{_ext.ToLower()}");
                        _outputFilePath = _tempFolderPath;
                    }
                    else
                    {
                        _tempFolderPath = Path.Combine(_tempFolderPath, $@"{_tempName}.{_ext.ToLower()}");
                    }
                    await youtube.Videos.Streams.DownloadAsync(_audioStreamInfo, tempFilePath, new Progress<double>(ReportProgress), _cancellationTokenSource.Token);
                    await ConvertVideoAsync(tempFilePath, _outputFilePath);
                }

                // Enable output button
                outputButton.IsEnabled = true;

                // Show success message
                statusTextBlock.Text = "Completed!";
                statusTextBlock.Foreground = new SolidColorBrush(Colors.Green);
            }
            catch (OperationCanceledException)
            {
                // Show cancellation message
                statusTextBlock.Text = "canceled.";
                statusTextBlock.Foreground = new SolidColorBrush(Colors.Purple);
            }
            catch (Exception ex)
            {
                // Show error message
                statusTextBlock.Text = $"An error occurred during the conversion: {ex.Message}";
                statusTextBlock.Foreground = new SolidColorBrush(Colors.Red);
            }
            finally
            {
                // Enable UI elements
                videoButton.IsEnabled = true;
                //convertButton.IsEnabled = true;
                cancelButton.IsEnabled = false;
                // Cleanup
                _cancellationTokenSource = null;
                progressBar.Value = 0;
            }
        }

        private async Task ConvertVideoAsync(string inputFilePath, string outputFilePath)
        {
            var ffmpegConverter = new FFMpegConverter();

            

            await Task.Run(() =>
            {
                var input = new FFMpegInput(inputFilePath);

                ffmpegConverter.ConvertMedia(new[] { input }, outputFilePath, Format.mp4, new ConvertSettings
                {
                    CustomOutputArgs = $"-c:v libx264 -crf 23 -preset medium"
                });
            });
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Cancel the conversion
            _cancellationTokenSource?.Cancel();
        }

        private void ReportProgress(double progress)
        {
            // Update the progress bar
            progressBar.IsIndeterminate = false;
            progressBar.Value = progress * 100;
        }

        public async Task ConvertToMp3(string inputFilePath, string outputFilePath)
        {
            int bufferSize = 16384; // Adjust the buffer size as needed

            using (var reader = new MediaFoundationReader(inputFilePath))
            {
                
                using (var writer = new LameMP3FileWriter(outputFilePath, reader.WaveFormat, 128))
                {
                    byte[] buffer = new byte[bufferSize];
                    int bytesRead = 0;
                    long totalBytes = reader.Length;
                    long bytesWritten = 0;

                    do
                    {
                        bytesRead = await reader.ReadAsync(buffer, 0, bufferSize);
                        writer.Write(buffer, 0, bytesRead);
                        bytesWritten += bytesRead;
                        // Calculate progress percentage
                        int progressPercentage = (int)((float)bytesWritten / totalBytes * 100);

                        // Report progress to the UI thread
                        ReportProgress(progressPercentage);
                    } while (bytesRead > 0);

                    writer.Flush();
                }
            }
        }

        private void OutputButton_Click(object sender, RoutedEventArgs e)
        {
            var saveFolderDialog = new System.Windows.Forms.FolderBrowserDialog();

            if (saveFolderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string saveFolderPath = saveFolderDialog.SelectedPath;
                _outputFilePath = saveFolderPath;
            }
        }

        private string GetVideoIdFromUrl(string url)
        {
            string videoId = null;

            // Regular expression pattern to match YouTube video URLs
            string pattern = @"(?<=youtube\.com/shorts/|youtu.be/shorts/|youtube\.com/watch\?v=|youtu.be/|\/v\/|embed\/|youtu.be\/|\/v=|\/embed\/|youtu.be\/|\/shorts\/|youtube.com\/shorts\/)([\w-]+)";
            string patternBit = @"https?:\/\/(?:www)?\.?bitchute\.com\/video\/([a-zA-Z0-9]{1,64})\/";

            Match match = Regex.Match(url, pattern);
            if (match.Success)
            {
                videoId = match.Groups[1].Value;
            }

            return videoId;
        }
    }
}
