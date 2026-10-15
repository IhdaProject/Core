using AutoMapper;
using Core.Domain.Common;
using Core.Domain.DTOs;
using Core.Infrastructure.Data.Repositories;

namespace Core.Web.Services;

public class GenericCrudService<TIn, TOut, TId>(GenericRepository<TIn, TId> repository, IMapper mapper)
    : BasicCrudService<TIn, TOut, TId>(repository, mapper)
    where TIn : ModelBase<TId>
    where TOut : BaseDto<TId>;
