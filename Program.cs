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

// 1. Define the policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp",
        policy =>
        { // .AllowAnyOrigin()  // Temporary for debugging 
          //.WithOrigins("http://localhost:4200", "https://todoui-947367955954.europe-west1.run.app") // Your frontend URL

            policy.WithOrigins("http://localhost:4200", "https://todoui-947367955954.europe-west1.run.app") // Your frontend URL//.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .WithExposedHeaders("ETag") // This is the critical line
                  .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS") // Explicitly include PUT
                                                                          //.AllowCredentials() // Optional, but often needed for authenticated Cloud Run calls                  
                  .SetIsOriginAllowedToAllowWildcardSubdomains();


        });
});


builder.Services.Configure<TodoSettings>(builder.Configuration.GetSection("TodoSettings"));
builder.Services.AddSingleton(StorageClient.Create());
builder.Services.AddScoped<TodoService>();
builder.Services.AddScoped<TodoServiceGcs>();
builder.Services.AddScoped<ProjectListService>();

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(config =>
{
    config.DocumentName = "TodoAPI";
    config.Title = "TodoAPI v1";
    config.Version = "v1";
});

var app = builder.Build();
app.UseRouting();
app.UseCors("AllowAngularApp"); // Use the policy name defined above
// app.UseAuthorization();
app.MapControllers();

// app.UseMiddleware<RequestLoggingMiddleware>();

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

gcs.MapGet("/", async (TodoServiceGcs todoServiceGcs, HttpContext context, string? bucketName = "cary-tasks", string? ProjectId = "todos.2.json") =>
{

    // Console.WriteLine("|MapGet|gcs ", bucketName, ProjectId);
    try
    {
        // Console.WriteLine("|MapGet|gcs|2| ", bucketName, ProjectId);
        var files = await todoServiceGcs.ListGcsFilesAsync(bucketName); // cary-tasks/projects
      
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

gcs.MapGet("/file-contents", async (TodoServiceGcs todoServiceGcs, HttpContext context, string? bucketName = "cary-tasks", string? ProjectId = "todos.2.json") =>
{
    Console.WriteLine("|MapGet|gcs|file-contents|getTodos|", bucketName, ProjectId);
    try
    {
        var gcsResponse = await todoServiceGcs.GetTasksJsonContentAsync(bucketName, ProjectId);
         

        // Console.WriteLine($"|MapGet|projectlist|plResponse.ETag={plResponse.ETag} ") ;

        if( context.Request.Headers.IfNoneMatch == gcsResponse.ETag)
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

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

gcs.MapPost("/file-contents", async (Todo dataObject, TodoServiceGcs todoServiceGcs, string? bucketName = "cary-tasks", string? ProjectId = "todos.2.json") =>
{
    Console.WriteLine("|MapPost|gcs|file-contents|", bucketName, ProjectId);
    try
    {
        await todoServiceGcs.SaveJsonObjectToGcsAsync(bucketName, ProjectId, dataObject);
        return Results.Ok();

    }
    catch (Exception ex)
    {
        Console.WriteLine("|MapPost|gcs|error| ", bucketName, ProjectId, ex);
        return Results.BadRequest(new { error = ex.Message });
    }

});

RouteGroupBuilder projectlist = app.MapGroup("/projectlist");

projectlist.MapGet("/", async (ProjectListService projectListService, HttpContext context, string? projectlistName) =>
{
    // Pass the optional ID to your service
    // var todos = await todoService.GetFileJsonContentAsync( "project_list.json" , projectlistName );
    // return Results.Ok(todos);
    try
    {
        var plResponse = await projectListService.GetFileJsonContentAsync("project_list.json", projectlistName);

        // Console.WriteLine($"|MapGet|projectlist|plResponse.ETag={plResponse.ETag} ") ;

        if( context.Request.Headers.IfNoneMatch == plResponse.ETag)
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }
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


projectlist.MapPost("/", async (ProjectList dataObject, ProjectListService projectListService, string? Owner = "NoOwner") =>
{
    Console.WriteLine("|MapPut|gcs|file-contents|");
    try
    {
        await projectListService.SaveJsonObjectToGcsAsync(dataObject, "project_list.json", Owner);
        return Results.Ok();

    }
    catch (Exception ex)
    {
        Console.WriteLine("|MapPut|gcs|error| ", ex);
        return Results.BadRequest(new { error = ex.Message });
    }

});


projectlist.MapPost("/all-todos-from-list", async (ProjectList dataObject, ProjectListService projectListService ) =>
{
    // Console.WriteLine($"|MapPost|projectlist|all-todos-from-list| {dataObject.Name}");
    try
    {
        var plResponse =await projectListService.getTodosFromProjectList(dataObject); //.SaveJsonObjectToGcsAsync( dataObject , "project_list.json"  );
        //return Results.Ok();
        return Results.Json(plResponse);

    }
    catch (Exception ex)
    {
        Console.WriteLine($"|MapPost|projectlist|all-todos-from-list|Error|{ex}");
        return Results.BadRequest(new { error = ex.Message });
    }

}); 

app.Run();
