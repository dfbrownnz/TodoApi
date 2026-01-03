// private_settings
using Google.Cloud.Storage.V1;
// gs://cary-tasks/1763908755463_server.js 
// dotnet add package Google.Cloud.Storage.V1
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

using TodoProject.Models;
using System.Linq; // Required for LINQ extension methods

public class ProjectListService
{
    private readonly StorageClient _storageClient;

    private const string BucketName = "cary-tasks";
    private const string Prefix = "projects/";

    //private readonly TodoSettings _settings = settings.Value;

    public object? ProjectId { get; private set; }

    public ProjectListService(StorageClient storage)
    {
        _storageClient = storage;
        if (storage == null)
        {
            Console.WriteLine("|ProjectListService|StorageClient could not be initialized. Check your credentials");
            throw new Exception("StorageClient could not be initialized. Check your credentials.");
        }
    }

    /// <summary>
    /// Downloads and returns the raw string content of tasks.json from the GCS bucket.
    /// </summary>
    /// <returns>A string containing the JSON data.</returns>
    public async Task<GcsResponse> GetFileJsonContentAsync(string fileName, string projectlistName)
    {

        // 1. Get object metadata to retrieve the ETag
        var storageObject = await _storageClient.GetObjectAsync(BucketName, fileName);
        string etag = storageObject.ETag;

        using (var memoryStream = new MemoryStream())
        {
            await _storageClient.DownloadObjectAsync(BucketName, fileName, memoryStream);
            memoryStream.Position = 0;
            using (var reader = new StreamReader(memoryStream))
            {
                string content = await reader.ReadToEndAsync();
                return new GcsResponse { Content = content, ETag = etag };
            }
        }
    }

    public async Task SaveJsonObjectToGcsAsync(ProjectList newProjectList , string fileName , string owner   )
    {
        Console.WriteLine("|SaveJsonObjectToGcsAsync|" , fileName , owner , newProjectList);

        List<ProjectList> oldProjectList = new();

        try
        {
            using (var memoryStream = new MemoryStream())
            {
                await _storageClient.DownloadObjectAsync(BucketName, fileName, memoryStream);
                var existingBytes = memoryStream.ToArray();
                oldProjectList = JsonSerializer.Deserialize<List<ProjectList>>(existingBytes) ?? new();
            }
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // If the file doesn't exist yet, start with an empty dictionary
            oldProjectList = new();
        }

        // Assuming your array is named oldProjectList

        int index =-1;

        // Using Method Syntax (Succinct and common)
        var filteredList = oldProjectList.Where(p => string.Equals(p.Owner, owner , StringComparison.OrdinalIgnoreCase)).ToList();

        if(filteredList.Count() > 0)
        {
        index = filteredList.FindIndex(t => t.Name == newProjectList.Name);
        }
        else
          {
        index = oldProjectList.FindIndex(t => t.Name == newProjectList.Name);
        }



        if (index != -1)
        {
            oldProjectList[index] = newProjectList;
        }
        else
        {
            oldProjectList.Add(newProjectList);
        }


        // 3. Save the merged object back to GCS
        var options = new JsonSerializerOptions { WriteIndented = true };
        byte[] finalBytes = JsonSerializer.SerializeToUtf8Bytes(oldProjectList, options);

        using (var uploadStream = new MemoryStream(finalBytes))
        {
            await _storageClient.UploadObjectAsync(BucketName, fileName, "application/json", uploadStream);
        }

    }
}
