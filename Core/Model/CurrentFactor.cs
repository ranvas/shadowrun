﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Core.Model
{
    [Table("current_factor")]
    public class CurrentFactor : BaseEntity
    {
        [ForeignKey("scoring_factor")]
        [Column("scoring_factor")]
        public int ScoringFactorId { get; set; }
        public ScoringFactor ScoringFactor { get; set; }
        [ForeignKey("current_category")]
        [Column("current_category")]
        public int CurrentCategoryId { get; set; }
        public CurrentCategory CurrentCategory { get; set; }
        [Column("current_value")]
        public decimal Value { get; set; }
    }
}
