﻿namespace SmE_CommerceModels.Models;

public partial class OrderItem
{
    public Guid OrderItemId { get; set; }

    public Guid? OrderId { get; set; }

    public Guid? ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal Price { get; set; }

    /// <summary>
    /// Values: active, inactive, deleted
    /// </summary>
    public string Status { get; set; } = null!;

    public virtual Order? Order { get; set; }

    public virtual Product? Product { get; set; }
}
