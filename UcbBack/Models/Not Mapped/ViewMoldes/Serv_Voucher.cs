using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace UcbBack.Models.Not_Mapped.ViewMoldes
{
    [NotMapped]
    public class Serv_Voucher
    {
        public string CardName{ get; set; }
        public string CardCode { get; set; }
        // DIM 1
        public string OU { get; set; }
        // DIM 2
        public string PEI { get; set; }
        // DIM 3
        public string Carrera { get; set; }
        // DIM 4
        public string Paralelo { get; set; }
        // DIM 5
        public string Periodo { get; set; }
        public string ProjectCode { get; set; }
        public string Memo { get; set; }
        public string LineMemo { get; set; }
        public string Concept { get; set; }
        public string AssignedAccount { get; set; }
        public string Account { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }
}