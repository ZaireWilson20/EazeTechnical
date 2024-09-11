

namespace EazeTechnical.Models;

public class JobRequestDto
{
    public string? Query { get; set; }
    public string? Location { get; set; }
    public int? LastNdays { get; set; }
}

public class JobPostingDto
{
    public string? Title { get; set; }
    public string? Company { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public string? Salary { get; set; }
}