using evalflow_backend_api.Features.Profile.Avatar;
using evalflow_backend_api.Infrastructure.Database;
using evalflow_backend_api.Infrastructure.Security.CurrentUserService;
using evalflow_backend_api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace evalflow_backend_api.Tests.Features.Profile.Avatar;

public class AvatarHandlerTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

    private readonly Mock<ICurrentUserService> _currentUser = new();

    private async Task<int> SeedUserAsync(AppDbContext db)
    {
        var company = TestDataFactory.CreateCompany(identificationId: "tenant-1");
        var user = TestDataFactory.CreateUser(company, email: "user@example.com");
        db.Companies.Add(company);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        _currentUser.Setup(c => c.GetUserId()).Returns(user.Id.ToString());
        return user.Id;
    }

    [Fact]
    public async Task Upload_WithValidPng_StoresAvatarWithDetectedType()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var userId = await SeedUserAsync(db);

        var result = await new UploadAvatarHandler(db, _currentUser.Object)
            .Handle(new UploadAvatarRecord($"data:image/jpeg;base64,{Convert.ToBase64String(Png)}"), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
        var stored = await db.UserAvatars.SingleAsync(a => a.UserId == userId);
        stored.ContentType.Should().Be("image/png"); // detected from the bytes, not the declared type
        stored.Data.Should().Equal(Png);
    }

    [Fact]
    public async Task Upload_Twice_ReplacesTheExistingAvatar()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var userId = await SeedUserAsync(db);
        var sut = new UploadAvatarHandler(db, _currentUser.Object);

        await sut.Handle(new UploadAvatarRecord(Convert.ToBase64String(Png)), CancellationToken.None);
        await sut.Handle(new UploadAvatarRecord(Convert.ToBase64String(Jpeg)), CancellationToken.None);

        var stored = await db.UserAvatars.SingleAsync(a => a.UserId == userId);
        stored.ContentType.Should().Be("image/jpeg");
    }

    [Theory]
    [InlineData("not-base64!!")]
    [InlineData("")]
    [InlineData("PHN2Zz48L3N2Zz4=")] // "<svg></svg>" — not an accepted raster format
    public async Task Upload_WithInvalidImage_ReturnsBadRequest(string data)
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedUserAsync(db);

        var result = await new UploadAvatarHandler(db, _currentUser.Object)
            .Handle(new UploadAvatarRecord(data), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
        (await db.UserAvatars.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Upload_TooLarge_ReturnsBadRequest()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedUserAsync(db);
        var big = new byte[AvatarImage.MaxBytes + 1];
        Png.CopyTo(big, 0);

        var result = await new UploadAvatarHandler(db, _currentUser.Object)
            .Handle(new UploadAvatarRecord(Convert.ToBase64String(big)), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Upload_WithoutUser_ReturnsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        _currentUser.Setup(c => c.GetUserId()).Returns((string?)null);

        var result = await new UploadAvatarHandler(db, _currentUser.Object)
            .Handle(new UploadAvatarRecord(Convert.ToBase64String(Png)), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public async Task Get_ReturnsTheImage_AndNotFoundAfterDelete()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedUserAsync(db);
        await new UploadAvatarHandler(db, _currentUser.Object)
            .Handle(new UploadAvatarRecord(Convert.ToBase64String(Jpeg)), CancellationToken.None);

        var file = await new GetAvatarHandler(db, _currentUser.Object).Handle(new GetAvatarRecord(), CancellationToken.None);
        file.Should().BeOfType<FileContentHttpResult>().Which.ContentType.Should().Be("image/jpeg");

        var deleted = await new DeleteAvatarHandler(db, _currentUser.Object).Handle(new DeleteAvatarRecord(), CancellationToken.None);
        deleted.Should().HaveStatusCode(StatusCodes.Status200OK);

        var missing = await new GetAvatarHandler(db, _currentUser.Object).Handle(new GetAvatarRecord(), CancellationToken.None);
        missing.Should().HaveStatusCode(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Delete_WithoutAvatar_IsStillOk()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedUserAsync(db);

        var result = await new DeleteAvatarHandler(db, _currentUser.Object).Handle(new DeleteAvatarRecord(), CancellationToken.None);

        result.Should().HaveStatusCode(StatusCodes.Status200OK);
    }
}
