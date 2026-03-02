using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    [CustomSchema("PagosProgramados")]
    [Table("PagosProgramados")]
    public class PagoProgramado
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int AsignacionCargaId { get; set; }

        public int? FechaPagoId { get; set; }
        public int? ProgramacionPagosId { get; set; }

        [Required]
        public int MesPago { get; set; }

        [Required]
        public int AnioPago { get; set; }

        [Required]
        public decimal Monto { get; set; }

        public decimal? MontoOriginal { get; set; }

        public decimal? Porcentaje { get; set; }

        [MaxLength(20)]
        public string Estado { get; set; }

        public bool EsExcepcion { get; set; }

        [MaxLength(255)]
        public string Observaciones { get; set; }

        [StringLength(50)]
        public string TipoDocente { get; set; }

        public DateTime? FechaPagado { get; set; }

        public DateTime? CreatedAt { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }

        // public virtual AsignacionCarga AsignacionCarga { get; set; }
        // public virtual FechaPago FechaPago { get; set; }
    }
}