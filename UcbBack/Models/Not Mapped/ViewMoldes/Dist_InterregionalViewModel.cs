using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models.Dist
{
    public class Dist_InterregionalViewModel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long Id { set; get; }
        public DateTime UploadedDate { get; set; }
        public int BranchesId { get; set; }
        public string Destino { get; set; }
        public int segmentoOrigen { get; set; }
        public string Origen { get; set; }
        public string mes { get; set; }
        public string gestion { get; set; }
        public string State { get; set; }
        public string TransNumber { get; set; }
        public string User { get; set; }
        public string FullName { get; set; }
        public string TransId { get; set; }

    }
}