﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SmE_CommerceModels.Models;

public partial class CartItem
{
    [Key]
    [Column("cartItemId")]
    public Guid CartItemId { get; set; }

    [Column("userId")]
    public Guid UserId { get; set; }

    [Column("productVariantId")]
    public Guid ProductVariantId { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    /// <summary>
    /// Price of the product when added to the cart
    /// </summary>
    [Column("price")]
    [Precision(15, 0)]
    public decimal Price { get; set; }

    [ForeignKey("ProductVariantId")]
    [InverseProperty("CartItems")]
    public virtual ProductVariant ProductVariant { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("CartItems")]
    public virtual User User { get; set; } = null!;
}
