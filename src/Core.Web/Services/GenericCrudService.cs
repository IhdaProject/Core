using System.Net;
using AutoMapper;
using Core.Domain.Common;
using Core.Domain.DTOs;
using Core.Domain.Exceptions;
using Core.Domain.Models.Api;
using Core.Infrastructure.Data.Extensions;
using Core.Infrastructure.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Core.Web.Services;

public class GenericCrudService<TIn, TOut, TId>(GenericRepository<TIn, TId> repository,
    IMapper mapper)
    where TIn : ModelBase<TId>
    where TOut : BaseDto<TId>
{
    public async Task<ResponseModel<TOut>> OnSaveAsync(TOut model)
    {
        if (EqualityComparer<TId>.Default.Equals(model.Id, default))
            return ResponseModel<TOut>.ResultFromContent(
                mapper.Map<TOut>(
                    await repository.AddWithSaveChangesAsync(
                        mapper.Map<TIn>(model))), HttpStatusCode.Created);

        var entity = await repository.GetByIdAsync(model.Id) ?? throw new NotFoundException($"Not found {typeof(TIn).Name}");
        mapper.Map(model, entity);
        await repository.UpdateWithSaveChangesAsync(entity);

        return ResponseModel<TOut>.ResultFromContent(mapper.Map<TOut>(entity));
    }

    public async Task<ResponseModel<List<TOut>>> GetAllAsync(MetaQueryModel metaQuery)
    {
        var query = repository.GetAllAsQueryable()
            .FilterByExpressions(metaQuery);

        var items = await query
            .Sort(metaQuery)
            .Paging(metaQuery)
            .Select(c => mapper.Map<TOut>(c))
            .ToListAsync();

        var totalCount = await query.CountAsync();

        return ResponseModel<List<TOut>>.ResultFromContent(
            items,
            total: totalCount);
    }

    public async Task<ResponseModel<TOut>> GetByIdAsync(TId id)
    {
        var item = await repository.GetByIdAsync(id) ??
                   throw new NotFoundException($"Not found {typeof(TIn).Name}");

        return ResponseModel<TOut>.ResultFromContent(
            mapper.Map<TOut>(item));
    }

    public async Task<ResponseModel<TOut>> DeleteByIdAsync(TId id)
    {
        var item = await repository.RemoveWithSaveChangesAsync(id) ??
                   throw new NotFoundException($"Not found {typeof(TIn).Name}");

        return ResponseModel<TOut>.ResultFromContent(
            mapper.Map<TOut>(item));
    }
}
