using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    public class PeopleData
    {
        public int Id { set; get; }
        public string CUNI { get; set; }
        public string TipoDocumento { get; set; }
        public string Documento { get; set; }
        public string Ext { get; set; }
        public string Nombres { get; set; }
        public string PrimerApellido { get; set; }
        public string SegundoApellido { get; set; }
        public string ApellidoCasada { get; set; }
        public DateTime? FechaNacimiento { get; set; }
        public string Genero { get; set; }
        public string Nacionalidad { get; set; }
        public string EmailPersonal { get; set; }
        public string EmailUCB { get; set; }
        public string AFP { get; set; }
        public string NUA { get; set; }
        public string Edad { get; set; }
        public string Seguro { get; set; }
    }
}