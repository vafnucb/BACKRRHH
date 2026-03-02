using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    [CustomSchema("ProgramacionPagos")]
    [Table("ProgramacionPagos")]
    public class ProgramacionPago
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int BranchesId { get; set; }

        [Required]
        [MaxLength(20)]
        public string PeriodoId { get; set; }

        [MaxLength(100)]
        public string NombrePlantilla { get; set; }

        [MaxLength(255)]
        public string Descripcion { get; set; }

        [MaxLength(20)]
        public string Estado { get; set; }

        public bool EsPlantilla { get; set; }

        public int TotalContratos { get; set; }

        public decimal MontoTotal { get; set; }

        public DateTime? CreatedAt { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }

 
        // public virtual ICollection<FechaPago> FechasPago { get; set; }
    }
}