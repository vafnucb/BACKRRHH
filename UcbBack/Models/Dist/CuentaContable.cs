using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models.Dist
{
    [CustomSchema("CuentasContables")]
    public class CuentaContable
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { set; get; }

        public GrupoContable GrupoContable { get; set; }

        [Required]
        public int GrupoContableId { get; set; }

        [MaxLength(50, ErrorMessage = "Cadena de texto muy grande")]
        [Required]
        public string Concept { get; set; }
        public Branches Branches { get; set; }

        [Required]
        public int BranchesId { get; set; }

        [MaxLength(50, ErrorMessage = "Cadena de texto muy grande")]
        [Required]
        public string Name { get; set; }

        [MaxLength(50, ErrorMessage = "Cadena de texto muy grande")]
        [Required]
        public string Indicator { get; set; }

        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT " + CustomSchema.Schema + ".\"rrhh_CuentasContables_sqs\".nextval FROM DUMMY;").ToList()[0];
        }
    }
}