﻿using SmE_CommerceModels.Models;
using SmE_CommerceModels.RequestDtos.Discount;
using SmE_CommerceModels.ReturnResult;
using Task = DocumentFormat.OpenXml.Office2021.DocumentTasks.Task;

namespace SmE_CommerceServices.Interface;

public interface IDiscountService
{
    Task<Return<bool>> AddDiscountAsync(AddDiscountReqDto discount);

    Task<Return<bool>> AddDiscountCodeAsync(Guid id, AddDiscountCodeReqDto req);
    Task<Return<DiscountCode>> GetDiscounCodeByCodeAsync(string code);
}