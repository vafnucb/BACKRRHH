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
    public class VoucherHeader
    {
        public string ParentKey { get; set; }
        public string LineNum { get; set; }
        public string ReferenceDate { get; set; }
        public string Memo { get; set; }
        public string Reference { get; set; }
        public string Reference2 { get; set; }
        public string TransactionCode { get; set; }
        public string ProjectCode { get; set; }
        public string TaxDate { get; set; }
        public string Indicator { get; set; }
        public string UseAutoStorno { get; set; }
        public string StornoDate { get; set; }
        public string VatDate { get; set; }
        public string Series { get; set; }
        public string StampTax { get; set; }
        public string DueDate { get; set; }
        public string AutoVAT { get; set; }
        public string ReportEU { get; set; }
        public string Report347 { get; set; }
        public string LocationCode { get; set; }
        public string BlockDunningLetter { get; set; }
        public string AutomaticWT { get; set; }
        public string Corisptivi { get; set; }
    }

}