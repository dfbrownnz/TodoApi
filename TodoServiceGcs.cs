// private_settings
using Google.Cloud.Storage.V1;
// gs://cary-tasks/1763908755463_server.js 
// dotnet add package Google.Cloud.Storage.V1
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using TodoProject.Models;
public class TodoServiceGcs
{
    private readonly StorageClient _storageClient;

    private const string BucketName = "cary-tasks";
    private const string Prefix = "projects/";

    //private readonly TodoSettings _settings = settings.Value;

    public object? ProjectId { get; private set; }

    public TodoServiceGcs(StorageClient storage)
    {
        _storageClient = storage;
        if (storage == null)
        {
            Console.WriteLine("|ListGcsFilesAsync|StorageClient could not be initialized. Check your credentials");
            throw new Exception("StorageClient could not be initialized. Check your credentials.");
        }
    }


    public async Task<List<string>> ListFilesInProjectsFolderAsync()
    {


        var fileNames = new List<string>();
        await foreach (var storageObject in _storageClient.ListObjectsAsync(BucketName, Prefix))
        {
            fileNames.Add(storageObject.Name);
        }
        return fileNames;
    }

    public async Task<List<GcsFileDto>> ListGcsFilesAsync(string bucketName)
    {

        // Console.WriteLine("|ListGcsFilesAsync|");

        var fileNames = new List<string>();
        var objects = _storageClient.ListObjectsAsync(bucketName);

        var fileList = new List<GcsFileDto>();
        await foreach (var obj in objects)
        {
            // if (obj.ContentType != "application/json")  continue;
            // {
            //     //Console.WriteLine($"|ListGcsFilesAsync|StorageClient {obj.ContentType}");
            //     continue;
            // }
            //     ;

            fileList.Add(new GcsFileDto
            {
                Name = obj.Name,
                Size = (long?)obj.Size,  // Size is returned in bytes
                ContentType = obj.ContentType,
                Etag = obj.ETag
            });
        }

        return fileList;
    }

public async Task<bool> FileExistsAsync(string bucketName, string fileName)
{
    try
    {
        // GetObjectAsync throws a Google.GoogleApiException if the file is not found
        await _storageClient.GetObjectAsync(bucketName, fileName);
        return true;
    }
    catch (Google.GoogleApiException e) when (e.Error.Code == 404)
    {
        // 404 means the file does not exist
        return false;
    }
}

    /// <summary>
    /// Downloads and returns the raw string content of tasks.json from the GCS bucket.
    /// </summary>
    /// <returns>A string containing the JSON data.</returns>
    public async Task<GcsResponse> GetTasksJsonContentAsync(string bucketName, string fileName)
    { 
        Console.WriteLine($"-----------------");
        Console.WriteLine($"|GetTasksJsonContentAsync| fileName={fileName}|");
        Console.WriteLine($"-----------------");

        if(await FileExistsAsync(bucketName, fileName) == false    )
        {
            // return new GcsResponse { Content = JsonSerializer.Serialize(new List<Todo>()), ETag = "0" };
            var defaultTodo = new List<Todo>
            {
                new Todo
                {
                    ProjectId = fileName,
                    Id = "1",
                    Description = $"Cant find the file for file name {fileName} in the bucket {bucketName}. Assign a Project id before coming here",
                    Name = "Initial Task",
                    Group = "Configuration",
                    Owner = "System",
                    StatusFlag = "New",
                    StatusDate = DateTime.UtcNow.ToString("yyyyMMdd")
                }
            };
            return new GcsResponse { Content = JsonSerializer.Serialize(defaultTodo), ETag = "0" };
        }
      
        // 1. Get object metadata to retrieve the ETag
        var storageObject = await _storageClient.GetObjectAsync(bucketName, fileName);
        string etag = storageObject.ETag;

        using (var memoryStream = new MemoryStream())
        {
            await _storageClient.DownloadObjectAsync(bucketName, fileName, memoryStream);
            memoryStream.Position = 0;
            using (var reader = new StreamReader(memoryStream))
            {
                string content = await reader.ReadToEndAsync();
                return new GcsResponse { Content = content, ETag = etag };
            }
            //     // Download the file (e.g., project_list.json) from the cary-tasks bucket
            //     await _storageClient.DownloadObjectAsync(bucketName, fileName, memoryStream);
            //     // Move to the beginning of the stream or convert to array
            //     var bytes = memoryStream.ToArray();
            //    // Deserialize the bytes into an object so ASP.NET serializes it as proper JSON
            //     return JsonSerializer.Deserialize<object>(bytes)!;
        }
    }

    /// <summary>
    /// Serializes an object to JSON and uploads it to the specified GCS bucket.
    /// </summary>
    /// <param name="bucketName">The target GCS bucket (e.g., 'cary-tasks').</param>
    /// <param name="fileName">The name for the file in GCS (e.g., 'tasks.json').</param>
    /// <param name="data">The object to be serialized and saved.</param>
    public async Task SaveJsonObjectToGcsAsyncOverWrite(string bucketName, string fileName, object data)
    {
        // Configure options for pretty-printing, as seen in the documentation
        var options = new JsonSerializerOptions { WriteIndented = true };

        // Serialize the object to a UTF-8 byte array for faster performance
        byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(data, options);

        using (var stream = new MemoryStream(jsonBytes))
        {
            // Upload the stream to GCS with the correct content type
            await _storageClient.UploadObjectAsync(
                bucketName,
                fileName,
                "application/json",
                stream
            );
        }
    }

    public async Task SaveJsonObjectToGcsAsync(string bucketName, string fileName, Todo newTodo)
    {
        List<Todo> oldTodo = new();

        try
        {
            using (var memoryStream = new MemoryStream())
            {
                await _storageClient.DownloadObjectAsync(bucketName, fileName, memoryStream);
                var existingBytes = memoryStream.ToArray();
                oldTodo = JsonSerializer.Deserialize<List<Todo>>(existingBytes) ?? new();
            }
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // If the file doesn't exist yet, start with an empty dictionary
            oldTodo = new();
        }
        // Console.WriteLine( $"|SaveJsonObjectToGcsAsync| index: .. mewTodo {newTodo.Id}" );
        int index = oldTodo.FindIndex(t => t.Id == newTodo.Id);
        // Console.WriteLine( $"|SaveJsonObjectToGcsAsync| index: {index} mewTodo {newTodo.Id}" );

        if (index != -1)
        {
            oldTodo[index] = newTodo;
        }
        else
        {
            //newTodo.Id = Guid.NewGuid().ToString();
            newTodo.Id = (oldTodo.Count + 1).ToString();
            oldTodo.Add(newTodo);
            Console.WriteLine( $"|SaveJsonObjectToGcsAsync| index: .. mewTodo {newTodo.Id} created." );

        }


        // 3. Save the merged object back to GCS
        var options = new JsonSerializerOptions { WriteIndented = true };
        byte[] finalBytes = JsonSerializer.SerializeToUtf8Bytes(oldTodo, options);

        using (var uploadStream = new MemoryStream(finalBytes))
        {
            await _storageClient.UploadObjectAsync(bucketName, fileName, "application/json", uploadStream);
        }

    }
}


public class GcsFileDto
{
    // 
    public string Name { get; set; } = string.Empty;
    public long? Size { get; set; }
    public string ContentType { get; set; }
    public string Etag { get; set; }

    //     Name: The full path and name of the object (e.g., "projects/names.csv").
    // Bucket: The name of the bucket containing the object (e.g., "cary-tasks").
    // Size: The content length of the data in bytes.
    // ContentType: The MIME type of the object (e.g., application/json or text/csv).
    // UpdatedDateTimeOffset: The modification time of the object.
    // Etag: The HTTP entity tag used for cache validation.
    // StorageClass: The storage class of the object (e.g., STANDARD, NEARLINE).
    // MediaLink: The direct link to the object's content.
    // Metadata: A dictionary of custom key-value pairs assigned to the object.

}
