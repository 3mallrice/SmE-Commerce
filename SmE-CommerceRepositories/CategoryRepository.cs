﻿using Microsoft.EntityFrameworkCore;
using SmE_CommerceModels.DBContext;
using SmE_CommerceModels.Enums;
using SmE_CommerceModels.Models;
using SmE_CommerceModels.ReturnResult;
using SmE_CommerceRepositories.Interface;

namespace SmE_CommerceRepositories;

public class CategoryRepository(SmECommerceContext dbContext) : ICategoryRepository
{
    public async Task<Return<Category>> AddCategoryAsync(Category category)
    {
        try
        {
            await dbContext.Categories.AddAsync(category);
            await dbContext.SaveChangesAsync();

            return new Return<Category>
            {
                Data = category,
                IsSuccess = true,
                Message = SuccessMessage.Created,
                ErrorCode = ErrorCodes.Ok,
                TotalRecord = 1
            };
        }
        catch (Exception ex)
        {
            return new Return<Category>
            {
                Data = null,
                IsSuccess = false,
                Message = ErrorMessage.InternalServerError,
                ErrorCode = ErrorCodes.InternalServerError,
                InternalErrorMessage = ex,
                TotalRecord = 0
            };
        }
    }

    public async Task<Return<Category>> GetCategoryByNameAsync(string name)
    {
        try
        {
            var result = await dbContext.Categories
                .Where(x => x.Status != GeneralStatus.Deleted)
                .FirstOrDefaultAsync(x => x.Name == name);

            if (result == null)
            {
                return new Return<Category>
                {
                    Data = null,
                    IsSuccess = false,
                    Message = ErrorMessage.CategoryNotFound,
                    ErrorCode = ErrorCodes.CategoryNotFound,
                    TotalRecord = 0
                };
            }
                
            return new Return<Category>
            {
                Data = result,
                IsSuccess = true,
                Message = SuccessMessage.Found,
                ErrorCode = ErrorCodes.Ok,
                TotalRecord = 1
            };
        }
        catch (Exception ex)
        {
            return new Return<Category>
            {
                Data = null,
                IsSuccess = false,
                Message = ErrorMessage.InternalServerError,
                ErrorCode = ErrorCodes.InternalServerError,
                InternalErrorMessage = ex,
                TotalRecord = 0
            };
        }
    }

    public async Task<Return<IEnumerable<Category>>> GetCategoriesAsync(string? name, string? status,
        int? pageNumber, int? pageSize)
    {
        try
        {
            var query = dbContext.Categories.AsQueryable();

            query = query.Where(x => x.Status != GeneralStatus.Deleted);

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(x => x.Status.Equals(status));
            }

            if (!string.IsNullOrEmpty(name))
            {
                query = query.Where(x => x.Name.Contains(name));
            }

            var totalRecords = await query.CountAsync();

            if (pageNumber.HasValue && pageSize.HasValue)
            {
                query = query.Skip((pageNumber.Value - 1) * pageSize.Value).Take(pageSize.Value);
            }

            var result = await query.ToListAsync();

            return new Return<IEnumerable<Category>>
            {
                Data = result,
                IsSuccess = true,
                Message = SuccessMessage.Successfully,
                ErrorCode = ErrorCodes.Ok,
                TotalRecord = totalRecords
            };
        }
        catch (Exception ex)
        {
            return new Return<IEnumerable<Category>>
            {
                Data = null,
                IsSuccess = false,
                Message = ErrorMessage.InternalServerError,
                ErrorCode = ErrorCodes.InternalServerError,
                InternalErrorMessage = ex,
                TotalRecord = 0
            };
        }
    }

    public async Task<Return<Category>> GetCategoryByIdAsync(Guid id)
    {
        try
        {
            var result = await dbContext.Categories.FirstOrDefaultAsync(x => x.CategoryId == id);
            if(result == null)
            {
                return new Return<Category>
                {
                    Data = null,
                    IsSuccess = false,
                    Message = ErrorMessage.CategoryNotFound,
                    ErrorCode = ErrorCodes.CategoryNotFound,
                    TotalRecord = 0
                };
            }
                
            return new Return<Category>
            {
                Data = result,
                IsSuccess = true,
                Message = SuccessMessage.Found,
                ErrorCode = ErrorCodes.Ok,
                TotalRecord = 1
            };
        }
        catch (Exception ex)
        {
            return new Return<Category>
            {
                Data = null,
                IsSuccess = false,
                Message = ErrorMessage.InternalServerError,
                ErrorCode = ErrorCodes.InternalServerError,
                InternalErrorMessage = ex,
                TotalRecord = 0
            };
        }
    }

    public async Task<Return<Category>> UpdateCategoryAsync(Category category)
    {
        try
        {
            dbContext.Categories.Update(category);
            await dbContext.SaveChangesAsync();

            return new Return<Category>
            {
                Data = category,
                IsSuccess = true,
                Message = SuccessMessage.Updated,
                ErrorCode = ErrorCodes.Ok,
                TotalRecord = 1
            };
        }
        catch (Exception ex)
        {
            return new Return<Category>
            {
                Data = null,
                IsSuccess = false,
                Message = ErrorMessage.InternalServerError,
                ErrorCode = ErrorCodes.InternalServerError,
                InternalErrorMessage = ex,
                TotalRecord = 0
            };
        }
    }
}