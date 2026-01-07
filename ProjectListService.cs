// private_settings
using Google.Cloud.Storage.V1;
// gs://cary-tasks/1763908755463_server.js 
// dotnet add package Google.Cloud.Storage.V1
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

using TodoProject.Models;
using System.Linq;
using Newtonsoft.Json;
using System.Data; // Required for LINQ extension methods

public class ProjectListService
{
    private readonly StorageClient _storageClient;

    private const string BucketName = "cary-tasks";
    private const string Prefix = "projects/";
    private const string fileNameProjectList = "project_list.json";


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



    private IEnumerable<TodoSummary> getTodosFromProjectListSummary(List<Todo> todosForProject)
    {
        var SummaryTodos = new List<Todo>();
        var todoGroups = new[] { "Approval", "Configuration", "Testing", "Production", "Validation" };
        var statusFlag = new[] { "Complete", "Not Started", "In Progress", "Issue", "Expected" };
        var todosForProjectSorted = todosForProject.OrderBy(t => t.StatusFlag).OrderBy(t => t.StatusDate).ToList();
        var Projectidsum = ""; // todosInGroup[0].ProjectId ;
        var TodoSummary = new TodoSummary { ProjectId = "", statusDate = "", Approval = "", Configuration = "", Testing = "", Production = "", Validation = "" };

        // Logic to find the maximum value
        var maxDate = todosForProjectSorted
            .Select(t => t.StatusDate)
            .Max() ?? "19990101";

        foreach (var group in todoGroups)
        {
            // Console.WriteLine($" todo {group} ");
            var todosInGroup = todosForProjectSorted.Where(t => string.Equals(t.Group, group, StringComparison.OrdinalIgnoreCase)).ToList();
            var StatusFlagMax = "Not Started";
            foreach (var tmpTodo in todosInGroup)
            {
                StatusFlagMax = GetMaxStatusFlag(StatusFlagMax, tmpTodo.StatusFlag);
                Projectidsum = tmpTodo.ProjectId;
                // Console.WriteLine($" {tmpTodo.ProjectId} and {tmpTodo.Id} and {tmpTodo.StatusFlag} ");
            }
            var myTodo = new Todo { ProjectId = Projectidsum, Id = "0", Description = "Summary", Name = "Summary", Group = group, StatusFlag = StatusFlagMax, StatusDate = DateTime.UtcNow.ToString("yyyyMMdd"), Owner = "Summary" };

            // Console.WriteLine($" {myTodo.ProjectId} and {myTodo.Group} and {myTodo.StatusFlag} ");
            TodoSummary.ProjectId = myTodo.ProjectId;
            TodoSummary.statusDate = maxDate;
            if (myTodo.Group == "Approval")
            {
                TodoSummary.Approval = myTodo.StatusFlag;

            }
            if (myTodo.Group == "Configuration")
            {
                TodoSummary.Configuration = myTodo.StatusFlag;

            }
            if (myTodo.Group == "Testing")
            {
                TodoSummary.Testing = myTodo.StatusFlag;

            }
            if (myTodo.Group == "Production")
            {
                TodoSummary.Production = myTodo.StatusFlag;

            }
            if (myTodo.Group == "Validation")
            {
                TodoSummary.Validation = myTodo.StatusFlag;

            }
            // GetMaxStatusFlag
            SummaryTodos.Add(myTodo);
        }
        // Console.WriteLine($" {TodoSummary.ProjectId}  and {TodoSummary.Approval} -> {TodoSummary} ");
        // throw new NotImplementedException();
        //return SummaryTodos;
        return [TodoSummary];
    }

    /// <summary>
    /// Returns the maximum StatusFlag based on a predefined order.
    /// </summary>
    /// <param name="todo">The Todo item to evaluate.</param>
    /// <returns>The highest priority StatusFlag for the given Todo.</returns>
    private string GetMaxStatusFlag(string StatusFlagPrior, string StatusFlagCurrent)
    {
        var statusOrder = new List<string> { "Issue", "Not Started", "In Progress", "Expected", "Complete" };
        var StatusFlagResult = "Not Started";

        if (StatusFlagPrior == "Issue" || StatusFlagCurrent == "Issue")
        {
            return "Issue";
        }

        if (StatusFlagPrior == "In Progress" || StatusFlagCurrent == "In Progress")
        {

            return "In Progress";
        }

        if (StatusFlagPrior == "Complete" || StatusFlagCurrent == "Complete")
        {
            return "Complete";
        }

        if (StatusFlagPrior == "Not Started" || StatusFlagCurrent == "Not Started")
        {
            return "Not Started";
        }

        // if (StatusFlagPrior == "In Progress" && StatusFlagCurrent == "Complete")
        // {
        //     return StatusFlagPrior;
        // }


        return StatusFlagResult;
    }

    // for a given project list return the todos summarized via getTodosFromProjectListSummary
    public async Task<TodoSummary[]> getTodosFromProjectList(ProjectList ProjectList)
    {

        var projectListRecord = await getProjectListRecord(ProjectList);

        if (projectListRecord == null)
        {
            Console.WriteLine($" ProjectList|getProjectListRecord|record not found {fileNameProjectList} in {BucketName}");
            return new TodoSummary[0]; // record does not exist
        }

        var allTodos = new List<TodoSummary>();

        // Split the 'Values' string by comma to get individual project IDs/filenames
        var projectFiles = projectListRecord.Values.Split(',', StringSplitOptions.RemoveEmptyEntries);
        // Console.WriteLine($" --------- ");
        // Console.WriteLine($"getTodosFromProjectList|ProjectList|get {projectListRecord.Values} from ProjectList={ProjectList.Name} Owner={ProjectList.Owner}");



        foreach (var projectFile in projectFiles)
        {
            var trimmedProjectFile = "todos." + projectFile.Trim() + ".json";
            Console.WriteLine($"|ProjectList|getProjectIdsfromProjectList|1| {trimmedProjectFile} ");

            try
            {
                var gcsResponse = await GetFileJsonContentAsync(trimmedProjectFile, projectListRecord.Name);
                var todosForProject = System.Text.Json.JsonSerializer.Deserialize<List<Todo>>(gcsResponse.Content);
                if (todosForProject != null)
                {
                    //allTodos.AddRange(todosForProject);
                    //allTodos.AddRange( getTodosFromProjectListSummary(todosForProject) );
                    allTodos.AddRange(getTodosFromProjectListSummary(todosForProject));
                    Console.WriteLine($"|ProjectList|getProjectIdsfromProjectList|2| {todosForProject.Count()} ");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"|ProjectList|getProjectIdsfromProjectList|Error reading file {trimmedProjectFile}: {ex.Message}");
            }
        }

        return allTodos.ToArray();
    }


    /// <summary>
    /// Downloads and returns the raw string content of tasks.json from the GCS bucket.
    /// </summary>
    /// <returns>A string containing the JSON data.</returns>
    public async Task<ProjectList?> getProjectListRecord(ProjectList projectListToFind)
    {

        if (await FileExistsAsync(BucketName, fileNameProjectList) == false)
        {
            Console.WriteLine($" ProjectList|getProjectListRecord|file not found {fileNameProjectList} in {BucketName}");
            return null; // File does not exist
        }
        //  Console.WriteLine($" ProjectList|getProjectListRecord|file  found {fileNameProjectList} in {BucketName}");

        using (var memoryStream = new MemoryStream())
        {
            await _storageClient.DownloadObjectAsync(BucketName, fileNameProjectList, memoryStream);
            memoryStream.Position = 0;
            using (var reader = new StreamReader(memoryStream))
            {
                string jsonContent = await reader.ReadToEndAsync();
                var allProjectLists = System.Text.Json.JsonSerializer.Deserialize<List<ProjectList>>(jsonContent);

                if (allProjectLists == null)
                {
                    Console.WriteLine($" ProjectList|getProjectListRecord|file no content {fileNameProjectList} in {BucketName}");
                    return null; // No project lists found or deserialization failed
                }

                //Console.WriteLine($" ProjectList|getProjectListRecord|  Name=|{projectListToFind.Name}| Owner=|{projectListToFind.Owner}|");

                // Find the matching ProjectList by Name and Type
                return allProjectLists.FirstOrDefault(pl =>
                    string.Equals(pl.Name, projectListToFind.Name, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(pl.Owner, projectListToFind.Owner, StringComparison.OrdinalIgnoreCase)
                );
            }
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
    // 1. if project-owner == Owner check for an existing task list for that project if not found copy the template 
    // 2. then add the new project to the list
    public async Task SaveJsonObjectToGcsAsyncNewProject(ProjectList newProjectList, string fileName, string owner)
    {

        var fileNameProjectTasks = $"todos.{newProjectList.Values}.json";
        if (await FileExistsAsync(BucketName, fileNameProjectTasks) == false)
        {
            // await _storageClient.CopyObjectAsync(BucketName, "todos.template.json", BucketName, fileNameProjectTasks);
            Console.WriteLine($"|SaveJsonObjectToGcsAsyncNewProject:2| file DOES NOT exists for {fileNameProjectTasks} {newProjectList.Name} owner: {owner} .");
            await SaveNewTaskFileforNewProjectId(fileNameProjectTasks , newProjectList.Values );


        }
        else
        {

            Console.WriteLine($"|SaveJsonObjectToGcsAsyncNewProject:3| file exists for {fileNameProjectTasks} {newProjectList.Name} owner: {owner} so it wasn't copied from a template");
            return;
        }
    }


    public async Task SaveNewTaskFileforNewProjectId(string fileNameProjectTasks , string ProjectId)
    {

        Console.WriteLine($"|SaveNewTaskFileforNewProjectId:2| copy tempate to {fileNameProjectTasks}");
        try
        {

            // 1. Download the template
            var memoryStream = new MemoryStream();
            await _storageClient.DownloadObjectAsync(BucketName, "todos.template.json", memoryStream);

            // 2. Read and Modify
            memoryStream.Position = 0;
            using (var reader = new StreamReader(memoryStream))
            {
                string content = await reader.ReadToEndAsync();

                // Deserializing to a dynamic object or your specific Todo list model
                // var data = JsonConvert.DeserializeObject<dynamic>(content);
                var TodoList = System.Text.Json.JsonSerializer.Deserialize<List<Todo>>(content) ?? new();
                // oldProjectList = System.Text.Json.JsonSerializer.Deserialize<List<ProjectList>>(existingBytes) ?? new();

                // Update the project id for each todo in the array
                foreach (var todo in TodoList)
                {
                    todo.ProjectId = ProjectId;

                }

                // Prepare modified content for upload
                string updatedContent = JsonConvert.SerializeObject(TodoList, Formatting.Indented);
                byte[] byteArray = Encoding.UTF8.GetBytes(updatedContent);

                // 3. Save as the new file: fileNameProjectTasks
                using (var uploadStream = new MemoryStream(byteArray))
                {
                    await _storageClient.UploadObjectAsync(
                        BucketName,
                        fileNameProjectTasks,
                        "application/json",
                        uploadStream
                    );
                }
            }
        }
        catch (Exception ex)
        {
            // General errors (network, null references, etc.)
            Console.WriteLine($"General Error in SaveNewTaskFileforNewProjectId: {ex.Message}");

        }

    }

    // project-name 
    public async Task SaveJsonObjectToGcsAsync(ProjectList newProjectList, string fileName, string owner)
    {
        Console.WriteLine($"|SaveJsonObjectToGcsAsync| fileName: {fileName} owner: {newProjectList.Owner} newProjectList: {newProjectList.Name}");

        List<ProjectList> oldProjectList = new();

        try
        {
            using (var memoryStream = new MemoryStream())
            {
                await _storageClient.DownloadObjectAsync(BucketName, fileName, memoryStream);
                var existingBytes = memoryStream.ToArray();
                oldProjectList = System.Text.Json.JsonSerializer.Deserialize<List<ProjectList>>(existingBytes) ?? new();
            }
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // If the file doesn't exist yet, start with an empty dictionary
            oldProjectList = new();
        }

        // Assuming your array is named oldProjectList

        int index = -1;

        // Owner = project-name or project-owner or Ted Dave etc 
        var filteredList = oldProjectList.Where(p => string.Equals(p.Owner, newProjectList.Owner, StringComparison.OrdinalIgnoreCase)).ToList();

        if (filteredList.Count() > 0)
        {
            index = filteredList.FindIndex(t => t.Name == newProjectList.Name);
        }
        else
        {
            // List Owner not found so use original list to find the name
            index = oldProjectList.FindIndex(t => t.Name == newProjectList.Name);
        }



        if (index != -1)
        {
            Console.WriteLine($"|SaveJsonObjectToGcsAsync| owner: {newProjectList.Owner} found so UDATE {newProjectList.Name} ");
            oldProjectList[index] = newProjectList;
        }
        else
        {
            Console.WriteLine($"|SaveJsonObjectToGcsAsync| owner: {newProjectList.Owner} not found so ADD {newProjectList.Name} ");

            // project-name is new then -> copying the todos template into a new file for this project
            if (newProjectList.Owner == "project-name")
            {
                await SaveJsonObjectToGcsAsyncNewProject(newProjectList, fileName, newProjectList.Owner);
            }
            oldProjectList.Add(newProjectList);
        }


        // 3. Save the merged object back to GCS
        var options = new JsonSerializerOptions { WriteIndented = true };
        byte[] finalBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(oldProjectList, options);

        using (var uploadStream = new MemoryStream(finalBytes))
        {
            await _storageClient.UploadObjectAsync(BucketName, fileName, "application/json", uploadStream);
        }

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
}
