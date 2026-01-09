using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    [CustomSchema("AsigContratos")]
    [Table("AsigContratos")]
    public class AsigContrato
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string NumeroContrato { get; set; }

        [Required]
        public int BranchesId { get; set; }

        public int? AsigProcesoId { get; set; }

        [MaxLength(20)]
        public string PeriodoId { get; set; }

        public decimal MontoTotal { get; set; }

        [MaxLength(20)]
        public string Estado { get; set; } // PENDIENTE / APROBADO / PAGADO

        [MaxLength(255)]
        public string Observaciones { get; set; }

        public DateTime? CreatedAt { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }

        // Navigation
        public virtual AsigProceso AsigProceso { get; set; }
    }
}