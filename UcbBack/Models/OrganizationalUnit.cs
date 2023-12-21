using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    [CustomSchema("OrganizationalUnit")]
    public class OrganizationalUnit
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { set; get; }

        [MaxLength(10, ErrorMessage = "Cadena de texto muy grande")]
        [Required]
        public string Cod { get; set; }

        [MaxLength(150, ErrorMessage = "Cadena de texto muy grande")]
        [Required]
        public string Name { get; set; }

        public bool Active { get; set; }

        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT \"" + CustomSchema.Schema + "\".\"rrhh_OrganizationalUnit_sqs\".nextval FROM DUMMY;").ToList()[0];
        }
    }
}