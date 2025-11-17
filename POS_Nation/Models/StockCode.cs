using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace POS_Nation.Models
{
    public class StockCode
    {
        public string id { get; set; }
        public string code { get; set; }
        public string upcfull { get; set; }
        public string stocklevel { get; set; }
    }
}
