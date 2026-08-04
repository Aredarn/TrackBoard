namespace TrackBoard.Entities;

public class Vehicle : BaseEntity
{
    /// <summary>Owning driver. Ownership is enforced in the service layer.</summary>
    public Guid OwnerId { get; set; }

    public User Owner { get; set; } = null!;

    public string Manufacturer { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public int Year { get; set; }

    public string EngineType { get; set; } = string.Empty;

    public int Horsepower { get; set; }

    /// <summary>Nm.</summary>
    public int? Torque { get; set; }

    /// <summary>kg.</summary>
    public double Weight { get; set; }

    /// <summary>km/h.</summary>
    public double? TopSpeed { get; set; }

    /// <summary>Seconds, 0-100 km/h.</summary>
    public double? Acceleration { get; set; }

    public string Drivetrain { get; set; } = string.Empty;

    public string FuelType { get; set; } = string.Empty;

    public string TireType { get; set; } = string.Empty;

    /// <summary>Litres.</summary>
    public double? FuelCapacity { get; set; }

    public string Transmission { get; set; } = string.Empty;

    public string? SuspensionType { get; set; }

    public ICollection<Result> Results { get; set; } = [];
}
