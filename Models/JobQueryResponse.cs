namespace EazeTechnical.Models;

public class Metadata
{
    public string QueryId { get; set; }
}


public class QueryResponse
{
    public Metadata Metadata { get; set; }
    public List<JobPostingDto> Results { get; set; }
}