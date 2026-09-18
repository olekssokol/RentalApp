using RentalApp.Domain.Common;

namespace RentalApp.Application.Applications;

public interface IPropertyManagerNoteService
{
    Task<Result<IReadOnlyList<PropertyManagerNoteDto>>> ListAsync(int applicationId, bool isManager, CancellationToken ct = default);
    Task<Result<PropertyManagerNoteDto>> GetAsync(int applicationId, int noteId, bool isManager, CancellationToken ct = default);
    Task<Result<int>> AddAsync(AddPropertyManagerNoteCommand command, CancellationToken ct = default);
    Task<Result> UpdateAsync(UpdatePropertyManagerNoteCommand command, CancellationToken ct = default);
}
