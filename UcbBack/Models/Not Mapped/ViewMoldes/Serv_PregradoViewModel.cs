using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web;

namespace UcbBack.Models.Not_Mapped.ViewMoldes
{
    public class Serv_PregradoViewModel
    {
        [DisplayName("Codigo Socio")]
        public string Codigo_Socio { get; set; }

        [DisplayName("Nombre Socio")]
        public string Nombre_Socio { get; set; }

        [DisplayName("Cod Dependencia")]
        public string Cod_Dependencia { get; set; }

        [DisplayName("PEI PO")]
        public string PEI_PO { get; set; }

        [DisplayName("Nombre del Servicio")]
        public string Nombre_del_Servicio { get; set; }

        [DisplayName("Codigo Carrera")]
        public string Codigo_Carrera { get; set; }

        [DisplayName("Documento Base")]
        public string Documento_Base { get; set; }

        [DisplayName("Postulante")]
        public string Postulante { get; set; }

        [DisplayName("Tipo Tarea Asignada")]
        public string Tipo_Tarea_Asignada { get; set; }

        [DisplayName("Cuenta Asignada")]
        public string Cuenta_Asignada { get; set; }

        [DisplayName("Importe del Contrato")]
        public Decimal Monto_Contrato { get; set; }

        [DisplayName("Importe Deducción IUE")]
        public Decimal Monto_IUE { get; set; }

        [DisplayName("Importe Deducción I.T.")]
        public Decimal Monto_IT { get; set; }

        [DisplayName("IUEExterior")]
        public decimal? IUEExterior { get; set; }

        [DisplayName("Monto a Pagar")]
        public Decimal Monto_a_Pagar { get; set; }

        [DisplayName("Observaciones")]
        public string Observaciones { get; set; }
    }
}