using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    public class AuxiliarPre
    {
        public int Id { set; get; }
        public string Name { get; set; }
        public string RegionalOrigen { get; set; }
        public string Regional { get; set; }
    }
}