using System.Transactions;
using SmE_CommerceModels.Enums;
using SmE_CommerceModels.Models;
using SmE_CommerceModels.RequestDtos.Product;
using SmE_CommerceModels.ResponseDtos.Product;
using SmE_CommerceModels.ResponseDtos.Product.Manager;
using SmE_CommerceModels.ReturnResult;
using SmE_CommerceRepositories.Interface;
using SmE_CommerceServices.Interface;

namespace SmE_CommerceServices;

public class ProductService(IProductRepository productRepository, IHelperService helperService)
    : IProductService
{
    #region Product Category

    public async Task<Return<List<GetProductCategoryResDto>>> UpdateProductCategoryAsync(
        Guid productId,
        List<Guid> categoryIds
    )
    {
        using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        try
        {
            var currentUser = await helperService.GetCurrentUserWithRoleAsync(RoleEnum.Manager);
            if (!currentUser.IsSuccess || currentUser.Data == null)
                return new Return<List<GetProductCategoryResDto>>
                {
                    Data = null,
                    IsSuccess = false,
                    StatusCode = currentUser.StatusCode,
                    InternalErrorMessage = currentUser.InternalErrorMessage,
                };

            // Get current categories of the product
            var currentCategories = await productRepository.GetProductCategoriesAsync(productId);
            if (!currentCategories.IsSuccess)
                return new Return<List<GetProductCategoryResDto>>
                {
                    Data = null,
                    IsSuccess = false,
                    StatusCode = currentCategories.StatusCode,
                    InternalErrorMessage = currentCategories.InternalErrorMessage,
                };

            // Get current category ids
            var currentCategoryIds =
                currentCategories.Data?.Select(c => c.CategoryId).ToList() ?? [];

            // Get categories to add and remove
            var categoriesToAdd = categoryIds.Except(currentCategoryIds).ToList();
            var categoriesToRemove = currentCategoryIds.Except(categoryIds).ToList();

            // Add new categories
            if (categoriesToAdd.Count != 0)
            {
                var productCategories = categoriesToAdd
                    .Select(categoryId => new ProductCategory
                    {
                        ProductId = productId,
                        CategoryId = categoryId,
                    })
                    .ToList();

                var addResult = await productRepository.AddProductCategoriesAsync(
                    productCategories
                );
                if (!addResult.IsSuccess)
                    return new Return<List<GetProductCategoryResDto>>
                    {
                        Data = null,
                        IsSuccess = false,
                        StatusCode = addResult.StatusCode,
                    };
            }

            // Delete categories
            if (categoriesToRemove.Count != 0)
            {
                var deleteResult = await productRepository.DeleteProductCategoryAsync(
                    productId,
                    categoriesToRemove
                );
                if (!deleteResult.IsSuccess)
                    return new Return<List<GetProductCategoryResDto>>
                    {
                        Data = null,
                        IsSuccess = false,
                        StatusCode = deleteResult.StatusCode,
                        InternalErrorMessage = deleteResult.InternalErrorMessage,
                    };
            }

            // Get updated categories
            var updatedCategories = await productRepository.GetProductCategoriesAsync(productId);
            if (!updatedCategories.IsSuccess)
                return new Return<List<GetProductCategoryResDto>>
                {
                    Data = null,
                    IsSuccess = false,
                    StatusCode = updatedCategories.StatusCode,
                    InternalErrorMessage = updatedCategories.InternalErrorMessage,
                };

            transaction.Complete();
            return new Return<List<GetProductCategoryResDto>>
            {
                Data = updatedCategories
                    .Data?.Select(category => new GetProductCategoryResDto
                    {
                        CategoryId = category.CategoryId,
                        Name = category.Category.Name,
                    })
                    .ToList(),
                IsSuccess = true,
                StatusCode = ErrorCode.Ok,
            };
        }
        catch (Exception ex)
        {
            return new Return<List<GetProductCategoryResDto>>
            {
                Data = null,
                IsSuccess = false,
                StatusCode = ErrorCode.InternalServerError,
                InternalErrorMessage = ex,
            };
        }
    }

    #endregion

    #region Product Variation

    public async Task<Return<bool>> AddProductVariantAsync(
        Guid productId,
        List<AddProductVariantReqDto> req
    )
    {
        // Step 1: Check if the request is empty
        if (req.Count == 0)
            return new Return<bool>
            {
                Data = false,
                IsSuccess = false,
                StatusCode = ErrorCode.BadRequest,
            };

        // Step 2: Check if the request is valid
        foreach (
            var isValid in req.Select(IsProductVariantReqValidAsync)
                .Where(isValid => !isValid.Data || !isValid.IsSuccess)
        )
            return new Return<bool>
            {
                Data = false,
                IsSuccess = false,
                StatusCode = isValid.StatusCode,
                InternalErrorMessage = isValid.InternalErrorMessage,
            };

        using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        try
        {
            // Step 3: Check if the current user is a manager
            var currentUser = await helperService.GetCurrentUserWithRoleAsync(RoleEnum.Manager);
            if (!currentUser.IsSuccess || currentUser.Data == null)
                return new Return<bool>
                {
                    Data = false,
                    IsSuccess = false,
                    StatusCode = currentUser.StatusCode,
                    InternalErrorMessage = currentUser.InternalErrorMessage,
                };

            // Step 4: Check product
            var existingProduct = await productRepository.GetProductByIdForUpdateAsync(productId);
            if (
                !existingProduct.IsSuccess
                || existingProduct.Data == null
                || existingProduct.Data.Status == ProductStatus.Deleted
            )
                return new Return<bool>
                {
                    Data = false,
                    IsSuccess = false,
                    StatusCode = ErrorCode.ProductNotFound,
                    InternalErrorMessage = existingProduct.InternalErrorMessage,
                };

            var hasVariants = existingProduct.Data.HasVariant;
            var existingVariantsCount = existingProduct.Data.ProductVariants.Count;

            switch (hasVariants)
            {
                // If product has variants, ensure ProductVariant exists
                case true when existingVariantsCount <= 1:
                    return new Return<bool>
                    {
                        Data = false,
                        IsSuccess = false,
                        StatusCode = ErrorCode.DataInconsistency,
                    };

                // If product does not have variants, ensure at least two variants are added
                case false when req.Count < 2:
                    return new Return<bool>
                    {
                        Data = false,
                        IsSuccess = false,
                        StatusCode = ErrorCode.AtLeastTwoProductVariant,
                    };
            }

            // Step 5: Check VariantAttributes for uniformity and non-duplication
            List<Guid> expectedVariantNameIds; // Number and type of attributes
            var existingAttributeDicts = new List<Dictionary<Guid, string>>(); // Existing attributes

            if (hasVariants && existingVariantsCount > 0)
            {
                // Already has variants
                if (existingProduct.Data.ProductVariants.Count == 0)
                    return new Return<bool>
                    {
                        Data = false,
                        IsSuccess = false,
                        StatusCode = ErrorCode.DataInconsistency,
                    };

                var firstExistingVariant = existingProduct.Data.ProductVariants.First();
                expectedVariantNameIds = firstExistingVariant
                    .VariantAttributes.Select(va => va.VariantNameId)
                    .OrderBy(id => id)
                    .ToList();

                existingAttributeDicts.AddRange(
                    existingProduct.Data.ProductVariants.Select(variant =>
                        variant.VariantAttributes.ToDictionary(
                            va => va.VariantNameId,
                            va => va.Value
                        )
                    )
                );
            }
            else
            {
                // No variants yet
                if (req.First().VariantValues.Count == 0)
                    return new Return<bool>
                    {
                        Data = false,
                        IsSuccess = false,
                        StatusCode = ErrorCode.BadRequest,
                    };

                expectedVariantNameIds = req.First()
                    .VariantValues.Select(vv => vv.VariantNameId)
                    .OrderBy(id => id)
                    .ToList();
            }

            // Check VariantAttributes for uniformity and non-duplication
            var newAttributeDicts = new List<Dictionary<Guid, string>>(); // New attributes
            foreach (var variantReq in req)
            {
                var variantNameIds = variantReq
                    .VariantValues.Select(vv => vv.VariantNameId)
                    .OrderBy(id => id)
                    .ToList();

                // Check uniformity
                if (!variantNameIds.SequenceEqual(expectedVariantNameIds))
                    return new Return<bool>()
                    {
                        Data = false,
                        IsSuccess = false,
                        StatusCode = ErrorCode.DataInconsistency,
                    };

                // Check duplication
                var newAttributeDict = variantReq.VariantValues.ToDictionary(
                    vv => vv.VariantNameId,
                    vv => vv.VariantValue
                );

                // Duplicate with existing variants
                if (
                    existingAttributeDicts.Any(dict =>
                        dict.Count == newAttributeDict.Count
                        && dict.All(kv =>
                            newAttributeDict.ContainsKey(kv.Key)
                            && newAttributeDict[kv.Key] == kv.Value
                        )
                    )
                )
                    return new Return<bool>()
                    {
                        Data = false,
                        IsSuccess = false,
                        StatusCode = ErrorCode.ProductVariantAlreadyExists,
                    };

                // Duplicate with new variants
                if (
                    newAttributeDicts.Any(dict =>
                        dict.Count == newAttributeDict.Count
                        && dict.All(kv =>
                            newAttributeDict.ContainsKey(kv.Key)
                            && newAttributeDict[kv.Key] == kv.Value
                        )
                    )
                )
                    return new Return<bool>()
                    {
                        Data = false,
                        IsSuccess = false,
                        StatusCode = ErrorCode.BadRequest,
                    };

                newAttributeDicts.Add(newAttributeDict);
            }

            // Step 6: Add ProductVariants and VariantAttributes
            var currentUserId = currentUser.Data.UserId;
            var now = DateTime.Now;

            // Map product variants
            var productVariants = req.Select(x => new ProductVariant
                {
                    ProductId = productId,
                    Price = x.Price,
                    StockQuantity = x.StockQuantity,
                    VariantImage = x.VariantImage,
                    SoldQuantity = 0,
                    Status =
                        x.StockQuantity > 0
                            ? existingProduct.Data.Status == ProductStatus.Active
                                ? ProductStatus.Active
                                : ProductStatus.Inactive
                            : ProductStatus.OutOfStock,
                    VariantAttributes = x
                        .VariantValues.Select(v => new VariantAttribute
                        {
                            VariantNameId = v.VariantNameId,
                            Value = v.VariantValue,
                            CreatedById = currentUserId,
                            CreatedAt = now,
                        })
                        .ToList(),
                    CreateById = currentUserId,
                    CreatedAt = now,
                })
                .ToList();

            // Add product variants => Update existing product
            existingProduct.Data.StockQuantity += productVariants.Sum(x => x.StockQuantity);
            if (existingProduct.Data.StockQuantity < 0)
                return new Return<bool>
                {
                    Data = false,
                    IsSuccess = false,
                    StatusCode = ErrorCode.InvalidStockQuantity,
                };

            // Convert from ICollection to List to use AddRange
            var productVariantList =
                existingProduct.Data.ProductVariants as List<ProductVariant>
                ?? existingProduct.Data.ProductVariants.ToList();
            productVariantList.AddRange(productVariants);
            existingProduct.Data.ProductVariants = productVariantList;

            existingProduct.Data.HasVariant = true;
            existingProduct.Data.ModifiedById = currentUserId;
            existingProduct.Data.ModifiedAt = now;

            var updateProductResult = await productRepository.UpdateProductAsync(
                existingProduct.Data
            );

            if (!updateProductResult.IsSuccess || updateProductResult.Data is null)
                return new Return<bool>
                {
                    Data = false,
                    IsSuccess = false,
                    StatusCode = updateProductResult.StatusCode,
                    InternalErrorMessage = updateProductResult.InternalErrorMessage,
                };

            transaction.Complete();
            return new Return<bool>
            {
                Data = true,
                IsSuccess = true,
                StatusCode = ErrorCode.Ok,
                TotalRecord = productVariants.Count,
            };
        }
        catch (Exception ex)
        {
            return new Return<bool>
            {
                Data = false,
                IsSuccess = false,
                StatusCode = ErrorCode.InternalServerError,
                InternalErrorMessage = ex,
            };
        }
    }

    public async Task<Return<bool>> UpdateProductVariantAsync(
        Guid productId,
        Guid productVariantId,
        UpdateProductVariantReqDto req
    )
    {
        using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        try
        {
            // Step 1: Check if the current user is a manager
            var currentUser = await helperService.GetCurrentUserWithRoleAsync(RoleEnum.Manager);
            if (!currentUser.IsSuccess || currentUser.Data == null)
                return new Return<bool>
                {
                    Data = false,
                    IsSuccess = false,
                    StatusCode = currentUser.StatusCode,
                    InternalErrorMessage = currentUser.InternalErrorMessage,
                };

            // Step 2: Check product
            var existingProduct = await productRepository.GetProductByIdForUpdateAsync(productId);
            if (
                !existingProduct.IsSuccess
                || existingProduct.Data == null
                || existingProduct.Data.Status == ProductStatus.Deleted
            )
                return new Return<bool>
                {
                    Data = false,
                    IsSuccess = false,
                    StatusCode = ErrorCode.ProductNotFound,
                    InternalErrorMessage = existingProduct.InternalErrorMessage,
                };

            var existingVariants = existingProduct.Data.ProductVariants;
            if (!existingProduct.Data.HasVariant || existingVariants.Count == 0)
                return new Return<bool>
                {
                    Data = false,
                    IsSuccess = false,
                    StatusCode = ErrorCode.DataInconsistency,
                };

            // Step 3: Check ProductVariant to update;
            var variantToUpdate = existingVariants.FirstOrDefault(pv =>
                pv.ProductVariantId == productVariantId
            );
            if (variantToUpdate == null)
                return new Return<bool>
                {
                    Data = false,
                    IsSuccess = false,
                    StatusCode = ErrorCode.ProductVariantNotFound,
                };

            // Step 4: Compare data with DB to check for changes
            var hasChanges = false;
            var stockChange = 0;

            // Compare Price
            if (req.Price != variantToUpdate.Price)
            {
                hasChanges = true;
                variantToUpdate.Price = req.Price;
            }

            // Compare StockQuantity
            var newStockQuantity =
                req.StockQuantity != 0 ? req.StockQuantity : variantToUpdate.StockQuantity;
            if (newStockQuantity != variantToUpdate.StockQuantity)
            {
                hasChanges = true;
                stockChange = newStockQuantity - variantToUpdate.StockQuantity;
                variantToUpdate.StockQuantity = newStockQuantity;
            }

            // Compare VariantImage
            if (req.VariantImage != variantToUpdate.VariantImage)
            {
                hasChanges = true;
                variantToUpdate.VariantImage = req.VariantImage;
            }

            // Compare Status
            var newStatus = string.IsNullOrEmpty(req.Status) ? ProductStatus.Active : req.Status;
            if (newStatus != variantToUpdate.Status)
            {
                hasChanges = true;
                variantToUpdate.Status = newStatus;
            }

            // Compare VariantAttributes
            if (req.VariantValues != null)
            {
                var existingVariantNameIds = variantToUpdate
                    .VariantAttributes.Select(va => va.VariantNameId)
                    .OrderBy(id => id)
                    .ToList();
                var newVariantNameIds = req
                    .VariantValues.Select(vv => vv.VariantNameId)
                    .OrderBy(id => id)
                    .ToList();

                // Check if number or type of VariantAttributes changed
                if (
                    existingVariantNameIds.Count != newVariantNameIds.Count
                    || !existingVariantNameIds.SequenceEqual(newVariantNameIds)
                )
                    return new Return<bool>
                    {
                        Data = false,
                        IsSuccess = false,
                        StatusCode = ErrorCode.InvalidVariantAttributeStructure,
                    };

                // Compare values using dictionary
                var existingAttrDict = variantToUpdate.VariantAttributes.ToDictionary(
                    va => va.VariantNameId,
                    va => va.Value
                );
                var attributesChanged = false;

                foreach (var newValue in req.VariantValues)
                    if (existingAttrDict.TryGetValue(newValue.VariantNameId, out var existingValue))
                        if (existingValue != newValue.VariantValue)
                        {
                            attributesChanged = true;
                            var attrToUpdate = variantToUpdate.VariantAttributes.First(va =>
                                va.VariantNameId == newValue.VariantNameId
                            );
                            attrToUpdate.Value = newValue.VariantValue;
                            attrToUpdate.ModifiedById = currentUser.Data.UserId;
                            attrToUpdate.ModifiedAt = DateTime.UtcNow;
                        }

                if (attributesChanged)
                    hasChanges = true;
            }

            // If no changes, return without updating
            if (!hasChanges)
                return new Return<bool>
                {
                    Data = true,
                    IsSuccess = true,
                    StatusCode = ErrorCode.Ok,
                    TotalRecord = 0,
                };

            // Step 5: Update ProductVariant fields
            var currentUserId = currentUser.Data.UserId;
            var now = DateTime.Now;

            variantToUpdate.ModifiedById = currentUserId;
            variantToUpdate.ModifiedAt = now;

            // Step 6: Update product
            existingProduct.Data.StockQuantity += stockChange;
            if (existingProduct.Data.StockQuantity < 0)
                return new Return<bool>
                {
                    Data = false,
                    IsSuccess = false,
                    StatusCode = ErrorCode.InvalidStockQuantity,
                };

            existingProduct.Data.HasVariant = true;
            existingProduct.Data.ModifiedById = currentUserId;
            existingProduct.Data.ModifiedAt = now;

            var updateProductResult = await productRepository.UpdateProductAsync(
                existingProduct.Data
            );
            if (!updateProductResult.IsSuccess || updateProductResult.Data == null)
                return new Return<bool>
                {
                    Data = false,
                    IsSuccess = false,
                    StatusCode = updateProductResult.StatusCode,
                    InternalErrorMessage = updateProductResult.InternalErrorMessage,
                };

            transaction.Complete();
            return new Return<bool>
            {
                Data = true,
                IsSuccess = true,
                StatusCode = ErrorCode.Ok,
                TotalRecord = 1,
            };
        }
        catch (Exception ex)
        {
            return new Return<bool>
            {
                Data = false,
                IsSuccess = false,
                StatusCode = ErrorCode.InternalServerError,
                InternalErrorMessage = ex,
            };
        }
    }

    #endregion

    #region Product

    public async Task<Return<GetProductDetailsResDto>> CustomerGetProductDetailsAsync(
        Guid productId
    )
    {
        try
        {
            var result = await productRepository.GetProductByIdAsync(productId);
            if (!result.IsSuccess || result.Data is not { Status: ProductStatus.Active })
                return new Return<GetProductDetailsResDto>
                {
                    Data = null,
                    IsSuccess = false,
                    StatusCode = result.StatusCode,
                    InternalErrorMessage = result.InternalErrorMessage,
                };
            if (result.Data.Status != ProductStatus.Active)
                return new Return<GetProductDetailsResDto>
                {
                    Data = null,
                    IsSuccess = false,
                    StatusCode = ErrorCode.ProductNotFound,
                };

            // return new Return<GetProductDetailsResDto>
            // {
            //     Data = new GetProductDetailsResDto
            //     {
            //         ProductId = result.Data.ProductId,
            //         ProductCode = result.Data.ProductCode,
            //         Name = result.Data.Name,
            //         PrimaryImage = result.Data.PrimaryImage,
            //         Description = result.Data.Description,
            //         Price = result.Data.Price,
            //         StockQuantity = result.Data.StockQuantity,
            //         SoldQuantity = result.Data.SoldQuantity,
            //         IsTopSeller = result.Data.IsTopSeller,
            //         SeoMetadata = new SeoMetadata
            //         {
            //             Slug = result.Data.Slug,
            //             MetaTitle = result.Data.MetaTitle,
            //             MetaDescription = result.Data.MetaDescription,
            //         },
            //         Status = result.Data.Status,
            //         Images = result
            //             .Data.ProductImages.Select(image => new GetProductImageResDto
            //             {
            //                 ImageId = image.ImageId,
            //                 Url = image.Url,
            //                 AltText = image.AltText,
            //             })
            //             .ToList(),
            //         Categories = result
            //             .Data.ProductCategories.Select(category => new GetProductCategoryResDto
            //             {
            //                 CategoryId = category.CategoryId,
            //                 Name = category.Category.Name,
            //             })
            //             .ToList(),
            //         Attributes = result
            //             .Data.ProductAttributes.Select(attribute => new GetProductAttributeResDto
            //             {
            //                 AttributeId = attribute.AttributeId,
            //                 Name = attribute.AttributeName,
            //                 Value = attribute.AttributeValue,
            //             })
            //             .ToList(),
            //     },
            //     IsSuccess = true,
            //     StatusCode = ErrorCode.Ok,
            // };
            return null;
        }
        catch (Exception ex)
        {
            return new Return<GetProductDetailsResDto>
            {
                Data = null,
                IsSuccess = false,
                StatusCode = ErrorCode.InternalServerError,
                InternalErrorMessage = ex,
            };
        }
    }

    public async Task<Return<ManagerGetProductDetailResDto>> ManagerGetProductDetailsAsync(
        Guid productId
    )
    {
        try
        {
            // var currentManager = await helperService.GetCurrentUserWithRoleAsync(RoleEnum.Manager);
            // if (!currentManager.IsSuccess || currentManager.Data == null)
            //     return new Return<ManagerGetProductDetailResDto>
            //     {
            //         Data = null,
            //         IsSuccess = false,
            //         StatusCode = currentManager.StatusCode,
            //         InternalErrorMessage = currentManager.InternalErrorMessage,
            //     };
            //
            // var result = await productRepository.GetProductByIdAsync(productId);
            // if (!result.IsSuccess || result.Data == null)
            //     return new Return<ManagerGetProductDetailResDto>
            //     {
            //         Data = null,
            //         IsSuccess = false,
            //         StatusCode = ErrorCode.ProductNotFound,
            //     };
            //
            // return new Return<ManagerGetProductDetailResDto>
            // {
            //     Data = new ManagerGetProductDetailResDto
            //     {
            //         ProductId = result.Data.ProductId,
            //         ProductCode = result.Data.ProductCode,
            //         Name = result.Data.Name,
            //         PrimaryImage = result.Data.PrimaryImage,
            //         Description = result.Data.Description,
            //         Price = result.Data.Price,
            //         StockQuantity = result.Data.StockQuantity,
            //         SoldQuantity = result.Data.SoldQuantity,
            //         IsTopSeller = result.Data.IsTopSeller,
            //         SeoMetadata = new SeoMetadata
            //         {
            //             Slug = result.Data.Slug,
            //             MetaTitle = result.Data.MetaTitle,
            //             MetaDescription = result.Data.MetaDescription,
            //         },
            //         Status = result.Data.Status,
            //         Images = result
            //             .Data.ProductImages.Select(image => new GetProductImageResDto
            //             {
            //                 ImageId = image.ImageId,
            //                 Url = image.Url,
            //                 AltText = image.AltText,
            //             })
            //             .ToList(),
            //         Categories = result
            //             .Data.ProductCategories.Select(category => new GetProductCategoryResDto
            //             {
            //                 CategoryId = category.CategoryId,
            //                 Name = category.Category.Name,
            //             })
            //             .ToList(),
            //         Attributes = result
            //             .Data.ProductAttributes.Select(attribute => new GetProductAttributeResDto
            //             {
            //                 AttributeId = attribute.AttributeId,
            //                 Name = attribute.AttributeName,
            //                 Value = attribute.AttributeValue,
            //             })
            //             .ToList(),
            //         AuditMetadata = new AuditMetadata
            //         {
            //             CreatedById = result.Data.CreateById,
            //             CreatedAt = result.Data.CreatedAt,
            //             CreatedBy = result.Data.CreateBy?.FullName,
            //             ModifiedById = result.Data.ModifiedById,
            //             ModifiedAt = result.Data.ModifiedAt,
            //             ModifiedBy = result.Data.ModifiedBy?.FullName,
            //         },
            //     },
            //     IsSuccess = true,
            //     StatusCode = ErrorCode.Ok,
            // };
            return null;
        }
        catch (Exception ex)
        {
            return new Return<ManagerGetProductDetailResDto>
            {
                Data = null,
                IsSuccess = false,
                StatusCode = ErrorCode.InternalServerError,
                InternalErrorMessage = ex,
            };
        }
    }

    public async Task<Return<GetProductDetailsResDto>> AddProductAsync(AddProductReqDto req)
    {
        try
        {
            // Get the current user
            var currentUser = await helperService.GetCurrentUserWithRoleAsync(RoleEnum.Manager);
            if (!currentUser.IsSuccess || currentUser.Data == null)
                return new Return<GetProductDetailsResDto>
                {
                    Data = null,
                    IsSuccess = false,
                    StatusCode = currentUser.StatusCode,
                    InternalErrorMessage = currentUser.InternalErrorMessage,
                };

            // Check if the product data is valid
            if (req.Description == null || req.Price < 0 || req.StockQuantity < 0)
                return new Return<GetProductDetailsResDto>
                {
                    Data = null,
                    IsSuccess = false,
                    StatusCode = ErrorCode.BadRequest,
                };

            if (req.CategoryIds.Count == 0)
                return new Return<GetProductDetailsResDto>
                {
                    Data = null,
                    IsSuccess = false,
                    StatusCode = ErrorCode.BadRequest,
                };

            // Initialize the product
            var prdResult = await AddProductPrivateAsync(req, currentUser.Data.UserId);
            if (!prdResult.IsSuccess || prdResult.Data == null)
                return new Return<GetProductDetailsResDto>
                {
                    Data = null,
                    IsSuccess = false,
                    StatusCode = prdResult.StatusCode,
                };

            return new Return<GetProductDetailsResDto>
            {
                Data = prdResult.Data,
                IsSuccess = true,
                StatusCode = ErrorCode.Ok,
            };
        }
        catch (Exception ex)
        {
            return new Return<GetProductDetailsResDto>
            {
                Data = null,
                IsSuccess = false,
                StatusCode = ErrorCode.InternalServerError,
                InternalErrorMessage = ex,
            };
        }
    }

    public async Task<Return<GetProductDetailsResDto>> UpdateProductAsync(
        Guid productId,
        UpdateProductReqDto req
    )
    {
        using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        try
        {
            // // Get the current user
            // var currentUser = await helperService.GetCurrentUserWithRoleAsync(RoleEnum.Manager);
            // if (!currentUser.IsSuccess || currentUser.Data == null)
            //     return new Return<GetProductDetailsResDto>
            //     {
            //         Data = null,
            //         IsSuccess = false,
            //         StatusCode = currentUser.StatusCode,
            //         InternalErrorMessage = currentUser.InternalErrorMessage,
            //     };
            //
            // var productResult = await productRepository.GetProductByIdForUpdateAsync(productId);
            // if (!productResult.IsSuccess || productResult.Data == null)
            //     return new Return<GetProductDetailsResDto>
            //     {
            //         Data = null,
            //         IsSuccess = false,
            //         StatusCode = productResult.StatusCode,
            //         InternalErrorMessage = productResult.InternalErrorMessage,
            //     };
            //
            // // Check if the product is deleted
            // if (productResult.Data.Status == ProductStatus.Deleted)
            //     return new Return<GetProductDetailsResDto>
            //     {
            //         Data = null,
            //         IsSuccess = false,
            //         StatusCode = ErrorCode.ProductNotFound,
            //     };
            //
            // // Check if the product data is valid
            // if (req.Description == null || req.Price < 0 || req.StockQuantity < 0)
            //     return new Return<GetProductDetailsResDto>
            //     {
            //         Data = null,
            //         IsSuccess = false,
            //         StatusCode = ErrorCode.BadRequest,
            //     };
            //
            // // Check if the product name is unique except the current product
            // var exitedProductResult = await productRepository.GetProductByNameAsync(req.Name);
            // if (
            //     exitedProductResult is { IsSuccess: true, Data: not null }
            //     && exitedProductResult.Data.ProductId != productId
            // )
            //     return new Return<GetProductDetailsResDto>
            //     {
            //         Data = null,
            //         IsSuccess = false,
            //         StatusCode = ErrorCode.ProductNameAlreadyExists,
            //     };
            //
            // productResult.Data.Name = req.Name.Trim();
            // productResult.Data.Description = req.Description.Trim();
            // productResult.Data.Price = req.Price < 0 ? 0 : req.Price;
            // productResult.Data.StockQuantity = req.StockQuantity;
            // productResult.Data.IsTopSeller = req.IsTopSeller;
            // productResult.Data.Slug = SlugUtil.GenerateSlug(req.Name).Trim();
            // productResult.Data.MetaTitle = (req.MetaTitle ?? req.Name).Trim();
            // productResult.Data.MetaDescription = (req.MetaDescription ?? req.Description).Trim();
            // productResult.Data.Status =
            //     req.StockQuantity > 0
            //         ? req.Status != ProductStatus.Inactive
            //             ? ProductStatus.Active
            //             : ProductStatus.Inactive
            //         : ProductStatus.OutOfStock;
            // productResult.Data.ModifiedById = currentUser.Data.UserId;
            // productResult.Data.ModifiedAt = DateTime.Now;
            //
            // // Update Product in the database
            // var result = await productRepository.UpdateProductAsync(productResult.Data);
            // if (!result.IsSuccess || result.Data == null)
            //     return new Return<GetProductDetailsResDto>
            //     {
            //         Data = null,
            //         IsSuccess = false,
            //         StatusCode = result.StatusCode,
            //     };
            //
            // // Mark the transaction as complete
            // transaction.Complete();
            // return new Return<GetProductDetailsResDto>
            // {
            //     Data = new GetProductDetailsResDto
            //     {
            //         ProductId = result.Data.ProductId,
            //         ProductCode = result.Data.ProductCode,
            //         Name = result.Data.Name,
            //         PrimaryImage = result.Data.PrimaryImage,
            //         Description = result.Data.Description,
            //         Price = result.Data.Price,
            //         StockQuantity = result.Data.StockQuantity,
            //         SoldQuantity = result.Data.SoldQuantity,
            //         IsTopSeller = result.Data.IsTopSeller,
            //         Attributes = result
            //             .Data.ProductAttributes.Select(attribute => new GetProductAttributeResDto
            //             {
            //                 AttributeId = attribute.AttributeId,
            //                 Name = attribute.AttributeName,
            //                 Value = attribute.AttributeValue,
            //             })
            //             .ToList(),
            //         Categories = result
            //             .Data.ProductCategories.Select(category => new GetProductCategoryResDto
            //             {
            //                 CategoryId = category.CategoryId,
            //                 Name = category.Category.Name,
            //             })
            //             .ToList(),
            //         Images = result
            //             .Data.ProductImages.Select(image => new GetProductImageResDto
            //             {
            //                 ImageId = image.ImageId,
            //                 Url = image.Url,
            //                 AltText = image.AltText,
            //             })
            //             .ToList(),
            //         SeoMetadata = new SeoMetadata
            //         {
            //             Slug = result.Data.Slug,
            //             MetaTitle = result.Data.MetaTitle,
            //             MetaDescription = result.Data.MetaDescription,
            //         },
            //         Status = result.Data.Status,
            //     },
            //     IsSuccess = true,
            //     StatusCode = ErrorCode.Ok,
            // };
            return null;
        }
        catch (Exception ex)
        {
            return new Return<GetProductDetailsResDto>
            {
                Data = null,
                IsSuccess = false,
                StatusCode = ErrorCode.InternalServerError,
                InternalErrorMessage = ex,
            };
        }
    }

    public async Task<Return<bool>> DeleteProductAsync(Guid productId)
    {
        using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        try
        {
            // Get the current user
            var currentUser = await helperService.GetCurrentUserWithRoleAsync(RoleEnum.Manager);
            if (!currentUser.IsSuccess || currentUser.Data == null)
                return new Return<bool>
                {
                    Data = false,
                    IsSuccess = false,
                    StatusCode = currentUser.StatusCode,
                    InternalErrorMessage = currentUser.InternalErrorMessage,
                };

            // Get the product
            var product = await productRepository.GetProductByIdForUpdateAsync(productId);
            if (!product.IsSuccess || product.Data == null)
                return new Return<bool>
                {
                    Data = false,
                    IsSuccess = false,
                    StatusCode = product.StatusCode,
                    InternalErrorMessage = product.InternalErrorMessage,
                };
            // Check if the product is deleted
            if (product.Data.Status == ProductStatus.Deleted)
                return new Return<bool>
                {
                    Data = false,
                    IsSuccess = false,
                    StatusCode = ErrorCode.ProductNotFound,
                };

            // Update product status to deleted
            product.Data.Status = ProductStatus.Deleted;
            product.Data.ModifiedById = currentUser.Data.UserId;
            product.Data.ModifiedAt = DateTime.Now;

            var result = await productRepository.UpdateProductAsync(product.Data);
            if (!result.IsSuccess)
                return new Return<bool>
                {
                    Data = false,
                    IsSuccess = false,
                    StatusCode = result.StatusCode,
                    InternalErrorMessage = result.InternalErrorMessage,
                };

            transaction.Complete();
            return new Return<bool>
            {
                Data = true,
                IsSuccess = true,
                StatusCode = ErrorCode.Ok,
            };
        }
        catch (Exception ex)
        {
            return new Return<bool>
            {
                Data = false,
                IsSuccess = false,
                StatusCode = ErrorCode.InternalServerError,
                InternalErrorMessage = ex,
            };
        }
    }

    #endregion

    #region Product Image

    public async Task<Return<GetProductImageResDto>> AddProductImageAsync(
        Guid productId,
        AddProductImageReqDto req
    )
    {
        try
        {
            var currentUser = await helperService.GetCurrentUserWithRoleAsync(RoleEnum.Manager);
            if (!currentUser.IsSuccess || currentUser.Data == null)
                return new Return<GetProductImageResDto>
                {
                    Data = null,
                    IsSuccess = false,
                    StatusCode = currentUser.StatusCode,
                    InternalErrorMessage = currentUser.InternalErrorMessage,
                };

            var productImage = new ProductImage
            {
                ProductId = productId,
                Url = req.Url,
                AltText = req.AltText,
            };

            var result = await productRepository.AddProductImageAsync(productImage);
            if (!result.IsSuccess || result.Data == null)
                return new Return<GetProductImageResDto>
                {
                    Data = null,
                    IsSuccess = false,
                    StatusCode = result.StatusCode,
                    InternalErrorMessage = result.InternalErrorMessage,
                };

            return new Return<GetProductImageResDto>
            {
                Data = new GetProductImageResDto
                {
                    ImageId = result.Data.ImageId,
                    Url = result.Data.Url,
                    AltText = result.Data.AltText,
                },
                IsSuccess = true,
                StatusCode = ErrorCode.Ok,
            };
        }
        catch (Exception ex)
        {
            return new Return<GetProductImageResDto>
            {
                Data = null,
                IsSuccess = false,
                StatusCode = ErrorCode.InternalServerError,
                InternalErrorMessage = ex,
            };
        }
    }

    public async Task<Return<GetProductImageResDto>> UpdateProductImageAsync(
        Guid productId,
        Guid imageId,
        AddProductImageReqDto req
    )
    {
        try
        {
            var currentUser = await helperService.GetCurrentUserWithRoleAsync(RoleEnum.Manager);
            if (!currentUser.IsSuccess || currentUser.Data == null)
                return new Return<GetProductImageResDto>
                {
                    Data = null,
                    IsSuccess = false,
                    StatusCode = currentUser.StatusCode,
                    InternalErrorMessage = currentUser.InternalErrorMessage,
                };

            var productImageResult = await productRepository.GetProductImageByIdAsync(imageId);
            if (!productImageResult.IsSuccess || productImageResult.Data == null)
                return new Return<GetProductImageResDto>
                {
                    Data = null,
                    IsSuccess = false,
                    StatusCode = productImageResult.StatusCode,
                    InternalErrorMessage = productImageResult.InternalErrorMessage,
                };
            // Check if the image belongs to the product
            if (productImageResult.Data.ProductId != productId)
                return new Return<GetProductImageResDto>
                {
                    Data = null,
                    IsSuccess = false,
                    StatusCode = ErrorCode.ProductImageNotFound,
                };

            productImageResult.Data.Url = req.Url;
            productImageResult.Data.AltText = req.AltText;

            var result = await productRepository.UpdateProductImageAsync(productImageResult.Data);
            if (!result.IsSuccess || result.Data == null)
                return new Return<GetProductImageResDto>
                {
                    Data = null,
                    IsSuccess = false,
                    StatusCode = result.StatusCode,
                    InternalErrorMessage = result.InternalErrorMessage,
                };

            return new Return<GetProductImageResDto>
            {
                Data = new GetProductImageResDto
                {
                    ImageId = result.Data.ImageId,
                    Url = result.Data.Url,
                    AltText = result.Data.AltText,
                },
                IsSuccess = true,
                StatusCode = ErrorCode.Ok,
            };
        }
        catch (Exception ex)
        {
            return new Return<GetProductImageResDto>
            {
                Data = null,
                IsSuccess = false,
                StatusCode = ErrorCode.InternalServerError,
                InternalErrorMessage = ex,
            };
        }
    }

    public async Task<Return<bool>> DeleteProductImageAsync(Guid productId, Guid imageId)
    {
        try
        {
            var currentUser = await helperService.GetCurrentUserWithRoleAsync(RoleEnum.Manager);
            if (!currentUser.IsSuccess || currentUser.Data == null)
                return new Return<bool>
                {
                    Data = false,
                    IsSuccess = false,
                    StatusCode = currentUser.StatusCode,
                    InternalErrorMessage = currentUser.InternalErrorMessage,
                };

            var productImagesResult = await productRepository.GetProductImagesAsync(productId);
            if (!productImagesResult.IsSuccess || productImagesResult.Data == null)
                return new Return<bool>
                {
                    Data = false,
                    IsSuccess = false,
                    StatusCode = productImagesResult.StatusCode,
                    InternalErrorMessage = productImagesResult.InternalErrorMessage,
                };
            // Check if the image belongs to the product
            if (productImagesResult.Data.All(i => i.ImageId != imageId))
                return new Return<bool>
                {
                    Data = false,
                    IsSuccess = false,
                    StatusCode = ErrorCode.ProductImageNotFound,
                };
            // Check if only one image is left
            if (productImagesResult.Data.Count == 1)
                return new Return<bool>
                {
                    Data = false,
                    IsSuccess = false,
                    StatusCode = ErrorCode.ProductImageMinimum,
                };

            var result = await productRepository.DeleteProductImageAsync(imageId);
            if (!result.IsSuccess)
                return new Return<bool>
                {
                    Data = false,
                    IsSuccess = false,
                    StatusCode = result.StatusCode,
                    InternalErrorMessage = result.InternalErrorMessage,
                };

            return new Return<bool>
            {
                Data = true,
                IsSuccess = true,
                StatusCode = ErrorCode.Ok,
            };
        }
        catch (Exception ex)
        {
            return new Return<bool>
            {
                Data = false,
                IsSuccess = false,
                StatusCode = ErrorCode.InternalServerError,
                InternalErrorMessage = ex,
            };
        }
    }

    #endregion

    #region Product Attribute

    public async Task<Return<GetProductAttributeResDto>> AddProductAttributeAsync(
        Guid productId,
        AddProductAttributeReqDto req
    )
    {
        try
        {
            var currentUser = await helperService.GetCurrentUserWithRoleAsync(RoleEnum.Manager);
            if (!currentUser.IsSuccess || currentUser.Data == null)
                return new Return<GetProductAttributeResDto>
                {
                    Data = null,
                    IsSuccess = false,
                    StatusCode = currentUser.StatusCode,
                    InternalErrorMessage = currentUser.InternalErrorMessage,
                };

            var productAttribute = new ProductAttribute
            {
                ProductId = productId,
                AttributeName = req.AttributeName,
                AttributeValue = req.AttributeValue,
            };

            var result = await productRepository.AddProductAttributeAsync(productAttribute);
            if (!result.IsSuccess || result.Data == null)
                return new Return<GetProductAttributeResDto>
                {
                    Data = null,
                    IsSuccess = false,
                    StatusCode = result.StatusCode,
                    InternalErrorMessage = result.InternalErrorMessage,
                };

            return new Return<GetProductAttributeResDto>
            {
                Data = new GetProductAttributeResDto
                {
                    AttributeId = result.Data.AttributeId,
                    Name = result.Data.AttributeName,
                    Value = result.Data.AttributeValue,
                },
                IsSuccess = true,
                StatusCode = ErrorCode.Ok,
            };
        }
        catch (Exception ex)
        {
            return new Return<GetProductAttributeResDto>
            {
                Data = null,
                IsSuccess = false,
                StatusCode = ErrorCode.InternalServerError,
                InternalErrorMessage = ex,
            };
        }
    }

    public async Task<Return<GetProductAttributeResDto>> UpdateProductAttributeAsync(
        Guid productId,
        Guid attributeId,
        AddProductAttributeReqDto req
    )
    {
        try
        {
            var currentUser = await helperService.GetCurrentUserWithRoleAsync(RoleEnum.Manager);
            if (!currentUser.IsSuccess || currentUser.Data == null)
                return new Return<GetProductAttributeResDto>
                {
                    Data = null,
                    IsSuccess = false,
                    StatusCode = currentUser.StatusCode,
                    InternalErrorMessage = currentUser.InternalErrorMessage,
                };

            var productAttribute = new ProductAttribute
            {
                ProductId = productId,
                AttributeId = attributeId,
                AttributeName = req.AttributeName,
                AttributeValue = req.AttributeValue,
            };

            var result = await productRepository.UpdateProductAttributeAsync(productAttribute);
            if (!result.IsSuccess || result.Data == null)
                return new Return<GetProductAttributeResDto>
                {
                    Data = null,
                    IsSuccess = false,
                    StatusCode = result.StatusCode,
                    InternalErrorMessage = result.InternalErrorMessage,
                };

            return new Return<GetProductAttributeResDto>
            {
                Data = new GetProductAttributeResDto
                {
                    AttributeId = result.Data.AttributeId,
                    Name = result.Data.AttributeName,
                    Value = result.Data.AttributeValue,
                },
                IsSuccess = true,
                StatusCode = ErrorCode.Ok,
            };
        }
        catch (Exception ex)
        {
            return new Return<GetProductAttributeResDto>
            {
                Data = null,
                IsSuccess = false,
                StatusCode = ErrorCode.InternalServerError,
                InternalErrorMessage = ex,
            };
        }
    }

    public async Task<Return<bool>> DeleteProductAttributeAsync(Guid productId, Guid attributeId)
    {
        try
        {
            var currentUser = await helperService.GetCurrentUserWithRoleAsync(RoleEnum.Manager);
            if (!currentUser.IsSuccess || currentUser.Data == null)
                return new Return<bool>
                {
                    Data = false,
                    IsSuccess = false,
                    StatusCode = currentUser.StatusCode,
                    InternalErrorMessage = currentUser.InternalErrorMessage,
                };

            var productAttributeResult = await productRepository.GetProductAttributeByIdAsync(
                attributeId
            );
            if (!productAttributeResult.IsSuccess || productAttributeResult.Data == null)
                return new Return<bool>
                {
                    Data = false,
                    IsSuccess = false,
                    StatusCode = productAttributeResult.StatusCode,
                    InternalErrorMessage = productAttributeResult.InternalErrorMessage,
                };

            // Check if the attribute belongs to the product
            if (productAttributeResult.Data.ProductId != productId)
                return new Return<bool>
                {
                    Data = false,
                    IsSuccess = false,
                    StatusCode = ErrorCode.ProductAttributeNotFound,
                };

            var result = await productRepository.DeleteProductAttributeAsync(attributeId);
            if (!result.IsSuccess)
                return new Return<bool>
                {
                    Data = false,
                    IsSuccess = false,
                    StatusCode = result.StatusCode,
                    InternalErrorMessage = result.InternalErrorMessage,
                };

            return new Return<bool>
            {
                Data = true,
                IsSuccess = true,
                StatusCode = ErrorCode.Ok,
            };
        }
        catch (Exception ex)
        {
            return new Return<bool>
            {
                Data = false,
                IsSuccess = false,
                StatusCode = ErrorCode.InternalServerError,
                InternalErrorMessage = ex,
            };
        }
    }

    #endregion

    #region private methods

    private async Task<Return<GetProductDetailsResDto>> AddProductPrivateAsync(
        AddProductReqDto req,
        Guid currentUserId
    )
    {
        using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        try
        {
            // // Check if the product name is unique
            // var exitedProductResult = await productRepository.GetProductByNameAsync(req.Name);
            // if (exitedProductResult.IsSuccess)
            //     return new Return<GetProductDetailsResDto>
            //     {
            //         Data = null,
            //         IsSuccess = false,
            //         StatusCode = ErrorCode.ProductNameAlreadyExists,
            //     };
            //
            // // Only add active categories
            // var categoryResult = await categoryRepository.GetCategoriesAsync(
            //     status: GeneralStatus.Active,
            //     name: null,
            //     pageNumber: null,
            //     pageSize: null
            // );
            // if (!categoryResult.IsSuccess)
            //     return new Return<GetProductDetailsResDto>
            //     {
            //         Data = null,
            //         IsSuccess = false,
            //         StatusCode = categoryResult.StatusCode,
            //         InternalErrorMessage = categoryResult.InternalErrorMessage,
            //     };
            //
            // var categories = categoryResult
            //     .Data?.Where(c => req.CategoryIds.Contains(c.CategoryId))
            //     .ToList();
            // if (categories == null)
            //     return new Return<GetProductDetailsResDto>
            //     {
            //         Data = null,
            //         IsSuccess = false,
            //         StatusCode = ErrorCode.CategoryNotFound,
            //     };
            //
            // // Initialize the product
            // var product = new Product
            // {
            //     Name = req.Name.Trim(),
            //     PrimaryImage = req.PrimaryImage,
            //     Description = req.Description?.Trim(),
            //     Price = req.Price < 0 ? 0 : req.Price,
            //     StockQuantity = req.StockQuantity,
            //     SoldQuantity = 0,
            //     IsTopSeller = req.IsTopSeller,
            //     Slug = SlugUtil.GenerateSlug(req.Name).Trim(),
            //     MetaTitle = (req.MetaTitle ?? req.Name).Trim(),
            //     MetaDescription = (req.MetaDescription ?? req.Description ?? req.Name).Trim(),
            //     Status =
            //         req.StockQuantity > 0
            //             ? req.Status != ProductStatus.Inactive
            //                 ? ProductStatus.Active
            //                 : ProductStatus.Inactive
            //             : ProductStatus.OutOfStock,
            //     CreateById = currentUserId,
            //     CreatedAt = DateTime.Now,
            //     ProductCategories = categories
            //         .Select(c => new ProductCategory { CategoryId = c.CategoryId })
            //         .ToList(),
            //     ProductImages = req
            //         .Images.Select(i => new ProductImage { Url = i.Url, AltText = i.AltText })
            //         .ToList(),
            //     ProductAttributes = req
            //         .Attributes.Select(a => new ProductAttribute
            //         {
            //             AttributeName = a.AttributeName,
            //             AttributeValue = a.AttributeValue,
            //         })
            //         .ToList(),
            // };
            //
            // // Add Product to the database
            // var result = await productRepository.AddProductAsync(product);
            // if (!result.IsSuccess || result.Data == null)
            //     return new Return<GetProductDetailsResDto>
            //     {
            //         Data = null,
            //         IsSuccess = false,
            //         StatusCode = result.StatusCode,
            //         InternalErrorMessage = result.InternalErrorMessage,
            //     };
            //
            // transaction.Complete();
            //
            // return new Return<GetProductDetailsResDto>
            // {
            //     Data = new GetProductDetailsResDto
            //     {
            //         ProductId = product.ProductId,
            //         ProductCode = product.ProductCode,
            //         Name = product.Name,
            //         PrimaryImage = result.Data.PrimaryImage,
            //         Description = product.Description,
            //         Price = product.Price,
            //         StockQuantity = product.StockQuantity,
            //         SoldQuantity = product.SoldQuantity,
            //         IsTopSeller = product.IsTopSeller,
            //         SeoMetadata = new SeoMetadata
            //         {
            //             Slug = result.Data.Slug,
            //             MetaTitle = result.Data.MetaTitle,
            //             MetaDescription = result.Data.MetaDescription,
            //         },
            //         Status = product.Status,
            //         Images = result
            //             .Data.ProductImages.Select(image => new GetProductImageResDto
            //             {
            //                 ImageId = image.ImageId,
            //                 Url = image.Url,
            //                 AltText = image.AltText,
            //             })
            //             .ToList(),
            //         Categories = result
            //             .Data.ProductCategories.Select(category => new GetProductCategoryResDto
            //             {
            //                 CategoryId = category.CategoryId,
            //                 Name = category.Category.Name,
            //             })
            //             .ToList(),
            //         Attributes = result
            //             .Data.ProductAttributes.Select(attribute => new GetProductAttributeResDto
            //             {
            //                 AttributeId = attribute.AttributeId,
            //                 Name = attribute.AttributeName,
            //                 Value = attribute.AttributeValue,
            //             })
            //             .ToList(),
            //     },
            //     IsSuccess = true,
            //     StatusCode = ErrorCode.Ok,
            // };
            return null;
        }
        catch (Exception e)
        {
            return new Return<GetProductDetailsResDto>
            {
                Data = null,
                IsSuccess = false,
                StatusCode = ErrorCode.InternalServerError,
                InternalErrorMessage = e,
            };
        }
    }

    private static string GetVariantKey(IEnumerable<dynamic> values)
    {
        return string.Join(
            "-",
            values.OrderBy(v => v.VariantNameId).Select(v => v.Value ?? v.VariantValue)
        );
    }

    private static Return<bool> IsProductVariantReqValidAsync(AddProductVariantReqDto req)
    {
        if (req.VariantValues.Count == 0)
            return new Return<bool>
            {
                Data = false,
                IsSuccess = false,
                StatusCode = ErrorCode.BadRequest,
            };

        if (req.Price < 0 || req.StockQuantity < 0)
            return new Return<bool>
            {
                Data = false,
                IsSuccess = false,
                StatusCode = ErrorCode.BadRequest,
            };

        foreach (var value in req.VariantValues)
        {
            if (value.VariantNameId == Guid.Empty)
                return new Return<bool>
                {
                    Data = false,
                    IsSuccess = false,
                    StatusCode = ErrorCode.BadRequest,
                };

            if (string.IsNullOrWhiteSpace(value.VariantValue) || value.VariantValue.Length > 255)
                return new Return<bool>
                {
                    Data = false,
                    IsSuccess = false,
                    StatusCode = ErrorCode.BadRequest,
                };
        }

        return new Return<bool>
        {
            Data = true,
            IsSuccess = true,
            StatusCode = ErrorCode.Ok,
        };
    }

    #endregion
}
