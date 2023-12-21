using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models.Dist
{
    [CustomSchema("Dist_OR")]
    public class Dist_OR
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long Id { set; get; }
        public string Document { set; get; }
        public string Names { get; set; }
        public string FirstSurName { get; set; }
        public string SecondSurName { get; set; }
        public string MariedSurName { get; set; }
        public string segmento { set; get; }
        public decimal TotalGanado { set; get; }
        public string CUNI { set; get; }
        public string Dependency { set; get; }
        public string PEI { set; get; }
        public string PlanEstudios { set; get; }
        public string Paralelo { set; get; }
        public string Periodo { set; get; }
        public string Project { set; get; }

        public decimal Porcentaje { get; set; }
        public string segmentoOrigen { get; set; }
        [StringLength(2)]
        public string mes { get; set; }
        [StringLength(4)]
        public string gestion { get; set; }

        public Dist_File DistFile { get; set; }
        public long DistFileId { get; set; }

        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT \"" + CustomSchema.Schema + "\".\"rrhh_Dist_OR_sqs\".nextval FROM DUMMY;").ToList()[0];
        }
    }
}