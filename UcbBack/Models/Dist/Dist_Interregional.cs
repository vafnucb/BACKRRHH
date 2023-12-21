using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models.Dist
{
    [CustomSchema("Dist_Interregional")]
    public class Dist_Interregional
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long Id { set; get; }

        public DateTime UploadedDate { get; set; }

        public Branches Branches { get; set; }
        public int BranchesId { get; set; }

        public int segmentoOrigen { get; set; }

        public int User { get; set; }

        public string mes { get; set; }
        public string gestion { get; set; }
        public string State { get; set; }
        public string TransNumber { get; set; }
        public DateTime? RegisterDate { get; set; }

        public static int GetNextId(ApplicationDbContext _context)
        {
            return _context.Database.SqlQuery<int>("SELECT \"" + CustomSchema.Schema + "\".\"rrhh_Dist_Interregional_sqs\".nextval FROM DUMMY;").ToList()[0];
        }
    }
}