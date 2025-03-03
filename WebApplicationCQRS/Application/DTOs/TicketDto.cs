using System.ComponentModel.DataAnnotations;

namespace WebApplicationCQRS.Application.DTOs;

public class CreateTicketDtoRequest
{
    [Required]
    public string Name { get; set; }
    public string FileDescription {set;get;}
}