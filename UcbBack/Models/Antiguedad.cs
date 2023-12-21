using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Configuration;
using System.Linq;
using System.Web;
using UcbBack.Logic;
using UcbBack.Models.Auth;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    [CustomSchema("Antiguedad")]
    public class Antiguedad
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string CUNI { get; set; }
        public int ContractDetailId { get; set; }
        public bool Activo { get; set; }
    }
}