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


        // Corregir el método GetPorcentajeRetencion
       // Helper: Always round to 2 decimals
        private static decimal Round2(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        // Calculate IUE (13%)
        public static decimal CalculateMontoIUE(decimal montoBruto, string tipoDocente)
        {
            if (tipoDocente?.ToUpper() == "INDEPENDIENTE_SIN_FACTURA")
                return Round2(montoBruto * 0.13m);
            return 0.00m;
        }

        // Calculate IT (3%)
        public static decimal CalculateMontoIT(decimal montoBruto, string tipoDocente)
        {
            if (tipoDocente?.ToUpper() == "INDEPENDIENTE_SIN_FACTURA")
                return Round2(montoBruto * 0.03m);
            return 0.00m;
        }

        // Calculate IUE Exterior (12.5%)
        public static decimal CalculateIUEExterior(decimal montoBruto, string tipoDocente)
        {
            if (tipoDocente?.ToUpper() == "EXTRANJERO")
                return Round2(montoBruto * 0.125m);
            return 0.00m;
        }

        // Calculate TOTAL retention = IUE + IT + IUEExterior
        public static decimal CalculateMontoRetencion(decimal montoBruto, string tipoDocente)
        {
            decimal iue = CalculateMontoIUE(montoBruto, tipoDocente);
            decimal it = CalculateMontoIT(montoBruto, tipoDocente);
            decimal iueExterior = CalculateIUEExterior(montoBruto, tipoDocente);

            return Round2(iue + it + iueExterior);
        }

        // Calculate NET amount (Bruto - Retention)
        public static decimal CalculateMontoContrato(decimal montoBruto, string tipoDocente)
        {
            decimal retencion = CalculateMontoRetencion(montoBruto, tipoDocente);
            return Round2(montoBruto - retencion);
        }

        // Get percentage for display
        public static decimal GetPorcentajeRetencion(string tipoDocente)
        {
            switch (tipoDocente?.ToUpper())
            {
                case "INDEPENDIENTE_SIN_FACTURA":
                    return 16.00m; // 13% + 3%
                case "INDEPENDIENTE_CON_FACTURA":
                    return 0.00m;
                case "EXTRANJERO":
                    return 12.50m;
                default:
                    return 0.00m;
            }
        }


    }
}