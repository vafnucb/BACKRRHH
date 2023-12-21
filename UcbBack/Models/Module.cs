using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    [CustomSchema("Module")]
    public class Module
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { set; get; }

        [MaxLength(50, ErrorMessage = "Cadena de texto muy grande")]
        [Required]
        public string Name { get; set; }

        [MaxLength(100, ErrorMessage = "Cadena de texto muy grande")]
        [Required]
        public string Icon { get; set; }

        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT \"" + CustomSchema.Schema + "\".\"rrhh_Module_sqs\".nextval FROM DUMMY;").ToList()[0];
        }
    }
}