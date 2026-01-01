using NSwag.AspNetCore;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using System.Text.Json.Serialization;
using Google.Cloud.Storage.V1;

// Authorization etc 
// https://medium.com/@asadikhan/uploading-csv-files-to-google-cloud-storage-using-c-net-9eaa951eabf2
//  Create Service Account Key


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(config =>
{
    config.DocumentName = "TodoAPI";
    config.Title = "TodoAPI v1";
    config.Version = "v1";
});
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.Configure<TodoSettings>(builder.Configuration.GetSection("TodoSettings"));
builder.Services.AddScoped<TodoService>();

// Don't forget to also register the StorageClient if you use it in the constructor
builder.Services.AddSingleton(StorageClient.Create());

// Register your GCS service here
builder.Services.AddScoped<TodoServiceGcs>();


// Or more simply, tell the serializer to use strings
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();
app.UseCors(); // Must be placed before other middleware

if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi(config =>
    {
        config.DocumentTitle = "TodoAPI";
        config.Path = "/swagger";
        config.DocumentPath = "/swagger/{documentName}/swagger.json";
        config.DocExpansion = "list";
    });
}

// Add this near your other endpoint mappings
app.MapGet("/", () => Results.Redirect("/swagger/index.html"));
app.MapFallback(() => Results.NotFound(new { 
    message = "Endpoint not found. Please refer to the API documentation at /swagger." 
}));

RouteGroupBuilder todoItems = app.MapGroup("/todoitems");

// todoItems.MapGet("/", async (TodoService todoService) =>
// {
//     var todos = await todoService.GetAllTodosAsync();
//     return Results.Ok(todos);
// });

todoItems.MapGet("/", async (string? projectId, TodoService todoService) =>
{
    // Pass the optional ID to your service
    var todos = await todoService.GetAllTodosAsync(projectId);
    return Results.Ok(todos);
});

// To make param optional, define the type as optional or provide a default value:
// todoItems.MapGet("/{id}&{name}", GetTodo);


todoItems.MapPost("/", async (JsonElement todoJson, TodoService todoService) =>
{
    var result = await todoService.CreateTodoAsync(todoJson);

    if (result is TaskError error)
    {
        // Returns a 400 Bad Request with your custom error fields
        return Results.BadRequest(error);
    }

    // Returns 201 Created as seen in your current Swagger output
    return Results.Created($"/todoitems/", result);
})
.Accepts<Todo>("application/json") // This line tells Swagger to show the Todo schema
.Produces<Todo>(201)
.Produces<TaskError>(400);

RouteGroupBuilder gcs = app.MapGroup("/gcs");

// gcloud auth login
// docker build -t todo-api .

// In PowerShell: $env:GOOGLE_APPLICATION_CREDENTIALS="C:\path\to\your\key.json"
// $env:GOOGLE_APPLICATION_CREDENTIALS="D:/dev/code/service_account/dev-task-controller-9699669388c2.json"
gcs.MapGet("/", async (TodoServiceGcs gcsService , string? bucketName= "cary-tasks", string? ProjectId= "todos.2.json") =>
{

    Console.WriteLine("|MapGet|gcs ", bucketName, ProjectId);
    try
    {
        // var files = await todoService.ListGcsFilesAsync(bucketName);
        //var gcsService = new TodoServiceGcs();
        Console.WriteLine("|MapGet|gcs|2| ", bucketName, ProjectId);
        var files = await gcsService.ListGcsFilesAsync(bucketName); // cary-tasks/projects
        //var files = await gcsService.ListFilesInProjectsFolderAsync(); //ListGcsFilesAsync
        //var files = await gcsService.ListGcsFilesAsync(bucketName); //ListGcsFilesAsync); //

        return Results.Ok(files);
    }
    catch (Exception ex)
    {
        Console.WriteLine("|MapGet|gcs|error| ", bucketName, ProjectId, ex);
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("GetGcsFiles")
;

// In PowerShell: $env:GOOGLE_APPLICATION_CREDENTIALS="C:\path\to\your\key.json"
// $env:GOOGLE_APPLICATION_CREDENTIALS="D:/dev/code/service_account/dev-task-controller-9699669388c2.json"
gcs.MapGet("/file-contents", async (TodoServiceGcs gcsService , string? bucketName= "cary-tasks", string? ProjectId= "todos.2.json") =>
{
    Console.WriteLine("|MapGet|gcs|file-contents|", bucketName, ProjectId);
    try
    {
        var content = await gcsService.GetTasksJsonContentAsync(bucketName, ProjectId);
        return Results.Ok(content);

    }
    catch (Exception ex)
    {
        Console.WriteLine("|MapGet|gcs|error| ", bucketName, ProjectId, ex);
        return Results.BadRequest(new { error = ex.Message });
    }
});

// In PowerShell: $env:GOOGLE_APPLICATION_CREDENTIALS="C:\path\to\your\key.json"
// $env:GOOGLE_APPLICATION_CREDENTIALS="D:/dev/code/service_account/dev-task-controller-9699669388c2.json"
gcs.MapPut("/file-contents", async (Todo dataObject, TodoServiceGcs gcsService , string? bucketName= "cary-tasks", string? ProjectId= "todos.2.json") =>
{
    Console.WriteLine("|MapPut|gcs|file-contents|", bucketName, ProjectId);
    try
    {
        await gcsService.SaveJsonObjectToGcsAsync(bucketName, ProjectId, dataObject);
        return Results.Ok();

    }
    catch (Exception ex)
    {
        Console.WriteLine("|MapPut|gcs|error| ", bucketName, ProjectId, ex);
        return Results.BadRequest(new { error = ex.Message });
    }

});


// SaveJsonObjectToGcsAsync

app.Run();


public class TodoSettings { public string FilePath { get; set; } = string.Empty; }

public class TaskError
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}