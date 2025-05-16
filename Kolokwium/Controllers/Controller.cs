using Kolokwium.Model;

namespace Kolokwium.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;



[ApiController]
[Route("api/appointments")]
public class Controller : ControllerBase
{
    private readonly IConfiguration _configuration;


    public Controller(IConfiguration configuration)
    {
        _configuration = configuration;
    }


    [HttpGet("{id}")]
    public IActionResult GetAppointmentDetails(int id)
    {
        using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
        var checkCmd = new SqlCommand("SELECT 1 FROM Appointment WHERE appoitment_id = @id", connection);
        checkCmd.Parameters.AddWithValue("@id", id);
        connection.Open();
        
        if (checkCmd.ExecuteScalar() == null)
            return NotFound("apoit not found");

        var apointmentCmd = new SqlCommand(
            @"select a.date, p.first_name,p.last_name,  p.date_of_birth, d.doctor_id, d.pwz from Appointment a join Patient p on p.patient_id = a.patient_id join Doctor d on d.doctor_id = a.doctor_id where a.appoitment_id = @id",
            connection);
        apointmentCmd.Parameters.AddWithValue("@id", id);

        AppointmentDetailsDto dto = new();
        using (var reader = apointmentCmd.ExecuteReader())
        {
            if (reader.Read())
            {
                dto.Date = reader.GetDateTime(0);
                dto.Patient = new PatientDto
                {
                    FirstName = reader.GetString(1),
                    LastName = reader.GetString(2),
                    DateOfBirth = reader.GetDateTime(3),
                };
                dto.Doctor = new DoctorDto()
                {
                    DoctorId = reader.GetInt32(4),
                    Pwz = reader.GetString(5)
                };
            }
        }

        var servicesCmd =
            new SqlCommand(
                @"select s.name, aps.service_fee from Appointment_Service aps join Service s on s.service_id = aps.service_id where aps.appoitment_id = @id",
                connection);
        servicesCmd.Parameters.AddWithValue("@id", id);
        
        dto.Services = new List<AppointmentServiceDto>();
        using (var reader = servicesCmd.ExecuteReader())
        {
            while (reader.Read())
            {
                dto.Services.Add(new AppointmentServiceDto
                {
                    Name = reader.GetString(0),
                    ServiceFee = reader.GetDecimal(1)
                });
            }
        }
        return Ok(dto);
    }



    [HttpPost]
    public IActionResult CreateAppointment([FromBody] AppointmentCreateDto dto)
    {
        if (dto == null  || dto.Services.Count == 0)
           return BadRequest("Invalid appointment data");
        
        using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
        connection.Open();
        
        using var transaction = connection.BeginTransaction();

        try
        {
            // czy wizyta istnieje
            var checkAppointmentCmd = new SqlCommand("select 1 from Appointment where appoitment_id = @id", connection, transaction);
            checkAppointmentCmd.Parameters.AddWithValue("@id", dto.AppointmentId);
            if (checkAppointmentCmd.ExecuteScalar() == null)
                return Conflict("appointment with  id  exists");
            
            // czy pacjent istnieje
            var checkPatientCmd = new SqlCommand("select 1 from Patient where patient_id = @id", connection, transaction);
            checkPatientCmd.Parameters.AddWithValue("@id", dto.PatientId);
            if (checkAppointmentCmd.ExecuteScalar() == null)
                return NotFound("patient not found");
            
            
            // czy lekarz istnieje
            var getDoctorIdCmd = new SqlCommand("select doctor_id from Doctor where pwz = @pwz",connection,transaction);
            getDoctorIdCmd.Parameters.AddWithValue("@pwz", dto.Pwz);
            var doctorIdObj = getDoctorIdCmd.ExecuteScalar();
            if (doctorIdObj == null)
                return NotFound("doctor not found");
            int doctorId = (int)(doctorIdObj);
            
            // dodaj wizyte 
            var insertAppointmentCmd = new SqlCommand(@"insert into Appointment (appointment_id, patient_id, doctor_id, date) values (@id, @patient_id, @doctor_id, @date)", connection, transaction);
            insertAppointmentCmd.Parameters.AddWithValue("@id", dto.AppointmentId);
            insertAppointmentCmd.Parameters.AddWithValue("@patient_id", dto.PatientId);
            insertAppointmentCmd.Parameters.AddWithValue("@doctor_id", doctorId);
            insertAppointmentCmd.Parameters.AddWithValue("@date", DateTime.Now);
            insertAppointmentCmd.ExecuteNonQuery();

            foreach (var service in dto.Services)
            {
                // Sprawdź czy usługa istnieje
                var getServiceIdCmd = new SqlCommand("SELECT service_id FROM Service WHERE name = @name", connection, transaction);
                getServiceIdCmd.Parameters.AddWithValue("@name", service.Name);
                var serviceIdObj = getServiceIdCmd.ExecuteScalar();
                if (serviceIdObj == null)
                    return NotFound($"Service '{service.Name}' not found.");

                int serviceId = (int)serviceIdObj;

                // Dodaj usługę do Appointment_Service
                var insertServiceCmd = new SqlCommand(@"
                INSERT INTO Appointment_Service (appointment_id, service_id, service_fee)
                VALUES (@appointmentId, @serviceId, @fee)", connection, transaction);
                insertServiceCmd.Parameters.AddWithValue("@appointmentId", dto.AppointmentId);
                insertServiceCmd.Parameters.AddWithValue("@serviceId", serviceId);
                insertServiceCmd.Parameters.AddWithValue("@fee", service.ServiceFee);
                insertServiceCmd.ExecuteNonQuery();
            }
            transaction.Commit();
            return Ok("apointment created");
        }
        catch (Exception ex){
            transaction.Rollback();
            return BadRequest(ex.Message);
        }
    }
}