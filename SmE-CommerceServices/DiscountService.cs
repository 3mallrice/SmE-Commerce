﻿using System.Text.RegularExpressions;
using System.Transactions;
using SmE_CommerceModels.Enums;
using SmE_CommerceModels.Models;
using SmE_CommerceModels.RequestDtos.Discount;
using SmE_CommerceModels.ResponseDtos.Discount;
using SmE_CommerceModels.ReturnResult;
using SmE_CommerceRepositories.Interface;
using SmE_CommerceServices.Interface;

namespace SmE_CommerceServices;

public class DiscountService(
    IDiscountRepository discountRepository,
    IHelperService helperService,
    IProductRepository productRepository,
    IUserRepository userRepository) : IDiscountService
{
    #region Discount

    public async Task<Return<bool>> AddDiscountAsync(AddDiscountReqDto discount)
    {
        using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        try
        {
            // Validate user
            var currentUser = await helperService.GetCurrentUserWithRoleAsync(nameof(RoleEnum.Manager));
            if (!currentUser.IsSuccess || currentUser.Data == null)
            {
                return new Return<bool>
                {
                    IsSuccess = false,
                    Message = currentUser.Message,
                    InternalErrorMessage = currentUser.InternalErrorMessage,
                    ErrorCode = currentUser.ErrorCode
                };
            }

            // Check ten co bi trung hay khong
            var existedName = await discountRepository.GetDiscountByNameAsync(discount.DiscountName);
            if (existedName is { IsSuccess: true, Data: not null })
            {
                return new Return<bool>
                {
                    IsSuccess = false,
                    Message = ErrorMessage.NameAlreadyExists,
                    ErrorCode = ErrorCodes.NameAlreadyExists
                };
            }

            // Check if DiscountValue is valid based on IsPercentage
            if (discount.IsPercentage)
            {
                if (discount.DiscountValue is < 0 or > 100)
                {
                    return new Return<bool>
                    {
                        IsSuccess = false,
                        Message = ErrorMessage.InvalidPercentage,
                        ErrorCode = ErrorCodes.InvalidPercentage
                    };
                }

                if (discount.MaximumDiscount is <= 0)
                {
                    return new Return<bool>
                    {
                        IsSuccess = false,
                        Message = ErrorMessage.InvalidNumber,
                        ErrorCode = ErrorCodes.InvalidNumber
                    };
                }
            }
            else
            {
                if (discount.DiscountValue <= 0)
                {
                    return new Return<bool>
                    {
                        IsSuccess = false,
                        Message = ErrorMessage.InvalidNumber,
                        ErrorCode = ErrorCodes.InvalidNumber
                    };
                }
            }

            if (discount is { FromDate: not null, ToDate: not null } || discount.FromDate != null ||
                discount.ToDate != null)
            {
                if (discount.FromDate > discount.ToDate)
                {
                    return new Return<bool>
                    {
                        IsSuccess = false,
                        Message = ErrorMessage.InvalidDate,
                        ErrorCode = ErrorCodes.InvalidDate
                    };
                }

                if (discount.FromDate < DateTime.Now)
                {
                    return new Return<bool>
                    {
                        IsSuccess = false,
                        Message = ErrorMessage.InvalidDate,
                        ErrorCode = ErrorCodes.InvalidDate
                    };
                }

                if (discount.DiscountCodes != null && discount.DiscountCodes.Any(discountCode =>
                        discountCode.FromDate < discount.FromDate || discountCode.ToDate > discount.ToDate))
                {
                    return new Return<bool>
                    {
                        IsSuccess = false,
                        Message = ErrorMessage.InvalidDate,
                        ErrorCode = ErrorCodes.InvalidDate
                    };
                }
            }

            if (discount.MinimumOrderAmount is < 0)
            {
                return new Return<bool>
                {
                    IsSuccess = false,
                    Message = ErrorMessage.InvalidNumber,
                    ErrorCode = ErrorCodes.InvalidNumber
                };
            }

            if (discount.UsageLimit is < 0)
            {
                return new Return<bool>
                {
                    IsSuccess = false,
                    Message = ErrorMessage.InvalidNumber,
                    ErrorCode = ErrorCodes.InvalidNumber
                };
            }

            if (discount is { MinQuantity: not null, MaxQuantity: not null } || discount.MinQuantity != null ||
                discount.MaxQuantity != null)
            {
                if (discount.MinQuantity > discount.MaxQuantity)
                {
                    return new Return<bool>
                    {
                        IsSuccess = false,
                        Message = ErrorMessage.InvalidNumber,
                        ErrorCode = ErrorCodes.InvalidNumber
                    };
                }

                if (discount.MinQuantity < 0 || discount.MaxQuantity < 0)
                {
                    return new Return<bool>
                    {
                        IsSuccess = false,
                        Message = ErrorMessage.InvalidNumber,
                        ErrorCode = ErrorCodes.InvalidNumber
                    };
                }
            }

            foreach (var productId in discount.ProductIds)
            {
                var productExists = await productRepository.GetProductByIdAsync(productId);
                if (productExists is { IsSuccess: false, Data: null } || productExists.Data?.Status == GeneralStatus.Inactive)
                {
                    return new Return<bool>
                    {
                        IsSuccess = false,
                        Message = ErrorMessage.DiscountNotFound,
                        ErrorCode = ErrorCodes.DiscountNotFound
                    };
                }
            }

            var discountModel = new Discount
            {
                DiscountName = discount.DiscountName,
                Description = discount.Description,
                IsPercentage = discount.IsPercentage,
                DiscountValue = discount.DiscountValue,
                MinimumOrderAmount = discount.MinimumOrderAmount,
                MaximumDiscount = discount.MaximumDiscount,
                FromDate = discount.FromDate ?? DateTime.Today,
                ToDate = discount.ToDate ?? DateTime.MaxValue,
                Status = discount.Status != GeneralStatus.Inactive
                    ? GeneralStatus.Active
                    : GeneralStatus.Inactive,
                UsageLimit = discount.UsageLimit,
                UsedCount = 0,
                MinQuantity = discount.MinQuantity,
                MaxQuantity = discount.MaxQuantity,
                IsFirstOrder = discount.IsFirstOrder,
                CreateById = currentUser.Data.UserId,
                CreatedAt = DateTime.Now
            };

            var result = await discountRepository.AddDiscountAsync(discountModel);
            if (!result.IsSuccess || result.Data == null)
            {
                return new Return<bool>
                {
                    IsSuccess = false,
                    Message = result.Message,
                    InternalErrorMessage = result.InternalErrorMessage,
                    ErrorCode = result.ErrorCode
                };
            }

            // Kiểm tra xem có cần update không
            var needUpdate = false;

            // Add products for discount
            if (discount.ProductIds.Count > 0)
            {
                result.Data.DiscountProducts = discount.ProductIds.Select(x => new DiscountProduct
                {
                    DiscountId = result.Data.DiscountId,
                    ProductId = x
                }).ToList();
                needUpdate = true;
            }

            if (discount.DiscountCodes != null && discount.DiscountCodes.Any())
            {
                foreach (var discountCode in discount.DiscountCodes)
                {
                    var existingCode =
                        await discountRepository.GetDiscountCodeByCodeAsync(discountCode.DiscountCode.ToUpper());
                    if (existingCode is { IsSuccess: true, Data: not null })
                        return new Return<bool>
                        {
                            IsSuccess = false,
                            Message = ErrorMessage.DiscountCodeAlreadyExists,
                            ErrorCode = ErrorCodes.DiscountCodeAlreadyExists
                        };
                }
            }

            // Add Discount Code
            if (discount.DiscountCodes != null && discount.DiscountCodes.Any())
            {
                if (discount.DiscountCodes.Any(discountCode =>
                        discountCode.FromDate < discount.FromDate || discountCode.ToDate > discount.ToDate))
                {
                    return new Return<bool>
                    {
                        IsSuccess = false,
                        Message = ErrorMessage.InvalidDate,
                        ErrorCode = ErrorCodes.InvalidDate
                    };
                }

                result.Data.DiscountCodes = discount.DiscountCodes.Select(x => new DiscountCode
                {
                    DiscountId = result.Data.DiscountId,
                    Code = x.DiscountCode.ToUpper().Trim(),
                    UserId = x.UserId,
                    FromDate = x.FromDate ?? DateTime.Today,
                    ToDate = x.ToDate ?? result.Data.ToDate,
                    Status = x.Status != DiscountCodeStatus.Inactive
                        ? DiscountCodeStatus.Active
                        : DiscountCodeStatus.Inactive,
                    CreatedAt = DateTime.Now,
                    CreateById = currentUser.Data.UserId
                }).ToList();
                needUpdate = true;
            }

            if (!needUpdate)
                return new Return<bool>
                {
                    Data = true,
                    IsSuccess = true,
                    Message = SuccessMessage.Created,
                    ErrorCode = ErrorCodes.Ok
                };
            var addProductsResult = await discountRepository.UpdateDiscountAsync(result.Data);
            if (!addProductsResult.IsSuccess || addProductsResult.Data == null)
            {
                return new Return<bool>
                {
                    IsSuccess = false,
                    Message = addProductsResult.Message,
                    InternalErrorMessage = addProductsResult.InternalErrorMessage,
                    ErrorCode = addProductsResult.ErrorCode
                };
            }

            transaction.Complete();

            return new Return<bool>
            {
                Data = true,
                IsSuccess = true,
                Message = SuccessMessage.Created,
                ErrorCode = ErrorCodes.Ok
            };
        }
        catch (Exception e)
        {
            return new Return<bool>
            {
                Data = false,
                IsSuccess = false,
                Message = ErrorMessage.InternalServerError,
                InternalErrorMessage = e,
                ErrorCode = ErrorCodes.InternalServerError
            };
        }
    }

    #endregion

    #region DiscountCode

    public async Task<Return<bool>> AddDiscountCodeAsync(Guid id, AddDiscountCodeReqDto req)
    {
        try
        {
            // Validate user
            var currentUser = await helperService.GetCurrentUserWithRoleAsync(nameof(RoleEnum.Manager));
            if (!currentUser.IsSuccess || currentUser.Data == null)
            {
                return new Return<bool>
                {
                    IsSuccess = false,
                    Message = currentUser.Message,
                    InternalErrorMessage = currentUser.InternalErrorMessage,
                    ErrorCode = currentUser.ErrorCode
                };
            }

            // Check if DiscountCode is valid
            if (req.DiscountCode.Length is < 4 or > 20 || !Regex.IsMatch(req.DiscountCode, "^[a-zA-Z0-9]+$"))
            {
                return new Return<bool>
                {
                    IsSuccess = false,
                    Message = ErrorMessage.InvalidDiscountCode,
                    ErrorCode = ErrorCodes.InvalidDiscountCode
                };
            }

            // Check if DiscountId is valid
            var discount = await discountRepository.GetDiscountByIdAsync(id);
            if (!discount.IsSuccess || discount.Data == null)
            {
                return new Return<bool>
                {
                    IsSuccess = false,
                    Message = discount.Message,
                    ErrorCode = discount.ErrorCode
                };
            }

            // Check if UserId is valid
            if (req.UserId.HasValue)
            {
                var existedUser = await userRepository.GetUserByIdAsync(req.UserId.Value);
                if (!existedUser.IsSuccess || existedUser.Data == null)
                {
                    return new Return<bool>
                    {
                        IsSuccess = false,
                        Message = existedUser.Message,
                        ErrorCode = existedUser.ErrorCode
                    };
                }
            }

            if (req is { FromDate: not null, ToDate: not null } || req.FromDate != null || req.ToDate != null)
            {
                if (req.FromDate > req.ToDate)
                {
                    return new Return<bool>
                    {
                        IsSuccess = false,
                        Message = ErrorMessage.InvalidDate,
                        ErrorCode = ErrorCodes.InvalidDate
                    };
                }

                if (req.FromDate < DateTime.Now)
                {
                    return new Return<bool>
                    {
                        IsSuccess = false,
                        Message = ErrorMessage.InvalidDate,
                        ErrorCode = ErrorCodes.InvalidDate
                    };
                }

                if (req.FromDate < discount.Data.FromDate || req.ToDate > discount.Data.ToDate)
                {
                    return new Return<bool>
                    {
                        IsSuccess = false,
                        Message = ErrorMessage.InvalidDate,
                        ErrorCode = ErrorCodes.InvalidDate
                    };
                }
            }

            var existedCode = await discountRepository.GetDiscountCodeByCodeAsync(req.DiscountCode.ToUpper());
            if (existedCode is { IsSuccess: true, Data: not null })
            {
                return new Return<bool>
                {
                    IsSuccess = false,
                    Message = ErrorMessage.DiscountCodeAlreadyExists,
                    ErrorCode = ErrorCodes.DiscountCodeAlreadyExists
                };
            }

            var newCode = new DiscountCode
            {
                DiscountId = id,
                UserId = req.UserId,
                Code = req.DiscountCode.ToUpper().Trim(),
                FromDate = req.FromDate ?? DateTime.Today,
                ToDate = req.ToDate ?? discount.Data.ToDate,
                Status = req.Status != DiscountCodeStatus.Inactive
                    ? DiscountCodeStatus.Active
                    : DiscountCodeStatus.Inactive,
                CreatedAt = DateTime.Now,
                CreateById = currentUser.Data.UserId
            };

            var result = await discountRepository.AddDiscountCodeAsync(newCode);
            if (!result.IsSuccess || result.Data == null)
            {
                return new Return<bool>
                {
                    IsSuccess = false,
                    Message = result.Message,
                    InternalErrorMessage = result.InternalErrorMessage,
                    ErrorCode = result.ErrorCode
                };
            }

            return new Return<bool>
            {
                Data = true,
                IsSuccess = true,
                Message = SuccessMessage.Created,
                ErrorCode = ErrorCodes.Ok
            };
        }
        catch (Exception e)
        {
            return new Return<bool>
            {
                Data = false,
                IsSuccess = false,
                Message = ErrorMessage.InternalServerError,
                InternalErrorMessage = e,
                ErrorCode = ErrorCodes.InternalServerError
            };
        }
    }

    public async Task<Return<GetDiscountCodeByCodeResDto>> GetDiscounCodeByCodeAsync(string code)
    {
        try
        {
            var result = await discountRepository.GetDiscountCodeByCodeAsync(code.ToUpper());
            if (!result.IsSuccess)
            {
                return new Return<GetDiscountCodeByCodeResDto>
                {
                    IsSuccess = false,
                    Message = result.Message,
                    InternalErrorMessage = result.InternalErrorMessage,
                    ErrorCode = result.ErrorCode
                };
            }

            var res = result.Data != null
                ? new GetDiscountCodeByCodeResDto
                {
                    UserId = result.Data.UserId,
                    DiscountCode = result.Data.Code,
                    FromDate = result.Data.FromDate,
                    ToDate = result.Data.ToDate,
                    Status = result.Data.Status
                }
                : null;

            return new Return<GetDiscountCodeByCodeResDto>
            {
                Data = res,
                IsSuccess = true,
                Message = res != null ? SuccessMessage.Found : ErrorMessage.DiscountNotFound,
                TotalRecord = res != null ? 1 : 0,
                ErrorCode = res != null ? ErrorCodes.Ok : ErrorCodes.DiscountNotFound
            };
        }
        catch (Exception e)
        {
            return new Return<GetDiscountCodeByCodeResDto>
            {
                Data = null,
                IsSuccess = false,
                Message = ErrorMessage.InternalServerError,
                InternalErrorMessage = e,
                ErrorCode = ErrorCodes.InternalServerError
            };
        }
    }

    #endregion
}