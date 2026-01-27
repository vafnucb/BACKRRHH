using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    [CustomSchema("EjecucionPagos")]
    [Table("EjecucionPagos")]
    public class EjecucionPago
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int PagoProgramadoId { get; set; }

        [Required]
        [StringLength(50)]
        public string TipoDocente { get; set; }

        public decimal PorcentajeRetencion { get; set; }

        public decimal MontoRetencion { get; set; }

        public decimal MontoContrato { get; set; }

        public decimal MontoReal { get; set; }

        [Required]
        [StringLength(50)]
        public string Estado { get; set; }

        [StringLength(5000)]
        public string ObservacionesEjecucion { get; set; }

        [StringLength(5000)]
        public string MotivoRechazo { get; set; }

        public DateTime? FechaEnvio { get; set; }

        public DateTime? FechaAprobacion { get; set; }

        public int? AprobadoPor { get; set; }

        public DateTime? CreatedAt { get; set; }

        public int? CreatedBy { get; set; }

      


        /// TAX RETENTION RATES - MODIFY HERE IF LAWS CHANGE
        /// These percentages are based on Bolivian tax laws (as of 2026).
        /// To change retention rates:
        /// 1. Modify the return values in this method
        /// 2. Update both database and new calculations
        /// 3. Document the change with date and legal reference
        /// 
        /// Current Rates (Jan 2026):
        /// - INDEPENDIENTE_CON_FACTURA: 0% (teacher provides invoice)
        /// - INDEPENDIENTE_SIN_FACTURA: 13% (no invoice - UCB retains tax)
        /// - EXTRANJERO: 12% (foreign teacher - special rate)
 
    
        public static decimal GetPorcentajeRetencion(string tipoDocente)
        {
            // IMPORTANT: Modificar aqui los porcentajes si cambiaran
            switch (tipoDocente?.ToUpper())
            {
                case "INDEPENDIENTE_CON_FACTURA":
                    return 0M;      // 0% - Teacher provides invoice and pays own taxes

                case "INDEPENDIENTE_SIN_FACTURA":
                    return 16M;     // 13% - UCB retains and pays tax on behalf of teacher

                case "EXTRANJERO":
                    return 12.5M;     // 12% - Foreign teacher retention rate

                default:
                    return 0M;      // Default: no retention
            }
        }

  
        /// Calculate net amount after tax retention

        public static decimal CalculateMontoContrato(decimal montoBruto, string tipoDocente)
        {
            var porcentaje = GetPorcentajeRetencion(tipoDocente);
            var retencion = montoBruto * (porcentaje / 100M);
            return montoBruto - retencion;
        }

        /// <summary>
        /// Calculate retention amount

        public static decimal CalculateMontoRetencion(decimal montoBruto, string tipoDocente)
        {
            var porcentaje = GetPorcentajeRetencion(tipoDocente);
            return montoBruto * (porcentaje / 100M);
        }
    }
}