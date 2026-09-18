using RentalApp.Domain.Common;

namespace RentalApp.Application.Applications;

public interface IApplicationCommandService
{
    Task<Result<int>> StartAsync(StartApplicationCommand command, CancellationToken ct = default);
    Task<Result> SaveApplicantInfoAsync(SaveApplicantInfoCommand command, CancellationToken ct = default);
    Task<Result> SaveResidenceHistoryAsync(SaveResidenceHistoryCommand command, CancellationToken ct = default);
    Task<Result> GoBackAsync(GoBackCommand command, CancellationToken ct = default);
    Task<Result> SubmitAsync(SubmitApplicationCommand command, CancellationToken ct = default);
    Task<Result> WithdrawAsync(WithdrawApplicationCommand command, CancellationToken ct = default);
    Task<Result<int>> AddResidenceAsync(AddResidenceCommand command, CancellationToken ct = default);
    Task<Result> UpdateResidenceAsync(UpdateResidenceCommand command, CancellationToken ct = default);
    Task<Result> DeleteResidenceAsync(DeleteResidenceCommand command, CancellationToken ct = default);
    Task<Result> ClaimAsync(ClaimApplicationCommand command, CancellationToken ct = default);
    Task<Result> ReleaseAsync(ReleaseApplicationCommand command, CancellationToken ct = default);
    Task<Result> ReviewAsync(ReviewApplicationCommand command, CancellationToken ct = default);
}
