namespace VideoTransciber.Web.Services
{
    using Newtonsoft.Json.Linq;

    public class SpeechService
    {
        private readonly string subscriptionKey = "<YourAzureSpeechKey>";
        private readonly string region = "<YourAzureRegion>";

        public async Task<string> TranscribeAsync(string audioUrl)
        {
            string endpoint = $"https://{region}.api.cognitive.microsoft.com/speechtotext/v3.0/transcriptions";
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

            var payload = new
            {
                contentUrls = new[] { audioUrl },
                locale = "en-US",
                displayName = "VideoTranscription"
            };

            var response = await client.PostAsJsonAsync(endpoint, payload);
            var result = await response.Content.ReadAsStringAsync();
            var transcriptionUrl = JObject.Parse(result)["self"]?.ToString();

            // Polling
            string status = "";
            string transcriptUrl = "";
            while (status != "Succeeded")
            {
                await Task.Delay(5000);
                var check = await client.GetAsync(transcriptionUrl);
                var checkJson = JObject.Parse(await check.Content.ReadAsStringAsync());
                status = checkJson["status"]?.ToString();
                if (status == "Failed") return "Transcription failed.";
                transcriptUrl = checkJson["results"]["urls"]["transcriptionFiles"]?.ToString();
            }

            var transcriptResult = await client.GetAsync(transcriptUrl);
            var files = JObject.Parse(await transcriptResult.Content.ReadAsStringAsync());
            var fileUrl = files["values"]?[0]?["links"]?["contentUrl"]?.ToString();
            var textContent = await client.GetStringAsync(fileUrl);
            return textContent;
        }
    }
}
