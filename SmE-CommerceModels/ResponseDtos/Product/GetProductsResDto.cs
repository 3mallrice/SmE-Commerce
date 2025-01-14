﻿namespace SmE_CommerceModels.ResponseDtos.Product;

public class GetProductsResDto
{
    public Guid ProductId { get; set; }

    public string? ProductName { get; set; }

    public decimal ProductPrice { get; set; }

    public int ProductStock { get; set; }

    public string? PrimaryImage { get; set; }

    public string? ProductSlug { get; set; }

    public List<Dictionary<Guid, string>>? Categories { get; set; }
}
