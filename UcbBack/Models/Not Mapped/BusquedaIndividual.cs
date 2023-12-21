using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Web;

namespace UcbBack.Models.Not_Mapped
{
    [NotMapped]
    public class BusquedaIndividual
    {
        public string Id { get; set; }
        public string PeopleId { get; set; }
        public string CUNI { get; set; }
        public string Documento { get; set; }
        public string Nombre { get; set; }
        public string Posicion { get; set; }
        public string Vinculacion { get; set; }
        public string Dependencia { get; set; }
        public string Regional { get; set; }
        public int BranchesId { get; set; }
        public string Status { get; set; }
        public string Estado { get; set; }
        public string Cargo { get; set; }
    }
}