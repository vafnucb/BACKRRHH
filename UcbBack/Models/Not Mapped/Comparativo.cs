using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace UcbBack.Models.Not_Mapped
{
    public class Comparativo
    {
        public string CUNI { get; set; }
        public string Documento { get; set; }
        public string NombreCompleto { get; set; }
        public string Posicion { get; set; }
        public decimal difHB { get; set; }
        public decimal difBA { get; set; }
        public decimal difOI { get; set; }
        public decimal difDOC { get; set; }
        public decimal difOAA { get; set; }
        public decimal difAFPL { get; set; }
        public decimal actHB { get; set; }
        public decimal actBA { get; set; }
        public decimal actOI { get; set; }
        public decimal actDOC { get; set; }
        public decimal actOAA { get; set; }
        public decimal actAFPL { get; set; }
        public decimal antHB { get; set; }
        public decimal antBA { get; set; }
        public decimal antOI { get; set; }
        public decimal antDOC { get; set; }

        public decimal antOAA { get; set; }
        public decimal antAFPL { get; set; }
    }
}