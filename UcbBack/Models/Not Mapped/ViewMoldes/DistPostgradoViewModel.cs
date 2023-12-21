using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace UcbBack.Models.Not_Mapped.ViewMoldes
{
    [NotMapped]
    public class DistPostgradoViewModel
    {
        public long Id { set; get; }
        public string Document { get; set; }
        public string Names { get; set; }
        public string FirstSurName { get; set; }
        public string SecondSurName { get; set; }
        public string MariedSurName { get; set; }
        public Decimal TotalNeto { get; set; }
        public string PrjName { get; set; }
        public string PrjCode { get; set; }
        public string TipoTarea { get; set; }
        public string U_Tipo { get; set; }
        public string U_PEI_PO { get; set; }
        public string Version { get; set; }
        public string PeriodoAcademico { get; set; }
        public string CUNI { get; set; }
        public string Dependency { get; set; }
        public int BranchesId { get; set; }        

    }
}