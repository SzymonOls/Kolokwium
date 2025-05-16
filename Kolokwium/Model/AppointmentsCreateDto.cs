namespace Kolokwium.Model;

public class AppointmentCreateDto
{
    public int AppointmentId { get; set; }
    public int PatientId { get; set; }
    public string Pwz { get; set; }
    public List<AppointmentServiceDto> Services { get; set; } = new();
}