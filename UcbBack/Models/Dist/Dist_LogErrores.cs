using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models.Dist
{
    [CustomSchema("Dist_LogErrores")]
    public class Dist_LogErrores
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long Id { set; get; }

        public int UserId { get; set; }
        public int DistProcessId { get; set; }
        public string CUNI { get; set; }
        public Error Error { get; set; }
        public int ErrorId { get; set; }
        public string Archivos { get; set; }
        public bool Inspected { get; set; }
    }
}