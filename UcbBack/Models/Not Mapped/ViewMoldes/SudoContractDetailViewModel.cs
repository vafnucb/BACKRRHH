using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UcbBack.Models.Not_Mapped.ViewMoldes
{
    [NotMapped]
    public class SudoContractDetailViewModel
    {
        public int Id { get; set; }
        public string CUNI { get; set; }
        public string Document { get; set; }
        public string FullName { get; set; }
        public int DependencyId { get; set; }
        public string Branches { get; set; }
        public int BranchesId { get; set; }
        public int PositionsId { get; set; }
        public string Dedication { get; set; }
        public string PositionDescription { get; set; }
        public int Linkage { get; set; }
        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:dd/MM/yyyy}")]
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:dd/MM/yyyy}")]
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? Cause { get; set; }
        public bool Active { get; set; }
        public bool AI { get; set; }
        public string NumGestion { get; set; }
        public string Respaldo { get; set; }
        public string Seguimiento { get; set; }
        public string Comunicado { get; set; }
        public DateTime? EndDateNombramiento { get; set; }
    }
}