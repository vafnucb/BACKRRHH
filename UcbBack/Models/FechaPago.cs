using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    [CustomSchema("FechasPago")]
    [Table("FechasPago")]
    public class FechaPago
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int ProgramacionPagosId { get; set; }

        [Required]
        [Column("FechaPago")]
        public DateTime FechaPagos { get; set; }

        [Required]
        public int Orden { get; set; }

        public decimal PorcentajePorDefecto { get; set; }

        [MaxLength(100)]
        public string Descripcion { get; set; }

        public int? Mes { get; set; }
        public int? Anio { get; set; }


        // public virtual ProgramacionPago ProgramacionPago { get; set; }
    }
}