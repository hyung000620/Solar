﻿using Solar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atom.CA
{
    class cNearBySMS
    {
        public void sendSMS(string[] args)
        {
            AuctSms auctSms = new AuctSms();
            auctSms.NearBy();            
        }
    }
}
