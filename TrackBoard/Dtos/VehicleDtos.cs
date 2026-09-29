using System.ComponentModel.DataAnnotations;

namespace TrackBoard.Dtos;

public record VehicleResponse(
    Guid Id,
    Guid OwnerId,
    string OwnerDisplayName,
    string Manufacturer,
    string Model,
    int Year,
    string EngineType,
    int Horsepower,
    int? Torque,
    double Weight,
    double? TopSpeed,
    double? Acceleration,
    string Drivetrain,
    string FuelType,
    string TireType,
    double? FuelCapacity,
    string Transmission,
    string? SuspensionType,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? PhotoUrl = null);

public record CreateVehicleRequest
{
    // Ownership is taken from the authenticated principal, never from the request body.

    [Required]
    [MaxLength(100)]
    public string Manufacturer { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Model { get; init; } = string.Empty;

    [Range(1885, 2100)]
    public int Year { get; init; }

    [Required]
    [MaxLength(50)]
    public string EngineType { get; init; } = string.Empty;

    [Range(0, 5000)]
    public int Horsepower { get; init; }

    [Range(0, 5000)]
    public int? Torque { get; init; }

    [Range(0.1, 10000)]
    public double Weight { get; init; }

    [Range(0, 1000)]
    public double? TopSpeed { get; init; }

    [Range(0, 60)]
    public double? Acceleration { get; init; }

    [Required]
    [MaxLength(20)]
    public string Drivetrain { get; init; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string FuelType { get; init; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string TireType { get; init; } = string.Empty;

    [Range(0, 1000)]
    public double? FuelCapacity { get; init; }

    [Required]
    [MaxLength(50)]
    public string Transmission { get; init; } = string.Empty;

    [MaxLength(100)]
    public string? SuspensionType { get; init; }
}

public record UpdateVehicleRequest
{
    [Required]
    [MaxLength(100)]
    public string Manufacturer { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Model { get; init; } = string.Empty;

    [Range(1885, 2100)]
    public int Year { get; init; }

    [Required]
    [MaxLength(50)]
    public string EngineType { get; init; } = string.Empty;

    [Range(0, 5000)]
    public int Horsepower { get; init; }

    [Range(0, 5000)]
    public int? Torque { get; init; }

    [Range(0.1, 10000)]
    public double Weight { get; init; }

    [Range(0, 1000)]
    public double? TopSpeed { get; init; }

    [Range(0, 60)]
    public double? Acceleration { get; init; }

    [Required]
    [MaxLength(20)]
    public string Drivetrain { get; init; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string FuelType { get; init; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string TireType { get; init; } = string.Empty;

    [Range(0, 1000)]
    public double? FuelCapacity { get; init; }

    [Required]
    [MaxLength(50)]
    public string Transmission { get; init; } = string.Empty;

    [MaxLength(100)]
    public string? SuspensionType { get; init; }
}
