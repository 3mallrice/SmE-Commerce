﻿using SmE_CommerceModels.Models;
using SmE_CommerceModels.ReturnResult;

namespace SmE_CommerceRepositories.Interface;

public interface IOrderRepository
{
    Task<Return<bool>> CreateOrder(Order order);
}