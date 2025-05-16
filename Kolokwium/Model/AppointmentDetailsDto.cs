namespace Kolokwium.Model;

public class AppointmentDetailsDto
{
    public DateTime Date { get; set; }
    public PatientDto Patient { get; set; }
    public DoctorDto Doctor { get; set; }
    public List<AppointmentServiceDto> Services { get; set; }
}