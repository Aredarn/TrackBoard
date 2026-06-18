namespace TrackBoard.Entities;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("vehicle_information_data")]
public class VehicleInformationData
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long VehicleId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Manufacturer { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Model { get; set; } = string.Empty;

    public int Year { get; set; }

    [Required]
    [MaxLength(50)]
    public string EngineType { get; set; } = string.Empty;

    public int Horsepower { get; set; }

    public int? Torque { get; set; } // Nm

    public double Weight { get; set; } // kg

    public double? TopSpeed { get; set; } // km/h

    public double? Acceleration { get; set; } // 0-100 km/h

    [Required]
    [MaxLength(20)]
    public string Drivetrain { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string FuelType { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string TireType { get; set; } = string.Empty;

    public double? FuelCapacity { get; set; } // liters

    [Required]
    [MaxLength(50)]
    public string Transmission { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? SuspensionType { get; set; }
}
