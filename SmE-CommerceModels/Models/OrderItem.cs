﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SmE_CommerceModels.Models;

public class OrderItem
{
    [Key]
    [Column("orderItemId")]
    public Guid OrderItemId { get; set; }

    [Column("orderId")]
    public Guid OrderId { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("price")]
    [Precision(15, 0)]
    public decimal Price { get; set; }

    [Column("productVariantId")]
    public Guid? ProductVariantId { get; set; }

    [Column("productName")]
    [StringLength(100)]
    public string ProductName { get; set; } = null!;

    [Column("variantName")]
    [StringLength(100)]
    public string? VariantName { get; set; }

    [Column("productId")]
    public Guid ProductId { get; set; }

    [ForeignKey("OrderId")]
    [InverseProperty("OrderItems")]
    public virtual Order Order { get; set; } = null!;

    [ForeignKey("ProductId")]
    [InverseProperty("OrderItems")]
    public virtual Product Product { get; set; } = null!;

    [ForeignKey("ProductVariantId")]
    [InverseProperty("OrderItems")]
    public virtual ProductVariant? ProductVariant { get; set; }
}
