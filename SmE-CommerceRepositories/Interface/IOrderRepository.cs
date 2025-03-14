﻿using SmE_CommerceModels.Models;
using SmE_CommerceModels.ReturnResult;

namespace SmE_CommerceRepositories.Interface;

public interface IOrderRepository
{
    Task<Return<Order>> CreateOrderAsync(Order order);
    Task<Return<Order>> CustomerGetOrderByIdAsync(Guid orderId, Guid userId);
    Task<Return<List<Order>>> GetOrderByUserIdAsync(Guid userId);
}
