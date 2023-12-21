using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    [CustomSchema("Branches")]
    public class Branches
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { set; get; }

        [MaxLength(20, ErrorMessage = "Cadena de texto muy grande")]
        [Required]
        public string Name { get; set; }

        [MaxLength(10, ErrorMessage = "Cadena de texto muy grande")]
        [Required]
        public string Abr { get; set; }

        [Required]
        [MaxLength(100, ErrorMessage = "Cadena de texto muy grande")]
        public string ADGroupName { get; set; }

        [Required]
        [MaxLength(100, ErrorMessage = "Cadena de texto muy grande")]
        public string ADOUName { get; set; }

        [Required]
        [MaxLength(100, ErrorMessage = "Cadena de texto muy grande")]
        public string SerieComprobanteContalbeSAP { get; set; }
        
        [Required]
        [MaxLength(100, ErrorMessage = "Cadena de texto muy grande")]
        public string InitialsInterRegional { get; set; }

        [Required]
        [MaxLength(100, ErrorMessage = "Cadena de texto muy grande")]
        public string SocioGenericDerechosLaborales { get; set; }

        public Dependency Dependency { get; set; }

        public int? DependencyId { get; set; }

        public string CodigoSAP { get; set; }

        public string InicialSN { get; set; }

        public string CuentaSociosRCUNI { get; set; }
        public string CuentaSociosHCUNI { get; set; }
        public string GroupCodeSocioNegocio { get; set; }
        public string VatGroup { get; set; }

        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT " + CustomSchema.Schema + ".\"rrhh_Branches_sqs\".nextval FROM DUMMY;").ToList()[0];
        }

    }
}