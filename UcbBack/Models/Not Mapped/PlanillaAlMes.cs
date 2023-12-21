using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using UcbBack.Models.Not_Mapped.CustomDataAnnotations;

namespace UcbBack.Models
{
    public class PlanillaAlMes
    {
        public string SEDE { set; get; }
        public string DOCUMENTO_DE_IDENTIDAD { set; get; }
        public string APELLIDOS_Y_NOMBRES { set; get; }
        public string PAIS_DE_NACIONALIDAD { set; get; }
        public DateTime? FECHA_DE_NACIMIENTO { set; get; }
        public string SEXO { set; get; }
        public string OCUPACION_QUE_DESEMPEÑA { set; get; }
        public DateTime? FECHA_DE_INGRESO { set; get; }
        public decimal HORAS_PAGADAS { set; get; }
        public string DIAS_PAGADOS { set; get; }
        public decimal HABER_BASICO { set; get; }
        public decimal BONO_ANTIGUEDAD { set; get; }
        public decimal OTROS_INGRESOS { set; get; }
        public decimal INGRESOS_POR_DOCENCIA { set; get; }
        public decimal INGRESOS_POR_OTRAS_ACTIVIDADES_ACADEMICAS { set; get; }
        public decimal REINTEGRO { set; get; }
        public decimal TOTAL_GANADO { set; get; }
        public decimal APORTE_A_AFP { set; get; }
        public decimal RC_IVA { set; get; }
        public decimal DESCUENTOS { set; get; }
        public decimal TOTAL_DESCUENTOS { set; get; }
        public decimal LIQUIDO_PAGABLE { set; get; }


    }
}