using NSwag.AspNetCore;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using System.Text.Json.Serialization;
using Google.Cloud.Storage.V1;

// Authorization etc 
// https://medium.com/@asadikhan/uploading-csv-files-to-google-cloud-storage-using-c-net-9eaa951eabf2
//  Create Service Account Key

using TodoProject.Models;
// curl -i -H "Origin: http://localhost:4200" "http://localhost:5173/gcs/file-contents?bucketName=cary-tasks&ProjectId=todos.2.json"

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
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("ETag"); // This is the critical line
    });
});

builder.Services.Configure<TodoSettings>(builder.Configuration.GetSection("TodoSettings"));
builder.Services.AddScoped<TodoService>();
// Don't forget to also register the StorageClient if you use it in the constructor
builder.Services.AddSingleton(StorageClient.Create());
// Register your GCS service here
builder.Services.AddScoped<TodoServiceGcs>();
// Example: register your service
builder.Services.AddScoped<ProjectListService>();


// Or more simply, tell the serializer to use strings
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();
app.UseCors(); // Must be placed before other middleware

// if (app.Environment.IsDevelopment())
// {
//     app.UseOpenApi();
//     app.UseSwaggerUi(config =>
//     {
//         config.DocumentTitle = "TodoAPI";
//         config.Path = "/swagger";
//         config.DocumentPath = "/swagger/{documentName}/swagger.json";
//         config.DocExpansion = "list";
//     });
// }

app.UseOpenApi();
app.UseSwaggerUi(config =>
{
    config.DocumentTitle = "TodoAPI";
    config.Path = "/swagger";
    config.DocumentPath = "/swagger/{documentName}/swagger.json";
    config.DocExpansion = "list";
});

// Add this near your other endpoint mappings
app.MapGet("/", () => Results.Redirect("/swagger/index.html"));
app.MapFallback(() => Results.NotFound(new
{
    message = "Endpoint not found. Please refer to the API documentation at /swagger."
}));

RouteGroupBuilder todoItems = app.MapGroup("/todoitems");

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

gcs.MapGet("/", async (TodoServiceGcs gcsService, string? bucketName = "cary-tasks", string? ProjectId = "todos.2.json") =>
{

    Console.WriteLine("|MapGet|gcs ", bucketName, ProjectId);
    try
    {
        // Console.WriteLine("|MapGet|gcs|2| ", bucketName, ProjectId);
        var files = await gcsService.ListGcsFilesAsync(bucketName); // cary-tasks/projects
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

gcs.MapGet("/file-contents", async (TodoServiceGcs gcsService, HttpContext context, string? bucketName = "cary-tasks", string? ProjectId = "todos.2.json") =>
{
    Console.WriteLine("|MapGet|gcs|file-contents|", bucketName, ProjectId);
    try
    {
        var gcsResponse = await gcsService.GetTasksJsonContentAsync(bucketName, ProjectId);

        // 1. Add the ETag to the response headers
        if (!string.IsNullOrEmpty(gcsResponse.ETag))
        {
            context.Response.Headers.ETag = gcsResponse.ETag;
        }
        return Results.Content(gcsResponse.Content, "application/json");

    }
    catch (Exception ex)
    {
        Console.WriteLine("|MapGet|gcs|error| ", bucketName, ProjectId, ex);
        return Results.BadRequest(new { error = ex.Message });
    }
});

gcs.MapPut("/file-contents", async (Todo dataObject, TodoServiceGcs gcsService, string? bucketName = "cary-tasks", string? ProjectId = "todos.2.json") =>
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

RouteGroupBuilder projectlist = app.MapGroup("/projectlist");

projectlist.MapGet("/", async (ProjectListService todoService, HttpContext context, string? projectlistName) =>
{
    // Pass the optional ID to your service
    // var todos = await todoService.GetFileJsonContentAsync( "project_list.json" , projectlistName );
    // return Results.Ok(todos);
    try
    {
        var plResponse = await todoService.GetFileJsonContentAsync("project_list.json", projectlistName);

        // 1. Add the ETag to the response headers
        if (!string.IsNullOrEmpty(plResponse.ETag))
        {
            context.Response.Headers.ETag = plResponse.ETag;
        }
        return Results.Content(plResponse.Content, "application/json");

    }
    catch (Exception ex)
    {
        Console.WriteLine("|MapGet|gcs|error| ", "project_list.json", projectlistName, ex);
        return Results.BadRequest(new { error = ex.Message });
    }

});


projectlist.MapPut("/", async (ProjectList dataObject, ProjectListService todoService,  string? Owner = "NoOwner") =>
{
    Console.WriteLine("|MapPut|gcs|file-contents|" );
    try
    {
        await todoService.SaveJsonObjectToGcsAsync( dataObject , "project_list.json" , Owner );
        return Results.Ok();

    }
    catch (Exception ex)
    {
        Console.WriteLine("|MapPut|gcs|error| ",  ex);
        return Results.BadRequest(new { error = ex.Message });
    }

});

app.Run();


