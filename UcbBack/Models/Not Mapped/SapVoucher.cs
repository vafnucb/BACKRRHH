using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Web;

namespace UcbBack.Models.Not_Mapped
{
    [NotMapped]
    public class SapVoucher
    {

        public string ParentKey { get; set; }
        public string LineNum { get; set; }
        public string AccountCode { get; set; }
        public string Debit { get; set; }
        public string Credit { get; set; }
        public string ShortName { get; set; }
        public string LineMemo { get; set; }
        public string ProjectCode { get; set; }
        public string CostingCode { get; set; }
        public string CostingCode2 { get; set; }
        public string CostingCode3 { get; set; }
        public string CostingCode4 { get; set; }
        public string CostingCode5 { get; set; }
        public string BPLId { get; set; }
    }
    
}