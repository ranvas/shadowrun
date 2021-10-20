﻿using Core.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace Billing.Dto.Shop
{
    public class UserDto
    {
        public UserDto(SIN sin)
        {
            if (sin == null)
                return;
            Name = sin.Passport?.PersonName;
            if (sin.Character != null)
            {
                ModelId = sin.Character.Model;
                Id = sin.Character.Model;
            }
            else
            {
                ModelId = 0;
            }
            Balance = sin.Wallet?.Balance ?? 0;
            Eversion = sin.EVersion;
        }
        public int Id { get; set; }
        public string Name { get; set; }
        public int ModelId { get; set; }
        public decimal Balance { get; set; }
        public string Rights { get; set; }
        public string Eversion { get; set; }
    }
}
