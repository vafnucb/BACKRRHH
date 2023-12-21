using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UcbBack.Models.Not_Mapped.ViewMoldes
{
    [NotMapped]
    public class FechaIngresoAux
    {
        public int Id { get; set; }
        public string CUNI { get; set; }
        public string FullName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string StartDateStr { get; set; }
        public string EndDateStr { get; set; }
        public int BranchesId { get; set; }
    }
}