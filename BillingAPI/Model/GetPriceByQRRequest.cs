using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BillingAPI.Model
{
    public class GetPriceByQRRequest: CharacterBasedRequest
    {

        public string Qr { get; set; }
    }
}
