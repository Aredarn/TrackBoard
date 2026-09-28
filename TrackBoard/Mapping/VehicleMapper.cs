using Riok.Mapperly.Abstractions;
using TrackBoard.Dtos;
using TrackBoard.Entities;

namespace TrackBoard.Mapping;

/// <summary>
/// Source-generated request-to-entity mapping (Mapperly is the .NET analogue of MapStruct).
/// Entity-to-response mapping is deliberately *not* here: those run as LINQ projections
/// inside the services so EF can translate flattening and aggregates into one SELECT
/// instead of materialising entity graphs.
/// </summary>
[Mapper]
public static partial class VehicleMapper
{
    [MapperIgnoreTarget(nameof(Vehicle.Id))]
    [MapperIgnoreTarget(nameof(Vehicle.CreatedAt))]
    [MapperIgnoreTarget(nameof(Vehicle.UpdatedAt))]
    [MapperIgnoreTarget(nameof(Vehicle.Owner))]
    [MapperIgnoreTarget(nameof(Vehicle.OwnerId))]
    [MapperIgnoreTarget(nameof(Vehicle.Results))]
    [MapperIgnoreTarget(nameof(Vehicle.PhotoPath))]
    public static partial Vehicle ToEntity(CreateVehicleRequest request);

    [MapperIgnoreTarget(nameof(Vehicle.Id))]
    [MapperIgnoreTarget(nameof(Vehicle.CreatedAt))]
    [MapperIgnoreTarget(nameof(Vehicle.UpdatedAt))]
    [MapperIgnoreTarget(nameof(Vehicle.Owner))]
    [MapperIgnoreTarget(nameof(Vehicle.OwnerId))]
    [MapperIgnoreTarget(nameof(Vehicle.Results))]
    // The sync upsert replaces the vehicle wholesale, and older app builds send no photo.
    // Mapping it here would let every such upload wipe a photo set from another phone.
    [MapperIgnoreTarget(nameof(Vehicle.PhotoPath))]
    public static partial void ApplyTo(UpdateVehicleRequest request, Vehicle target);
}
