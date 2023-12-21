using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace UcbBack.Models.Not_Mapped
{
    public class CostCenterAux
    {
        public string PrcCode { get; set; }
        public string PrcName { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public string U_NUM_INT_CAR { get; set; }
        public string U_CODIGO_DEPARTAMENTO { get; set; }
        public string U_CODIGO_SEGMENTO { get; set; }
        public string TipoUnidad { get; set; }
        public int BranchesId { get; set; }
    }
}