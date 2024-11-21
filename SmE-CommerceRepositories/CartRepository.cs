﻿using Microsoft.EntityFrameworkCore;
using SmE_CommerceModels.DBContext;
using SmE_CommerceModels.Enums;
using SmE_CommerceModels.Models;
using SmE_CommerceModels.ReturnResult;
using SmE_CommerceRepositories.Interface;

namespace SmE_CommerceRepositories;

public class CartRepository(SmECommerceContext defaultdbContext) : ICartRepository
{
    public async Task<Return<CartItem>> GetCartItemById(Guid cartId)
    {
        try
        {
            var cart = await defaultdbContext.CartItems
                .SingleOrDefaultAsync(x => x.CartItemId == cartId);

            return new Return<CartItem>
            {
                Data = cart,
                IsSuccess = true,
                TotalRecord = cart == null ? 0 : 1,
                Message = cart == null ? ErrorMessage.NotFoundCart :SuccessMessage.Found
            };
        }
        catch (Exception ex)
        {
            return new Return<CartItem>
            {
                Data = null,
                IsSuccess = false,
                Message = ErrorMessage.InternalServerError,
                InternalErrorMessage = ex
            };
        }
    }

    public async Task<Return<CartItem>> GetCartItemByProductIdAndUserId(Guid productId, Guid userId)
    {
        try
        {
            var cart = await defaultdbContext.CartItems
                .SingleOrDefaultAsync(x => x.ProductId == productId && x.UserId == userId);

            return new Return<CartItem>
            {
                Data = cart,
                IsSuccess = true,
                TotalRecord = cart == null ? 0 : 1,
                Message = cart == null ? ErrorMessage.NotFoundCart :SuccessMessage.Found
            };
        }
        catch (Exception ex)
        {
            return new Return<CartItem>
            {
                Data = null,
                IsSuccess = false,
                Message = ErrorMessage.InternalServerError,
                InternalErrorMessage = ex
            };
        }
    }

    public async Task<Return<List<CartItem>>> GetCartItemsByUserId(Guid userId, int? pageIndex, int? pageSize)
    {
        try
        {
            var query = defaultdbContext.CartItems
                .Where(x => x.UserId == userId)
                .AsQueryable();

            var totalRecords = await query.CountAsync();

            if (pageIndex != null && pageSize != null && pageIndex > 0 && pageSize > 0)
            {
                query = query
                    .Skip((pageIndex.Value - 1) * pageSize.Value)
                    .Take(pageSize.Value);
            }

            var carts = await query
                .Include(x => x.Product)
                .ToListAsync();

            return new Return<List<CartItem>>
            {
                Data = carts,
                IsSuccess = true,
                TotalRecord = totalRecords,
                Message = carts.Count == 0 ? ErrorMessage.NotFoundCart : SuccessMessage.Found
            };
        }
        catch (Exception ex)
        {
            return new Return<List<CartItem>>
            {
                Data = null,
                IsSuccess = false,
                Message = ErrorMessage.InternalServerError,
                InternalErrorMessage = ex
            };
        }
    }

    public async Task<Return<List<CartItem>>> GetCartItems()
    {
        try
        {
            var carts = await defaultdbContext.CartItems
                .Include(x => x.Product)
                .Include(x => x.User)
                .ToListAsync();

            return new Return<List<CartItem>>
            {
                Data = carts,
                IsSuccess = true,
                TotalRecord = carts.Count,
                Message = carts.Count == 0 ? ErrorMessage.NotFoundCart : SuccessMessage.Found
            };
        }
        catch (Exception ex)
        {
            return new Return<List<CartItem>>
            {
                Data = null,
                IsSuccess = false,
                Message = ErrorMessage.InternalServerError,
                InternalErrorMessage = ex
            };
        }
    }

    public async Task<Return<bool>> AddToCart(CartItem cartItem)
    {
        try
        {
            defaultdbContext.CartItems.Add(cartItem);
            await defaultdbContext.SaveChangesAsync();

            return new Return<bool>
            {
                Data = true,
                IsSuccess = true,
                Message = SuccessMessage.Successfully
            };
        }
        catch (Exception ex)
        {
            return new Return<bool>
            {
                Data = false,
                IsSuccess = false,
                Message = ErrorMessage.InternalServerError,
                InternalErrorMessage = ex
            };
        }
    }

    public async Task<Return<bool>> SyncCart(List<CartItem> carts)
    {
        try
        {
            defaultdbContext.CartItems.AddRange(carts);
            await defaultdbContext.SaveChangesAsync();

            return new Return<bool>
            {
                Data = true,
                IsSuccess = true,
                Message = SuccessMessage.Successfully
            };
        }
        catch (Exception ex)
        {
            return new Return<bool>
            {
                Data = false,
                IsSuccess = false,
                Message = ErrorMessage.InternalServerError,
                InternalErrorMessage = ex
            };
        }
    }

    public async Task<Return<bool>> UpdateCartItem(CartItem cart)
    {
        try
        {
            defaultdbContext.CartItems.Update(cart);
            await defaultdbContext.SaveChangesAsync();

            return new Return<bool>
            {
                Data = true,
                IsSuccess = true,
                Message = SuccessMessage.Updated
            };
        }
        catch (Exception ex)
        {
            return new Return<bool>
            {
                Data = false,
                IsSuccess = false,
                Message = ErrorMessage.InternalServerError,
                InternalErrorMessage = ex
            };
        }
    }

    public async Task<Return<bool>> DeleteCartItem(Guid cartId)
    {
        try
        {
            var cart = await defaultdbContext.CartItems
                .SingleOrDefaultAsync(x => x.CartItemId == cartId);

            if (cart == null)
            {
                return new Return<bool>
                {
                    Data = false,
                    IsSuccess = false,
                    Message = ErrorMessage.NotFoundCart
                };
            }

            defaultdbContext.CartItems.Remove(cart);
            await defaultdbContext.SaveChangesAsync();

            return new Return<bool>
            {
                Data = true,
                IsSuccess = true,
                Message = SuccessMessage.Deleted
            };
        }
        catch (Exception ex)
        {
            return new Return<bool>
            {
                Data = false,
                IsSuccess = false,
                Message = ErrorMessage.InternalServerError,
                InternalErrorMessage = ex
            };
        }
    }
}
