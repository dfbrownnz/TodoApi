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
    [Description("The unique name of the project is used to find a file name with all the task")]
    public required string ProjectId { get; set; }
    [Required]
    [Description("The unique id of the task - leave blank to create a new task. ")]
    public required string Id { get; set; }
    [Required]
    [Description("The full Details for the task ")]
    public required string Description { get; set; }
    [Required]
    [Description("The name of the task - a group name the user can set ")]
    public required string Name { get; set; }
    [Required]
    [Description("The group name - a group name the user cannot set ")]
    public required string Group { get; set; }
    [Required]
    [Description("The group name - a group name the user cannot set ")]
    public required string Owner { get; set; }
    [Required]
    [Description("The task owner  ")]
    public required string StatusFlag { get; set; }
    [Required]
    [Description("Date the task was changed in yyyyMMdd format")]

    public required string StatusDate { get; set; }

    public static implicit operator Todo(List<Todo> v)
    {
        throw new NotImplementedException();
    }

}

public class TodoSummary
{
 

    [Required]
    [Description("Date the task was changed in yyyyMMdd format")]
    public required string ProjectId { get; set; }
    
    [Required]
    [Description("Date the task was changed in yyyyMMdd format")]
    public required string Owner { get; set; }

    [Required]
    [Description("Date the task was changed in yyyyMMdd format")]
    public required string statusDate { get; set; }

    [Required]
    [Description("Date the task was changed in yyyyMMdd format")]
    public required string Approval { get; set; }
    [Required]
    [Description("Date the task was changed in yyyyMMdd format")]
    public required string Configuration { get; set; }

    [Required]
    [Description("Date the task was changed in yyyyMMdd format")]
    public required string Testing { get; set; }

    [Required]
    [Description("Date the task was changed in yyyyMMdd format")]
    public required string Production { get; set; }
    [Required]
    [Description("Date the task was changed in yyyyMMdd format")]
    public required string Validation { get; set; }

}

