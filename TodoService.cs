using Microsoft.Extensions.Options;
using System.Text.Json;

/// <summary>
/// Service responsible for managing Todo items, including persistence to a JSON file and validation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="TodoService"/> class.
/// </remarks>
/// <param name="settings">The configuration settings containing the file path for data storage.</param>
public class TodoService(IOptions<TodoSettings> settings)
{
    private readonly TodoSettings _settings = settings.Value;

    public object? ProjectId { get; private set; }

    /// <summary>
    /// Retrieves all todo items from the configured JSON file.
    /// </summary>
    /// <returns>A list of <see cref="Todo"/> objects. Returns an empty list if the file does not exist.</returns>
    public async Task<List<Todo>> GetAllTodosAsync(string? projectId = null)
    {
        if (!File.Exists(_settings.FilePath))
        {
            return new List<Todo>();
        }
        var json = await File.ReadAllTextAsync(_settings.FilePath);
        var todos = JsonSerializer.Deserialize<List<Todo>>(json) ?? new List<Todo>();

        // If a projectId is provided, filter the list
        if (!string.IsNullOrEmpty(projectId))
        {
            return todos.Where(t => t.ProjectId == projectId).ToList();
        }

        return todos;
    }

    /// <summary>
    /// Validates and saves a new or existing todo item to the JSON file.
    /// </summary>
    /// <param name="todoJson">The raw JSON input representing a todo item.</param>
    /// <returns>
    /// Returns the saved <see cref="Todo"/> object on success, 
    /// or a <see cref="TaskError"/> if validation or deserialization fails.
    /// </returns>
    public async Task<object> CreateTodoAsync(JsonElement todoJson)
    {
        try
        {
            var todo = JsonSerializer.Deserialize<Todo>(todoJson.GetRawText());
            if (todo == null) throw new JsonException("Invalid data format.");

            var validationError = ValidateTodo(todo);
            if (validationError != null) return validationError;

            var todos = await GetAllTodosAsync();
            int index = todos.FindIndex(t => t.Id == todo.Id);

            if (index != -1)
            {
                todos[index] = todo;
            }
            else
            {
                todos.Add(todo);
            }

            var json = JsonSerializer.Serialize(todos, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settings.FilePath, json);

            return todo;
        }
        catch (JsonException ex)
        {
            // {   "ProjectId": "1",  "Id": 3,  "Name": "cleanup",  "Group": "Approval",  "StatusFlag": "Not Started",  "StatusDate": "20250101"}
            return new TaskError
            {
                Name = "Data Type Error",
                Description = $"Required property missing or invalid: {ex.Message}. Field names are case sensitive."
            };
        }
    }

    /// <summary>
    /// Performs internal validation on a Todo object, checking for required fields and valid enum values.
    /// </summary>
    /// <param name="todo">The todo object to validate.</param>
    /// <returns>A <see cref="TaskError"/> if validation fails; otherwise, null.</returns>
    private TaskError? ValidateTodo(Todo todo)
    {
        if (string.IsNullOrWhiteSpace(todo.ProjectId))
            return new TaskError { Name = "Validation Error", Description = "ProjectId is required." };

        if (todo.Id <= 0)
            return new TaskError { Name = "Validation Error", Description = "A valid ID is required." };

        // if (!Enum.IsDefined(typeof(TodoGroup), todo.Group))
        //     return new TaskError { Name = "Validation Error", Description = "Invalid Group. Must be 'Configuration' or 'Testing'." };

        return null;
    }
}