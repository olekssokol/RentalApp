using RentalApp.Application.Applications;
using RentalApp.Domain.Entities;
using RentalApp.UnitTests.Support;

namespace RentalApp.UnitTests.Application;

public class PropertyManagerNoteServiceTests
{
    [Fact]
    public async Task ListAsync_Applicant_IsRejectedBeforeRepositoryAccess()
    {
        var repository = new PropertyManagerNoteRepositoryFake();
        var service = new PropertyManagerNoteService(repository);

        var result = await service.ListAsync(1, isManager: false);

        Assert.True(result.IsFailure);
        Assert.Equal(0, repository.ReadCount);
    }

    [Fact]
    public async Task AddAsync_Manager_TrimsTextAndRecordsAuditFields()
    {
        var before = DateTime.UtcNow;
        var repository = new PropertyManagerNoteRepositoryFake();
        var service = new PropertyManagerNoteService(repository);

        var result = await service.AddAsync(new AddPropertyManagerNoteCommand(
            7, "manager-id", "Pat Manager", "  Follow up tomorrow.  ", IsManager: true));

        Assert.True(result.IsSuccess, result.Error);
        var note = Assert.Single(repository.Notes);
        Assert.Equal("Follow up tomorrow.", note.Text);
        Assert.Equal(7, note.RentalApplicationId);
        Assert.Equal("manager-id", note.AuthorUserId);
        Assert.Equal("Pat Manager", note.AuthorDisplayName);
        Assert.True(note.CreatedAtUtc >= before);
        Assert.Equal(note.CreatedAtUtc, note.UpdatedAtUtc);
        Assert.Equal(1, repository.SaveCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddAsync_EmptyText_IsRejected(string text)
    {
        var repository = new PropertyManagerNoteRepositoryFake();
        var service = new PropertyManagerNoteService(repository);

        var result = await service.AddAsync(new AddPropertyManagerNoteCommand(
            1, "manager", "Manager", text, IsManager: true));

        Assert.True(result.IsFailure);
        Assert.Empty(repository.Notes);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task AddAsync_TextAboveMaximum_IsRejected()
    {
        var repository = new PropertyManagerNoteRepositoryFake();
        var service = new PropertyManagerNoteService(repository);

        var result = await service.AddAsync(new AddPropertyManagerNoteCommand(
            1, "manager", "Manager", new string('x', PropertyManagerNoteService.MaxTextLength + 1), IsManager: true));

        Assert.True(result.IsFailure);
        Assert.Empty(repository.Notes);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task UpdateAsync_NoteFromAnotherApplication_IsNotFound()
    {
        var repository = new PropertyManagerNoteRepositoryFake();
        repository.Notes.Add(new PropertyManagerNote { Id = 3, RentalApplicationId = 8, Text = "Original" });
        var service = new PropertyManagerNoteService(repository);

        var result = await service.UpdateAsync(new UpdatePropertyManagerNoteCommand(
            ApplicationId: 7, NoteId: 3, Text: "Changed", IsManager: true));

        Assert.True(result.IsFailure);
        Assert.Equal("Original", repository.Notes[0].Text);
        Assert.Equal(0, repository.SaveCount);
    }
}
