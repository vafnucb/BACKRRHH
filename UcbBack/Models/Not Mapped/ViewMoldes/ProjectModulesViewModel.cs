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
    [CustomSchema("ProjectModules")]
    public class ProjectModulesViewModel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public string CodProject { get; set; }
        public string PrjAbr { get; set; }
        public string CodModule { get; set; }
        public string NameModule { get; set; }
        public string TeacherFullName { get; set; }
        public string TeacherCI { get; set; }
        public string SocioNegocio { get; set; }
        public int BranchesId { get; set; }
        public decimal? Horas { get; set; }
        public decimal? MontoHora { get; set; }
        public string Observaciones { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string Cod { get; set; }
        public string Name { get; set; }
        public string U_UORGANIZA { get; set; }
        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT \"" + CustomSchema.Schema + "\".\"rrhh_ProjectModules_sqs\".nextval FROM DUMMY;").ToList()[0];
        }
    }
}