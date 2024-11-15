﻿using System.ComponentModel.DataAnnotations;
using SmE_CommerceModels.Enums;

namespace SmE_CommerceModels.RequestDtos.Product;

public class UpdateProductReqDto
{
    [Required(ErrorMessage = "Product code is required")]
    public required string ProductCode { get; set; }

    [Required(ErrorMessage = "Product name is required")]
    public required string Name { get; set; }

    public string? Description { get; set; }

    [Required(ErrorMessage = "Product price is required")]
    public decimal Price { get; set; }

    public int StockQuantity { get; set; } = 0;

    public int SoldQuantity { get; set; } = 0;

    public bool IsTopSeller { get; set; } = false;

    public string Status { get; set; } = ProductStatus.Active;

    public string? Slug { get; set; }

    public string? MetaTitle { get; set; }

    public string? MetaDescription { get; set; }

    public List<string>? MetaKeywords { get; set; }
}
