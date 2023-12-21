using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace UcbBack.Models.Not_Mapped.ViewMoldes
{
    public class AsesoriaTeachers
    {
        public string CUNI { get; set; }
        public string FullName { get; set; }
        public int BranchesId { get; set; }
        public string TipoPago { get; set; }
        public decimal Precio { get; set; }
        public string Categoria { get; set; }
        public string Regional { get; set; }
    }
}
