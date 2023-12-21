using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;
using System.Data;
using System.Reflection;

namespace UcbBack.Models
{
    [CustomSchema("AsesoriaDocente")]
    public class AsesoriaDocente
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public string Carrera { get; set; }
        public string TeacherFullName { get; set; }
        public string TeacherCUNI { get; set; }
        public string TeacherBP { get; set; }
        public string Categoría { get; set; }
        public int? ModalidadId { get; set; }
        public int? TipoTareaId { get; set; }
        public int BranchesId { get; set; }
        public string DependencyCod { get; set; }
        public string Acta { get; set; }
        public DateTime? ActaFecha { get; set; }
        public int Horas { get; set; }
        public decimal MontoHora { get; set; }
        public decimal TotalNeto { get; set; }
        public decimal TotalBruto { get; set; }
        public string StudentFullName { get; set; }
        public int Mes { get; set; }
        public int Gestion { get; set; }
        public string Observaciones { get; set; }
        public string Estado { get; set; }
        public string Origen { get; set; }
        public decimal? Deduccion { get; set; }
        public decimal? IUE { get; set; }
        public decimal? IT { get; set; }
        public bool? Ignore { get; set; }
        public bool? Factura { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ToAuthAt { get; set; }
        public int? UserCreate { get; set; }
        public int? UserUpdate { get; set; }
        public int? UserAuth { get; set; }

        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT \"" + CustomSchema.Schema + "\".\"rrhh_Asesoria_sqs\".nextval FROM DUMMY;").ToList()[0];
        }
    }
}