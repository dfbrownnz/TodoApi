using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

public enum TodoGroup
{
    Approval,
    Configuration,
    Testing
    ,
    Production
    ,
    Validation
}
public class Todo
{
    [Required]
    [Description("The unique name of the project is used to find a file name")]
    public required string ProjectId { get; set; }
    [Required]
    [Description("The unique id of the task ")]
    public required int Id { get; set; }
    [Required]
    [Description("The unique name of the task ")]
    public required string Name { get; set; }
    [Required]
    [Description("The unique name of the task group Configuration Testing etc ")]
    public required string Group { get; set; }

    [Required]
    [Description("The Status of Complete Not Started etc  ")]
    public required string StatusFlag { get; set; }
    [Required]
    [Description("Date the task was changes in yyyyMMdd format")]

    public required string StatusDate { get; set; }

    public static implicit operator Todo(List<Todo> v)
    {
        throw new NotImplementedException();
    }
}
