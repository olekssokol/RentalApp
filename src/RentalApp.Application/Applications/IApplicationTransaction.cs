namespace RentalApp.Application.Applications;

public interface IApplicationTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
}
