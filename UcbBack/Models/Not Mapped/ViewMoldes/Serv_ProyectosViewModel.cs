using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web;

namespace UcbBack.Models.Not_Mapped.ViewMoldes
{
    public class Serv_ProyectosViewModel
    {
        [DisplayName("Codigo Socio")]
        public string Codigo_Socio { get; set; }

        [DisplayName("Nombre Socio")]
        public string Nombre_Socio { get; set; }

        [DisplayName("Cod. Dependencia")]
        public string Cod_Dependencia { get; set; }

        [DisplayName("PEI-PO")]
        public string PEI_PO { get; set; }

        public string Nombre_del_Servicio { get; set; }

        [DisplayName("Código Proyecto SAP")]
        public string Código_Proyecto_SAP { get; set; }

        [DisplayName("Nombre del Proyecto")]
        public string Nombre_del_Proyecto { get; set; }

        [DisplayName("Versión")]
        public string Versión { get; set; }

        [DisplayName("Periodo Académico")]
        public string Periodo_Académico { get; set; }

        [DisplayName("Tipo de Tarea Asignada")]
        public string Tipo_Tarea_Asignada { get; set; }

        [DisplayName("Cuenta Asignada")]
        public string Cuenta_Asignada { get; set; }

        [DisplayName("Monto Contrato")]
        public Decimal Monto_Contrato { get; set; }

        [DisplayName("Monto IUE")]
        public Decimal Monto_IUE { get; set; }

        [DisplayName("Monto I.T.")]
        public Decimal Monto_IT { get; set; }

        [DisplayName("Monto a Pagar")]
        public Decimal Monto_a_Pagar { get; set; }

        [DisplayName("Observaciones")]
        public string Observaciones { get; set; }
    }
}