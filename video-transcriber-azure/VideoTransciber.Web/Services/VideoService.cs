namespace VideoTransciber.Web.Services
{
    using System.Diagnostics;

    public class VideoService
    {
        public async Task<string> ExtractAudioAsync(string videoUrl)
        {
            string videoFile = "video.mp4";
            string audioFile = "audio.wav";

            using (var client = new HttpClient())
            {
                var bytes = await client.GetByteArrayAsync(videoUrl);
                await File.WriteAllBytesAsync(videoFile, bytes);
            }

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-i {videoFile} -vn -acodec pcm_s16le -ar 44100 -ac 2 {audioFile} -y",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            await process.WaitForExitAsync();
            return Path.GetFullPath(audioFile);
        }
    }
}
