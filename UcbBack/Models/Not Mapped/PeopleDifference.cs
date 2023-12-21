using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    public class PeopleDifference
    {
        public string NOMBRE_COMPLETO_ACTUAL { set; get; }
        public string NOMBRE_COMPLETO { set; get; }
        public string CUNI { set; get; }
        public int? COD_DEPENDENCIA { set; get; }
        public string DEPENDENCIA { set; get; }
        public int? COD_DEPENDENCIA_ANTERIOR { set; get; }
        public string DEPENDENCIA_ANTERIOR { set; get; }
        public string SEDE { set; get; }
        public string SEDE_ANTERIOR { set; get; }
        public string POSICION { set; get; }
        public string POSICION_ANTERIOR { set; get; }
        public string DEDICACION { set; get; }
        public string DEDICACION_ANTERIOR { set; get; }
        public string VINCULACION { set; get; }
        public string VINCULACION_ANTERIOR { set; get; }
        public string DESCRIPCION_DEL_CARGO { set; get; }
        public string DESCRIPCION_DEL_CARGO_ANTERIOR { set; get; }
        public string FECHA_INICIO { set; get; }
        public string FECHA_FIN { set; get; }
        public string CAUSA_BAJA_MOVILIDAD { set; get; }
        public string CAUSA_BAJA_MOVILIDAD_ANTERIOR { set; get; }
        public string OBSERVACION { set; get; }

    }
}