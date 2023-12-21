using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    public class PeopleAtDate
    {
        public string DOCUMENTO { set; get; }
        public string NOMBRE_COMPLETO { get; set; }
        public string PRIMER_APELLIDO { get; set; }
        public string SEGUNDO_APELLIDO { get; set; }
        public string NOMBRES { get; set; }
        public string APELLIDO_DE_CASADA { get; set; }
        public string CUNI { get; set; }
        public int? COD_DEPENDENCIA { get; set; }
        public string DEPENDENCIA { get; set; }
        public int? COD_UO { get; set; }
        public string UNIDAD_ORGANIZACIONAL { get; set; }
        public string SEDE { get; set; }
        public string POSICION { get; set; }
        public string DEDICACION { get; set; }
        public string VINCULACION { get; set; }
        public string FECHA_INICIO { get; set; }
        public string FECHA_FIN { get; set; }
        public string FECHA_NACIMIENTO { get; set; }
        public int? EDAD { get; set; }
        public string TIPO_DE_DOCUMENTO { get; set; }
        public string EXTENSION { get; set; }
        public string GENERO { get; set; }
        public string EMAIL_INSTITUCIONAL { get; set; }
        public string INTERINATO { get; set; }
        public string DESCRIPCION_DEL_CARGO { get; set; }
        public string TELEFONO { get; set; }
    }
}