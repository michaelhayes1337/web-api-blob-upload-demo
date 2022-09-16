using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Mvc;

namespace TestBlob.Controllers
{
    public class FileController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        public FileController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public BlobClient GetBlobClient(Guid id, string fileName)
        {
            // Connection variables
            string connectionString = _configuration.GetValue<string>("BlobConnectionString");
            string containerName = _configuration.GetValue<string>("BlobContainerName");
            string blobName = id.ToString() + fileName;
            // Create Connection
            BlobContainerClient container = new BlobContainerClient(connectionString, containerName);
            BlobClient blob = container.GetBlobClient(blobName);
            return blob;
        }
        public static async Task AddBlobMetadataAsync(BlobClient blob, IDictionary<string, string>? metadataIn)
        {
            try
            {
                IDictionary<string, string> metadata = new Dictionary<string, string>();
                metadata["formstate"] = "IN PROGRESS";
                metadata["datecreated"] = DateTime.Now.ToString();
                if (metadataIn is not null)
                {
                    foreach (var kp in metadataIn)
                    {
                        metadata[kp.Key] = kp.Value;
                    }
                }
                // Set the blob's metadata.
                await blob.SetMetadataAsync(metadata);
            }
            catch (RequestFailedException e)
            {
                Console.WriteLine(e.Message);
            }
        }

        [HttpPost(nameof(Upload))]
        public async Task<IActionResult> Upload(IFormFile files, Guid id)
        {
            try
            {
                BlobClient blob = GetBlobClient(id, files.FileName);
                await using (var data = files.OpenReadStream())
                {
                    await blob.UploadAsync(data);
                }
                IDictionary<string, string> metadata = new Dictionary<string, string>();
                metadata["guid"] = id.ToString();
                metadata["filename"] = files.FileName;
                metadata["contenttype"] = files.ContentType;
                await AddBlobMetadataAsync(blob, metadata);
                return Ok(files.FileName);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error uploading: " + e.Message);
                return BadRequest();
            }
        }

        [HttpPost(nameof(Download))]
        public async Task<IActionResult> Download(string fileName, Guid id)
        {
            try
            {
                BlobClient blob;
                await using (MemoryStream memoryStream = new())
                {
                    blob = GetBlobClient(id, fileName);
                    await blob.DownloadToAsync(memoryStream);
                }
                Stream blobStream = blob.OpenReadAsync().Result;
                BlobProperties properties = await blob.GetPropertiesAsync();
                return File(blobStream, properties.ContentType, fileName);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error downloading: " + e.Message);
                return BadRequest();
            }
        }

        [HttpDelete(nameof(Delete))]
        public async Task<IActionResult> Delete(string fileName, Guid id)
        {
            try
            {
                BlobClient blob = GetBlobClient(id, fileName);
                await blob.DeleteAsync();
                return Ok(fileName);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error deleting: " + e.Message);
                return BadRequest();
            }
        }
    }
}
