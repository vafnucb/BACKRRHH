using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web;

namespace UcbBack.Models.Not_Mapped.ViewMoldes
{
    public class Serv_PDF
    {

        public int Id { get; set; }
        /*
        [DisplayName("Codigo Socio")]
        public string Codigo_Socio { get; set; }
        */
        [DisplayName("Nombre Socio")]
        public string Nombre_Socio { get; set; }
        /*
        [DisplayName("Cod. Dependencia")]
        public string Cod_Dependencia { get; set; }
        */

        [DisplayName("Cod. UO")]
        public string Cod_UO { get; set; }


        [DisplayName("PEI-PO")]
        public string PEI_PO { get; set; }

        public string Nombre_del_Servicio { get; set; }
        
        [DisplayName("Sigla Asignatura")]
        public string Sigla_Asignatura { get; set; }

        [DisplayName("Paralelo")]
        public string Paralelo { get; set; }

        [DisplayName("Código de Paralelo SAP")]
        public string Codigo_Paralelo_SAP { get; set; }

        [DisplayName("Tipo de Servicio")]
        public string Cuenta{ get; set; }

        [DisplayName("Importe del Contrato")]
        public Decimal Contrato { get; set; }

        [DisplayName("Importe Deducción IUE")]
        public Decimal IUE { get; set; }

        [DisplayName("Importe Deducción I.T.")]
        public Decimal IT { get; set; }

        [DisplayName("IUEExterior")]
        public Decimal? IUEExterior { get; set; }

        [DisplayName("Monto a Pagar")]
        public Decimal xPagar { get; set; }

        [DisplayName("Observaciones")]
        public string Observaciones { get; set; }

        [DisplayName("Código Proyecto SAP")]
        public string Codigo_Proyecto_SAP { get; set; }

        [DisplayName("Nombre del Proyecto")]
        public string Nombre_del_Proyecto { get; set; }

        [DisplayName("Versión")]
        public string Version { get; set; }
        /*
        [DisplayName("Periodo Académico")]
        public string Periodo_Academico { get; set; }
        */
        [DisplayName("Tipo de Tarea Asignada")]
        public string Tarea_Asignada { get; set; }

        [DisplayName("Codigo Carrera")]
        public string Codigo_Carrera { get; set; }

        [DisplayName("Documento Base")]
        public string Documento_Base { get; set; }

        [DisplayName("Postulante")]
        public string Postulante { get; set; }

        [DisplayName("Objeto del Contrato")]
        public string Objeto_del_Contrato { get; set; }

        [DisplayName("SAPId")]
        public string SAPId { get; set; }

        [DisplayName("FileType")]
        public string FileType { get; set; }

        public int BranchesId { get; set; }
    }
}