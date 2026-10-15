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

public class BasicCrudService<TIn, TOut, TId>(GenericRepository<TIn, TId> repository,
    IMapper mapper) : IBasicCrudService<TIn, TOut, TId>
    where TIn : ModelBase<TId>
    where TOut : BaseDto<TId>
{
    public virtual async Task<ResponseModel<TOut>> OnSaveAsync(TOut model)
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

    public virtual async Task<ResponseModel<List<TOut>>> GetAllAsync(MetaQueryModel metaQuery)
    {
        var query = repository.GetAllAsQueryable()
            .FilterByExpressions(metaQuery);

        var entities = await query
            .Sort(metaQuery)
            .Paging(metaQuery)
            .ToListAsync();

        var items = mapper.Map<List<TOut>>(entities);

        var totalCount = await query.CountAsync();

        return ResponseModel<List<TOut>>.ResultFromContent(
            items,
            total: totalCount);
    }

    public virtual async Task<ResponseModel<TOut>> GetByIdAsync(TId id)
    {
        var item = await repository.GetByIdAsync(id) ??
                   throw new NotFoundException($"Not found {typeof(TIn).Name}");

        return ResponseModel<TOut>.ResultFromContent(
            mapper.Map<TOut>(item));
    }

    public virtual async Task<ResponseModel<TOut>> DeleteByIdAsync(TId id)
    {
        var item = await repository.RemoveWithSaveChangesAsync(id) ??
                   throw new NotFoundException($"Not found {typeof(TIn).Name}");

        return ResponseModel<TOut>.ResultFromContent(
            mapper.Map<TOut>(item));
    }
}
