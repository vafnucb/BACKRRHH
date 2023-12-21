using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UcbBack.Models.Not_Mapped.ViewMoldes
{
    [NotMapped]
    public class ContractDetailViewModel
    {
        public int Id { get; set; }
        public string CUNI { get; set; }
        public string Document { get; set; } 
        public string FullName { get; set; }
        public string Dependency { get; set; }
        public string DependencyCod { get; set; }
        public string Branches { get; set; }
        public int BranchesId { get; set; }
        public string Positions { get; set; }
        public string Dedication { get; set; }
        public string PositionDescription { get; set; }
        public string Linkage { get; set; }
        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:dd/MM/yyyy}")]
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Status { get; set; }
    }
}