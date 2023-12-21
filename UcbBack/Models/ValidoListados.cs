using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    [CustomSchema("ValidoListados")]
    public class ValidoListados
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int ContractDetailId { set; get; }

        public string CUNI { get; set; }

        public int PeopleId { set; get; }
        public int Prioridad { set; get; }

    }
}