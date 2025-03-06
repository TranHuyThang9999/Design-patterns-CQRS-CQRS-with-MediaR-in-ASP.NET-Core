using System.ComponentModel.DataAnnotations;

namespace WebApplicationCQRS.Application.DTOs;

public class CreateTicketDtoRequest
{
    [Required]
    public string Name { get; set; }
    public string FileDescription {set;get;}
    
    public string Description { get; set; }

}

public class TicketDtoResponse
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string FileDescription { get; set; }
    
    public string Description { get; set; }

}