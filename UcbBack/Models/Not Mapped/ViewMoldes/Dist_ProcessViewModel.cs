using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models.Dist
{
    [CustomSchema("Dist_Process")]
    public class Dist_ProcessViewModel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long Id { set; get; }

        public DateTime UploadedDate { get; set; }

        public string Branches { get; set; }
        public int BranchesId { get; set; }

        public string mes { get; set; }
        public string gestion { get; set; }
        public string State { get; set; }
        public string Name { get; set; }
        public string FileType { get; set; }
        public string ComprobanteSAP { get; set; }
        public DateTime? RegisterDate { get; set; }

        
    }
}