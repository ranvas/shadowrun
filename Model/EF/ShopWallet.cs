﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Core.Model
{
    [Table("shop_wallet")]
    public class ShopWallet: BaseEntity
    {
        [ForeignKey("wallet")]
        [Column("wallet")]
        public int? WalletId { get; set; }
        public virtual Wallet Wallet { get; set; }
        [Column("name")]
        public string Name { get; set; }
        [Column("lifestyle")]
        public int LifeStyle { get; set; }
        public virtual List<Specialisation> Specialisations { get; set; }
    }
}
