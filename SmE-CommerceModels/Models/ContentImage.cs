﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmE_CommerceModels.Models;

public class ContentImage
{
    [Key]
    [Column("contentImageId")]
    public Guid ContentImageId { get; set; }

    [Column("contentId")]
    public Guid ContentId { get; set; }

    [Column("imageUrl")]
    [StringLength(255)]
    public string ImageUrl { get; set; } = null!;

    [Column("altText")]
    [StringLength(255)]
    public string? AltText { get; set; }

    [ForeignKey("ContentId")]
    [InverseProperty("ContentImages")]
    public virtual Content Content { get; set; } = null!;
}
