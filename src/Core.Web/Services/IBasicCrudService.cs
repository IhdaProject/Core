using Core.Domain.Common;
using Core.Domain.DTOs;
using Core.Domain.Models.Api;

namespace Core.Web.Services;

public interface IBasicCrudService<TIn, TOut, TId>
    where TIn : ModelBase<TId>
    where TOut : BaseDto<TId>
{
    Task<ResponseModel<TOut>> OnSaveAsync(TOut model);
    Task<ResponseModel<List<TOut>>> GetAllAsync(MetaQueryModel metaQuery);
    Task<ResponseModel<TOut>> GetByIdAsync(TId id);
    Task<ResponseModel<TOut>> DeleteByIdAsync(TId id);
}
