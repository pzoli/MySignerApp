using System;

namespace MySignerApp
{
    internal class ResponseMessage
    {
        public String action { get; set; }
        public String textDoc { get; set; }
        public String textSign { get; set; }
        public String pubkey { get; set; }

    }
}