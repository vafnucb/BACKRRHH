using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;
using System.Linq;

namespace UcbBack.Models
{
    [CustomSchema("Factura")]
    public class Factura
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public int RecordId { get; set; }
        public string ServiceType { get; set; }
        public string RazonSocial { get; set; }
        public string NIT { get; set; }
        public DateTime? FechaFactura { get; set; }
        public string NumeroFactura { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int? CreatedBy { get; set; }
        public string CodigoAutorizacion { get; set; }
        public decimal? Monto { get; set; }
        public string TipoFactura { get; set; }

        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT \"" + CustomSchema.Schema + "\".\"rrhh_Factura_sqs\".nextval FROM DUMMY;").ToList()[0];
        }
    }
}