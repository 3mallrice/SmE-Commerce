﻿using SmE_CommerceModels.ResponseDtos.Category;

namespace SmE_CommerceModels.ResponseDtos.Product
{
    public class GetProductsResDto
    {
        public Guid ProductId { get; set; }

        public string? ProductName { get; set; }

        public decimal ProductPrice { get; set; }

        public int ProductStock { get; set; }

        public string? ProductImage { get; set; }

        public List<CategoryResDto>? Categories { get; set; }
    }
}