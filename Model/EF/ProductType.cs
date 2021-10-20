﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Core.Model
{
    [Table("product_type")]
    public class ProductType : BaseEntity
    {
        [Column("name")]
        public string Name { get; set; }
        [Column("discount_type")]
        public int DiscountType { get; set; }
    }
}
