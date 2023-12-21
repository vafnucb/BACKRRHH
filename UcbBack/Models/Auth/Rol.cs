using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models.Auth
{
    [CustomSchema("Rol")]
    public class Rol
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        [Required]
        [MaxLength(50, ErrorMessage = "Cadena de texto muy grande")]
        public string Name { get; set; }

        [Required]
        public int Level { get; set; }

        [Required]
        [MaxLength(100, ErrorMessage = "Cadena de texto muy grande")]
        public string ADGroupName { get; set; }

        public Resource Resource { get; set; }
        public int ResourceId { get; set; }

        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT \"" + CustomSchema.Schema + "\".\"rrhh_Rol_sqs\".nextval FROM DUMMY;").ToList()[0];
        }
    }
}