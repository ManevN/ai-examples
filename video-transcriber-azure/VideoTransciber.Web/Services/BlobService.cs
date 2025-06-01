namespace VideoTransciber.Web.Services
{
    using Azure.Storage.Blobs;

    public class BlobService
    {
        private readonly string connectionString = "<YourAzureBlobConnectionString>";
        private readonly string containerName = "transcriber-audio";

        public async Task<string> UploadAudioAsync(string filePath)
        {
            var client = new BlobContainerClient(connectionString, containerName);
            await client.CreateIfNotExistsAsync();
            var blobClient = client.GetBlobClient(Path.GetFileName(filePath));
            await blobClient.UploadAsync(filePath, overwrite: true);
            return blobClient.Uri.ToString();
        }
    }
}
