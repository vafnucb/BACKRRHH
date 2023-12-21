using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    public class ContractData
    {
        public int Id { set; get; }
        public string Regional { get; set; }
        public string Dependencia { get; set; }
        public string Posicion { get; set; }
        public string Vinculacion { get; set; }
        public string Dedicacion { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
    }
}