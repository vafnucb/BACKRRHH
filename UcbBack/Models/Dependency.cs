using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    [CustomSchema("Dependency")]
    public class Dependency
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { set; get; }

        [Required(ErrorMessage = "Te olvidaste de {0}")]
        [MaxLength(10,ErrorMessage = "Cadena de texto muy grande")]
        public string Cod { get; set; }

        [Required(ErrorMessage = "Te olvidaste de {0}")]
        [MaxLength(150,ErrorMessage = "Cadena de texto muy grande")]
        public string Name { get; set; }

        public Dependency Parent { get; set; }

        [Required(ErrorMessage = "Te olvidaste de {0}")]
        [ForeignKey("Parent")]
        [Column("Parent")]
        public int? ParentId { get; set; }

        public OrganizationalUnit OrganizationalUnit { get; set; }
        [Required(ErrorMessage = "Te olvidaste de {0}")]
        public int OrganizationalUnitId { get; set; }

        [Required]
        public int BranchesId { get; set; }

        public bool Active { get; set; }

        public bool Academic { get; set; }

        public int PerformanceAreaId { get; set; }
        public PerformanceArea PerformanceArea { get; set; }


        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT \"" + CustomSchema.Schema + "\".\"rrhh_Dependency_sqs\".nextval FROM DUMMY;").ToList()[0];
        }
    }
}