using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace UcbBack.Models.Not_Mapped
{
    public class OPRJ
    {
        public string PrjCode { get; set; }
        public string PrjName { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public string U_Sucursal { get; set; }
        public string U_UOrganiza { get; set; }
        public int BranchesId { get; set; }
        public string U_PEI_PO { get; set; }
        public string U_Tipo { get; set; }
    }
}