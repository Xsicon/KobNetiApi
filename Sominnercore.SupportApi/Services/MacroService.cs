using Sominnercore.SupportApi.Data;
using Sominnercore.SupportApi.DTOs;
using Sominnercore.SupportApi.Shared;

namespace Sominnercore.SupportApi.Services;

public interface IMacroService
{
    Task<Response<List<SupportMacroDTO>>> ListAsync(string tenantId);
    Task<Response<SupportMacroDTO>> CreateAsync(string tenantId, SaveMacroDTO request);
    Task<Response<SupportMacroDTO>> UpdateAsync(string tenantId, Guid id, SaveMacroDTO request);
    Task<Response<bool>> DeleteAsync(string tenantId, Guid id);
}

public class MacroService : IMacroService
{
    private readonly ISupportStore _store;

    public MacroService(ISupportStore store) => _store = store;

    public async Task<Response<List<SupportMacroDTO>>> ListAsync(string tenantId)
    {
        var items = await _store.ListMacrosAsync(tenantId);
        return Response<List<SupportMacroDTO>>.SuccessResponse(items.Select(ToDto).ToList(), "Macros loaded");
    }

    public async Task<Response<SupportMacroDTO>> CreateAsync(string tenantId, SaveMacroDTO request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
            return Response<SupportMacroDTO>.Fail("Title and body are required");

        var now = DateTime.UtcNow;
        var entity = new MacroEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
        await _store.InsertMacroAsync(entity);
        return Response<SupportMacroDTO>.SuccessResponse(ToDto(entity), "Macro created");
    }

    public async Task<Response<SupportMacroDTO>> UpdateAsync(string tenantId, Guid id, SaveMacroDTO request)
    {
        var existing = await _store.GetMacroAsync(tenantId, id);
        if (existing is null) return Response<SupportMacroDTO>.Fail("Macro not found");
        existing.Title = request.Title.Trim();
        existing.Body = request.Body.Trim();
        existing.Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        existing.UpdatedAt = DateTime.UtcNow;
        await _store.UpdateMacroAsync(existing);
        return Response<SupportMacroDTO>.SuccessResponse(ToDto(existing), "Macro updated");
    }

    public async Task<Response<bool>> DeleteAsync(string tenantId, Guid id)
    {
        var existing = await _store.GetMacroAsync(tenantId, id);
        if (existing is null) return Response<bool>.Fail("Macro not found");
        await _store.DeleteMacroAsync(tenantId, id);
        return Response<bool>.SuccessResponse(true, "Macro deleted");
    }

    private static SupportMacroDTO ToDto(MacroEntity m) => new()
    {
        Id = m.Id,
        Title = m.Title,
        Body = m.Body,
        Category = m.Category,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt
    };
}

public interface ISupportCountsService
{
    Task<Response<SupportCountsDTO>> GetCountsAsync(string tenantId);
}

public class SupportCountsService : ISupportCountsService
{
    private readonly ISupportStore _store;

    public SupportCountsService(ISupportStore store) => _store = store;

    public async Task<Response<SupportCountsDTO>> GetCountsAsync(string tenantId)
    {
        var activeChats = await _store.CountActiveChatsAsync(tenantId);
        var openTickets = await _store.CountOpenTicketsAsync(tenantId);
        return Response<SupportCountsDTO>.SuccessResponse(new SupportCountsDTO
        {
            ActiveChats = activeChats,
            OpenTickets = openTickets
        }, "Counts loaded");
    }
}
