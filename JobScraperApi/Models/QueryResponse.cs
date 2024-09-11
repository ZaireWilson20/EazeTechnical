using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EazeTechnical.Models;

public class Metadata
{
    public int QueryId { get; set; }
}


public class QueryResponseDto
{
    public Metadata Metadata { get; set; }
    public List<JobPostingDto> Results { get; set; }

    public string Message { get; set; }
}

public class QueryResponse
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    [Column(TypeName = "jsonb")]
    public string Results { get; set; }

}