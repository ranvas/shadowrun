﻿using System;
using System.Collections.Generic;
using System.Text;

namespace PubSubService.Model
{
    public class BaseLocationModel : BasePubSubModel
    {
        public string CharacterId { get; set; }
        public Location Location { get; set; }
    }

    public class Location
    {
        public int ManaLevel { get; set; }
        public int Id { get; set; }
    }
}
