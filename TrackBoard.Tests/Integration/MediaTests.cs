using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using TrackBoard.Dtos;
using TrackBoard.Storage;

namespace TrackBoard.Tests.Integration;

/// <summary>Stands in for Supabase Storage: signs anything, remembers what it was asked to delete.</summary>
public class FakeMediaStorage : IMediaStorage
{
    public const string Base = "https://media.test/public/";

    public ConcurrentBag<string> Deleted { get; } = [];

    public bool IsConfigured => true;

    public string? PublicBaseUrl => Base;

    public Task<SignedUpload> CreateSignedUploadAsync(string path, CancellationToken ct) =>
        Task.FromResult(new SignedUpload($"https://media.test/upload/{path}?token=t", path, Base + path));

    public string? PublicUrl(string? path) => string.IsNullOrEmpty(path) ? null : Base + path;

    public Task DeleteAsync(IEnumerable<string?> paths, CancellationToken ct)
    {
        foreach (var path in paths.OfType<string>())
        {
            Deleted.Add(path);
        }

        return Task.CompletedTask;
    }
}

public class MediaApiFactory : TrackBoardApiFactory
{
    public FakeMediaStorage Media { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IMediaStorage>();
            services.AddSingleton<IMediaStorage>(Media);
        });
    }
}

public class MediaTests(MediaApiFactory factory) : TrackProTestBase(factory), IClassFixture<MediaApiFactory>
{
    private readonly FakeMediaStorage _media = factory.Media;

    [Fact]
    public async Task An_avatar_goes_upload_then_attach_and_the_old_one_is_removed()
    {
        var (client, auth) = await DriverAsync();

        var first = await UploadAsync(client, new { kind = "Avatar", contentType = "image/jpeg" });
        first.Path.ShouldStartWith($"users/{auth.User.Id:N}/avatar/");
        first.Path.ShouldEndWith(".jpg");

        var withFirst = await PutAsync<ProfileResponse>(client, "/api/v1/me/avatar", new { path = first.Path });
        withFirst.Body!.AvatarUrl.ShouldBe(FakeMediaStorage.Base + first.Path);

        var second = await UploadAsync(client, new { kind = "Avatar", contentType = "image/webp" });
        second.Path.ShouldNotBe(first.Path);
        await PutAsync<ProfileResponse>(client, "/api/v1/me/avatar", new { path = second.Path });

        _media.Deleted.ShouldContain(first.Path);

        var cleared = await client.DeleteAsync("/api/v1/me/avatar");
        (await cleared.Content.ReadFromJsonAsync<ProfileResponse>(Json))!.AvatarUrl.ShouldBeNull();
        _media.Deleted.ShouldContain(second.Path);
    }

    [Fact]
    public async Task A_path_signed_for_someone_else_cannot_be_attached()
    {
        var (alice, _) = await DriverAsync("Alice");
        var alicesUpload = await UploadAsync(alice, new { kind = "Avatar", contentType = "image/jpeg" });

        var (mallory, _) = await DriverAsync("Mallory");
        var (status, _) = await PutAsync<ProfileResponse>(mallory, "/api/v1/me/avatar", new { path = alicesUpload.Path });

        status.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_vehicle_photo_shows_in_the_vehicle_response_and_survives_a_sync_upsert()
    {
        var (client, _) = await DriverAsync();
        var vehicle = await CreateVehicleAsync(client);

        var upload = await UploadAsync(client, new { kind = "VehiclePhoto", vehicleId = vehicle, contentType = "image/jpeg" });
        var (status, withPhoto) = await PutAsync<VehicleResponse>(client, $"/api/v1/vehicles/{vehicle}/photo", new { path = upload.Path });

        status.ShouldBe(HttpStatusCode.OK);
        withPhoto!.PhotoUrl.ShouldBe(FakeMediaStorage.Base + upload.Path);

        // The app's background sync re-uploads the vehicle without knowing about photos.
        await PutAsync<VehicleResponse>(client, $"/api/v1/vehicles/{vehicle}", SampleVehicle);

        var listed = await client.GetFromJsonAsync<PagedResultProbe>("/api/v1/vehicles", Json);
        listed!.Items.Single(v => v.Id == vehicle).PhotoUrl.ShouldBe(FakeMediaStorage.Base + upload.Path);
    }

    [Fact]
    public async Task Someone_elses_vehicle_cannot_get_an_upload_or_a_photo()
    {
        var (owner, _) = await DriverAsync("Owner");
        var vehicle = await CreateVehicleAsync(owner);

        var (stranger, _) = await DriverAsync("Stranger");
        var response = await stranger.PostAsJsonAsync(
            "/api/v1/me/uploads",
            new { kind = "VehiclePhoto", vehicleId = vehicle, contentType = "image/jpeg" },
            Json);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_vehicle_photo_upload_without_a_vehicle_is_a_bad_request()
    {
        var (client, _) = await DriverAsync();

        var response = await client.PostAsJsonAsync(
            "/api/v1/me/uploads",
            new { kind = "VehiclePhoto", contentType = "image/jpeg" },
            Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Only_jpeg_and_webp_are_accepted()
    {
        var (client, _) = await DriverAsync();

        var response = await client.PostAsJsonAsync(
            "/api/v1/me/uploads",
            new { kind = "Avatar", contentType = "image/svg+xml" },
            Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Deleting_the_account_removes_its_photos_from_storage()
    {
        var (client, _) = await DriverAsync();
        var vehicle = await CreateVehicleAsync(client);

        var avatar = await UploadAsync(client, new { kind = "Avatar", contentType = "image/jpeg" });
        await PutAsync<ProfileResponse>(client, "/api/v1/me/avatar", new { path = avatar.Path });
        var photo = await UploadAsync(client, new { kind = "VehiclePhoto", vehicleId = vehicle, contentType = "image/jpeg" });
        await PutAsync<VehicleResponse>(client, $"/api/v1/vehicles/{vehicle}/photo", new { path = photo.Path });

        (await DeleteAccountAsync(client)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        _media.Deleted.ShouldContain(avatar.Path);
        _media.Deleted.ShouldContain(photo.Path);
    }

    private static async Task<UploadTargetResponse> UploadAsync(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/api/v1/me/uploads", body, Json);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<UploadTargetResponse>(Json))!;
    }

    private sealed record PagedResultProbe(IReadOnlyList<VehicleResponse> Items);
}
