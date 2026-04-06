using Lfm.Contracts.Specializations;

namespace Lfm.Api.Repositories;

public interface ISpecializationsRepository
{
    Task<IReadOnlyList<SpecializationDto>> ListAsync(CancellationToken ct);
}
