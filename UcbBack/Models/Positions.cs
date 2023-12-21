using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Security.AccessControl;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    [CustomSchema("Position")]
    public class Positions
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { set; get; }

        [MaxLength(50, ErrorMessage = "Cadena de texto muy grande")]
        [Required]
        public String Name { get; set; }

        [Required]
        public int LevelId { get; set; }
        public Level Level { get; set; }
        [Required]
        public int PerformanceAreaId { get; set; }
        public PerformanceArea PerformanceArea { get; set; }

        public bool IsDesignated { get; set; }
        public int? DefaultLinkage { get; set; }

        public String NameAbr { get; set; }

        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT \"" + CustomSchema.Schema + "\".\"rrhh_Position_sqs\".nextval FROM DUMMY;").ToList()[0];
        }
    }
}