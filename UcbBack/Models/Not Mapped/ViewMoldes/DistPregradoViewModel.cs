using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace UcbBack.Models.Not_Mapped.ViewMoldes
{
    [NotMapped]
    public class DistPregradoViewModel
    {
        public long Id { set; get; }
        public string Document { get; set; }
        public string Names { get; set; }
        public string FirstSurName { get; set; }
        public string SecondSurName { get; set; }
        public string MariedSurName { get; set; }
        public Decimal TotalNeto { get; set; }
        public string Carrera { get; set; }
        public string CUNI { get; set; }
        public string Dependency { get; set; }
        public int BranchesId { get; set; }        

    }
}