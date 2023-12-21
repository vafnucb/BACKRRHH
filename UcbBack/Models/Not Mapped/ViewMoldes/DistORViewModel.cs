using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace UcbBack.Models.Not_Mapped.ViewMoldes
{
    [NotMapped]
    public class DistORViewModel
    {
        public long Id { set; get; }
        public string Document { get; set; }
        public string FirstSurName { get; set; }
        public string SecondSurName { get; set; }
        public string Names { get; set; }
        public string MariedSurName { get; set; }
        public int SegmentoOrigen { get; set; }
        public Decimal TotalNeto { get; set; }
        public string CUNI { get; set; }
        public string CCD1 { get; set; }
        public string CCD2 { get; set; }
        public string CCD3 { get; set; }
        public string CCD4 { get; set; }
        public int BranchesId { get; set; }        

    }
}