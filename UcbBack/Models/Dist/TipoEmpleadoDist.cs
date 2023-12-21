using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models.Dist
{
    [CustomSchema("Dist_TipoEmpleado")]
    public class TipoEmpleadoDist
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { set; get; }
        public GrupoContable GrupoContable { get; set; }

        [Required]
        public int GrupoContableId { get; set; }

        [MaxLength(50, ErrorMessage = "Cadena de texto muy grande")]
        [Required]
        public string Name { get; set; }

        [MaxLength(50, ErrorMessage = "Cadena de texto muy grande")]
        [Required]
        public string Description { get; set; }

        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT \"" + CustomSchema.Schema + "\".\"rrhh_Dist_TipoEmpleado_sqs\".nextval FROM DUMMY;").ToList()[0];
        }
    }
}